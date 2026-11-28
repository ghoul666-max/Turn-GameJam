using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 绘本纸片风格 HUD（11/28，Figma「Turn · HUD（绘本纸片风格）」A / B / C 三版 + 三套图标）。
    /// 运行时自己搭 UI（场景里不用摆），布局 / 图标风格读 <see cref="HudPrefs"/>，在标题页或暂停菜单的「界面设置（H）」里切换，改了立刻重搭。
    /// 选「原版白模」时本组件隐藏，HudUI 的文字 HUD 重新显示。屏幕闪色、升级三选一仍由 HudUI 负责（HudUI 的 Canvas 在本 Canvas 之上）。
    /// 单向：每帧只读系统状态；只订阅「翻滚被拒绝」做一次性闪红。
    /// 贴图：Resources/HUD/Prims（白色基础形状，代码着色）、Resources/HUD/Icons/{Ink|Picture|Stamp}。
    /// 字体：Resources/HUD/Fonts/ZCOOLKuaiLe-Regular、Fredoka-SemiBold（没有就退回系统字体）。
    /// </summary>
    public sealed class PaperHud : MonoBehaviour
    {
        // ---------------- 颜色（和 Figma 色板一致） ----------------
        private static readonly Color Paper = Hex("F7EDD9");
        private static readonly Color Paper2 = Hex("EFE1C6");
        private static readonly Color Kraft = Hex("C9A57C");
        private static readonly Color Track = Hex("E2CFAE");
        private static readonly Color Ink = Hex("3B2A22");
        private static readonly Color Ink2 = Hex("7A6252");
        private static readonly Color Red = Hex("D2483B");
        private static readonly Color Gold = Hex("EBB33F");
        private static readonly Color Purple = Hex("8C62B9");
        private static readonly Color PurpleLight = Hex("CDB3E6");
        private static readonly Color PurpleDark = Hex("6F4A98");
        private static readonly Color Grey = Hex("A99C8C");

        private const float RefW = 1920f;
        private const float RefH = 1080f;
        /// <summary>C 版脚下翻滚环的半径（世界单位）。</summary>
        private const float RingRadius = 0.85f;
        private const int RingSegments = 64;

        private static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }

        private static string HexOf(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        // ---------------- 字体 / 贴图 ----------------
        private static Font _cnFont;
        private static Font _numFont;
        private static bool _numIsFallback;
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        private static Font CnFont
        {
            get
            {
                if (_cnFont == null)
                {
                    _cnFont = Resources.Load<Font>("HUD/Fonts/ZCOOLKuaiLe-Regular");
                    if (_cnFont == null) _cnFont = UIFont.Chinese;
                }
                return _cnFont;
            }
        }

        private static Font NumFont
        {
            get
            {
                if (_numFont == null)
                {
                    _numFont = Resources.Load<Font>("HUD/Fonts/Fredoka-SemiBold");
                    if (_numFont == null)
                    {
                        _numIsFallback = true;
                        _numFont = Font.CreateDynamicFontFromOSFont(
                            new[] { "Arial Rounded MT Bold", "Segoe UI Semibold", "Microsoft YaHei UI", "Arial" }, 32);
                    }
                }
                return _numFont;
            }
        }

        private static Sprite Load(string path)
        {
            Sprite s;
            if (SpriteCache.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            SpriteCache[path] = s;
            return s;
        }

        private static Sprite Prim(string name) { return Load("HUD/Prims/" + name); }

        /// <summary>当前图标风格下的图标（翻滚 roll / 生命 hp / 等级 lv / 计时 time / 击杀 kill / 水枪 gun / 浮游炮 drone / 水渍 mark）；没有返回 null。</summary>
        public static Sprite IconSprite(HudIconSet set, string key) { return Load("HUD/Icons/" + HudPrefs.Folder(set) + "/" + key); }

        // ---------------- 绑定 ----------------
        private ITurnContext _ctx;
        private HudUI _classic;
        private Transform _player;
        private Camera _camera;
        private Material _lineMaterial;
        private SpriteRenderer[] _playerRenderers;
        private int _rendererRefresh;

        private Canvas _canvas;
        private RectTransform _canvasRt;
        private RectTransform _root;
        private HudLayout _layout;
        private HudIconSet _icons;
        private float _rejectTimer;

        // ---------------- 组件引用（各版本有的就填，没有就是 null） ----------------
        private sealed class Bar
        {
            public RectTransform Fill;
            public float Width;
            public float Height;
            public bool Stretch;

            public void Set(float ratio)
            {
                if (Fill == null) return;
                ratio = Mathf.Clamp01(ratio);
                bool show = ratio > 0.001f;
                if (Fill.gameObject.activeSelf != show) Fill.gameObject.SetActive(show);
                if (!show) return;
                if (Stretch) Fill.anchorMax = new Vector2(ratio, 1f);
                else Fill.sizeDelta = new Vector2(Mathf.Max(Height, Width * ratio), Height);
            }
        }

        private Text _hpNum;
        private Bar _hpBar;
        private Image _hpEdge;
        private Image _hpPulse;
        private Image _hpIcon;
        private GameObject _invTag;
        private Text _lvNum;
        private Bar _xpBar;
        private Text _xpNum;
        private Text _time;
        private Text _stage;
        private Text _next;
        private GameObject _nextRoot;
        private Text _kills;
        private Image _wFrame;
        private Image _wIcon;
        private Text _wChar;
        private Text _wName;
        private RectTransform _pipRoot;
        private readonly List<Image> _pipFills = new List<Image>();
        private readonly List<Image> _pipLines = new List<Image>();
        private Vector2 _pipSize;
        private float _pipGap;
        private int _pipCells = -1;
        private Text _ammo;
        private bool _ammoShowCap;
        private GameObject _markRoot;
        private Image _markIcon;
        private Text _markText;
        private bool _markCountOnly;
        private GameObject _emptyStamp;
        private GameObject _subRoot;
        private Text _subName;
        private Image _subIcon;
        private RectTransform _subNameRt;
        private float _subNameX;
        private float _subNameXNoIcon;
        private bool _subPrefix;

        // 翻滚圆牌（A / B）或角色旁小圆章（C）
        private GameObject _glow;
        private Image _face;
        private Image _ringTrack;
        private Image _ringFill;
        private Image _rollIcon;
        private RectTransform _rollIconRt;
        private Vector2 _rollIconBig;   // x = 尺寸，y = 竖直偏移（向下为正）
        private Vector2 _rollIconSmall;
        private Vector2 _rollCenter;
        private Text _rollSec;
        private Text _rollLabel;
        private GameObject _tagRoot;
        private Image _tagBg;
        private Text _tagText;
        private bool _tagAlways;

        // C 版：跟随角色
        private RectTransform _head;
        private RectTransform _foot;
        private RectTransform _badgeRt;
        private RectTransform _keyRt;
        private Image _vignette;
        private GameObject _ringGo;
        private LineRenderer _ringTrackLine;
        private LineRenderer _ringFillLine;
        private LineRenderer _ringOuterLine;
        private readonly Vector3[] _ringPoints = new Vector3[RingSegments + 1];

        // ---------------- 生命周期 ----------------

        private void OnEnable()
        {
            HudPrefs.Changed += OnPrefsChanged;
        }

        private void OnDisable()
        {
            HudPrefs.Changed -= OnPrefsChanged;
        }

        private void OnDestroy()
        {
            Unbind();
            DestroyRing();
        }

        /// <summary>每局开始时由 GameManager 调用。player = 主角的场景物体（C 版跟随、脚下环），lineMaterial = 画线用材质（可为 null）。</summary>
        public void Bind(ITurnContext ctx, HudUI classic, Transform player, Camera cam, Material lineMaterial)
        {
            Unbind();
            _ctx = ctx;
            _classic = classic;
            _player = player;
            _camera = cam;
            _lineMaterial = lineMaterial;
            _playerRenderers = null;
            _rejectTimer = 0f;
            if (_ctx != null) _ctx.Events.Subscribe<RollRejectedEvent>(OnRollRejected);
            Rebuild();
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<RollRejectedEvent>(OnRollRejected);
            _ctx = null;
        }

        private void OnRollRejected(RollRejectedEvent evt)
        {
            _rejectTimer = 0.4f;
        }

        private void OnPrefsChanged()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            EnsureCanvas();
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            ClearRefs();
            DestroyRing();

            _layout = HudPrefs.Layout;
            _icons = HudPrefs.Icons;
            bool classic = _layout == HudLayout.Classic;
            if (_classic != null) _classic.SetClassicWidgetsVisible(classic);

            _root = Node("Layout_" + _layout, _canvasRt);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            if (classic)
            {
                _root.gameObject.SetActive(false);
                return;
            }
            switch (_layout)
            {
                case HudLayout.SplitB: BuildB(); break;
                case HudLayout.FollowC: BuildC(); break;
                default: BuildA(); break;
            }
            if (_ctx != null) Refresh(0f);
        }

        private void ClearRefs()
        {
            _hpNum = null; _hpBar = null; _hpEdge = null; _hpPulse = null; _hpIcon = null; _invTag = null;
            _lvNum = null; _xpBar = null; _xpNum = null;
            _time = null; _stage = null; _next = null; _nextRoot = null; _kills = null;
            _wFrame = null; _wIcon = null; _wChar = null; _wName = null; _pipRoot = null;
            _pipFills.Clear(); _pipLines.Clear(); _pipCells = -1;
            _ammo = null; _markRoot = null; _markIcon = null; _markText = null; _emptyStamp = null;
            _subRoot = null; _subName = null; _subIcon = null; _subNameRt = null;
            _glow = null; _face = null; _ringTrack = null; _ringFill = null; _rollIcon = null; _rollIconRt = null;
            _rollSec = null; _rollLabel = null; _tagRoot = null; _tagBg = null; _tagText = null;
            _head = null; _foot = null; _badgeRt = null; _keyRt = null; _vignette = null;
            _ammoShowCap = true; _markCountOnly = false; _subPrefix = false; _tagAlways = false;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("PaperHud", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -2; // HudUI（闪色 / 升级三选一，0）之下、困意遮罩（-5）之上
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRt = (RectTransform)go.transform;
        }

        // ---------------- A 版：四角分散 ----------------

        private void BuildA()
        {
            XpStrip(72f, 24f, 28f);
            RectTransform badge = Group("等级", 0f, 0f, 160f, 120f, 0f, 1f);
            LvBadge(badge, 71f, 38f, 34f, 13, 30);

            // 生命（左上）
            RectTransform hp = Group("生命", 32f, 88f, 390f, 62f, 0f, 1f);
            HpPulse(hp, 390f, 62f);
            Image edge;
            Card(hp, "底卡", 0f, 0f, 390f, 62f, Paper, true, out edge);
            _hpEdge = edge;
            _hpIcon = Icon(hp, "心", "hp", 36f, 32f, 58f);
            _hpBar = MakeBar(hp, "生命条", 66f, 20f, 214f, 22f, Red);
            _hpNum = Txt(hp, "生命数值", "", 26, Ink, true, TextAnchor.MiddleRight, 270f, 10f, 104f, 42f);
            InvTag(hp, 66f, -14f);

            StageCardA();
            KillCard(1728f, 88f, 160f, 54f, 26);
            WeaponCardA(32f, 906f);
            SubTagA(484f, 990f);

            RectTransform roll = Group("翻滚", 960f - 120f, 964f - 150f, 240f, 270f, 0.5f, 0f);
            Medal(roll, 120f, 150f, 66f, false);
        }

        private void StageCardA()
        {
            RectTransform g = Group("关卡计时", 960f - 120f, 64f, 240f, 140f, 0.5f, 1f);
            Image line;
            Card(g, "底卡", 0f, 0f, 240f, 98f, Paper, true, out line);
            _stage = Txt(g, "关卡波次", "", 18, Ink2, false, TextAnchor.MiddleCenter, 0f, 8f, 240f, 28f);
            bool hasIcon = IconSprite(_icons, "time") != null;
            if (hasIcon) Icon(g, "计时图标", "time", 30f, 64f, 44f);
            _time = Txt(g, "过关倒计时", "", 46, Ink, true, TextAnchor.MiddleCenter, hasIcon ? 30f : 0f, 36f, hasIcon ? 210f : 240f, 58f);
            _nextRoot = Node("下一波", g).gameObject;
            Local((RectTransform)_nextRoot.transform, 58f, 104f, 124f, 28f);
            Card(_nextRoot.transform, "底卡", 0f, 0f, 124f, 28f, Paper2, true, out line);
            _next = Txt(_nextRoot.transform, "下一波", "", 15, Ink2, false, TextAnchor.MiddleCenter, 0f, 0f, 124f, 28f);
        }

        private void KillCard(float x, float y, float w, float h, int size)
        {
            RectTransform g = Group("击杀", x, y, w, h, 1f, 1f);
            Image line;
            Card(g, "底卡", 0f, 0f, w, h, Paper, true, out line);
            Icon(g, "击杀图标", "kill", h * 0.58f, h * 0.5f, h * 0.95f);
            _kills = Txt(g, "击杀数", "", size, Ink, true, TextAnchor.MiddleRight, w * 0.35f, 0f, w * 0.65f - 16f, h);
        }

        private void WeaponCardA(float x, float y)
        {
            RectTransform g = Group("主武器", x, y, 430f, 140f, 0f, 0f);
            Image line;
            Card(g, "底卡", 0f, 0f, 430f, 140f, Paper, true, out line);
            WeaponFrame(g, 16f, 16f, 108f, 96f, 52);
            _wName = Txt(g, "武器名", "", 26, Ink, false, TextAnchor.MiddleLeft, 142f, 10f, 270f, 40f);
            Pips(g, 142f, 58f, new Vector2(10f, 24f), 3.2f);
            _ammo = Txt(g, "弹药数", "", 26, Ink, true, TextAnchor.MiddleLeft, 142f, 96f, 150f, 36f);
            MarkChip(g, 300f, 94f, 112f, 30f, 16);
            EmptyStamp(g, 236f, 88f, 180f, 38f, 18, -6f);
        }

        private void SubTagA(float x, float y)
        {
            RectTransform g = Group("副武器", x, y, 200f, 56f, 0f, 0f);
            _subRoot = g.gameObject;
            Image line;
            Card(g, "底卡", 0f, 0f, 200f, 56f, Paper2, true, out line);
            Img(g, "副底", Prim("circle_fill"), Ink, 13f, 13f, 30f, 30f);
            Txt(g, "副", "副", 15, Paper, false, TextAnchor.MiddleCenter, 13f, 13f, 30f, 30f);
            _subIcon = Icon(g, "副武器图标", "drone", 66f, 26f, 50f);
            _subName = Txt(g, "副武器名", "", 18, Ink, false, TextAnchor.MiddleLeft, 92f, 10f, 108f, 36f);
            _subNameRt = _subName.rectTransform;
            _subNameX = 92f;
            _subNameXNoIcon = 52f;
        }

        // ---------------- B 版：状态左上 · 操作右下 ----------------

        private void BuildB()
        {
            // 状态卡：等级 + 生命 + 经验
            RectTransform s = Group("状态卡", 28f, 24f, 470f, 112f, 0f, 1f);
            HpPulse(s, 470f, 112f);
            Image edge;
            Card(s, "底卡", 0f, 0f, 470f, 112f, Paper, true, out edge);
            _hpEdge = edge;
            LvBadge(s, 55f, 56f, 40f, 14, 34);
            _hpIcon = Icon(s, "心", "hp", 126f, 40f, 54f);
            _hpBar = MakeBar(s, "生命条", 150f, 28f, 200f, 24f, Red);
            _hpNum = Txt(s, "生命数值", "", 26, Ink, true, TextAnchor.MiddleRight, 350f, 20f, 104f, 40f);
            Txt(s, "经验字", "经验", 14, Ink2, false, TextAnchor.MiddleLeft, 108f, 75f, 40f, 20f);
            _xpBar = MakeBar(s, "经验条", 150f, 78f, 200f, 14f, Gold);
            _xpNum = Txt(s, "经验数值", "", 14, Ink2, true, TextAnchor.MiddleRight, 350f, 74f, 104f, 22f);
            InvTag(s, 150f, -14f);

            // 书签计时（从顶边垂下）
            RectTransform r = Group("书签计时", 960f - 94f, 0f, 188f, 170f, 0.5f, 1f);
            Img(r, "书签", Prim("bookmark"), Color.white, 0f, -6f, 188f, 142f);
            _stage = Txt(r, "关卡波次", "", 16, Paper2, false, TextAnchor.MiddleCenter, 0f, 12f, 188f, 26f);
            _time = Txt(r, "过关倒计时", "", 42, Paper, true, TextAnchor.MiddleCenter, 0f, 40f, 188f, 52f);
            _nextRoot = Node("下一波", r).gameObject;
            Local((RectTransform)_nextRoot.transform, 38f, 128f, 112f, 26f);
            Image line;
            Card(_nextRoot.transform, "底卡", 0f, 0f, 112f, 26f, Paper2, false, out line);
            _next = Txt(_nextRoot.transform, "下一波", "", 14, Ink2, false, TextAnchor.MiddleCenter, 0f, 0f, 112f, 26f);

            KillCard(1742f, 28f, 150f, 50f, 24);

            // 右下操作簇：武器条 + 副武器 + 翻滚大圆牌
            RectTransform a = Group("操作簇", 1340f, 840f, 580f, 240f, 1f, 0f);
            const float oy = 20f; // 组内整体下移（给光晕留边）
            Card(a, "武器条", 10f, 88f + oy, 400f, 112f, Paper, true, out line);
            WeaponFrame(a, 24f, 102f + oy, 84f, 76f, 40);
            _wName = Txt(a, "武器名", "", 22, Ink, false, TextAnchor.MiddleLeft, 124f, 96f + oy, 200f, 34f);
            Pips(a, 124f, 142f + oy, new Vector2(9f, 20f), 3f);
            _ammo = Txt(a, "弹药数", "", 22, Ink, true, TextAnchor.MiddleLeft, 124f, 168f + oy, 130f, 30f);
            MarkChip(a, 260f, 168f + oy, 96f, 26f, 14);
            EmptyStamp(a, 206f, 164f + oy, 150f, 32f, 15, -5f);

            RectTransform sub = Node("副武器", a);
            Local(sub, 192f, 48f + oy, 190f, 34f);
            _subRoot = sub.gameObject;
            Card(sub, "底卡", 0f, 0f, 190f, 34f, Paper2, false, out line);
            Img(sub, "副底", Prim("circle_fill"), Ink, 9f, 6f, 22f, 22f);
            Txt(sub, "副", "副", 12, Paper, false, TextAnchor.MiddleCenter, 9f, 6f, 22f, 22f);
            _subIcon = Icon(sub, "副武器图标", "drone", 46f, 17f, 40f);
            _subName = Txt(sub, "副武器名", "", 16, Ink, false, TextAnchor.MiddleLeft, 66f, 4f, 122f, 26f);
            _subNameRt = _subName.rectTransform;
            _subNameX = 66f;
            _subNameXNoIcon = 38f;

            Medal(a, 462f, 104f + oy, 80f, true);
        }

        // ---------------- C 版：跟随角色极简 ----------------

        private void BuildC()
        {
            XpStrip(72f, 26f, 26f);
            RectTransform badge = Group("等级", 0f, 0f, 160f, 120f, 0f, 1f);
            LvBadge(badge, 71f, 37f, 32f, 12, 28);

            // 计时吊牌
            RectTransform t = Group("计时吊牌", 960f - 104f, 26f, 208f, 100f, 0.5f, 1f);
            Rope(t, 66f, 4f, 22f);
            Rope(t, 142f, 4f, -22f);
            Image line;
            Card(t, "底卡", 0f, 36f, 208f, 58f, Paper, true, out line);
            bool hasIcon = IconSprite(_icons, "time") != null;
            if (hasIcon) Icon(t, "计时图标", "time", 26f, 65f, 40f);
            _time = Txt(t, "过关倒计时", "", 34, Ink, true, TextAnchor.MiddleCenter, hasIcon ? 40f : 8f, 40f, hasIcon ? 110f : 142f, 50f);
            _stage = Txt(t, "波次", "", 14, Ink2, false, TextAnchor.MiddleCenter, 150f, 42f, 56f, 22f);
            _next = Txt(t, "下一波", "", 13, Ink2, false, TextAnchor.MiddleCenter, 150f, 64f, 56f, 22f);
            _nextRoot = _next.gameObject;

            // 击杀（顶栏右侧）
            RectTransform k = Group("击杀", 1920f - 196f, 60f, 150f, 40f, 1f, 1f);
            Card(k, "底卡", 0f, 0f, 150f, 40f, Paper, false, out line);
            Icon(k, "击杀图标", "kill", 26f, 20f, 40f);
            _kills = Txt(k, "击杀数", "", 20, Ink, true, TextAnchor.MiddleRight, 50f, 0f, 86f, 40f);

            // 武器小卡（左下）
            RectTransform w = Group("武器小卡", 28f, 990f, 250f, 70f, 0f, 0f);
            Card(w, "底卡", 0f, 0f, 250f, 70f, Paper, true, out line);
            WeaponFrame(w, 10f, 10f, 50f, 46f, 26);
            _wName = Txt(w, "武器名", "", 20, Ink, false, TextAnchor.MiddleLeft, 72f, 6f, 120f, 28f);
            _subName = Txt(w, "副武器", "", 14, Ink2, false, TextAnchor.MiddleLeft, 72f, 36f, 124f, 24f);
            _subRoot = _subName.gameObject;
            _subPrefix = true;
            _markCountOnly = true;
            MarkChip(w, 192f, 20f, 46f, 28f, 15);

            // 低血屏幕红边
            _vignette = Img(_root, "低血屏幕红边", Prim("vignette"), new Color(Red.r, Red.g, Red.b, 0f), 0f, 0f, 0f, 0f, Image.Type.Sliced);
            Stretch(_vignette.rectTransform);
            _vignette.gameObject.SetActive(false);

            // 头顶生命
            _head = Node("头顶生命", _root);
            _head.anchorMin = _head.anchorMax = new Vector2(0.5f, 0.5f);
            _head.pivot = new Vector2(0.5f, 0f);
            _head.sizeDelta = new Vector2(196f, 26f);
            _hpIcon = Icon(_head, "心", "hp", 14f, 13f, 40f);
            _hpBar = MakeBar(_head, "生命条", 30f, 6f, 120f, 14f, Red);
            _hpNum = Txt(_head, "生命数值", "", 16, Ink, true, TextAnchor.MiddleLeft, 156f, 0f, 44f, 26f);

            // 脚下：翻滚小圆章 + 键帽 + 弹药格
            _foot = Node("脚下", _root);
            _foot.anchorMin = _foot.anchorMax = new Vector2(0.5f, 0.5f);
            _foot.pivot = new Vector2(0.5f, 0.5f);
            _foot.sizeDelta = Vector2.zero;

            _badgeRt = Node("翻滚徽章", _foot);
            _badgeRt.anchorMin = _badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            _badgeRt.pivot = new Vector2(0.5f, 0.5f);
            _badgeRt.sizeDelta = new Vector2(48f, 48f);
            Img(_badgeRt, "投影", Prim("circle_fill"), Kraft, 4f, 6f, 44f, 44f);
            _face = Img(_badgeRt, "圆章", Prim("circle_fill"), Purple, 2f, 2f, 44f, 44f);
            Img(_badgeRt, "描边", Prim("circle_line"), Ink, 2f, 2f, 44f, 44f);
            _rollCenter = new Vector2(24f, 24f);
            _rollIcon = Icon(_badgeRt, "翻滚图标", "roll", 24f, 24f, 34f);
            _rollIconRt = CenterPivot(_rollIcon);
            _rollIconBig = new Vector2(34f, 0f);
            _rollIconSmall = new Vector2(0f, 0f); // C 版冷却时只显示秒数
            _rollSec = Txt(_badgeRt, "剩余秒数", "", 15, Ink2, true, TextAnchor.MiddleCenter, 0f, 0f, 48f, 48f);

            _keyRt = Node("按键 Space", _foot);
            _keyRt.anchorMin = _keyRt.anchorMax = new Vector2(0.5f, 0.5f);
            _keyRt.pivot = new Vector2(0f, 0.5f);
            _keyRt.sizeDelta = new Vector2(54f, 24f);
            Card(_keyRt, "键帽", 0f, 0f, 54f, 24f, Ink, false, out line);
            Txt(_keyRt, "Space", "Space", 12, Paper, true, TextAnchor.MiddleCenter, 0f, 0f, 54f, 24f);

            _pipRoot = Node("弹药格", _foot);
            _pipRoot.anchorMin = _pipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _pipRoot.pivot = new Vector2(0f, 1f);
            _pipRoot.sizeDelta = new Vector2(180f, 14f);
            _pipSize = new Vector2(6f, 14f);
            _pipGap = 3f;
            _ammo = Txt(_foot, "弹药数", "", 15, Ink, true, TextAnchor.MiddleLeft, 0f, 0f, 40f, 20f);
            _ammo.rectTransform.anchorMin = _ammo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ammo.rectTransform.pivot = new Vector2(0f, 1f);
            _ammoShowCap = false;
            Text empty = Txt(_foot, "空弹提示", "空弹 · 翻滚补满", 14, Red, false, TextAnchor.MiddleCenter, 0f, 0f, 200f, 22f);
            empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            empty.rectTransform.pivot = new Vector2(0.5f, 1f);
            _emptyStamp = empty.gameObject;

            CreateRing();
        }

        private void Rope(RectTransform parent, float x, float y, float angle)
        {
            Image rope = Img(parent, "挂绳", null, Ink, x - 1.25f, y, 2.5f, 34f);
            rope.rectTransform.pivot = new Vector2(0.5f, 1f);
            rope.rectTransform.anchoredPosition = new Vector2(x, -y);
            rope.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ---------------- 共用组件 ----------------

        /// <summary>顶部通栏撕边经验纸条（左右拉伸到屏幕边）。body 高 22，贴图上下各留 5 给撕边。</summary>
        private void XpStrip(float left, float right, float y)
        {
            RectTransform g = Node("经验条", _root);
            g.anchorMin = new Vector2(0f, 1f);
            g.anchorMax = new Vector2(1f, 1f);
            g.pivot = new Vector2(0f, 1f);
            g.offsetMin = new Vector2(left, -(y - 5f + 32f));
            g.offsetMax = new Vector2(-right, -(y - 5f));

            Image shadow = Img(g, "投影", Prim("torn_fill"), Kraft, 0f, 0f, 0f, 0f, Image.Type.Tiled);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(2f, -4f);
            shadow.rectTransform.offsetMax = new Vector2(2f, -4f);
            Image fill = Img(g, "纸条", Prim("torn_fill"), Paper, 0f, 0f, 0f, 0f, Image.Type.Tiled);
            Stretch(fill.rectTransform);

            RectTransform inner = Node("经验槽", g);
            Stretch(inner);
            inner.offsetMin = new Vector2(6f, 10f);
            inner.offsetMax = new Vector2(-6f, -10f);
            Image xp = Img(inner, "经验填充", Prim("pill_fill"), Gold, 0f, 0f, 0f, 0f, Image.Type.Sliced);
            RectTransform xpRt = xp.rectTransform;
            xpRt.anchorMin = Vector2.zero;
            xpRt.anchorMax = new Vector2(0.5f, 1f);
            xpRt.pivot = new Vector2(0f, 0.5f);
            xpRt.offsetMin = Vector2.zero;
            xpRt.offsetMax = Vector2.zero;
            _xpBar = new Bar { Fill = xpRt, Stretch = true };

            Image edge = Img(g, "描边", Prim("torn_line"), Ink, 0f, 0f, 0f, 0f, Image.Type.Tiled);
            Stretch(edge.rectTransform);
            Image cap = Img(g, "右端", null, Ink, 0f, 0f, 0f, 0f);
            RectTransform capRt = cap.rectTransform;
            capRt.anchorMin = new Vector2(1f, 0f);
            capRt.anchorMax = new Vector2(1f, 1f);
            capRt.pivot = new Vector2(1f, 0.5f);
            capRt.offsetMin = new Vector2(-2f, 5f);
            capRt.offsetMax = new Vector2(0f, -5f);

            _xpNum = Txt(g, "经验数值", "", 13, Ink2, true, TextAnchor.MiddleRight, 0f, 0f, 0f, 0f);
            RectTransform t = _xpNum.rectTransform;
            t.anchorMin = new Vector2(1f, 0f);
            t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(1f, 0.5f);
            t.offsetMin = new Vector2(-140f, 0f);
            t.offsetMax = new Vector2(-10f, 0f);
        }

        /// <summary>等级徽章：图标风格决定外形（金圆章 / 星星 / 金印章），上面叠 Lv 与等级数字。</summary>
        private void LvBadge(RectTransform parent, float cx, float cy, float r, int lvSize, int numSize)
        {
            float factor = _icons == HudIconSet.Picture ? 92f / 66f : (_icons == HudIconSet.Stamp ? 92f / 60f : 92f / 56f);
            if (_icons == HudIconSet.Ink) Img(parent, "徽章投影", Prim("circle_fill"), Kraft, cx - r + 3f, cy - r + 5f, r * 2f, r * 2f);
            Icon(parent, "等级徽章", "lv", cx, cy, r * 2f * factor);
            if (_icons == HudIconSet.Picture)
            {
                _lvNum = Txt(parent, "等级", "", Mathf.RoundToInt(numSize * 0.75f), Ink, true, TextAnchor.MiddleCenter, cx - r, cy - r * 0.6f, r * 2f, r * 1.4f);
            }
            else
            {
                Txt(parent, "Lv", "Lv", lvSize, Ink, true, TextAnchor.MiddleCenter, cx - r, cy - r * 0.78f, r * 2f, r * 0.6f);
                _lvNum = Txt(parent, "等级", "", numSize, Ink, true, TextAnchor.MiddleCenter, cx - r, cy - r * 0.32f, r * 2f, r * 1.1f);
            }
        }

        private void HpPulse(RectTransform parent, float w, float h)
        {
            _hpPulse = Img(parent, "危险脉冲", Prim("card_line"), Red, -8f, -8f, w + 16f, h + 16f, Image.Type.Sliced);
            _hpPulse.gameObject.SetActive(false);
        }

        private void InvTag(RectTransform parent, float x, float y)
        {
            RectTransform tag = Node("无敌标签", parent);
            Local(tag, x, y, 64f, 26f);
            Image line;
            Card(tag, "底卡", 0f, 0f, 64f, 26f, Gold, false, out line);
            Txt(tag, "无敌", "无敌", 16, Ink, false, TextAnchor.MiddleCenter, 0f, 0f, 64f, 26f);
            _invTag = tag.gameObject;
            _invTag.SetActive(false);
        }

        private void WeaponFrame(RectTransform parent, float x, float y, float size, float iconSize, int charSize)
        {
            Image line;
            RectTransform frame = Card(parent, "武器图标框", x, y, size, size, Color.white, false, out line);
            _wFrame = frame.Find("纸面").GetComponent<Image>();
            _wIcon = Icon(parent, "武器图标", "gun", x + size * 0.5f, y + size * 0.5f, iconSize * 1.25f);
            _wChar = Txt(parent, "武器字", "", charSize, Ink, false, TextAnchor.MiddleCenter, x, y, size, size);
        }

        private void Pips(RectTransform parent, float x, float y, Vector2 size, float gap)
        {
            _pipRoot = Node("弹药格", parent);
            Local(_pipRoot, x, y, 20f * (size.x + gap), size.y);
            _pipSize = size;
            _pipGap = gap;
        }

        private void MarkChip(RectTransform parent, float x, float y, float w, float h, int size)
        {
            RectTransform chip = Node("武器印记", parent);
            Local(chip, x, y, w, h);
            _markRoot = chip.gameObject;
            Image line;
            Card(chip, "底卡", 0f, 0f, w, h, Paper2, false, out line);
            _markIcon = Icon(chip, "印记图标", "mark", h * 0.5f + 2f, h * 0.5f, h * 1.05f);
            _markText = Txt(chip, "印记", "", size, Ink2, _markCountOnly, TextAnchor.MiddleCenter, h * 0.5f + 4f, 0f, w - h * 0.5f - 6f, h);
        }

        private void EmptyStamp(RectTransform parent, float x, float y, float w, float h, int size, float angle)
        {
            RectTransform s = Node("空弹印章", parent);
            Local(s, x, y, w, h);
            s.pivot = new Vector2(0.5f, 0.5f);
            s.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            s.localRotation = Quaternion.Euler(0f, 0f, angle);
            Img(s, "纸面", Prim("card_fill"), Paper, 0f, 0f, w, h, Image.Type.Sliced);
            Img(s, "描边", Prim("card_line"), Red, 0f, 0f, w, h, Image.Type.Sliced);
            Txt(s, "字", "空弹 · 翻滚补满", size, Red, false, TextAnchor.MiddleCenter, 0f, 0f, w, h);
            _emptyStamp = s.gameObject;
            _emptyStamp.SetActive(false);
        }

        /// <summary>翻滚圆牌（A：R66、上方状态标签常驻；B：R80、圆牌里写「翻滚」，只有亚空间时出标签）。</summary>
        private void Medal(RectTransform parent, float cx, float cy, float r, bool bStyle)
        {
            Image glow = Img(parent, "就绪光晕", Prim("circle_fill"), new Color(PurpleLight.r, PurpleLight.g, PurpleLight.b, 0.55f), cx - r - 14f, cy - r - 14f, (r + 14f) * 2f, (r + 14f) * 2f);
            _glow = glow.gameObject;
            Img(parent, "投影", Prim("circle_fill"), Kraft, cx - r + 3f, cy - r + 6f, r * 2f, r * 2f);
            _face = Img(parent, "圆牌", Prim("circle_fill"), Paper, cx - r, cy - r, r * 2f, r * 2f);
            Img(parent, "描边", Prim("circle_line"), Ink, cx - r, cy - r, r * 2f, r * 2f);
            float rr = r * 0.8f;
            float ringSize = rr * 128f / 52f;
            _ringTrack = Img(parent, "环槽", Prim("ring"), Track, cx - ringSize * 0.5f, cy - ringSize * 0.5f, ringSize, ringSize);
            _ringFill = Img(parent, "环进度", Prim("ring"), Purple, cx - ringSize * 0.5f, cy - ringSize * 0.5f, ringSize, ringSize, Image.Type.Filled);
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;

            _rollCenter = new Vector2(cx, cy);
            _rollIcon = Icon(parent, "翻滚图标", "roll", cx, cy, r * 1.3f);
            _rollIconRt = CenterPivot(_rollIcon);
            _rollIconBig = new Vector2(r * (bStyle ? 1.2f : 1.3f), bStyle ? -r * 0.08f : 0f);
            _rollIconSmall = new Vector2(r * 0.85f, -r * 0.2f);
            _rollSec = Txt(parent, "剩余秒数", "", Mathf.RoundToInt(r * 0.31f), Ink2, true, TextAnchor.MiddleCenter, cx - r, cy + r * 0.25f, r * 2f, r * 0.45f);
            if (bStyle) _rollLabel = Txt(parent, "翻滚", "翻滚", 17, Purple, false, TextAnchor.MiddleCenter, cx - r, cy + r * 0.3f, r * 2f, r * 0.4f);

            float keyY = cy + r - (bStyle ? 2f : 14f);
            RectTransform key = Node("按键 Space", parent);
            Local(key, cx - 40f, keyY - 15f, 80f, 30f);
            Image line;
            Card(key, "键帽", 0f, 0f, 80f, 30f, Ink, true, out line);
            Txt(key, "Space", "Space", 15, Paper, true, TextAnchor.MiddleCenter, 0f, 0f, 80f, 30f);

            RectTransform tag = Node("状态标签", parent);
            float tw = bStyle ? 92f : 104f;
            Local(tag, cx - tw * 0.5f, cy - r - (bStyle ? 32f : 38f), tw, bStyle ? 28f : 30f);
            Img(tag, "投影", Prim("card_fill"), Kraft, 2f, 3f, tw, bStyle ? 28f : 30f, Image.Type.Sliced);
            _tagBg = Img(tag, "底", Prim("card_fill"), Purple, 0f, 0f, tw, bStyle ? 28f : 30f, Image.Type.Sliced);
            Img(tag, "描边", Prim("card_line"), Ink, 0f, 0f, tw, bStyle ? 28f : 30f, Image.Type.Sliced);
            _tagText = Txt(tag, "字", "", bStyle ? 16 : 17, Paper, false, TextAnchor.MiddleCenter, 0f, 0f, tw, bStyle ? 28f : 30f);
            _tagRoot = tag.gameObject;
            _tagAlways = !bStyle;
        }

        /// <summary>把图标的轴心改到中心（位置不变），之后按中心点摆放。</summary>
        private static RectTransform CenterPivot(Image img)
        {
            if (img == null) return null;
            RectTransform rt = img.rectTransform;
            Vector2 size = rt.sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
            return rt;
        }

        // ---------------- C 版脚下环（世界空间） ----------------

        private void CreateRing()
        {
            _ringGo = new GameObject("PaperHud_RollRing");
            _ringGo.transform.SetParent(transform, false);
            _ringTrackLine = NewLine("环槽", 0.07f);
            _ringFillLine = NewLine("环进度", 0.07f);
            _ringOuterLine = NewLine("就绪外圈", 0.025f);
            _ringTrackLine.sortingOrder = ViewMath.ShadowOrder + 1;
            _ringFillLine.sortingOrder = ViewMath.ShadowOrder + 2;
            _ringOuterLine.sortingOrder = ViewMath.ShadowOrder + 1;
            SubspaceView.KeepColor(_ringGo);
        }

        private LineRenderer NewLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_ringGo.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            if (_lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) _lineMaterial = new Material(shader);
            }
            if (_lineMaterial != null) line.sharedMaterial = _lineMaterial;
            return line;
        }

        private void DestroyRing()
        {
            if (_ringGo != null) Destroy(_ringGo);
            _ringGo = null;
            _ringTrackLine = null;
            _ringFillLine = null;
            _ringOuterLine = null;
        }

        private void SetRing(LineRenderer line, Vector3 center, float radius, float fraction, Color color)
        {
            if (line == null) return;
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0.001f)
            {
                line.enabled = false;
                return;
            }
            line.enabled = true;
            int count = Mathf.Max(2, Mathf.CeilToInt(RingSegments * fraction) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1) * fraction;
                float a = (90f - 360f * t) * Mathf.Deg2Rad; // 从正上方（远处）顺时针
                _ringPoints[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                line.SetPosition(i, _ringPoints[i]);
            }
            line.startColor = color;
            line.endColor = color;
        }

        // ---------------- 每帧刷新 ----------------

        private void LateUpdate()
        {
            if (_ctx == null || _root == null || _layout == HudLayout.Classic) return;
            Refresh(Time.unscaledDeltaTime);
        }

        private void Refresh(float dt)
        {
            if (_rejectTimer > 0f) _rejectTimer -= dt;
            float now = Time.unscaledTime;
            PlayerState p = _ctx.Player.State;

            // 生命
            float hpRatio = p.MaxHp > 0f ? Mathf.Clamp01(p.Hp / p.MaxHp) : 0f;
            bool low = hpRatio < 0.25f && p.Hp > 0f;
            bool inv = p.Invincible && !p.InSubspace;
            if (_hpBar != null) _hpBar.Set(hpRatio);
            if (_hpNum != null)
            {
                string hp = Mathf.CeilToInt(p.Hp).ToString();
                _hpNum.text = _layout == HudLayout.FollowC ? hp : hp + Suffix(" / " + Mathf.CeilToInt(p.MaxHp), 16);
                _hpNum.color = low ? Red : Ink;
            }
            if (_hpEdge != null) _hpEdge.color = low ? Red : (inv ? Gold : Ink);
            if (_hpPulse != null)
            {
                if (_hpPulse.gameObject.activeSelf != low) _hpPulse.gameObject.SetActive(low);
                if (low) _hpPulse.color = new Color(Red.r, Red.g, Red.b, 0.45f + 0.4f * Mathf.Sin(now * 6f));
            }
            if (_invTag != null && _invTag.activeSelf != inv) _invTag.SetActive(inv);
            if (_hpIcon != null) _hpIcon.color = inv ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white;
            if (_vignette != null)
            {
                if (_vignette.gameObject.activeSelf != low) _vignette.gameObject.SetActive(low);
                if (low) _vignette.color = new Color(Red.r, Red.g, Red.b, 0.3f + 0.15f * Mathf.Sin(now * 6f));
            }

            // 等级 / 经验
            IProgressionSystem prog = _ctx.Progression;
            if (_lvNum != null) _lvNum.text = prog.Level.ToString();
            if (_xpBar != null) _xpBar.Set(prog.XpToNext > 0 ? (float)prog.Xp / prog.XpToNext : 0f);
            if (_xpNum != null) _xpNum.text = prog.Xp + " / " + prog.XpToNext;
            if (_kills != null) _kills.text = prog.Kills.ToString();

            // 计时
            ISessionSystem session = _ctx.Session;
            bool endless = _ctx.Config.Level.Endless;
            if (_time != null) _time.text = Clock(endless ? session.Time : session.LevelTimeLeft);
            if (_stage != null)
            {
                string level = endless ? "无尽" : "第 " + ((int)_ctx.Plane.ActivePair + 1) + " 关";
                _stage.text = _layout == HudLayout.FollowC ? "第 " + session.Wave + " 波"
                    : level + (_layout == HudLayout.SplitB ? " · " : "  ·  ") + "第 " + session.Wave + " 波";
            }
            bool hasNext = session.WaveTimeLeft > 0f;
            if (_nextRoot != null && _nextRoot.activeSelf != hasNext) _nextRoot.SetActive(hasNext);
            if (_next != null && hasNext) _next.text = (_layout == HudLayout.FollowC ? "下 " : "下一波 ") + Clock(session.WaveTimeLeft, true);

            RefreshWeapon();
            RefreshSub();
            RefreshRoll(p);
            if (_layout == HudLayout.FollowC) RefreshFollow(p);
        }

        private void RefreshWeapon()
        {
            IWeaponSystem w = _ctx.Weapons;
            WeaponKind kind = w.Current;
            Color wc = ViewMath.WeaponColor(kind);
            bool water = kind == WeaponKind.WaterGun;
            if (_wName != null) _wName.text = WeaponKinds.DisplayName(kind);
            if (_wFrame != null) _wFrame.color = Color.Lerp(wc, Color.white, 0.65f);
            if (_wIcon != null && _wIcon.gameObject.activeSelf != water) _wIcon.gameObject.SetActive(water);
            if (_wChar != null)
            {
                if (_wChar.gameObject.activeSelf == water) _wChar.gameObject.SetActive(!water);
                string name = WeaponKinds.DisplayName(kind);
                if (!water && name.Length > 0) _wChar.text = name.Substring(0, 1);
            }

            int cap = w.Capacity;
            int ammo = w.Ammo;
            bool empty = ammo <= 0;
            int cells = Mathf.Clamp(cap, 1, 20);
            if (_pipRoot != null)
            {
                if (cells != _pipCells) BuildPips(cells);
                int filled = cap > 0 ? Mathf.CeilToInt((float)ammo / cap * cells) : 0;
                Color fill = Color.Lerp(wc, Ink, 0.2f);
                for (int i = 0; i < _pipFills.Count; i++)
                {
                    bool on = i < filled;
                    _pipFills[i].color = on ? fill : Track;
                    _pipLines[i].color = on ? Ink : Ink2;
                }
            }
            if (_ammo != null)
            {
                _ammo.text = _ammoShowCap ? ammo + Suffix(" / " + cap, 16) : ammo.ToString();
                _ammo.color = empty ? Red : Ink;
            }
            if (_emptyStamp != null && _emptyStamp.activeSelf != empty) _emptyStamp.SetActive(empty);

            string label;
            int count;
            bool hasMark = MarkInfo(w, kind, out label, out count) && !(empty && _layout != HudLayout.FollowC);
            if (_markRoot != null && _markRoot.activeSelf != hasMark) _markRoot.SetActive(hasMark);
            if (hasMark)
            {
                if (_markIcon != null && _markIcon.gameObject.activeSelf != water) _markIcon.gameObject.SetActive(water);
                if (_markText != null)
                {
                    _markText.text = _markCountOnly ? count.ToString() : label + " " + count;
                    RectTransform rt = _markText.rectTransform;
                    RectTransform chip = (RectTransform)_markRoot.transform;
                    float h = chip.sizeDelta.y;
                    float left = water ? h * 0.5f + 4f : 4f;
                    rt.anchoredPosition = new Vector2(left, 0f);
                    rt.sizeDelta = new Vector2(chip.sizeDelta.x - left - 4f, h);
                }
            }
        }

        private void BuildPips(int cells)
        {
            for (int i = _pipRoot.childCount - 1; i >= 0; i--) Destroy(_pipRoot.GetChild(i).gameObject);
            _pipFills.Clear();
            _pipLines.Clear();
            float[] rot = { -4f, 3f, -2f, 4f, -3f };
            for (int i = 0; i < cells; i++)
            {
                RectTransform cell = Node("格" + (i + 1), _pipRoot);
                Local(cell, i * (_pipSize.x + _pipGap), 0f, _pipSize.x, _pipSize.y);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.anchoredPosition = new Vector2(i * (_pipSize.x + _pipGap) + _pipSize.x * 0.5f, -_pipSize.y * 0.5f);
                cell.localRotation = Quaternion.Euler(0f, 0f, rot[i % rot.Length]);
                _pipFills.Add(Img(cell, "填充", Prim("pip_fill"), Track, 0f, 0f, _pipSize.x, _pipSize.y));
                _pipLines.Add(Img(cell, "描边", Prim("pip_line"), Ink2, 0f, 0f, _pipSize.x, _pipSize.y));
            }
            _pipCells = cells;
            _pipRoot.sizeDelta = new Vector2(cells * (_pipSize.x + _pipGap), _pipSize.y);
        }

        /// <summary>当前武器的铺垫计数（和原 HUD 的「水渍 / 矛印记 / 剑气印记……」一致）；没有返回 false。</summary>
        private static bool MarkInfo(IWeaponSystem w, WeaponKind kind, out string label, out int count)
        {
            label = "";
            count = 0;
            switch (kind)
            {
                case WeaponKind.WaterGun: label = "水渍"; count = w.Puddles.Count; return true;
                case WeaponKind.Spear: label = "矛印"; count = w.TotalSpearMarks; return true;
                case WeaponKind.Blade: label = "剑气"; count = w.TotalBladeMarks; return true;
                case WeaponKind.Palm: label = "掌印"; count = w.TotalPalmMarks; return true;
                case WeaponKind.Crossbow: label = "杀气"; count = w.KillingIntent; return true;
                case WeaponKind.Poker: label = "手牌"; count = w.Hand.Count; return true;
                case WeaponKind.Stapler: label = "钉痕"; count = w.TotalStapleMarks; return true;
                case WeaponKind.Shuriken:
                case WeaponKind.Sword:
                {
                    IReadOnlyList<WeaponField> fields = w.Fields;
                    for (int i = 0; i < fields.Count; i++)
                    {
                        WeaponFieldKind k = fields[i].Kind;
                        if (kind == WeaponKind.Shuriken && (k == WeaponFieldKind.Shuriken || k == WeaponFieldKind.OrbitShuriken)) count++;
                        else if (kind == WeaponKind.Sword && k == WeaponFieldKind.SwordTrail) count++;
                    }
                    label = kind == WeaponKind.Shuriken ? "飞镖" : "剑痕";
                    return true;
                }
                default: return false;
            }
        }

        private void RefreshSub()
        {
            if (_subRoot == null) return;
            SubWeaponKind kind = _ctx.Loadout.Sub;
            bool has = kind != SubWeaponKind.None;
            if (_subRoot.activeSelf != has) _subRoot.SetActive(has);
            if (!has || _subName == null) return;
            ISubWeaponSystem sub = _ctx.SubWeapons;
            string status = "";
            switch (sub.Kind)
            {
                case SubWeaponKind.Drones: status = " ×" + sub.UnitCount; break;
                case SubWeaponKind.SpaceAnchor: status = " " + sub.Anchors.Count; break;
                case SubWeaponKind.Hourglass: status = " " + Mathf.RoundToInt(sub.MonsterSpeedMultiplier * 100f) + "%"; break;
                case SubWeaponKind.GravityWell:
                case SubWeaponKind.Magnet: status = " " + sub.Anchors.Count; break;
                case SubWeaponKind.Cursor: status = sub.Trail.Count > 0 ? " 绘制中" : ""; break;
            }
            string name = SubWeaponKinds.DisplayName(kind) + status;
            _subName.text = _subPrefix ? "副 · " + name : name;
            bool drone = kind == SubWeaponKind.Drones;
            if (_subIcon != null && _subIcon.gameObject.activeSelf != drone) _subIcon.gameObject.SetActive(drone);
            if (_subNameRt != null)
            {
                Vector2 pos = _subNameRt.anchoredPosition;
                pos.x = drone ? _subNameX : _subNameXNoIcon;
                _subNameRt.anchoredPosition = pos;
            }
        }

        private void RefreshRoll(PlayerState p)
        {
            bool sub = p.InSubspace;
            float cd = _ctx.Roll.EffectiveCooldown;
            float readyFrac = cd > 0f ? 1f - Mathf.Clamp01(p.RollCooldownLeft / cd) : 1f;
            bool ready = !sub && readyFrac >= 1f;
            float total = Mathf.Max(0.05f, _ctx.Config.Roll.SubspaceDuration);
            float frac = sub ? Mathf.Clamp01(p.SubspaceLeft / total) : readyFrac;
            float secs = sub ? p.SubspaceLeft : p.RollCooldownLeft;
            bool rejected = _rejectTimer > 0f;

            Color ringColor = rejected ? Red : (sub ? Paper : (ready ? Purple : Grey));
            if (_glow != null && _glow.activeSelf != ready) _glow.SetActive(ready);
            if (_ringFill != null)
            {
                _ringFill.fillAmount = frac;
                _ringFill.color = ringColor;
            }
            if (_ringTrack != null) _ringTrack.color = sub ? PurpleDark : Track;
            if (_face != null)
            {
                if (_layout == HudLayout.FollowC) _face.color = rejected ? Red : (sub ? Ink : (ready ? Purple : Paper));
                else _face.color = sub ? Purple : Paper;
            }

            if (_rollIcon != null && _rollIconRt != null)
            {
                bool inkSet = _icons == HudIconSet.Ink;
                Vector2 look = ready ? _rollIconBig : _rollIconSmall;
                bool showIcon = look.x > 0.5f;
                if (_rollIcon.gameObject.activeSelf != showIcon) _rollIcon.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    _rollIconRt.sizeDelta = new Vector2(look.x, look.x);
                    _rollIconRt.anchoredPosition = new Vector2(_rollCenter.x, -(_rollCenter.y + look.y));
                    if (_layout == HudLayout.FollowC) _rollIcon.color = inkSet ? Paper : Color.white;
                    else if (inkSet) _rollIcon.color = sub ? Paper : (ready ? Purple : Grey);
                    else _rollIcon.color = ready || sub ? Color.white : new Color(0.75f, 0.72f, 0.7f, 0.6f);
                }
            }
            if (_rollSec != null)
            {
                bool show = !ready;
                if (_rollSec.gameObject.activeSelf != show) _rollSec.gameObject.SetActive(show);
                if (show)
                {
                    _rollSec.text = _layout == HudLayout.FollowC ? secs.ToString("0.0") : secs.ToString("0.0") + "s";
                    _rollSec.color = sub ? Paper : Ink2;
                }
            }
            if (_rollLabel != null && _rollLabel.gameObject.activeSelf != ready) _rollLabel.gameObject.SetActive(ready);
            if (_tagRoot != null)
            {
                bool show = _tagAlways || sub;
                if (_tagRoot.activeSelf != show) _tagRoot.SetActive(show);
                if (show)
                {
                    _tagText.text = sub ? "亚空间" : (ready ? "翻滚就绪" : "冷却中");
                    _tagBg.color = sub ? Ink : (ready ? Purple : Paper2);
                    _tagText.color = sub || ready ? Paper : Ink2;
                }
            }

            // C 版脚下环
            if (_ringGo != null)
            {
                Vector3 center = _player != null ? _player.position : ViewMath.ToWorld(p.Position);
                center.z = 0f;
                Color track = sub ? PurpleDark : new Color(Track.r, Track.g, Track.b, 0.95f);
                SetRing(_ringTrackLine, center, RingRadius, 1f, track);
                SetRing(_ringFillLine, center, RingRadius, frac, ringColor);
                SetRing(_ringOuterLine, center, RingRadius + 0.11f, ready ? 1f : 0f, new Color(Purple.r, Purple.g, Purple.b, 0.45f));
            }
        }

        /// <summary>C 版：头顶生命跟着角色头顶，翻滚徽章 / 键帽 / 弹药格跟着脚下环。</summary>
        private void RefreshFollow(PlayerState p)
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null || _foot == null) return;
            Vector3 feet = _player != null ? _player.position : ViewMath.ToWorld(p.Position);
            feet.z = 0f;
            Vector3 sFeet = cam.WorldToScreenPoint(feet);
            bool visible = sFeet.z > 0f;
            if (_foot.gameObject.activeSelf != visible) _foot.gameObject.SetActive(visible);
            if (_head != null && _head.gameObject.activeSelf != visible) _head.gameObject.SetActive(visible);
            if (!visible) return;

            Vector2 lFeet = ToCanvas(sFeet);
            Vector2 lRight = ToCanvas(cam.WorldToScreenPoint(feet + new Vector3(RingRadius, 0f, 0f)));
            Vector2 lDown = ToCanvas(cam.WorldToScreenPoint(feet + new Vector3(0f, -RingRadius, 0f)));
            float rx = Mathf.Abs(lRight.x - lFeet.x);
            float ry = Mathf.Abs(lFeet.y - lDown.y);

            _foot.anchoredPosition = lFeet;
            if (_badgeRt != null) _badgeRt.anchoredPosition = new Vector2(rx + 26f, 0f);
            if (_keyRt != null) _keyRt.anchoredPosition = new Vector2(rx + 54f, 0f);
            float tw = _pipCells > 0 ? _pipCells * (_pipSize.x + _pipGap) - _pipGap : 0f;
            if (_pipRoot != null) _pipRoot.anchoredPosition = new Vector2(-tw * 0.5f, -(ry + 12f));
            if (_ammo != null) _ammo.rectTransform.anchoredPosition = new Vector2(tw * 0.5f + 8f, -(ry + 9f));
            if (_emptyStamp != null) ((RectTransform)_emptyStamp.transform).anchoredPosition = new Vector2(0f, -(ry + 30f));

            if (_head != null)
            {
                float top = HeadTopScreenY(cam, sFeet.y);
                Vector2 lTop = ToCanvas(new Vector3(sFeet.x, top, sFeet.z));
                _head.anchoredPosition = new Vector2(lFeet.x, lTop.y + 10f);
            }
        }

        /// <summary>主角贴图最高点的屏幕 y（取主角身上所有非阴影 SpriteRenderer 的包围盒）；取不到时按脚下往上 160 像素。</summary>
        private float HeadTopScreenY(Camera cam, float feetY)
        {
            if (_player == null) return feetY + 160f;
            if (_playerRenderers == null || --_rendererRefresh <= 0)
            {
                _playerRenderers = _player.GetComponentsInChildren<SpriteRenderer>(false);
                _rendererRefresh = 30;
            }
            float top = float.MinValue;
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                SpriteRenderer r = _playerRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.sprite == null) continue;
                if (r.sortingOrder <= ViewMath.ShadowOrder) continue;
                string n = r.gameObject.name;
                if (n.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Bounds b = r.bounds;
                Vector3 min = b.min, max = b.max;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) == 0 ? min.x : max.x, (k & 2) == 0 ? min.y : max.y, (k & 4) == 0 ? min.z : max.z);
                    float y = cam.WorldToScreenPoint(corner).y;
                    if (y > top) top = y;
                }
            }
            return top == float.MinValue ? feetY + 160f : top;
        }

        private Vector2 ToCanvas(Vector3 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screen, null, out local);
            return local;
        }

        // ---------------- 文本工具 ----------------

        private static string Suffix(string s, int size)
        {
            return "<size=" + size + "><color=" + HexOf(Ink2) + ">" + s + "</color></size>";
        }

        private static string Clock(float seconds, bool shortMinutes = false)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.001f));
            int m = total / 60, s = total % 60;
            return (shortMinutes ? m.ToString() : m.ToString("00")) + ":" + s.ToString("00");
        }

        // ---------------- 搭 UI 的小工具（设计坐标：1920×1080，左上为原点，y 向下） ----------------

        private static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>在父物体里按「左上角 x, y + 宽高」摆放（父物体左上为原点，y 向下）。</summary>
        private static void Local(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>顶层分组：按设计坐标摆放，锚在屏幕的 (ax, ay) 点（ay = 1 顶边，0 底边），换分辨率时贴着那个角 / 边走。</summary>
        private RectTransform Group(string name, float x, float y, float w, float h, float ax, float ay)
        {
            RectTransform rt = Node(name, _root);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x - ax * RefW, -(y - (1f - ay) * RefH));
            return rt;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h, Image.Type type = Image.Type.Simple)
        {
            RectTransform rt = Node(name, parent);
            Local(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (type == Image.Type.Sliced && (sprite == null || sprite.border == Vector4.zero)) type = Image.Type.Simple;
            img.type = type;
            return img;
        }

        /// <summary>图标（以 cx, cy 为中心的正方形）。当前风格没有这个图标时返回 null。</summary>
        private Image Icon(Transform parent, string name, string key, float cx, float cy, float size)
        {
            Sprite s = IconSprite(_icons, key);
            if (s == null) return null;
            Image img = Img(parent, name, s, Color.white, cx - size * 0.5f, cy - size * 0.5f, size, size);
            img.preserveAspect = true;
            return img;
        }

        private static Text Txt(Transform parent, string name, string content, int size, Color color, bool number, TextAnchor anchor, float x, float y, float w, float h)
        {
            RectTransform rt = Node(name, parent);
            Local(rt, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = number ? NumFont : CnFont;
            t.fontStyle = number && _numIsFallback ? FontStyle.Bold : FontStyle.Normal;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>纸片卡：牛皮纸投影（右下 3, 5）+ 纸面 + 墨线描边。返回卡片节点；line = 描边（危险 / 无敌时改色）。</summary>
        private static RectTransform Card(Transform parent, string name, float x, float y, float w, float h, Color fill, bool shadow, out Image line)
        {
            RectTransform rt = Node(name, parent);
            Local(rt, x, y, w, h);
            if (shadow) Img(rt, "投影", Prim("card_fill"), Kraft, 3f, 5f, w, h, Image.Type.Sliced);
            Img(rt, "纸面", Prim("card_fill"), fill, 0f, 0f, w, h, Image.Type.Sliced);
            line = Img(rt, "描边", Prim("card_line"), Ink, 0f, 0f, w, h, Image.Type.Sliced);
            return rt;
        }

        private static Bar MakeBar(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            RectTransform rt = Node(name, parent);
            Local(rt, x, y, w, h);
            Img(rt, "槽", Prim("pill_fill"), Track, 0f, 0f, w, h, Image.Type.Sliced);
            Image fill = Img(rt, "填充", Prim("pill_fill"), color, 0f, 0f, w, h, Image.Type.Sliced);
            Img(rt, "描边", Prim("pill_line"), Ink, 0f, 0f, w, h, Image.Type.Sliced);
            return new Bar { Fill = fill.rectTransform, Width = w, Height = h };
        }
    }
}
