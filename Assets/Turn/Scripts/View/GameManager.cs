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

        [Header("武器选择")]
        [Tooltip("开局（标题页之后）弹出武器选择页；关掉 = 直接用水枪开局")]
        [SerializeField] private bool weaponSelect = true;
        private WeaponKind _startWeapon = WeaponKind.WaterGun;

        [Header("调试快捷键（发布前取消勾选）")]
        [Tooltip("F3 立刻通过本关 / F4 无敌开关 / F5 游戏速度 1× → 2× → 4× / F6 切到下一把已解锁的武器")]
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
        // 全程统计（胜利结算用，只累计已通过的关）
        private float _totalTime;
        private int _totalKills;
        private int _totalRolls;

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
                weaponSelect ? OpenWeaponSelect : (Action)null, OnWeaponChosen);
            if (hud != null) hud.UseBuiltInPanels = false;

            // 音效：没有手动挂就自动挂一个（自动挂的没有音频，要配音效请在编辑器里给 GameManager 加上 TurnAudio 组件）
            _audio = GetComponent<TurnAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<TurnAudio>();
        }

        /// <summary>从场景组件 / Prefab 的 Inspector 读出当前数值（数值面板“从 Inspector 读取”用）。</summary>
        private TurnTuningData CaptureTuning()
        {
            return TurnTuningData.Capture(player, weapon, spawnManager, mapManager, upgradeManager);
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

        /// <summary>F6：按 水枪 → 投矛 → 陌刀 的顺序切到下一把已解锁的武器（亚空间里 Equip 会拒绝）。</summary>
        private void EquipNextUnlockedWeapon()
        {
            ILoadoutSystem loadout = _ctx.Loadout;
            int start = (int)loadout.Equipped;
            for (int i = 1; i < WeaponKinds.Count; i++)
            {
                var next = (WeaponKind)((start + i) % WeaponKinds.Count);
                if (loadout.IsUnlocked(next))
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
            bool frozen = _paused || (_tuning != null && _tuning.IsOpen);
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
            string text = "调试：" + (_godMode ? "无敌（F4）  " : "") + (_debugSpeed != 1f ? "速度 ×" + _debugSpeed.ToString("0") + "（F5）" : "");
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

        // ---------------- 武器选择（正式入口） ----------------

        /// <summary>打开武器选择页：卡片上的数值直接读这一局的配置（Inspector / F1 面板改了这里跟着变）。</summary>
        private void OpenWeaponSelect()
        {
            if (_flow == null || _ctx == null) return;
            TurnConfig c = _ctx.Config;
            var titles = new string[WeaponKinds.Count];
            var bodies = new string[WeaponKinds.Count];
            var colors = new Color[WeaponKinds.Count];
            for (int i = 0; i < WeaponKinds.Count; i++)
            {
                var w = (WeaponKind)i;
                titles[i] = (i + 1) + "  " + WeaponKinds.DisplayName(w);
                bodies[i] = WeaponCard(w, c);
                colors[i] = ViewMath.WeaponColor(w);
            }
            string header = _flow.IsTitleOpen ? "选择武器" : "换武器重开";
            _flow.ShowWeaponSelect(header, titles, bodies, colors, (int)_startWeapon);
        }

        private string WeaponCard(WeaponKind w, TurnConfig c)
        {
            ArsenalConfig a = c.Arsenal;
            float cd = _ctx.Roll.CooldownFor(w);
            switch (w)
            {
                case WeaponKind.WaterGun:
                    return "弹药 " + a.WaterGun.Ammo + " · " + Num(a.WaterGun.FireInterval) + " 秒/发 · 伤害 " + Num(a.WaterGun.Damage) +
                           "\n翻滚冷却 " + Num(cd) + " 秒" +
                           "\n\n<b>铺垫</b>  水弹落地或命中处留下一滩水渍，永久存在" +
                           "\n\n<b>爆发</b>  全图水渍同时喷发喷泉（每滩 " + Num(a.WaterGun.FountainDamage) + "），重叠处伤害叠加" +
                           "\n\n适合：边走边把一片地铺满，再翻滚";
                case WeaponKind.Spear:
                    return "弹药 " + a.Spear.Ammo + " · " + Num(a.Spear.FireInterval) + " 秒/发 · 伤害 " + Num(a.Spear.Damage) +
                           "\n翻滚冷却 " + Num(cd) + " 秒" +
                           "\n\n<b>铺垫</b>  长矛插在敌人身上叠矛印记；直接击杀则穿透下一个，印记一起带走" +
                           "\n\n<b>爆发</b>  所有长矛向你回收，沿途穿刺（每根 " + Num(a.Spear.RecallDamage) + "）" +
                           "\n\n适合：把敌人引到身前排成一线";
                default:
                    return "弹药 " + a.Blade.Ammo + " · " + Num(a.Blade.FireInterval) + " 秒/发 · 伤害 " + Num(a.Blade.Damage) +
                           "\n翻滚冷却 " + Num(cd) + " 秒" +
                           "\n\n<b>铺垫</b>  " + Num(a.Blade.Angle) + "° 扇形剑气，命中的敌人各叠 1 层剑气印记" +
                           "\n\n<b>爆发</b>  横贯全图的一刀斩（" + Num(a.Blade.SlashDamage) + "），全场每层印记 +" + Mathf.RoundToInt(a.Blade.MarkBonus * 100f) + "%" +
                           "\n\n适合：攒满层数，一刀清屏";
            }
        }

        private static string Num(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");
        }

        /// <summary>选定武器：从头开一局新的（标题页来的会接着翻开封面；结算界面来的直接开始）。</summary>
        private void OnWeaponChosen(int index)
        {
            _startWeapon = WeaponKinds.IsValid((WeaponKind)index) ? (WeaponKind)index : WeaponKind.WaterGun;
            RestartFromFirstLevel();
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
            if (victory) AddTotals();
            string body;
            if (victory)
            {
                body = "三组位面全部通过\n\n全程 " + FormatTime(_totalTime) + "    击杀 " + _totalKills + "    翻滚 " + _totalRolls + " 次";
                if (_flow != null && !string.IsNullOrEmpty(_flow.Credits)) body += "\n\n<size=22>" + _flow.Credits + "</size>";
            }
            else
            {
                string where = _ctx.Config.Level.Endless
                    ? "第 " + _ctx.Session.Wave + " 波 · " + WeaponKinds.DisplayName(_ctx.Loadout.Equipped)
                    : "第 " + ((int)_levelPair + 1) + " 关 · " + PlaneIds.DisplayName(_levelPair);
                body = where + "\n\n存活 " + FormatTime(_ctx.Session.Time) +
                       "    等级 " + _ctx.Progression.Level + "    击杀 " + _ctx.Progression.Kills + "    翻滚 " + _ctx.Roll.RollCount + " 次";
            }
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
            // 开局武器 = 武器选择页选的那一把（它总是已解锁；其余武器按波次解锁）
            config.Loadout.StartWeapon = _startWeapon;
            // 关卡：开局位面换成本关这一组的第一面（如果 Inspector 的开局位面就在这一组里，保留它，方便测试第二面）
            if (_levelChosen && PlaneIds.PairOf(config.Environment.StartPlane) != _levelPair)
            {
                config.Environment.StartPlane = PlaneIds.First(_levelPair);
            }
            _resultShown = false;
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

            // 武器选择页：1 / 2 / 3 直接选，← → 切换 + Space / Enter 确认，也可以点卡片
            if (_flow != null && _flow.IsSelectOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (_tuning != null && _tuning.IsOpen) return;
                int pick = TurnInput.ChoicePressed;
                int selectStep = TurnInput.SelectStepPressed;
                if (pick >= 0) _flow.ChooseWeapon(pick);
                else if (selectStep != 0) _flow.HighlightWeapon(_flow.SelectHighlight + selectStep);
                else if (TurnInput.RollPressed || TurnInput.RestartPressed) _flow.ChooseWeapon(_flow.SelectHighlight);
                return;
            }

            // 标题页：只等开始（Space / Enter / 按钮），封面翻开动画播完才放行
            if (_flow != null && _flow.IsTitleOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                if (!(_tuning != null && _tuning.IsOpen) && (TurnInput.RollPressed || TurnInput.RestartPressed)) _flow.StartFromTitle();
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
            if (_tuning != null && _tuning.IsOpen)
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
                if (_flow != null) _flow.SetPause(_paused);
                if (hud != null) hud.SetPaused(_paused);
            }
            if (_paused)
            {
                // 暂停菜单快捷键：R 重打本关，T 回到标题（Esc / P 继续）
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
