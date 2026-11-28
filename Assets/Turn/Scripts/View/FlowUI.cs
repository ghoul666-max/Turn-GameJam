using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Turn.View
{
    /// <summary>
    /// 流程界面（程序A）：标题页、暂停菜单、胜利 / 失败结算。由 GameManager 自动挂上，运行时自己搭 UI，场景里不需要摆东西；
    /// 想改标题文字就在编辑器里给 GameManager 手动加上本组件再改 Inspector。
    ///   · 标题页：一本“绘本封面”，Space / Enter / 点按钮开始 → 出战选择页（另有音量设置 V、窗口设置 O、退出游戏 Esc，退出前弹确认） → 选好后封面像翻页一样往左翻开，露出这一局；
    ///   · 出战选择页（正式入口，11/25 双武器策划案）：方案（双主武器 / 主 + 副）→ 主武器 → 第二把主武器或副武器，
    ///     每一页由 GameManager 用 ShowSelectPage 填卡片；点卡片 / 按数字直接选，或 ← → 切换 + Space / Enter 确认，Esc 返回上一步；
    ///     结算界面的「换武器重开」也会打开它（不经过封面）；
    ///   · 暂停（Esc / P）：继续 / 音量设置（V）/ 窗口设置（O）/ 重打本关（R）/ 回到标题（T）；左侧血量下方显示「已选强化」卡牌列表（可滚动，GameManager 填）；
    ///   · 音量设置（标题页、暂停菜单都能打开）：总音量 / 音乐 / 音效三条滑条 + 静音 + 恢复默认，
    ///     鼠标拖动，或 ↑↓ 选一行、←→ 每次 ±5%，Esc 返回；设置存在 PlayerPrefs（TurnVolume），立即生效；
    ///   · 窗口设置（O，标题页、暂停菜单都能打开，11/27）：窗口化 / 全屏 / 无边框窗口三选一，点选或 ↑↓ + Space / Enter，Esc 返回；
    ///     存在 PlayerPrefs（TurnDisplay），立即切换，下次打开游戏沿用；
    ///   · 结算：失败 → 重打本关（R / Enter）；胜利 → 从第一关开始（R / Enter）；都可以回到标题（T）。
    /// 文字格式（11/27 统一）：按钮 =「动作（快捷键）」，没有快捷键就只写动作；提示行 =「键 动作 · 键 动作」，多个键用「 / 」，
    /// 方向键写「↑↓」「←→」；标签和值之间用全角冒号「：」。
    /// 只负责显示和按钮，具体做什么由 GameManager 传进来的回调决定（onStart 在封面开始翻开时调用）；键盘输入也由 GameManager 统一读取。
    /// 用到按钮，需要场景里有 EventSystem（白模生成器已经建好）。
    /// </summary>
    public sealed class FlowUI : MonoBehaviour
    {
        [Header("标题页文字")]
        [SerializeField] private string gameTitle = "TURN";
        [SerializeField] private string subtitle = "翻滚进入亚空间，让铺垫一起爆发";
        [SerializeField, TextArea] private string startHint = "Space / Enter 开始游戏 · V 音量设置 · O 窗口设置 · H 界面设置 · Esc 退出游戏\n<size=22>调试：F1 数值面板 · F2 遮挡物 · F4 无敌 · F5 加速 · F6 切武器</size>";
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

        // 音量设置
        private GameObject _volume;
        private readonly Slider[] _volumeSliders = new Slider[TurnVolume.ChannelCount];
        private readonly Text[] _volumeValues = new Text[TurnVolume.ChannelCount];
        private readonly Image[] _volumeRows = new Image[TurnVolume.ChannelCount];
        private Text _muteLabel;
        private int _volumeRow;
        private bool _volumeRefreshing;
        private float _lastPreview = -10f;
        private Action _onVolumePreview;

        /// <summary>音量设置界面是否打开（打开时 GameManager 只把 ↑↓←→ / Esc 交给它）。</summary>
        public bool IsVolumeOpen { get { return _volume != null && _volume.activeSelf; } }

        // 窗口设置（11/27）
        private GameObject _display;
        private readonly Image[] _displayOptions = new Image[TurnDisplay.ModeCount];
        private readonly Image[] _displayCursors = new Image[TurnDisplay.ModeCount];
        private readonly Text[] _displayNames = new Text[TurnDisplay.ModeCount];
        private readonly Text[] _displayDescs = new Text[TurnDisplay.ModeCount];
        private int _displayRow;
        private TurnDisplay.Mode _displayCurrent;

        /// <summary>窗口设置界面是否打开（打开时 GameManager 只把 ↑↓ / Space / Enter / Esc 交给它）。</summary>
        public bool IsDisplayOpen { get { return _display != null && _display.activeSelf; } }

        // 界面设置（11/28）：HUD 布局 + 图标风格
        private GameObject _hudPanel;
        private readonly Image[] _hudLayoutTiles = new Image[HudPrefs.LayoutCount];
        private readonly Image[] _hudLayoutCursors = new Image[HudPrefs.LayoutCount];
        private readonly Text[] _hudLayoutNames = new Text[HudPrefs.LayoutCount];
        private readonly Text[] _hudLayoutDescs = new Text[HudPrefs.LayoutCount];
        private readonly Image[] _hudIconTiles = new Image[HudPrefs.IconSetCount];
        private readonly Image[] _hudIconCursors = new Image[HudPrefs.IconSetCount];
        private readonly Text[] _hudIconNames = new Text[HudPrefs.IconSetCount];
        private Text[] _hudRowLabels = new Text[2];
        private int _hudRow;

        /// <summary>界面设置是否打开（打开时 GameManager 只把 ↑↓←→ / Esc / H 交给它）。</summary>
        public bool IsHudSettingsOpen { get { return _hudPanel != null && _hudPanel.activeSelf; } }

        // 退出确认（11/27）
        private GameObject _quit;

        /// <summary>退出确认框是否打开（打开时 GameManager 只把 Space / Enter / Esc 交给它）。</summary>
        public bool IsQuitOpen { get { return _quit != null && _quit.activeSelf; } }

        // 暂停时的「已选强化」列表
        private RectTransform _buildContent;
        private Text _buildHeader;
        private ScrollRect _buildScroll;

        /// <summary>「已选强化」列表里的一项：Header = 分组标题（装备名）；否则是一张卡（Text 支持富文本）。</summary>
        public struct BuildEntry
        {
            public string Text;
            public Color Color;
            public bool Header;
        }

        /// <summary>拖动「总音量 / 音效」时的试听（GameManager 传 TurnAudio.PlayPreview）。</summary>
        public void SetVolumePreview(Action preview) { _onVolumePreview = preview; }

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
            HideVolume();
            HideHudSettings();
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

        // ---------------- 音量设置 ----------------

        public void ShowVolume()
        {
            EnsureUI();
            _volume.SetActive(true);
            _volume.transform.SetAsLastSibling();
            RefreshVolume();
            HighlightVolumeRow(0);
        }

        public void HideVolume()
        {
            if (_volume == null || !_volume.activeSelf) return;
            _volume.SetActive(false);
            TurnVolume.Flush();
        }

        /// <summary>↑↓：换一行（循环）。</summary>
        public void VolumeMoveRow(int step)
        {
            if (!IsVolumeOpen) return;
            HighlightVolumeRow(((_volumeRow + step) % TurnVolume.ChannelCount + TurnVolume.ChannelCount) % TurnVolume.ChannelCount);
        }

        /// <summary>←→：当前行 ±5%。</summary>
        public void VolumeAdjust(int step)
        {
            if (!IsVolumeOpen) return;
            var channel = (TurnVolume.Channel)_volumeRow;
            float value = Mathf.Round((TurnVolume.Get(channel) + step * 0.05f) * 20f) / 20f;
            TurnVolume.Set(channel, value);
            RefreshVolume();
            Preview(channel);
        }

        private void HighlightVolumeRow(int row)
        {
            _volumeRow = row;
            for (int i = 0; i < _volumeRows.Length; i++)
                if (_volumeRows[i] != null)
                    _volumeRows[i].color = i == row ? new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.14f) : new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>按存档刷新滑条 / 百分比 / 静音按钮（刷新期间不触发滑条回调）。</summary>
        private void RefreshVolume()
        {
            _volumeRefreshing = true;
            bool muted = TurnVolume.Muted;
            for (int i = 0; i < TurnVolume.ChannelCount; i++)
            {
                float v = TurnVolume.Get((TurnVolume.Channel)i);
                if (_volumeSliders[i] != null) _volumeSliders[i].value = v;
                if (_volumeValues[i] != null)
                {
                    _volumeValues[i].text = Mathf.RoundToInt(v * 100f) + "%";
                    _volumeValues[i].color = muted ? new Color(InkColor.r, InkColor.g, InkColor.b, 0.4f) : InkColor;
                }
            }
            if (_muteLabel != null) _muteLabel.text = muted ? "静音：开" : "静音：关";
            _volumeRefreshing = false;
        }

        private void OnVolumeSlider(int index, float value)
        {
            if (_volumeRefreshing) return;
            var channel = (TurnVolume.Channel)index;
            TurnVolume.Set(channel, value);
            HighlightVolumeRow(index);
            RefreshVolume();
            Preview(channel);
        }

        private void Preview(TurnVolume.Channel channel)
        {
            // 音乐本来就在放，调了马上听得出来；总音量 / 音效放一个短音效试听（拖动时最多每 0.12 秒一次）
            if (channel == TurnVolume.Channel.Music || _onVolumePreview == null) return;
            if (Time.unscaledTime - _lastPreview < 0.12f) return;
            _lastPreview = Time.unscaledTime;
            _onVolumePreview();
        }

        private void ToggleMute()
        {
            TurnVolume.Muted = !TurnVolume.Muted;
            RefreshVolume();
        }

        private void ResetVolume()
        {
            TurnVolume.ResetDefaults();
            RefreshVolume();
        }

        // ---------------- 窗口设置 ----------------

        public void ShowDisplay()
        {
            EnsureUI();
            _display.SetActive(true);
            _display.transform.SetAsLastSibling();
            _displayCurrent = TurnDisplay.Current;
            _displayRow = (int)_displayCurrent;
            RefreshDisplay();
        }

        public void HideDisplay()
        {
            if (_display != null) _display.SetActive(false);
        }

        /// <summary>↑↓（或 ←→）：移动光标（循环）。</summary>
        public void DisplayMove(int step)
        {
            if (!IsDisplayOpen || step == 0) return;
            _displayRow = ((_displayRow + step) % TurnDisplay.ModeCount + TurnDisplay.ModeCount) % TurnDisplay.ModeCount;
            RefreshDisplay();
        }

        /// <summary>Space / Enter：应用光标所在的选项。</summary>
        public void DisplayConfirm()
        {
            if (IsDisplayOpen) ChooseDisplay(_displayRow);
        }

        private void ChooseDisplay(int index)
        {
            if (index < 0 || index >= TurnDisplay.ModeCount) return;
            var mode = (TurnDisplay.Mode)index;
            _displayRow = index;
            if (mode != _displayCurrent) TurnDisplay.Set(mode); // Screen 下一帧才变，界面按选择显示
            _displayCurrent = mode;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            for (int i = 0; i < TurnDisplay.ModeCount; i++)
            {
                bool current = i == (int)_displayCurrent;
                var mode = (TurnDisplay.Mode)i;
                if (_displayOptions[i] != null)
                    _displayOptions[i].color = current ? ButtonColor : new Color(InkColor.r, InkColor.g, InkColor.b, 0.08f);
                if (_displayNames[i] != null)
                {
                    _displayNames[i].text = TurnDisplay.DisplayName(mode) + (current ? "（当前）" : "");
                    _displayNames[i].color = current ? ButtonTextColor : InkColor;
                }
                if (_displayDescs[i] != null)
                    _displayDescs[i].color = current ? new Color(ButtonTextColor.r, ButtonTextColor.g, ButtonTextColor.b, 0.85f)
                                                     : new Color(InkColor.r, InkColor.g, InkColor.b, 0.7f);
                if (_displayCursors[i] != null)
                    _displayCursors[i].color = i == _displayRow ? new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.35f) : new Color(0f, 0f, 0f, 0f);
            }
        }

        // ---------------- 界面设置（11/28） ----------------

        public void ShowHudSettings()
        {
            EnsureUI();
            _hudPanel.SetActive(true);
            _hudPanel.transform.SetAsLastSibling();
            _hudRow = 0;
            RefreshHudSettings();
        }

        public void HideHudSettings()
        {
            if (_hudPanel != null) _hudPanel.SetActive(false);
        }

        /// <summary>↑↓：在「HUD 布局」和「图标风格」两行之间切换。</summary>
        public void HudSettingsMoveRow(int step)
        {
            if (!IsHudSettingsOpen || step == 0) return;
            _hudRow = 1 - _hudRow;
            RefreshHudSettings();
        }

        /// <summary>←→：当前行换到上一个 / 下一个选项，立刻生效（自动保存）。</summary>
        public void HudSettingsStep(int step)
        {
            if (!IsHudSettingsOpen || step == 0) return;
            if (_hudRow == 0)
            {
                int n = HudPrefs.LayoutCount;
                ChooseHudLayout((((int)HudPrefs.Layout + step) % n + n) % n);
            }
            else
            {
                int n = HudPrefs.IconSetCount;
                ChooseHudIcons((((int)HudPrefs.Icons + step) % n + n) % n);
            }
        }

        private void ChooseHudLayout(int index)
        {
            if (index < 0 || index >= HudPrefs.LayoutCount) return;
            _hudRow = 0;
            HudPrefs.Layout = (HudLayout)index;
            RefreshHudSettings();
        }

        private void ChooseHudIcons(int index)
        {
            if (index < 0 || index >= HudPrefs.IconSetCount) return;
            _hudRow = 1;
            HudPrefs.Icons = (HudIconSet)index;
            RefreshHudSettings();
        }

        private void RefreshHudSettings()
        {
            int layout = (int)HudPrefs.Layout;
            int icons = (int)HudPrefs.Icons;
            for (int i = 0; i < HudPrefs.LayoutCount; i++)
            {
                bool on = i == layout;
                if (_hudLayoutTiles[i] != null) _hudLayoutTiles[i].color = on ? ButtonColor : new Color(InkColor.r, InkColor.g, InkColor.b, 0.08f);
                if (_hudLayoutNames[i] != null) _hudLayoutNames[i].color = on ? ButtonTextColor : InkColor;
                if (_hudLayoutDescs[i] != null) _hudLayoutDescs[i].color = on ? new Color(ButtonTextColor.r, ButtonTextColor.g, ButtonTextColor.b, 0.85f) : new Color(InkColor.r, InkColor.g, InkColor.b, 0.7f);
                if (_hudLayoutCursors[i] != null) _hudLayoutCursors[i].color = on && _hudRow == 0 ? new Color(0.92f, 0.7f, 0.25f, 0.9f) : new Color(0f, 0f, 0f, 0f);
            }
            for (int i = 0; i < HudPrefs.IconSetCount; i++)
            {
                bool on = i == icons;
                if (_hudIconTiles[i] != null) _hudIconTiles[i].color = on ? ButtonColor : new Color(InkColor.r, InkColor.g, InkColor.b, 0.08f);
                if (_hudIconNames[i] != null) _hudIconNames[i].color = on ? ButtonTextColor : InkColor;
                if (_hudIconCursors[i] != null) _hudIconCursors[i].color = on && _hudRow == 1 ? new Color(0.92f, 0.7f, 0.25f, 0.9f) : new Color(0f, 0f, 0f, 0f);
            }
            for (int r = 0; r < _hudRowLabels.Length; r++)
            {
                if (_hudRowLabels[r] == null) continue;
                _hudRowLabels[r].color = r == _hudRow ? InkColor : new Color(InkColor.r, InkColor.g, InkColor.b, 0.55f);
                _hudRowLabels[r].text = (r == _hudRow ? "▶ " : "   ") + (r == 0 ? "HUD 布局" : "图标风格");
            }
        }

        // ---------------- 退出游戏 ----------------

        /// <summary>标题页「退出游戏」/ Esc：先弹确认框。</summary>
        public void ShowQuit()
        {
            EnsureUI();
            _quit.SetActive(true);
            _quit.transform.SetAsLastSibling();
        }

        public void HideQuit()
        {
            if (_quit != null) _quit.SetActive(false);
        }

        /// <summary>确认退出：打包后关闭游戏；编辑器里停止 Play。</summary>
        public void ConfirmQuit()
        {
            TurnVolume.Flush();
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- 暂停 / 结算 ----------------

        public void SetPause(bool open)
        {
            EnsureUI();
            _pause.SetActive(open);
        }

        /// <summary>重填暂停界面左侧的「已选强化」列表（打开暂停前调用）。header = 列表标题。</summary>
        public void SetBuildList(string header, System.Collections.Generic.IList<BuildEntry> entries)
        {
            EnsureUI();
            if (_buildHeader != null) _buildHeader.text = header;
            for (int i = _buildContent.childCount - 1; i >= 0; i--) Destroy(_buildContent.GetChild(i).gameObject);
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BuildEntry e = entries[i];
                    if (e.Header) MakeBuildHeader(e);
                    else MakeBuildCard(e);
                }
            }
            if (_buildScroll != null) _buildScroll.verticalNormalizedPosition = 1f;
        }

        private void MakeBuildHeader(BuildEntry e)
        {
            Text t = MakeText("Group", _buildContent, e.Text, 24, FontStyle.Bold);
            t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.Lerp(e.Color, InkColor, 0.35f);
            t.supportRichText = true;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 34f;
        }

        private void MakeBuildCard(BuildEntry e)
        {
            Image card = MakeImage("Build", _buildContent, new Color(1f, 0.99f, 0.95f, 1f), false);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 10, 7, 7);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Image stripe = MakeImage("Stripe", card.rectTransform, e.Color, false);
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stripe.rectTransform.anchorMin = Vector2.zero;
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(7f, 0f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;

            Text t = MakeText("Text", card.rectTransform, e.Text, 22, FontStyle.Normal);
            t.alignment = TextAnchor.UpperLeft;
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
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
            BuildVolume(root.transform);
            BuildDisplay(root.transform);
            BuildHudSettings(root.transform);
            BuildQuit(root.transform);
        }

        private void BuildVolume(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Volume", out _volume);
            Text title = MakeText("Title", card, "音量设置", 60, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.82f), new Vector2(1f, 0.97f), Vector2.zero);

            float[] rows = { 0.67f, 0.52f, 0.37f };
            for (int i = 0; i < TurnVolume.ChannelCount; i++)
            {
                int index = i;
                float y = rows[i];
                Image row = MakeImage("Row" + i, card, new Color(0f, 0f, 0f, 0f), false);
                Stretch(row.rectTransform, new Vector2(0.03f, y - 0.06f), new Vector2(0.97f, y + 0.06f), Vector2.zero);
                _volumeRows[i] = row;

                Text label = MakeText("Label", card, TurnVolume.DisplayName((TurnVolume.Channel)i), 34, FontStyle.Bold);
                label.alignment = TextAnchor.MiddleLeft;
                Stretch(label.rectTransform, new Vector2(0.07f, y - 0.05f), new Vector2(0.27f, y + 0.05f), Vector2.zero);

                Slider slider = MakeSlider("Slider" + i, card, new Vector2(0.28f, y - 0.022f), new Vector2(0.8f, y + 0.022f));
                slider.onValueChanged.AddListener(v => OnVolumeSlider(index, v));
                _volumeSliders[i] = slider;

                Text value = MakeText("Value", card, "", 32, FontStyle.Normal);
                value.alignment = TextAnchor.MiddleRight;
                Stretch(value.rectTransform, new Vector2(0.82f, y - 0.05f), new Vector2(0.94f, y + 0.05f), Vector2.zero);
                _volumeValues[i] = value;
            }

            Button mute = MakeButton("Mute", card, "静音：关", new Vector2(0.2f, 0.17f), new Vector2(230f, 68f), ToggleMute);
            _muteLabel = mute.GetComponentInChildren<Text>();
            MakeButton("Reset", card, "恢复默认", new Vector2(0.5f, 0.17f), new Vector2(230f, 68f), ResetVolume);
            MakeButton("Back", card, "返回（Esc）", new Vector2(0.8f, 0.17f), new Vector2(230f, 68f), HideVolume);

            Text hint = MakeText("Hint", card, "↑↓ 选择 · ←→ 调节（每次 5%） · 拖动滑条也可以 · Esc 返回 · 设置自动保存", 22, FontStyle.Normal);
            hint.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.6f);
            Stretch(hint.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.08f), Vector2.zero);
            _volume.SetActive(false);
        }

        private void BuildDisplay(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Display", out _display);
            Text title = MakeText("Title", card, "窗口设置", 60, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.82f), new Vector2(1f, 0.97f), Vector2.zero);

            float[] rows = { 0.67f, 0.5f, 0.33f };
            for (int i = 0; i < TurnDisplay.ModeCount; i++)
            {
                int index = i;
                float y = rows[i];
                var mode = (TurnDisplay.Mode)i;

                // 键盘光标：选项外面一圈半透明底
                Image cursor = MakeImage("Cursor" + i, card, new Color(0f, 0f, 0f, 0f), false);
                Stretch(cursor.rectTransform, new Vector2(0.06f, y - 0.075f), new Vector2(0.94f, y + 0.075f), Vector2.zero);
                _displayCursors[i] = cursor;

                Image option = MakeImage("Option" + i, card, ButtonColor, true);
                Stretch(option.rectTransform, new Vector2(0.08f, y - 0.06f), new Vector2(0.92f, y + 0.06f), Vector2.zero);
                var button = option.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.2f, 1.15f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                Navigation nav = button.navigation;
                nav.mode = Navigation.Mode.None; // 方向键由 GameManager 统一处理
                button.navigation = nav;
                button.onClick.AddListener(() => ChooseDisplay(index));
                _displayOptions[i] = option;

                Text name = MakeText("Name", option.rectTransform, TurnDisplay.DisplayName(mode), 34, FontStyle.Bold);
                name.alignment = TextAnchor.MiddleLeft;
                Stretch(name.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.45f, 1f), Vector2.zero);
                _displayNames[i] = name;

                Text desc = MakeText("Desc", option.rectTransform, TurnDisplay.Describe(mode), 22, FontStyle.Normal);
                desc.alignment = TextAnchor.MiddleRight;
                Stretch(desc.rectTransform, new Vector2(0.4f, 0f), new Vector2(0.96f, 1f), Vector2.zero);
                _displayDescs[i] = desc;
            }

            MakeButton("Back", card, "返回（Esc）", new Vector2(0.5f, 0.15f), new Vector2(260f, 68f), HideDisplay);

            string hintText = Application.isEditor
                ? "↑↓ 选择 · Space / Enter 确认 · Esc 返回 · 编辑器里只记录选择，打包后才会切换"
                : "↑↓ 选择 · Space / Enter 确认 · Esc 返回 · 设置自动保存 · Alt + Enter 切换全屏";
            Text hint = MakeText("Hint", card, hintText, 22, FontStyle.Normal);
            hint.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.6f);
            Stretch(hint.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.08f), Vector2.zero);
            _display.SetActive(false);
        }

        /// <summary>界面设置：上排 4 种 HUD 布局（带预览图），下排 3 种图标风格（带图标示例）。</summary>
        private void BuildHudSettings(Transform parent)
        {
            RectTransform card = BuildCard(parent, "HudSettings", out _hudPanel);
            card.sizeDelta = new Vector2(1500f, 840f);
            Text title = MakeText("Title", card, "界面设置", 56, FontStyle.Bold);
            PlaceCenter(title.rectTransform, 0f, 360f, 1400f, 80f);

            _hudRowLabels[0] = MakeText("LayoutLabel", card, "HUD 布局", 30, FontStyle.Bold);
            _hudRowLabels[0].alignment = TextAnchor.MiddleLeft;
            PlaceCenter(_hudRowLabels[0].rectTransform, -440f, 296f, 520f, 40f);
            float[] lx = { -540f, -180f, 180f, 540f };
            for (int i = 0; i < HudPrefs.LayoutCount; i++)
            {
                int index = i;
                var layout = (HudLayout)i;
                Image cursor = MakeImage("LayoutCursor" + i, card, new Color(0f, 0f, 0f, 0f), false);
                PlaceCenter(cursor.rectTransform, lx[i], 130f, 346f, 266f);
                _hudLayoutCursors[i] = cursor;
                Image tile = MakeImage("Layout" + i, card, ButtonColor, true);
                PlaceCenter(tile.rectTransform, lx[i], 130f, 330f, 250f);
                AddClick(tile, () => ChooseHudLayout(index));
                _hudLayoutTiles[i] = tile;
                Image preview = MakeImage("Preview", tile.rectTransform, Color.white, false);
                preview.sprite = Resources.Load<Sprite>("HUD/Preview/" + HudPrefs.PreviewName(layout));
                preview.preserveAspect = true;
                PlaceCenter(preview.rectTransform, 0f, 30f, 304f, 171f);
                Text name = MakeText("Name", tile.rectTransform, HudPrefs.DisplayName(layout), 24, FontStyle.Bold);
                PlaceCenter(name.rectTransform, 0f, -78f, 320f, 32f);
                _hudLayoutNames[i] = name;
                Text desc = MakeText("Desc", tile.rectTransform, HudPrefs.Describe(layout), 16, FontStyle.Normal);
                PlaceCenter(desc.rectTransform, 0f, -106f, 320f, 24f);
                _hudLayoutDescs[i] = desc;
            }

            _hudRowLabels[1] = MakeText("IconLabel", card, "图标风格", 30, FontStyle.Bold);
            _hudRowLabels[1].alignment = TextAnchor.MiddleLeft;
            PlaceCenter(_hudRowLabels[1].rectTransform, -440f, -44f, 520f, 40f);
            float[] ix = { -480f, 0f, 480f };
            string[] keys = { "roll", "hp", "kill", "gun" };
            for (int i = 0; i < HudPrefs.IconSetCount; i++)
            {
                int index = i;
                var set = (HudIconSet)i;
                Image cursor = MakeImage("IconCursor" + i, card, new Color(0f, 0f, 0f, 0f), false);
                PlaceCenter(cursor.rectTransform, ix[i], -160f, 456f, 166f);
                _hudIconCursors[i] = cursor;
                Image tile = MakeImage("Icons" + i, card, ButtonColor, true);
                PlaceCenter(tile.rectTransform, ix[i], -160f, 440f, 150f);
                AddClick(tile, () => ChooseHudIcons(index));
                _hudIconTiles[i] = tile;
                // 图标放在一块纸色底上，选中（深色底）时也看得清
                Image strip = MakeImage("Strip", tile.rectTransform, PaperColor, false);
                PlaceCenter(strip.rectTransform, 0f, 20f, 400f, 84f);
                for (int k = 0; k < keys.Length; k++)
                {
                    Image icon = MakeImage(keys[k], strip.rectTransform, Color.white, false);
                    icon.sprite = PaperHud.IconSprite(set, keys[k]);
                    icon.preserveAspect = true;
                    PlaceCenter(icon.rectTransform, -138f + 92f * k, 0f, 76f, 76f);
                }
                Text name = MakeText("Name", tile.rectTransform, HudPrefs.DisplayName(set), 26, FontStyle.Bold);
                PlaceCenter(name.rectTransform, 0f, -50f, 400f, 36f);
                _hudIconNames[i] = name;
            }

            MakeButton("Back", card, "返回（Esc）", new Vector2(0.5f, 0.1f), new Vector2(260f, 64f), HideHudSettings);
            Text hint = MakeText("Hint", card, "↑↓ 切换行 · ←→ 选择（立刻生效、自动保存） · 也可以直接点 · Esc / H 返回", 22, FontStyle.Normal);
            hint.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.6f);
            Stretch(hint.rectTransform, new Vector2(0f, 0.005f), new Vector2(1f, 0.05f), Vector2.zero);
            _hudPanel.SetActive(false);
        }

        /// <summary>以父物体中心为原点摆放（y 向上）。</summary>
        private static void PlaceCenter(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void AddClick(Image target, Action onClick)
        {
            var button = target.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None; // 方向键由 GameManager 统一处理
            button.navigation = nav;
            button.onClick.AddListener(() => { if (onClick != null) onClick(); });
        }

        private void BuildQuit(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Quit", out _quit);
            card.sizeDelta = new Vector2(720f, 340f);
            Text title = MakeText("Title", card, "退出游戏", 60, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.94f), Vector2.zero);
            Text body = MakeText("Body", card, "设置和解锁进度都已保存，确定要退出吗？", 30, FontStyle.Normal);
            Stretch(body.rectTransform, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.66f), Vector2.zero);
            MakeButton("Confirm", card, "退出（Enter）", new Vector2(0.3f, 0.22f), new Vector2(260f, 72f), ConfirmQuit);
            MakeButton("Cancel", card, "取消（Esc）", new Vector2(0.7f, 0.22f), new Vector2(260f, 72f), HideQuit);
            _quit.SetActive(false);
        }

        /// <summary>纯色的横向滑条（底槽 + 填充 + 把手），0–1。</summary>
        private static Slider MakeSlider(string name, Transform parent, Vector2 min, Vector2 max)
        {
            Image track = MakeImage(name, parent, new Color(InkColor.r, InkColor.g, InkColor.b, 0.18f), true);
            Stretch(track.rectTransform, min, max, Vector2.zero);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(track.rectTransform, false);
            Stretch((RectTransform)fillArea.transform, Vector2.zero, Vector2.one, Vector2.zero);
            Image fill = MakeImage("Fill", fillArea.transform, ButtonColor, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(track.rectTransform, false);
            Stretch((RectTransform)handleArea.transform, Vector2.zero, Vector2.one, Vector2.zero);
            Image handle = MakeImage("Handle", handleArea.transform, new Color(0.5f, 0.32f, 0.2f, 1f), true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(26f, 26f); // 比底槽高出一截，好抓
            handle.rectTransform.anchoredPosition = Vector2.zero;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            Navigation nav = slider.navigation;
            nav.mode = Navigation.Mode.None; // 方向键由 GameManager 统一处理，避免 EventSystem 也去移动滑条
            slider.navigation = nav;
            return slider;
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

            MakeButton("StartButton", page.rectTransform, "开始游戏（Space）", new Vector2(0.5f, 0.27f), new Vector2(400f, 84f), StartFromTitle);
            MakeButton("VolumeButton", page.rectTransform, "音量设置（V）", new Vector2(0.26f, 0.19f), new Vector2(260f, 60f), ShowVolume);
            MakeButton("DisplayButton", page.rectTransform, "窗口设置（O）", new Vector2(0.42f, 0.19f), new Vector2(260f, 60f), ShowDisplay);
            MakeButton("HudButton", page.rectTransform, "界面设置（H）", new Vector2(0.58f, 0.19f), new Vector2(260f, 60f), ShowHudSettings);
            MakeButton("QuitButton", page.rectTransform, "退出游戏（Esc）", new Vector2(0.74f, 0.19f), new Vector2(260f, 60f), ShowQuit);
            _title.gameObject.SetActive(false);
        }

        private void BuildPause(Transform parent)
        {
            RectTransform card = BuildCard(parent, "Pause", out _pause);
            Text title = MakeText("Title", card, "暂停", 64, FontStyle.Bold);
            Stretch(title.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), Vector2.zero);
            MakeButton("Resume", card, "继续（Esc）", new Vector2(0.5f, 0.6f), new Vector2(420f, 70f), () => Call(_onResume));
            MakeButton("Volume", card, "音量设置（V）", new Vector2(0.2f, 0.445f), new Vector2(250f, 70f), ShowVolume);
            MakeButton("Display", card, "窗口设置（O）", new Vector2(0.5f, 0.445f), new Vector2(250f, 70f), ShowDisplay);
            MakeButton("Hud", card, "界面设置（H）", new Vector2(0.8f, 0.445f), new Vector2(250f, 70f), ShowHudSettings);
            MakeButton("Restart", card, "重打本关（R）", new Vector2(0.5f, 0.29f), new Vector2(420f, 70f), () => Call(_onRestartLevel));
            MakeButton("Title", card, "回到标题（T）", new Vector2(0.5f, 0.135f), new Vector2(420f, 70f), () => Call(_onTitle));
            BuildBuildList(_pause.transform);
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

        /// <summary>暂停界面左侧、血量 / 弹药行下方的「已选强化」面板：标题 + 可滚动的卡牌列表（鼠标滚轮滚动）。</summary>
        private void BuildBuildList(Transform parent)
        {
            Image panel = MakeImage("Builds", parent, new Color(PaperColor.r, PaperColor.g, PaperColor.b, 0.96f), true);
            Stretch(panel.rectTransform, new Vector2(0.022f, 0.06f), new Vector2(0.255f, 0.84f), Vector2.zero);

            _buildHeader = MakeText("Header", panel.rectTransform, "已选强化", 30, FontStyle.Bold);
            _buildHeader.alignment = TextAnchor.MiddleLeft;
            _buildHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            _buildHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
            _buildHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            _buildHeader.rectTransform.offsetMin = new Vector2(20f, -58f);
            _buildHeader.rectTransform.offsetMax = new Vector2(-16f, -8f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.rectTransform, false);
            var vp = (RectTransform)viewport.transform;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(12f, 12f);
            vp.offsetMax = new Vector2(-12f, -62f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp, false);
            _buildContent = (RectTransform)content.transform;
            _buildContent.anchorMin = new Vector2(0f, 1f);
            _buildContent.anchorMax = new Vector2(1f, 1f);
            _buildContent.pivot = new Vector2(0.5f, 1f);
            _buildContent.offsetMin = Vector2.zero;
            _buildContent.offsetMax = Vector2.zero;
            var list = content.AddComponent<VerticalLayoutGroup>();
            list.spacing = 6f;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _buildScroll = panel.gameObject.AddComponent<ScrollRect>();
            _buildScroll.viewport = vp;
            _buildScroll.content = _buildContent;
            _buildScroll.horizontal = false;
            _buildScroll.vertical = true;
            _buildScroll.movementType = ScrollRect.MovementType.Clamped;
            _buildScroll.scrollSensitivity = 40f;
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
