using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Turn.View
{
    /// <summary>
    /// 流程界面（程序A）：标题页、暂停菜单、胜利 / 失败结算。由 GameManager 自动挂上，运行时自己搭 UI，场景里不需要摆东西；
    /// 想改标题文字就在编辑器里给 GameManager 手动加上本组件再改 Inspector。
    ///   · 标题页：一本“绘本封面”，Space / Enter / 点按钮开始 → 出战选择页 → 选好后封面像翻页一样往左翻开，露出这一局；
    ///   · 出战选择页（正式入口，11/25 双武器策划案）：方案（双主武器 / 主 + 副）→ 主武器 → 第二把主武器或副武器，
    ///     每一页由 GameManager 用 ShowSelectPage 填卡片；点卡片 / 按数字直接选，或 ← → 切换 + Space / Enter 确认，Esc 返回上一步；
    ///     结算界面的「换武器重开」也会打开它（不经过封面）；
    ///   · 暂停（Esc / P）：继续 / 重打本关（R）/ 回到标题（T）；
    ///   · 结算：失败 → 重打本关（R / Enter）；胜利 → 从第一关开始（R / Enter）；都可以回到标题（T）。
    /// 只负责显示和按钮，具体做什么由 GameManager 传进来的回调决定（onStart 在封面开始翻开时调用）；键盘输入也由 GameManager 统一读取。
    /// 用到按钮，需要场景里有 EventSystem（白模生成器已经建好）。
    /// </summary>
    public sealed class FlowUI : MonoBehaviour
    {
        [Header("标题页文字")]
        [SerializeField] private string gameTitle = "TURN";
        [SerializeField] private string subtitle = "翻滚进入亚空间，让铺垫一起爆发";
        [SerializeField] private string startHint = "Space / Enter 开始（选择武器）      F1 数值面板      F2 遮挡物      F4 无敌 · F5 加速 · F6 切武器（调试）";
        [Tooltip("音乐署名：Incompetech 的曲子是 CC BY 4.0，必须署名（见 docs/AUDIO-HANDOFF.md）。标题页底部和胜利结算都会显示。")]
        [SerializeField] private string credits = "Music by Kevin MacLeod (incompetech.com), licensed under CC BY 4.0.";

        /// <summary>音乐署名（胜利结算也会附上）。</summary>
        public string Credits { get { return credits; } }

        public const float OpenCoverTime = 0.7f;

        private static readonly Color CoverColor = new Color(0.34f, 0.2f, 0.15f, 1f);
        private static readonly Color PaperColor = new Color(0.96f, 0.92f, 0.82f, 1f);
        private static readonly Color InkColor = new Color(0.22f, 0.17f, 0.12f, 1f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.22f, 0.16f, 1f);
        private static readonly Color ButtonTextColor = new Color(0.98f, 0.95f, 0.88f, 1f);

        private Canvas _canvas;
        private RectTransform _title;
        private GameObject _pause;
        private GameObject _result;
        private Text _resultTitle;
        private Text _resultBody;
        private Text _resultPrimaryLabel;
        private bool _resultVictory;
        private Coroutine _opening;

        private Action _onStart;
        private Action _onOpenSelect;
        private Action<int> _onCardChosen;
        private Action _onResume;
        private Action _onRestartLevel;
        private Action _onRestartFromFirst;
        private Action _onTitle;

        public bool IsTitleOpen { get; private set; }
        /// <summary>选择页是否打开（标题流程里打开时 IsTitleOpen 也仍为 true）。</summary>
        public bool IsSelectOpen { get { return _select != null && _select.activeSelf; } }
        /// <summary>当前高亮的卡片下标。</summary>
        public int SelectHighlight { get { return _selectHighlight; } }
        /// <summary>当前这一页有几张卡。</summary>
        public int SelectCount { get { return _selectCount; } }

        /// <summary>选择页上的一张卡（出战方案 / 主武器 / 副武器）。Enabled = false 显示为灰色「开发中」，不能选。</summary>
        public struct SelectCard
        {
            public string Title;
            public string Body;
            public Color Color;
            public bool Enabled;
        }

        /// <summary>一页最多几张卡；超过 5 张时分两行排。</summary>
        public const int MaxSelectCards = 10;

        private GameObject _select;
        private Text _selectHeader;
        private Text _selectHint;
        private readonly Image[] _selectCards = new Image[MaxSelectCards];
        private readonly Button[] _selectButtons = new Button[MaxSelectCards];
        private readonly Image[] _selectStripes = new Image[MaxSelectCards];
        private readonly Text[] _selectTitles = new Text[MaxSelectCards];
        private readonly Text[] _selectBodies = new Text[MaxSelectCards];
        private readonly SelectCard[] _selectData = new SelectCard[MaxSelectCards];
        private int _selectCount;
        private int _selectHighlight;

        /// <summary>
        /// onOpenSelect：标题页点「开始」/ 结算界面点「换武器重开」时调用，由 GameManager 填好第一页后调 ShowSelectPage；传 null = 不用选择页，直接开局。
        /// onCardChosen：在选择页点了第 index 张卡（GameManager 决定翻到下一页还是 FinishSelect）。
        /// </summary>
        public void Setup(Action onStart, Action onResume, Action onRestartLevel, Action onRestartFromFirst, Action onTitle,
            Action onOpenSelect = null, Action<int> onCardChosen = null)
        {
            _onStart = onStart;
            _onOpenSelect = onOpenSelect;
            _onCardChosen = onCardChosen;
            _onResume = onResume;
            _onRestartLevel = onRestartLevel;
            _onRestartFromFirst = onRestartFromFirst;
            _onTitle = onTitle;
            EnsureUI();
        }

        // ---------------- 标题 ----------------

        public void ShowTitle()
        {
            EnsureUI();
            if (_opening != null)
            {
                StopCoroutine(_opening);
                _opening = null;
            }
            SetPause(false);
            HideResult();
            HideWeaponSelect();
            SetCover(0f, 1f);
            _title.gameObject.SetActive(true);
            IsTitleOpen = true;
        }

        /// <summary>标题页「开始」：有选择页就先打开武器选择页，否则直接翻开封面。重复调用无效。</summary>
        public void StartFromTitle()
        {
            if (!IsTitleOpen || _opening != null || IsSelectOpen) return;
            if (_onOpenSelect != null)
            {
                _onOpenSelect(); // GameManager 填第一页并调 ShowSelectPage
                if (IsSelectOpen) return;
            }
            OpenCoverNow();
        }

        /// <summary>封面往左翻开，翻完才放行游戏。</summary>
        private void OpenCoverNow()
        {
            if (!IsTitleOpen || _opening != null) return;
            Call(_onStart); // 开始翻开的那一刻通知（播翻页音效等）；翻完之前 IsTitleOpen 仍为 true，游戏不会开始
            _opening = StartCoroutine(OpenCover());
        }

        // ---------------- 出战选择（方案 → 主武器 → 第二主武器 / 副武器） ----------------

        /// <summary>显示选择页的一页（最多 MaxSelectCards 张卡）。highlight = 默认高亮的那张（不可选时自动跳到下一张可选的）。</summary>
        public void ShowSelectPage(string header, string hint, SelectCard[] cards, int highlight)
        {
            EnsureUI();
            SetPause(false);
            HideResult();
            _selectCount = cards != null ? Mathf.Min(cards.Length, MaxSelectCards) : 0;
            if (_selectHeader != null) _selectHeader.text = header;
            if (_selectHint != null) _selectHint.text = hint;
            const float left = 0.04f, right = 0.96f, gap = 0.02f;
            int rows = _selectCount > 5 ? 2 : 1;
            int perRow = rows == 2 ? (_selectCount + 1) / 2 : Mathf.Max(1, _selectCount);
            float width = _selectCount > 0 ? (right - left - gap * (perRow - 1)) / perRow : 0f;
            for (int i = 0; i < MaxSelectCards; i++)
            {
                bool shown = i < _selectCount;
                _selectCards[i].gameObject.SetActive(shown);
                if (!shown) continue;
                SelectCard card = cards[i];
                _selectData[i] = card;
                int row = i / perRow;
                float x0 = left + (i % perRow) * (width + gap);
                // 一行：0.14–0.80；两行：上 0.48–0.80，下 0.12–0.45
                float y0 = rows == 1 ? 0.14f : (row == 0 ? 0.48f : 0.12f);
                float y1 = rows == 1 ? 0.8f : (row == 0 ? 0.8f : 0.45f);
                Stretch(_selectCards[i].rectTransform, new Vector2(x0, y0), new Vector2(x0 + width, y1), Vector2.zero);
                _selectTitles[i].text = card.Enabled ? card.Title : card.Title + "\n<size=24>（开发中）</size>";
                _selectBodies[i].text = card.Body;
                _selectBodies[i].fontSize = rows == 2 ? 15 : (_selectCount >= 5 ? 21 : (_selectCount >= 4 ? 23 : 26));
                _selectTitles[i].fontSize = rows == 2 ? 30 : (_selectCount >= 4 ? 42 : 52);
                _selectStripes[i].color = card.Enabled ? card.Color : new Color(0.6f, 0.6f, 0.6f, 1f);
                _selectButtons[i].interactable = card.Enabled;
                Color ink = card.Enabled ? InkColor : new Color(InkColor.r, InkColor.g, InkColor.b, 0.45f);
                _selectTitles[i].color = ink;
                _selectBodies[i].color = ink;
            }
            _select.SetActive(true);
            _select.transform.SetAsLastSibling();
            HighlightCard(highlight, 1);
        }

        public void HideWeaponSelect()
        {
            if (_select != null) _select.SetActive(false);
        }

        /// <summary>高亮第 index 张卡（循环）；不可选的卡沿 direction 方向跳过。</summary>
        public void HighlightCard(int index, int direction)
        {
            if (_selectCount <= 0) return;
            int dir = direction >= 0 ? 1 : -1;
            int i = ((index % _selectCount) + _selectCount) % _selectCount;
            for (int tries = 0; tries < _selectCount && !_selectData[i].Enabled; tries++)
                i = ((i + dir) % _selectCount + _selectCount) % _selectCount;
            _selectHighlight = i;
            for (int k = 0; k < _selectCount; k++)
            {
                bool on = k == _selectHighlight && _selectData[k].Enabled;
                Color baseColor = _selectData[k].Enabled ? PaperColor : new Color(0.78f, 0.76f, 0.72f, 1f);
                _selectCards[k].color = on ? Color.Lerp(PaperColor, _selectData[k].Color, 0.28f) : baseColor;
                _selectCards[k].rectTransform.localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
            }
        }

        /// <summary>点了第 index 张卡：不可选的忽略；可选的交给 GameManager（翻下一页或 FinishSelect）。</summary>
        public void ChooseCard(int index)
        {
            if (!IsSelectOpen || index < 0 || index >= _selectCount || !_selectData[index].Enabled) return;
            _selectHighlight = index;
            if (_onCardChosen != null) _onCardChosen(index);
        }

        /// <summary>选完了：关掉选择页；如果是从标题页来的，接着翻开封面。</summary>
        public void FinishSelect()
        {
            bool fromTitle = IsTitleOpen;
            HideWeaponSelect();
            if (fromTitle) OpenCoverNow();
        }

        private IEnumerator OpenCover()
        {
            for (float t = 0f; t < OpenCoverTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / OpenCoverTime);
                SetCover(0f, 1f - k * k * (3f - 2f * k));
                yield return null;
            }
            _title.gameObject.SetActive(false);
            IsTitleOpen = false;
            _opening = null;
        }

        private void SetCover(float xMin, float xMax)
        {
            _title.anchorMin = new Vector2(xMin, 0f);
            _title.anchorMax = new Vector2(Mathf.Max(xMin, xMax), 1f);
            _title.offsetMin = Vector2.zero;
            _title.offsetMax = Vector2.zero;
        }

        // ---------------- 暂停 / 结算 ----------------

        public void SetPause(bool open)
        {
            EnsureUI();
            _pause.SetActive(open);
        }

        public void ShowResult(bool victory, string title, string body)
        {
            EnsureUI();
            _resultVictory = victory;
            _resultTitle.text = title;
            _resultBody.text = body;
            _resultPrimaryLabel.text = victory ? "从第一关开始（R）" : "再来一局（R）";
            _result.SetActive(true);
        }

        public void HideResult()
        {
            if (_result != null) _result.SetActive(false);
        }

        /// <summary>结算界面「换武器重开」：打开武器选择页（不经过封面）。</summary>
        public void OpenSelectFromResult()
        {
            if (_onOpenSelect != null) _onOpenSelect();
        }

        private void OnResultPrimary()
        {
            if (_resultVictory) Call(_onRestartFromFirst);
            else Call(_onRestartLevel);
        }

        private static void Call(Action action)
        {
            if (action != null) action();
        }

        // ---------------- 搭 UI ----------------

        private void EnsureUI()
        {
            if (_canvas != null) return;

            var root = new GameObject("FlowUI", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50; // 在 HUD 之上、翻页过场之下
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            BuildTitle(root.transform);
            BuildPause(root.transform);
            BuildResult(root.transform);
            BuildWeaponSelect(root.transform);
        }

        private void BuildWeaponSelect(Transform parent)
        {
            Image dim = MakeImage("WeaponSelect", parent, new Color(0.12f, 0.08f, 0.06f, 0.92f), true);
            Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            _select = dim.gameObject;

            _selectHeader = MakeText("Header", dim.rectTransform, "选择出战方案", 64, FontStyle.Bold);
            _selectHeader.color = PaperColor;
            Stretch(_selectHeader.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 0.96f), Vector2.zero);

            _selectHint = MakeText("Hint", dim.rectTransform, "", 26, FontStyle.Normal);
            _selectHint.color = new Color(PaperColor.r, PaperColor.g, PaperColor.b, 0.7f);
            Stretch(_selectHint.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.1f), Vector2.zero);

            for (int i = 0; i < MaxSelectCards; i++)
            {
                int index = i;
                Image card = MakeImage("Card" + i, dim.rectTransform, PaperColor, true);
                _selectCards[i] = card;
                var button = card.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => ChooseCard(index));
                _selectButtons[i] = button;

                Image stripe = MakeImage("Stripe", card.rectTransform, Color.white, false);
                Stretch(stripe.rectTransform, new Vector2(0f, 0.92f), new Vector2(1f, 1f), Vector2.zero);
                _selectStripes[i] = stripe;

                Text title = MakeText("Title", card.rectTransform, "", 52, FontStyle.Bold);
                Stretch(title.rectTransform, new Vector2(0f, 0.74f), new Vector2(1f, 0.91f), Vector2.zero);
                _selectTitles[i] = title;

                Text body = MakeText("Body", card.rectTransform, "", 26, FontStyle.Normal);
                body.alignment = TextAnchor.UpperLeft;
                body.lineSpacing = 1.12f;
                Stretch(body.rectTransform, new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.72f), Vector2.zero);
                _selectBodies[i] = body;
                card.gameObject.SetActive(false);
            }
            _select.SetActive(false);
        }

        private void BuildTitle(Transform parent)
        {
            Image cover = MakeImage("Title", parent, CoverColor, true);
            _title = cover.rectTransform;
            Stretch(_title, Vector2.zero, Vector2.one, Vector2.zero);

            Image page = MakeImage("Page", _title, PaperColor, false);
            Stretch(page.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 48f));

            Text title = MakeText("GameTitle", page.rectTransform, gameTitle, 160, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.85f), Vector2.zero);
            Text sub = MakeText("Subtitle", page.rectTransform, subtitle, 40, FontStyle.Normal);
            Stretch(sub.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.5f), Vector2.zero);
            Text hint = MakeText("Hint", page.rectTransform, startHint, 26, FontStyle.Normal);
            hint.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.7f);
            Stretch(hint.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.15f), Vector2.zero);
            Text credit = MakeText("Credits", page.rectTransform, credits, 20, FontStyle.Normal);
            credit.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.6f);
            Stretch(credit.rectTransform, new Vector2(0f, 0.015f), new Vector2(1f, 0.06f), Vector2.zero);

            MakeButton("StartButton", page.rectTransform, "开始游戏", new Vector2(0.5f, 0.27f), new Vector2(360f, 84f), StartFromTitle);
            _title.gameObject.SetActive(false);
        }

        private void BuildPause(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Pause", out _pause);
            Text title = MakeText("Title", card, "暂停", 64, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), Vector2.zero);
            MakeButton("Resume", card, "继续（Esc）", new Vector2(0.5f, 0.56f), new Vector2(420f, 76f), () => Call(_onResume));
            MakeButton("Restart", card, "重打本关（R）", new Vector2(0.5f, 0.36f), new Vector2(420f, 76f), () => Call(_onRestartLevel));
            MakeButton("Title", card, "回到标题（T）", new Vector2(0.5f, 0.16f), new Vector2(420f, 76f), () => Call(_onTitle));
            _pause.SetActive(false);
        }

        private void BuildResult(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Result", out _result);
            _resultTitle = MakeText("Title", card, "", 64, FontStyle.Bold);
            Stretch(_resultTitle.rectTransform, new Vector2(0f, 0.74f), new Vector2(1f, 0.95f), Vector2.zero);
            _resultBody = MakeText("Body", card, "", 32, FontStyle.Normal);
            Stretch(_resultBody.rectTransform, new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.74f), Vector2.zero);
            Button primary = MakeButton("Primary", card, "", new Vector2(0.19f, 0.16f), new Vector2(260f, 76f), OnResultPrimary);
            _resultPrimaryLabel = primary.GetComponentInChildren<Text>();
            MakeButton("SwitchWeapon", card, "换武器重开（W）", new Vector2(0.5f, 0.16f), new Vector2(260f, 76f), OpenSelectFromResult);
            MakeButton("Title", card, "回到标题（T）", new Vector2(0.81f, 0.16f), new Vector2(260f, 76f), () => Call(_onTitle));
            _result.SetActive(false);
        }

        /// <summary>半透明遮罩 + 居中的纸片卡片。</summary>
        private static RectTransform BuildCard(Transform parent, string name, out GameObject root)
        {
            Image dim = MakeImage(name, parent, new Color(0f, 0f, 0f, 0.55f), true);
            Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            root = dim.gameObject;

            Image card = MakeImage("Card", dim.rectTransform, PaperColor, true);
            RectTransform rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 560f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Image MakeImage(string name, Transform parent, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast; // 遮罩挡住点击，避免点到后面
            return image;
        }

        private static Text MakeText(string name, Transform parent, string content, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = UIFont.Chinese;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = InkColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size, Action onClick)
        {
            Image bg = MakeImage(name, parent, ButtonColor, true);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var button = bg.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => { if (onClick != null) onClick(); });

            Text text = MakeText("Label", rt, label, 32, FontStyle.Bold);
            text.color = ButtonTextColor;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            return button;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 inset)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = inset;
            rt.offsetMax = -inset;
        }
    }
}
