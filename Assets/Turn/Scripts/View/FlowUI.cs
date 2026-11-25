using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Turn.View
{
    /// <summary>
    /// 流程界面（程序A）：标题页、暂停菜单、胜利 / 失败结算。由 GameManager 自动挂上，运行时自己搭 UI，场景里不需要摆东西；
    /// 想改标题文字就在编辑器里给 GameManager 手动加上本组件再改 Inspector。
    ///   · 标题页：一本“绘本封面”，Space / Enter / 点按钮开始 → 武器选择页 → 选好后封面像翻页一样往左翻开，露出这一局；
    ///   · 武器选择页（正式入口）：三张武器卡，点卡片 / 按 1 2 3 直接选，或 ← → 切换 + Space / Enter 确认；
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
        private Action<int> _onWeaponChosen;
        private Action _onResume;
        private Action _onRestartLevel;
        private Action _onRestartFromFirst;
        private Action _onTitle;

        public bool IsTitleOpen { get; private set; }
        /// <summary>武器选择页是否打开（标题流程里打开时 IsTitleOpen 也仍为 true）。</summary>
        public bool IsSelectOpen { get { return _select != null && _select.activeSelf; } }
        /// <summary>当前高亮的武器卡（0..2）。</summary>
        public int SelectHighlight { get { return _selectHighlight; } }

        private GameObject _select;
        private Text _selectHeader;
        private readonly Image[] _selectCards = new Image[3];
        private readonly Image[] _selectStripes = new Image[3];
        private readonly Text[] _selectTitles = new Text[3];
        private readonly Text[] _selectBodies = new Text[3];
        private readonly Color[] _selectColors = { Color.white, Color.white, Color.white };
        private int _selectHighlight;
        public bool IsPauseOpen { get { return _pause != null && _pause.activeSelf; } }
        public bool IsResultOpen { get { return _result != null && _result.activeSelf; } }

        /// <summary>
        /// onOpenSelect：标题页点「开始」时调用，由 GameManager 填好武器数据后调 ShowWeaponSelect；传 null = 不用选择页，直接开局。
        /// onWeaponChosen：在选择页选定了第 index 把武器。
        /// </summary>
        public void Setup(Action onStart, Action onResume, Action onRestartLevel, Action onRestartFromFirst, Action onTitle,
            Action onOpenSelect = null, Action<int> onWeaponChosen = null)
        {
            _onStart = onStart;
            _onOpenSelect = onOpenSelect;
            _onWeaponChosen = onWeaponChosen;
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
                _onOpenSelect(); // GameManager 填数据并调 ShowWeaponSelect
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

        // ---------------- 武器选择 ----------------

        /// <summary>打开武器选择页。titles / bodies / colors 各 3 项（水枪 / 投矛 / 陌刀），highlight = 默认高亮的那张。</summary>
        public void ShowWeaponSelect(string header, string[] titles, string[] bodies, Color[] colors, int highlight)
        {
            EnsureUI();
            SetPause(false);
            HideResult();
            if (_selectHeader != null) _selectHeader.text = header;
            for (int i = 0; i < 3; i++)
            {
                _selectColors[i] = colors != null && i < colors.Length ? colors[i] : Color.white;
                if (_selectTitles[i] != null) _selectTitles[i].text = titles != null && i < titles.Length ? titles[i] : "";
                if (_selectBodies[i] != null) _selectBodies[i].text = bodies != null && i < bodies.Length ? bodies[i] : "";
                if (_selectStripes[i] != null) _selectStripes[i].color = _selectColors[i];
            }
            _select.SetActive(true);
            _select.transform.SetAsLastSibling();
            HighlightWeapon(highlight);
        }

        public void HideWeaponSelect()
        {
            if (_select != null) _select.SetActive(false);
        }

        /// <summary>高亮第 index 张卡（循环）。</summary>
        public void HighlightWeapon(int index)
        {
            _selectHighlight = ((index % 3) + 3) % 3;
            for (int i = 0; i < 3; i++)
            {
                if (_selectCards[i] == null) continue;
                bool on = i == _selectHighlight;
                _selectCards[i].color = on ? Color.Lerp(PaperColor, _selectColors[i], 0.28f) : PaperColor;
                _selectCards[i].rectTransform.localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
            }
        }

        /// <summary>选定第 index 把武器：关掉选择页 → 回调 → 如果是从标题页来的，接着翻开封面。</summary>
        public void ChooseWeapon(int index)
        {
            if (!IsSelectOpen || index < 0 || index > 2) return;
            bool fromTitle = IsTitleOpen;
            HideWeaponSelect();
            if (_onWeaponChosen != null) _onWeaponChosen(index);
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

            _selectHeader = MakeText("Header", dim.rectTransform, "选择武器", 64, FontStyle.Bold);
            _selectHeader.color = PaperColor;
            Stretch(_selectHeader.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 0.96f), Vector2.zero);

            Text hint = MakeText("Hint", dim.rectTransform, "点卡片 / 按 1 2 3 直接选择      ← → 切换，Space / Enter 确认", 26, FontStyle.Normal);
            hint.color = new Color(PaperColor.r, PaperColor.g, PaperColor.b, 0.7f);
            Stretch(hint.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.1f), Vector2.zero);

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float x0 = 0.05f + i * 0.31f;
                Image card = MakeImage("Card" + i, dim.rectTransform, PaperColor, true);
                Stretch(card.rectTransform, new Vector2(x0, 0.14f), new Vector2(x0 + 0.28f, 0.8f), Vector2.zero);
                _selectCards[i] = card;
                var button = card.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => ChooseWeapon(index));

                Image stripe = MakeImage("Stripe", card.rectTransform, Color.white, false);
                Stretch(stripe.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 1f), Vector2.zero);
                _selectStripes[i] = stripe;

                Text title = MakeText("Title", card.rectTransform, "", 52, FontStyle.Bold);
                Stretch(title.rectTransform, new Vector2(0f, 0.74f), new Vector2(1f, 0.89f), Vector2.zero);
                _selectTitles[i] = title;

                Text body = MakeText("Body", card.rectTransform, "", 26, FontStyle.Normal);
                body.alignment = TextAnchor.UpperLeft;
                body.lineSpacing = 1.15f;
                Stretch(body.rectTransform, new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.72f), Vector2.zero);
                _selectBodies[i] = body;
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
