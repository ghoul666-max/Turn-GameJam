using System;
using UnityEngine;
using Turn.Core;
using Turn.Systems;

namespace Turn.View
{
    /// <summary>
    /// 单例世界管理器（节点 GameManager）。启动入口 + 一局的编排：
    ///   读 Inspector 参数（TurnConfigReader）→ 组装系统（TurnFactory）→ 绑定表现 → 固定步长推进模拟 → 失败 → 重开。
    /// 输入只在这里读取并转成系统接口调用（WASD → SetMoveInput，Space → TryRoll，1/2/3 → TryChooseUpgrade，F1 → 数值面板）；
    /// 规则全在模拟层的系统里，这里不做任何玩法判断。
    /// 时间：模拟按现实时间推进（unscaledDeltaTime × F5 调试倍速），亚空间的慢放由 SessionSystem 自己处理；
    /// Time.timeScale 只给表现层用（动画 / 粒子在亚空间里跟着慢放），每帧在 ApplyTimeScale 里统一设置。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("场景引用（生成器自动拖好）")]
        [SerializeField] private MapManager mapManager;
        [SerializeField] private WorldBounds worldBounds;
        [SerializeField] private PlayerController player;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private TerrainDetector terrainDetector;
        [SerializeField] private SpawnManager spawnManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private HudUI hud;
        [SerializeField] private UpgradeUI upgradeUI;
        [SerializeField] private VfxController vfx;
        [SerializeField] private CameraFollow cameraFollow;

        [Header("弹幕表现")]
        [Tooltip("敌方弹幕（泡泡 / 代码弹 / 落石 / 激光等）的表现 Prefab；不填时借用当前位面的玩家子弹 Prefab 并染红")]
        [SerializeField] private Projectile enemyProjectilePrefab;

        [Header("实体容器")]
        [SerializeField] private Transform monsterContainer;
        [SerializeField] private Transform sharkContainer;
        [SerializeField] private Transform projectileContainer;
        [SerializeField] private Transform experienceContainer;

        [Header("模拟")]
        [Tooltip("随机种子；0 = 每局随机")]
        [SerializeField] private int randomSeed;
        [Tooltip("模拟固定步长（秒）")]
        [SerializeField] private float simStep = 1f / 60f;
        [Tooltip("帧率低时一帧最多补几步")]
        [SerializeField] private int maxStepsPerFrame = 5;

        [Header("出战选择")]
        [Tooltip("开局（标题页之后）弹出出战选择页（方案 → 主武器 → 第二主武器 / 副武器）；关掉 = 直接用水枪、不带副武器开局")]
        [SerializeField] private bool weaponSelect = true;
        // 上一次的选择（选择页默认高亮它们；R 重开沿用）
        private WeaponKind _mainWeapon = WeaponKind.WaterGun;
        private WeaponKind _secondWeapon = WeaponKind.Spear;
        private SubWeaponKind _subWeapon = SubWeaponKind.None;
        private SelectStep _selectStep;
        private bool _selectFromTitle;

        [Header("调试快捷键（发布前取消勾选）")]
        [Tooltip("F3 立刻通过本关 / F4 无敌开关 / F5 游戏速度 1× → 2× → 4× / F6 换主武器（换成下一把，跳过已下架的；调试用，不检查解锁）")]
        [SerializeField] private bool debugShortcuts = true;
        private bool _godMode;
        private float _debugSpeed = 1f;
        private GUIStyle _debugStyle;

        [Header("运行时只读")]
        [SerializeField] private RunPhase phase;
        [SerializeField] private PlaneId activeMap;
        [SerializeField] private int monsterCount;
        [SerializeField] private int sharkCount;

        private ITurnContext _ctx;
        private EntityViewSync _views;
        private float _accumulator;
        private bool _paused;
        private TurnTuningPanel _tuning;
        private BuildPickerPanel _buildPicker;
        private EnvironmentView _environmentView;
        private PageTurnView _pageTurn;
        private FlowUI _flow;
        private TurnAudio _audio;
        private SubspaceView _subspaceView;
        private CombatFxView _combatFx;

        // 关卡（一关 = 一组位面）：第一关 = Inspector 里开局位面所在的组；每关都是全新的一局（等级 / 词条 / 怪物全部清空）
        private PlanePairId _firstPair;
        private PlanePairId _levelPair;
        private bool _levelChosen;
        private bool _resultShown;
        // 武器解锁（11/27）：这一局是否已经记过「完成 1 局」，以及结算界面上的新解锁提示
        private bool _runCounted;
        private string _unlockMessage = "";
        // 全程统计（胜利结算用，只累计已通过的关）
        private float _totalTime;
        private int _totalKills;
        private int _totalRolls;

        /// <summary>调试面板（F1 数值 / F7 强化词条）打开中：暂停模拟、屏蔽玩法输入。</summary>
        private bool PanelOpen
        {
            get { return (_tuning != null && _tuning.IsOpen) || (_buildPicker != null && _buildPicker.IsOpen); }
        }

        /// <summary>当前一局的系统上下文（调试 / 扩展用；表现层只读）。</summary>
        public ITurnContext Context { get { return _ctx; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Turn] 场景里有多个 GameManager，已销毁多余的一个。");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 游戏内数值面板（F1）：没有手动挂就自动挂一个
            _tuning = GetComponent<TurnTuningPanel>();
            if (_tuning == null) _tuning = gameObject.AddComponent<TurnTuningPanel>();
            _tuning.SetupInGame(StartNewRun, CaptureTuning);

            // 调试 · 强化词条面板（F7）：直接加 / 减任意词条层数
            _buildPicker = GetComponent<BuildPickerPanel>();
            if (_buildPicker == null) _buildPicker = gameObject.AddComponent<BuildPickerPanel>();
            _buildPicker.Setup(() => _ctx, () => _tuning != null && _tuning.IsOpen);

            // 位面机制表现（困意遮罩、老板来电）：没有手动挂就自动挂一个
            _environmentView = GetComponent<EnvironmentView>();
            if (_environmentView == null) _environmentView = gameObject.AddComponent<EnvironmentView>();

            // 亚空间灰白画面：没有手动挂就自动挂一个
            _subspaceView = GetComponent<SubspaceView>();
            if (_subspaceView == null) _subspaceView = gameObject.AddComponent<SubspaceView>();

            // 战斗特效（水渍 / 毒潭 / 宝箱 / 爆发）：没有手动挂就自动挂一个
            _combatFx = GetComponent<CombatFxView>();
            if (_combatFx == null) _combatFx = gameObject.AddComponent<CombatFxView>();

            // 过关翻页过场：没有手动挂就自动挂一个
            _pageTurn = GetComponent<PageTurnView>();
            if (_pageTurn == null) _pageTurn = gameObject.AddComponent<PageTurnView>();

            // 标题页 / 暂停菜单 / 结算界面：没有手动挂就自动挂一个（手动挂上可以在 Inspector 里改标题文字）
            _flow = GetComponent<FlowUI>();
            if (_flow == null) _flow = gameObject.AddComponent<FlowUI>();
            _flow.Setup(OnTitleOpened, ResumeFromPause, RestartLevel, RestartFromFirstLevel, ReturnToTitle,
                weaponSelect ? OpenWeaponSelect : (Action)null, OnSelectCard);
            if (hud != null) hud.UseBuiltInPanels = false;

            // 音效：没有手动挂就自动挂一个（自动挂的没有音频，要配音效请在编辑器里给 GameManager 加上 TurnAudio 组件）
            _audio = GetComponent<TurnAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<TurnAudio>();
            if (_flow != null) _flow.SetVolumePreview(_audio.PlayPreview);
        }

        /// <summary>从场景组件 / Prefab 的 Inspector 读出当前数值（数值面板“从 Inspector 读取”用）。</summary>
        private TurnTuningData CaptureTuning()
        {
            return TurnTuningData.Capture(player, weapon, spawnManager, mapManager, upgradeManager, cameraFollow);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_views != null) _views.Dispose();
            Time.timeScale = 1f;
        }

        // ---------------- 调试快捷键 ----------------

        private void HandleDebugKeys()
        {
            if (TurnInput.DebugGodModePressed) _godMode = !_godMode;
            if (TurnInput.DebugSpeedPressed)
            {
                _debugSpeed = _debugSpeed >= 4f ? 1f : _debugSpeed * 2f;
            }
            if (TurnInput.DebugNextWeaponPressed) EquipNextUnlockedWeapon();
            bool blocked = (_flow != null && _flow.IsTitleOpen) || (_pageTurn != null && _pageTurn.IsPlaying);
            if (TurnInput.DebugSkipLevelPressed && !blocked) _ctx.Session.DebugClearLevel();
        }

        /// <summary>
        /// 调试无敌：每个模拟步前把无敌剩余时间垫到 0.25 秒，所有伤害都在 CombatSystem 的无敌判定里被挡下。
        /// （InvincibleLeft 平时只由 RollSystem 写；这里是唯一的调试例外。）
        /// </summary>
        private void ApplyGodMode()
        {
            PlayerState p = _ctx.Player.State;
            if (p.InvincibleLeft < 0.25f) p.InvincibleLeft = 0.25f;
        }

        /// <summary>
        /// F6 调试换武器：方案 A 立刻互换两把主武器（等于不翻滚手动切换）；方案 B 按顺序换成下一把已实现的主武器。亚空间里 Equip 会拒绝。
        /// </summary>
        private void EquipNextUnlockedWeapon()
        {
            ILoadoutSystem loadout = _ctx.Loadout;
            if (loadout.Scheme == LoadoutScheme.DualMain)
            {
                loadout.Equip(loadout.Standby);
                return;
            }
            int start = (int)loadout.Equipped;
            for (int i = 1; i < WeaponKinds.Count; i++)
            {
                var next = (WeaponKind)((start + i) % WeaponKinds.Count);
                if (_ctx.Weapons.Supports(next) && !WeaponKinds.IsRetired(next))
                {
                    loadout.Equip(next);
                    return;
                }
            }
        }

        /// <summary>表现层的时间倍率 = F5 调试倍速 × 亚空间慢放倍率（只影响动画 / 粒子 / 用 deltaTime 的表现，不影响模拟）。</summary>
        private void ApplyTimeScale()
        {
            float scale = _debugSpeed;
            bool frozen = _paused || PanelOpen;
            if (_ctx != null && !frozen && _ctx.Player.State.InSubspace)
            {
                float s = _ctx.Config.Roll.SubspaceTimeScale;
                scale *= Mathf.Clamp(s, 0.05f, 1f);
            }
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
        }

        private void OnGUI()
        {
            if (!debugShortcuts || (!_godMode && _debugSpeed == 1f)) return;
            if (_debugStyle == null)
            {
                _debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                _debugStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f);
            }
            string text = "调试：" + (_godMode ? "无敌（F4）" : "") + (_godMode && _debugSpeed != 1f ? " · " : "") + (_debugSpeed != 1f ? "速度 ×" + _debugSpeed.ToString("0") + "（F5）" : "");
            GUI.Label(new Rect(12f, 8f, 600f, 28f), text, _debugStyle);
        }

        private void Start()
        {
            PlaneId start = mapManager != null ? mapManager.Environment.startMap : PlaneId.Volcano;
            _firstPair = PlaneIds.PairOf(start);
            _levelPair = _firstPair;
            _levelChosen = true;
            ResetTotals();
            if (_audio != null) _audio.PlayTitleMusic(); // 先进标题模式，开局事件就不会先响一下位面曲
            StartNewRun();
            if (_flow != null) _flow.ShowTitle();
        }

        // ---------------- 流程（标题 / 暂停 / 结算的按钮与快捷键都走这里） ----------------

        private void OnTitleOpened()
        {
            if (_audio != null)
            {
                _audio.PlayPageTurn();
                _audio.PlayPlaneMusic(); // 标题曲 → 当前位面曲
            }
        }

        // ---------------- 出战选择（正式入口，11/25 双武器策划案） ----------------

        // 11/27：删除双主武器方案，出战固定为「1 把主武器 + 1 把副武器（可不带）」，选择页只剩两步。
        private enum SelectStep { Main, Sub }

        private const string SelectHint = "点卡片或按数字键直接选 · ←→ 切换 · Space / Enter 确认 · Esc 返回上一步";

        // 选择页上的卡片顺序 = 解锁顺序（已下架的掌 / 订书机不在里面）；未解锁的显示成灰色
        private static readonly WeaponKind[] MainChoices = WeaponKinds.UnlockOrder;
        private static SubWeaponKind[] _subChoices;
        private static SubWeaponKind[] SubChoices
        {
            get
            {
                if (_subChoices == null)
                {
                    _subChoices = new SubWeaponKind[SubWeaponKinds.UnlockOrder.Length + 1];
                    _subChoices[0] = SubWeaponKind.None;
                    Array.Copy(SubWeaponKinds.UnlockOrder, 0, _subChoices, 1, SubWeaponKinds.UnlockOrder.Length);
                }
                return _subChoices;
            }
        }

        private bool MainSelectable(WeaponKind w)
        {
            return !WeaponKinds.IsRetired(w) && WeaponUnlocks.IsUnlocked(w) && _ctx != null && _ctx.Weapons.Supports(w);
        }

        private bool SubSelectable(SubWeaponKind k)
        {
            return WeaponUnlocks.IsUnlocked(k) && _ctx != null && _ctx.SubWeapons.Supports(k);
        }

        /// <summary>选中的装备没解锁 / 已下架时退回第一把可选的（主武器）/ 不带（副武器）。</summary>
        private void ClampLoadoutToUnlocked()
        {
            if (WeaponKinds.IsRetired(_mainWeapon) || !WeaponUnlocks.IsUnlocked(_mainWeapon)) _mainWeapon = WeaponKinds.UnlockOrder[0];
            if (!WeaponUnlocks.IsUnlocked(_subWeapon)) _subWeapon = SubWeaponKind.None;
        }

        /// <summary>打开出战选择页的第一页（主武器）。卡片上的数值直接读这一局的配置（Inspector / F1 面板改了这里跟着变）。</summary>
        private void OpenWeaponSelect()
        {
            if (_flow == null || _ctx == null) return;
            _selectFromTitle = _flow.IsTitleOpen;
            ClampLoadoutToUnlocked();
            _selectStep = SelectStep.Main;
            ShowSelectStep();
        }

        private void ShowSelectStep()
        {
            string prefix = _selectFromTitle ? "" : "换装备重开 · ";
            if (_selectStep == SelectStep.Main)
            {
                var cards = new FlowUI.SelectCard[MainChoices.Length];
                int highlight = 0;
                for (int i = 0; i < MainChoices.Length; i++)
                {
                    WeaponKind w = MainChoices[i];
                    bool ok = MainSelectable(w);
                    if (w == _mainWeapon) highlight = i;
                    cards[i] = new FlowUI.SelectCard
                    {
                        Title = KeyLabel(i) + "  " + WeaponKinds.DisplayName(w),
                        Body = ok ? WeaponCard(w) : LockedText(WeaponUnlocks.UnlockAfterRuns(w)) + WeaponCard(w),
                        Color = ViewMath.WeaponColor(w),
                        Enabled = ok,
                    };
                }
                string header = "第 1 / 2 步 · 选择主武器（已解锁 " + WeaponUnlocks.MainCount + " / " + MainChoices.Length + "）";
                _flow.ShowSelectPage(prefix + header, SelectHint, cards, highlight);
            }
            else
            {
                SubWeaponKind[] subs = SubChoices;
                var cards = new FlowUI.SelectCard[subs.Length];
                int highlight = 0;
                for (int i = 0; i < subs.Length; i++)
                {
                    SubWeaponKind k = subs[i];
                    bool ok = SubSelectable(k);
                    if (k == _subWeapon) highlight = i;
                    cards[i] = new FlowUI.SelectCard
                    {
                        Title = KeyLabel(i) + "  " + (k == SubWeaponKind.None ? "不带副武器" : SubWeaponKinds.DisplayName(k)),
                        Body = ok ? SubWeaponCard(k) : LockedText(WeaponUnlocks.UnlockAfterRuns(k)) + SubWeaponCard(k),
                        Color = ViewMath.SubWeaponColor(k),
                        Enabled = ok,
                    };
                }
                string header = "第 2 / 2 步 · 选择副武器（已解锁 " + WeaponUnlocks.SubCount + " / " + SubWeaponKinds.UnlockOrder.Length + "）";
                _flow.ShowSelectPage(prefix + header, SelectHint, cards, highlight);
            }
        }

        private static string LockedText(int runs)
        {
            return "<b>未解锁</b>：完成 " + runs + " 局后解锁（已完成 " + WeaponUnlocks.RunsFinished + " 局）\n\n";
        }

        /// <summary>选择页点了第 index 张卡：翻到下一页，或者选完开局。</summary>
        private void OnSelectCard(int index)
        {
            if (_selectStep == SelectStep.Main)
            {
                if (index < 0 || index >= MainChoices.Length || !MainSelectable(MainChoices[index])) return;
                _mainWeapon = MainChoices[index];
                // 一把副武器都还没解锁（第一次打开）：不用再翻一页，直接不带副武器开局
                if (WeaponUnlocks.SubCount > 0)
                {
                    _selectStep = SelectStep.Sub;
                    ShowSelectStep();
                    return;
                }
                _subWeapon = SubWeaponKind.None;
            }
            else
            {
                SubWeaponKind[] subs = SubChoices;
                if (index < 0 || index >= subs.Length || !SubSelectable(subs[index])) return;
                _subWeapon = subs[index];
            }
            // 选完：从头开一局新的（标题页来的会接着翻开封面；结算界面来的直接开始）
            RestartFromFirstLevel();
            _flow.FinishSelect();
        }

        /// <summary>Esc：返回上一页；第一页再按就关掉选择页（回到标题 / 结算界面）。</summary>
        private void OnSelectBack()
        {
            if (_selectStep == SelectStep.Sub)
            {
                _selectStep = SelectStep.Main;
                ShowSelectStep();
                return;
            }
            _flow.HideWeaponSelect();
            _resultShown = false; // 从结算界面来的：重新显示结算
        }

        /// <summary>选择页卡片的数字键（第 10 张是 0）。</summary>
        private static string KeyLabel(int index) { return index == 9 ? "0" : (index + 1).ToString(); }

        /// <summary>主武器卡片。数值读这一局的配置。</summary>
        private string WeaponCard(WeaponKind w)
        {
            ArsenalConfig a = _ctx.Config.Arsenal;
            float cd = _ctx.Roll.CooldownFor(w);
            string roll = "\n翻滚冷却 " + Num(cd) + " 秒";
            switch (w)
            {
                case WeaponKind.WaterGun:
                    return "弹药 " + a.WaterGun.Ammo + " · " + Num(a.WaterGun.FireInterval) + " 秒/发 · 伤害 " + Num(a.WaterGun.Damage) + roll +
                           "\n\n<b>铺垫</b>：水弹落地或命中处留下一滩水渍，永久存在" +
                           "\n\n<b>爆发</b>：全图水渍同时喷发喷泉（每滩 " + Num(a.WaterGun.FountainDamage) + "），重叠处伤害叠加";
                case WeaponKind.Spear:
                    return "弹药 " + a.Spear.Ammo + " · " + Num(a.Spear.FireInterval) + " 秒/发 · 伤害 " + Num(a.Spear.Damage) + roll +
                           "\n\n<b>铺垫</b>：长矛插在敌人身上叠矛印记；直接击杀则穿透下一个，印记一起带走" +
                           "\n\n<b>爆发</b>：所有长矛向你回收，沿途穿刺（每根 " + Num(a.Spear.RecallDamage) + "）";
                case WeaponKind.Blade:
                    return "弹药 " + a.Blade.Ammo + " · " + Num(a.Blade.FireInterval) + " 秒/发 · 伤害 " + Num(a.Blade.Damage) + roll +
                           "\n\n<b>铺垫</b>：朝最近的敌人发射月牙剑气弹，命中第一个敌人即消失，并叠 1 层剑气印记" +
                           "\n\n<b>爆发</b>：横贯全图的一刀斩（" + Num(a.Blade.SlashDamage) + "），全场每层印记 +" + Mathf.RoundToInt(a.Blade.MarkBonus * 100f) + "%";
                case WeaponKind.Palm:
                    return "弹药 " + a.Palm.Ammo + " · " + Num(a.Palm.FireInterval) + " 秒/发 · 伤害 " + Num(a.Palm.Damage) + roll +
                           "\n\n<b>铺垫</b>：朝面向方向 " + Num(a.Palm.Angle) + "° 扇形掌击，击退敌人（冲锋中的免疫），命中叠 1 层掌印" +
                           "\n\n<b>爆发</b>：以自身为中心的天降掌（" + Num(a.Palm.SkyDamage) + "），每层掌印 +" + Mathf.RoundToInt(a.Palm.MarkBonus * 100f) + "%";
                case WeaponKind.Poker:
                    return "手牌上限 " + a.Poker.HandSize + " · " + Num(a.Poker.DrawInterval) + " 秒抽 1 张" + roll +
                           "\n\n<b>常态</b>：不攻击，自动抽牌到头顶（1–K、大小王）" +
                           "\n\n<b>爆发</b>：打出全部手牌：♥追踪爱心弹（点数×" + a.Poker.HeartsPerPoint + " 发，命中小范围爆炸，会反弹）♠最密集处炸弹 ♦护盾（层数 = 点数）♣吸附裂隙；" +
                           "小王 / 大王永久 +" + Mathf.RoundToInt(a.Poker.SmallJokerBonus * 100f) + "% / +" + Mathf.RoundToInt(a.Poker.BigJokerBonus * 100f) + "%（切换主武器清零）";
                case WeaponKind.Shuriken:
                    return "弹药 " + a.Shuriken.Ammo + " · " + Num(a.Shuriken.FireInterval) + " 秒/发 · 旋转 " + Num(a.Shuriken.SpinDamage) + "/" + Num(a.Shuriken.SpinInterval) + " 秒" + roll +
                           "\n\n<b>铺垫</b>：手里剑飞出后停在原地旋转，持续伤害周围敌人，永久留存" +
                           "\n\n<b>爆发</b>：全部高速回收，路径穿刺（每枚 " + Num(a.Shuriken.RecallDamage) + "）";
                case WeaponKind.Stapler:
                    return "弹药 " + a.Stapler.Ammo + " · " + Num(a.Stapler.FireInterval) + " 秒/发 · 伤害 " + Num(a.Stapler.Damage) + roll +
                           "\n\n<b>铺垫</b>：订书钉击退敌人；被击退的撞到别的怪，就把沿途的钉合成一组（叠钉痕 + 减速，最多 " + a.Stapler.GroupMax + " 只）" +
                           "\n\n<b>爆发</b>：每只带钉痕的怪向四周散射 " + a.Stapler.SecondaryCount + " 枚次级钉（可继续钉合），钉痕越多越痛";
                case WeaponKind.Sword:
                    return "弹药 " + a.Sword.Ammo + " · " + Num(a.Sword.FireInterval) + " 秒/次 · 伤害 " + Num(a.Sword.Damage) + roll +
                           "\n\n<b>铺垫</b>：朝面向方向突进刺击（突进中无敌），路径留下剑痕" +
                           "\n\n<b>爆发</b>：影子沿所有剑痕复刻刺击（" + Num(a.Sword.ShadowDamage) + "，每条剑痕 +" + Mathf.RoundToInt(a.Sword.TrailBonus * 100f) +
                           "%）；影刺每击杀 1 名敌人永久 +1 突进次数（切换主武器清零）";
                case WeaponKind.Lightning:
                    return "弹药 " + a.Lightning.Ammo + " · " + Num(a.Lightning.FireInterval) + " 秒/发 · 伤害 " + Num(a.Lightning.Damage) + roll +
                           "\n\n<b>铺垫</b>：闪电在怪物之间连锁跳跃（一道最多命中 " + a.Lightning.ChainCount + " 只），命中的怪物叠 1 层雷痕" +
                           "\n\n<b>爆发</b>：引爆全场雷痕（每层 " + Num(a.Lightning.DetonateDamage) + "）；被炸死的怪会向最近的敌人再发一道同样的闪电，连环传染";
                default:
                    return "弹药 " + a.Crossbow.Ammo + " · 伤害 " + Num(a.Crossbow.Damage) + " · 穿透 " + a.Crossbow.Pierce + roll +
                           "\n\n<b>常态</b>：重型弩箭强制击退；敌人撞墙眩晕，你获得 1 层杀气（每层弩伤害 +" + Mathf.RoundToInt(a.Crossbow.IntentBonus * 100f) +
                           "%，无限叠加，切换主武器时清零）" +
                           "\n\n<b>翻滚</b>：没有爆发，冷却特别短：高频翻滚补弹、驱动副武器";
            }
        }

        private string SubWeaponCard(SubWeaponKind k)
        {
            ArsenalConfig a = _ctx.Config.Arsenal;
            switch (k)
            {
                case SubWeaponKind.None:
                    return "只带一把主武器。\n\n翻滚只触发主武器的爆发。";
                case SubWeaponKind.Drones:
                    return "环绕你飞行、自动攻击的小炮台（每台每发 " + Num(a.Drones.Damage) + "，" + Num(a.Drones.FireInterval) + " 秒/发）。" +
                           "\n\n<b>翻滚</b>：盲盒：" + Mathf.RoundToInt(a.Drones.SplitChance * 100f) + "% 分裂（+" + a.Drones.SplitAdd + " 台），否则合体（−" +
                           a.Drones.MergeRemove + " 台，最少 " + Mathf.Max(1, a.Drones.MinCount) + " 台）";
                case SubWeaponKind.GravityWell:
                case SubWeaponKind.Magnet:
                    return "持久黑洞：持续牵引，造成每秒 " + Num(a.Gravity.DamagePerSecond) + " 伤害，并吸取附近经验球。" +
                           "\n\n<b>翻滚</b>：起点留下黑洞；新旧黑洞相向移动，在中点爆炸（" + Num(a.Magnet.BlastDamage) + "）并留下一个黑洞。合并不叠加基础伤害或体积。";
                case SubWeaponKind.Hourglass:
                    return "全场敌人移速随时间渐进衰减，最低降到 " + Mathf.RoundToInt(a.Hourglass.FloorSpeed * 100f) + "%。" +
                           "\n\n<b>翻滚</b>：敌人移速立即重置回基础值，回到常态后重新开始减速——用翻滚时机把控节奏";
                case SubWeaponKind.Wasd:
                    return "按住移动键时，对应方向生成按键判定区（" + Num(a.Wasd.Damage) + "，每只敌人 " + Num(a.Wasd.HitCooldown) + " 秒最多 1 次），最多同时 2 个方向。" +
                           "\n\n<b>翻滚</b>：W / A / S / D 四向齐发（" + Num(a.Wasd.BurstDamage) + "）";
                case SubWeaponKind.Cursor:
                    return "没有常态效果。" +
                           "\n\n<b>翻滚</b>：第一次翻滚开始绘制，你走过的路线就是选区边界；之后每次翻滚：选区内敌人受到降维打击（" +
                           Num(a.Cursor.Damage) + "）并被压扁定身，然后自动开始下一轮绘制";
                default:
                    return "每次翻滚记录一个锚点。" +
                           "\n\n<b>翻滚</b>：回溯到上一个锚点，连线斩击路径上所有敌人（" + Num(a.Anchor.SlashDamage) + "）；连续翻滚会在两点之间来回折返";
            }
        }

        private static string Num(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");
        }

        /// <summary>出战装备的显示名（结算 / HUD 用）。</summary>
        private string LoadoutName()
        {
            ILoadoutSystem l = _ctx.Loadout;
            if (l.Scheme == LoadoutScheme.DualMain) return WeaponKinds.DisplayName(l.Equipped) + " / " + WeaponKinds.DisplayName(l.Standby);
            return l.Sub != SubWeaponKind.None ? WeaponKinds.DisplayName(l.Equipped) + " + " + SubWeaponKinds.DisplayName(l.Sub) : WeaponKinds.DisplayName(l.Equipped);
        }

        /// <summary>暂停界面左侧的「已选强化」：按 当前主武器 → 另一把主武器 / 副武器 → 其他（F7 调试加的）分组，最后是宝箱代价。</summary>
        private void RefreshPauseBuilds()
        {
            if (_flow == null || _ctx == null) return;
            IProgressionSystem prog = _ctx.Progression;
            BuildDef[] defs = _ctx.Config.Builds.Builds;
            ILoadoutSystem loadout = _ctx.Loadout;
            var entries = new System.Collections.Generic.List<FlowUI.BuildEntry>();
            int cards = 0, levels = 0;

            // 分组顺序：key = 主武器 0..8，副武器 100 + kind
            var order = new System.Collections.Generic.List<int>();
            order.Add((int)loadout.Equipped);
            if (loadout.Scheme == LoadoutScheme.DualMain && loadout.Standby != loadout.Equipped) order.Add((int)loadout.Standby);
            if (loadout.Sub != SubWeaponKind.None) order.Add(100 + (int)loadout.Sub);
            for (int w = 0; w < WeaponKinds.Count; w++) if (!order.Contains(w)) order.Add(w);
            for (int s = 1; s < SubWeaponKinds.Count; s++) if (!order.Contains(100 + s)) order.Add(100 + s);

            for (int g = 0; g < order.Count; g++)
            {
                int key = order[g];
                bool isSub = key >= 100;
                var weapon = isSub ? WeaponKind.WaterGun : (WeaponKind)key;
                var sub = isSub ? (SubWeaponKind)(key - 100) : SubWeaponKind.None;
                bool headerAdded = false;
                for (int i = 0; i < BuildIds.Count; i++)
                {
                    var id = (BuildId)i;
                    int level = prog.GetLevel(id);
                    if (level <= 0) continue;
                    if (isSub ? BuildIds.SubOf(id) != sub : (BuildIds.IsSub(id) || BuildIds.WeaponOf(id) != weapon)) continue;
                    Color color = isSub ? ViewMath.SubWeaponColor(sub) : ViewMath.WeaponColor(weapon);
                    if (!headerAdded)
                    {
                        headerAdded = true;
                        string role = isSub ? "副武器" : (weapon == loadout.Equipped ? "当前主武器" : (loadout.Carries(weapon) ? "主武器" : "未携带"));
                        entries.Add(new FlowUI.BuildEntry
                        {
                            Header = true, Color = color,
                            Text = (isSub ? SubWeaponKinds.DisplayName(sub) : WeaponKinds.DisplayName(weapon)) + "  <size=18>" + role + "</size>",
                        });
                    }
                    BuildDef def = defs != null && i < defs.Length ? defs[i] : null;
                    int max = prog.MaxLevel(id);
                    entries.Add(new FlowUI.BuildEntry
                    {
                        Color = color,
                        Text = "<b>" + Turn.Weapons.BuildCatalog.Name(id) + "</b>  <color=#8A7560>Lv " + level + " / " + max + (level >= max ? "  已满级" : "") +
                               "</color>\n<size=18>" + Turn.Weapons.BuildCatalog.Describe(id, def, level) + "</size>",
                    });
                    cards++;
                    levels += level;
                }
            }

            // 宝箱代价
            BuildConfig bc = _ctx.Config.Builds;
            Color curseColor = new Color(0.75f, 0.22f, 0.2f, 1f);
            bool curseHeader = false;
            for (int c = 1; c < ChestCurses.Count; c++)
            {
                var curse = (ChestCurse)c;
                int stacks = prog.CurseStacks(curse);
                if (stacks <= 0) continue;
                if (!curseHeader)
                {
                    curseHeader = true;
                    entries.Add(new FlowUI.BuildEntry { Header = true, Color = curseColor, Text = "宝箱代价" });
                }
                string name, cost;
                switch (curse)
                {
                    case ChestCurse.Frail: name = "血契"; cost = "最大生命 −" + Mathf.RoundToInt(bc.FrailMaxHp * stacks); break;
                    case ChestCurse.Fury: name = "狂怒"; cost = "怪物伤害 +" + Mathf.RoundToInt(bc.FuryDamage * 100f * stacks) + "%"; break;
                    default: name = "疾行"; cost = "怪物移速 +" + Mathf.RoundToInt(bc.HasteSpeed * 100f * stacks) + "%"; break;
                }
                entries.Add(new FlowUI.BuildEntry
                {
                    Color = curseColor,
                    Text = "<b>" + name + "</b>  <color=#8A7560>×" + stacks + "</color>\n<size=18>" + cost + "（本局永久）</size>",
                });
            }

            if (cards == 0 && !curseHeader)
                entries.Add(new FlowUI.BuildEntry
                {
                    Color = new Color(0.6f, 0.55f, 0.5f, 1f),
                    Text = "还没有选择任何强化\n<size=18>升级三选一、开宝箱拿到的强化都会记在这里</size>",
                });
            _flow.SetBuildList("已选强化  <size=22>" + cards + " 张 · 共 " + levels + " 层</size>", entries);
        }

        private void ResumeFromPause()
        {
            _paused = false;
            if (_flow != null) _flow.SetPause(false);
            if (hud != null) hud.SetPaused(false);
        }

        private void RestartLevel()
        {
            StartNewRun();
        }

        private void RestartFromFirstLevel()
        {
            _levelPair = _firstPair;
            ResetTotals();
            StartNewRun();
        }

        private void ReturnToTitle()
        {
            if (_audio != null) _audio.PlayTitleMusic();
            RestartFromFirstLevel();
            if (_flow != null) _flow.ShowTitle();
        }

        private void ShowResult(bool victory)
        {
            _resultShown = true;
            // 武器解锁：一局打到结算界面算完成 1 局（每局只记一次；从结算进选择页再退回来不会重复记）
            if (!_runCounted)
            {
                _runCounted = true;
                string unlocked = WeaponUnlocks.RegisterRunFinished();
                _unlockMessage = unlocked.Length > 0 ? "\n\n<b>新解锁</b>：" + unlocked : "";
            }
            if (victory) AddTotals();
            string body;
            if (victory)
            {
                body = "三组位面全部通过\n\n全程 " + FormatTime(_totalTime) + " · 击杀 " + _totalKills + " · 翻滚 " + _totalRolls + " 次";
                if (_flow != null && !string.IsNullOrEmpty(_flow.Credits)) body += "\n\n<size=22>" + _flow.Credits + "</size>";
            }
            else
            {
                string where = _ctx.Config.Level.Endless
                    ? "第 " + _ctx.Session.Wave + " 波 · " + LoadoutName()
                    : "第 " + ((int)_levelPair + 1) + " 关 · " + PlaneIds.DisplayName(_levelPair);
                body = where + "\n\n存活 " + FormatTime(_ctx.Session.Time) +
                       " · 等级 " + _ctx.Progression.Level + " · 击杀 " + _ctx.Progression.Kills + " · 翻滚 " + _ctx.Roll.RollCount + " 次";
            }
            body += _unlockMessage;
            if (_flow != null) _flow.ShowResult(victory, victory ? "胜利！" : "倒下了……", body);
            else if (hud != null) hud.SetResultText(body);
        }

        // ---------------- 关卡 ----------------

        /// <summary>本关存活够时间：累计统计 → 翻页 → 在纸后面开下一关的新一局。</summary>
        private void BeginPageTurn()
        {
            AddTotals();
            PlanePairId cleared = _levelPair;
            PlanePairId next = (PlanePairId)Mathf.Min((int)cleared + 1, PlaneIds.PairCount - 1);
            if (_audio != null) _audio.PlayPageTurn();
            if (_pageTurn == null)
            {
                _levelPair = next;
                StartNewRun();
                return;
            }
            string done = "第 " + ((int)cleared + 1) + " 页 · " + PlaneIds.DisplayName(cleared) + "\n完成";
            string upcoming = "第 " + ((int)next + 1) + " 页 · " + PlaneIds.DisplayName(next);
            _pageTurn.Play(done, upcoming, () =>
            {
                _levelPair = next;
                StartNewRun();
            }, null);
        }

        private void AddTotals()
        {
            if (_ctx == null) return;
            _totalTime += _ctx.Session.Time;
            _totalKills += _ctx.Progression.Kills;
            _totalRolls += _ctx.Roll.RollCount;
        }

        private void ResetTotals()
        {
            _totalTime = 0f;
            _totalKills = 0;
            _totalRolls = 0;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        // ---------------- 一局 ----------------

        public void StartNewRun()
        {
            if (_views != null)
            {
                _views.Dispose();
                _views = null;
            }
            if (vfx != null) vfx.Clear();

            TurnConfig config = TurnConfigReader.Read(player, weapon, mapManager, worldBounds, spawnManager, upgradeManager);
            // 数值面板：有存档且启用时覆盖 Inspector 数值；第一次运行时把 Inspector 数值记成存档
            TurnTuningStore.ApplyOrCapture(config, player, weapon, spawnManager, mapManager, upgradeManager);
            if (cameraFollow != null)
            {
                // 相机 / 2.5D 视角：面板启用且存档里有这一组时用存档，否则用 CameraFollow 的 Inspector 参数
                TurnTuningFile tuningFile = TurnTuningStore.Load();
                cameraFollow.ApplySettings(tuningFile != null && tuningFile.useTuning && tuningFile.values != null ? tuningFile.values.camera : null);
            }
            // 出战配置 = 选择页的选择（局内不再按波次解锁）
            // 11/27：只剩「主 + 副」方案；没解锁 / 已下架的装备退回默认（防止旧选择残留）
            ClampLoadoutToUnlocked();
            config.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = _mainWeapon, SecondMain = _secondWeapon, Sub = _subWeapon };
            // 关卡：开局位面换成本关这一组的第一面（如果 Inspector 的开局位面就在这一组里，保留它，方便测试第二面）
            if (_levelChosen && PlaneIds.PairOf(config.Environment.StartPlane) != _levelPair)
            {
                config.Environment.StartPlane = PlaneIds.First(_levelPair);
            }
            _resultShown = false;
            _runCounted = false;
            _unlockMessage = "";
            int seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;
            _ctx = TurnFactory.Create(config, seed);

            if (mapManager != null) mapManager.Bind(_ctx);
            if (_subspaceView != null) _subspaceView.Bind(_ctx, cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main);
            if (_environmentView != null) _environmentView.Bind(_ctx, worldBounds != null ? worldBounds.DebugLineMaterial : null);
            if (_combatFx != null) _combatFx.Bind(_ctx, worldBounds != null ? worldBounds.DebugLineMaterial : null);
            if (_audio != null) _audio.Bind(_ctx);
            if (_flow != null)
            {
                _flow.SetPause(false);
                _flow.HideResult();
            }
            if (player != null) player.Bind(_ctx);
            if (weapon != null) weapon.Bind(_ctx);
            if (terrainDetector != null) terrainDetector.Bind(_ctx);
            if (hud != null) hud.Bind(_ctx);
            if (upgradeUI != null) upgradeUI.Bind(_ctx, OnChooseUpgrade);
            if (vfx != null) vfx.Bind(_ctx);

            _views = new EntityViewSync(_ctx, new EntityViewSync.Refs
            {
                MonsterPrefab = spawnManager != null ? spawnManager.MonsterPrefab : null,
                SharkPrefab = spawnManager != null ? spawnManager.SharkPrefab : null,
                FireballPrefab = weapon != null ? weapon.GetPrefab(PlaneId.Volcano) : null,
                HarpoonPrefab = weapon != null ? weapon.GetPrefab(PlaneId.Ocean) : null,
                FormPrefabs = CollectFormPrefabs(),
                WaterPrefab = weapon != null ? weapon.GetPrefab(WeaponKind.WaterGun) : null,
                SpearPrefab = weapon != null ? weapon.GetPrefab(WeaponKind.Spear) : null,
                EnemyPrefab = enemyProjectilePrefab,
                OrbPrefab = upgradeManager != null ? upgradeManager.ExperienceOrbPrefab : null,
                MonsterContainer = monsterContainer,
                SharkContainer = sharkContainer,
                ProjectileContainer = projectileContainer,
                ExperienceContainer = experienceContainer,
            });

            _ctx.Session.Start();
            _views.Sync();
            if (cameraFollow != null) cameraFollow.SnapToTarget();

            _accumulator = 0f;
            _paused = false;
            if (hud != null) hud.SetPaused(false);
        }

        /// <summary>六把武器各自的子弹 Prefab（WeaponController 上每个形态的 prefab 槽），按 PlaneId 下标。</summary>
        private Projectile[] CollectFormPrefabs()
        {
            var prefabs = new Projectile[PlaneIds.Count];
            if (weapon == null) return prefabs;
            for (int i = 0; i < prefabs.Length; i++) prefabs[i] = weapon.GetPrefab((PlaneId)i);
            return prefabs;
        }

        private void OnChooseUpgrade(int index)
        {
            if (_ctx != null) _ctx.Session.TryChooseUpgrade(index);
        }

        // ---------------- 每帧 ----------------

        private void Update()
        {
            if (_ctx == null) return;

            if (TurnInput.ObstacleDebugPressed) Obstacle.ShowDebug = !Obstacle.ShowDebug;
            if (debugShortcuts) HandleDebugKeys();

            // 退出确认（11/27）：盖在标题页上；Space / Enter 退出，Esc 取消（不认 R，避免误退）
            if (_flow != null && _flow.IsQuitOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (PanelOpen) return;
                if (TurnInput.ConfirmPressed) _flow.ConfirmQuit();
                else if (TurnInput.PausePressed) _flow.HideQuit();
                return;
            }

            // 窗口设置（11/27）：盖在标题页 / 暂停菜单上；打开时只处理 ↑↓←→ / Space / Enter / Esc，模拟不推进
            if (_flow != null && _flow.IsDisplayOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (PanelOpen) return;
                int displayStep = TurnInput.VerticalStepPressed;
                if (displayStep == 0) displayStep = TurnInput.SelectStepPressed;
                if (displayStep != 0) _flow.DisplayMove(displayStep);
                else if (TurnInput.RollPressed || TurnInput.RestartPressed) _flow.DisplayConfirm();
                else if (TurnInput.PausePressed) _flow.HideDisplay();
                return;
            }

            // 音量设置：盖在标题页 / 暂停菜单上；打开时只处理 ↑↓←→ / Esc，模拟不推进
            if (_flow != null && _flow.IsVolumeOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (PanelOpen) return;
                int row = TurnInput.VerticalStepPressed;
                int volumeStep = TurnInput.SelectStepPressed;
                if (row != 0) _flow.VolumeMoveRow(row);
                if (volumeStep != 0) _flow.VolumeAdjust(volumeStep);
                if (TurnInput.PausePressed || TurnInput.VolumePressed) _flow.HideVolume();
                return;
            }

            // 武器选择页：1 / 2 / 3 直接选，← → 切换 + Space / Enter 确认，也可以点卡片
            if (_flow != null && _flow.IsSelectOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (PanelOpen) return;
                int pick = TurnInput.ChoicePressed;
                int selectStep = TurnInput.SelectStepPressed;
                if (pick >= 0) _flow.ChooseCard(pick);
                else if (selectStep != 0) _flow.HighlightCard(_flow.SelectHighlight + selectStep, selectStep);
                else if (TurnInput.RollPressed || TurnInput.RestartPressed) _flow.ChooseCard(_flow.SelectHighlight);
                else if (TurnInput.PausePressed) OnSelectBack();
                return;
            }

            // 标题页：只等开始（Space / Enter / 按钮），封面翻开动画播完才放行
            if (_flow != null && _flow.IsTitleOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (!PanelOpen && TurnInput.VolumePressed) _flow.ShowVolume();
                else if (!PanelOpen && TurnInput.DisplayPressed) _flow.ShowDisplay();
                else if (!PanelOpen && TurnInput.EscapePressed) _flow.ShowQuit();
                else if (!PanelOpen && (TurnInput.RollPressed || TurnInput.RestartPressed)) _flow.StartFromTitle();
                return;
            }

            // 翻页过场中：暂停模拟、屏蔽玩法输入
            if (_pageTurn != null && _pageTurn.IsPlaying)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                return;
            }

            // 数值面板打开时：暂停模拟、屏蔽玩法输入（面板里打字不会触发翻滚 / 选升级）
            if (PanelOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                return;
            }
            ISessionSystem session = _ctx.Session;

            // 过关：翻页进下一关
            if (session.Phase == RunPhase.LevelCleared)
            {
                BeginPageTurn();
                return;
            }

            if (session.IsOver)
            {
                bool victory = session.Phase == RunPhase.Victory;
                if (!_resultShown) ShowResult(victory);
                // 胜利：R / Enter 从第一关重新开始；失败：重打本关；T 回到标题
                if (TurnInput.TitlePressed) ReturnToTitle();
                else if (weaponSelect && TurnInput.SwitchWeaponPressed && _flow != null) _flow.OpenSelectFromResult();
                else if (TurnInput.RestartPressed)
                {
                    if (victory) RestartFromFirstLevel();
                    else RestartLevel();
                }
                return;
            }

            if (TurnInput.PausePressed && session.Phase == RunPhase.Playing)
            {
                _paused = !_paused;
                if (_paused) RefreshPauseBuilds();
                if (_flow != null) _flow.SetPause(_paused);
                if (hud != null) hud.SetPaused(_paused);
            }
            if (_paused)
            {
                // 暂停菜单快捷键：V 音量设置，O 窗口设置，R 重打本关，T 回到标题（Esc / P 继续）
                if (TurnInput.VolumePressed && _flow != null)
                {
                    _flow.ShowVolume();
                    return;
                }
                if (TurnInput.DisplayPressed && _flow != null)
                {
                    _flow.ShowDisplay();
                    return;
                }
                if (TurnInput.TitlePressed)
                {
                    ReturnToTitle();
                    return;
                }
                if (TurnInput.RestartPressed)
                {
                    RestartLevel();
                    return;
                }
            }

            if (session.Phase == RunPhase.LevelUp || session.Phase == RunPhase.Chest)
            {
                int choice = TurnInput.ChoicePressed;
                if (choice >= 0) OnChooseUpgrade(choice);
            }

            _ctx.Player.SetMoveInput(ViewMath.ToVec2(TurnInput.Move));

            if (_paused || session.Phase != RunPhase.Playing)
            {
                _accumulator = 0f;
                return;
            }

            // Space：一次成功翻滚 = 进入亚空间（全程无敌）+ 全图爆发；亚空间结束时补满弹药（全部在 RollSystem 里）
            if (TurnInput.RollPressed) _ctx.Roll.TryRoll();

            // 模拟按现实时间推进（亚空间的慢放在 SessionSystem 里算），F5 调试倍速乘在这里
            float step = Mathf.Max(0.005f, simStep);
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * _debugSpeed;
            int steps = 0;
            while (_accumulator >= step && steps < maxStepsPerFrame)
            {
                if (_godMode) ApplyGodMode();
                session.Tick(step);
                _accumulator -= step;
                steps++;
                if (session.Phase != RunPhase.Playing)
                {
                    _accumulator = 0f;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            ApplyTimeScale();
            if (_ctx == null) return;
            if (_views != null) _views.Sync();
            phase = _ctx.Session.Phase;
            activeMap = _ctx.Plane.Active;
            monsterCount = _ctx.Monsters.AliveCount;
            sharkCount = _ctx.Sharks.Active.Count;
        }
    }
}
