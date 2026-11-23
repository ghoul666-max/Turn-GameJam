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

            // 过关翻页过场：没有手动挂就自动挂一个
            _pageTurn = GetComponent<PageTurnView>();
            if (_pageTurn == null) _pageTurn = gameObject.AddComponent<PageTurnView>();

            // 标题页 / 暂停菜单 / 结算界面：没有手动挂就自动挂一个（手动挂上可以在 Inspector 里改标题文字）
            _flow = GetComponent<FlowUI>();
            if (_flow == null) _flow = gameObject.AddComponent<FlowUI>();
            _flow.Setup(OnTitleOpened, ResumeFromPause, RestartLevel, RestartFromFirstLevel, ReturnToTitle);
            if (hud != null) hud.UseBuiltInPanels = false;

            // 音效：没有手动挂就自动挂一个（自动挂的没有音频，要配音效请在编辑器里给 GameManager 加上 TurnAudio 组件）
            _audio = GetComponent<TurnAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<TurnAudio>();
        }

        /// <summary>从场景组件 / Prefab 的 Inspector 读出当前数值（数值面板“从 Inspector 读取”用）。</summary>
        private TurnTuningData CaptureTuning()
        {
            return TurnTuningData.Capture(player, weapon, spawnManager, mapManager);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_views != null) _views.Dispose();
        }

        private void Start()
        {
            PlaneId start = mapManager != null ? mapManager.Environment.startMap : PlaneId.Volcano;
            _firstPair = PlaneIds.PairOf(start);
            _levelPair = _firstPair;
            _levelChosen = true;
            ResetTotals();
            StartNewRun();
            if (_flow != null) _flow.ShowTitle();
            if (_audio != null) _audio.PlayTitleMusic();
        }

        // ---------------- 流程（标题 / 暂停 / 结算的按钮与快捷键都走这里） ----------------

        private void OnTitleOpened()
        {
            if (_audio != null)
            {
                _audio.PlayPageTurn();
                _audio.PlayPlaneMusic(); // 封面翻开 = 真正开局：标题曲换回本关位面曲
            }
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
            RestartFromFirstLevel();
            if (_flow != null) _flow.ShowTitle();
            if (_audio != null) _audio.PlayTitleMusic();
        }

        private void ShowResult(bool victory)
        {
            _resultShown = true;
            if (victory) AddTotals();
            string body;
            if (victory)
            {
                body = "三组位面全部通过\n\n全程 " + FormatTime(_totalTime) + "    击杀 " + _totalKills + "    翻滚 " + _totalRolls + " 次";
            }
            else
            {
                body = "第 " + ((int)_levelPair + 1) + " 关 · " + PlaneIds.DisplayName(_levelPair) + "\n\n存活 " + FormatTime(_ctx.Session.Time) +
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
            TurnTuningStore.ApplyOrCapture(config, player, weapon, spawnManager, mapManager);
            // 关卡：开局位面换成本关这一组的第一面（如果 Inspector 的开局位面就在这一组里，保留它，方便测试第二面）
            if (_levelChosen && PlaneIds.PairOf(config.Environment.StartPlane) != _levelPair)
            {
                config.Environment.StartPlane = PlaneIds.First(_levelPair);
            }
            _resultShown = false;
            int seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;
            _ctx = TurnFactory.Create(config, seed);

            if (mapManager != null) mapManager.Bind(_ctx);
            if (_environmentView != null) _environmentView.Bind(_ctx, worldBounds != null ? worldBounds.DebugLineMaterial : null);
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

        private void OnChooseUpgrade(int index)
        {
            if (_ctx != null) _ctx.Session.TryChooseUpgrade(index);
        }

        // ---------------- 每帧 ----------------

        private void Update()
        {
            if (_ctx == null) return;

            if (TurnInput.ObstacleDebugPressed) Obstacle.ShowDebug = !Obstacle.ShowDebug;

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

            if (session.Phase == RunPhase.LevelUp)
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

            // Space：一次成功翻滚 = 无敌 + 切图 + 装填 + 惩罚刷怪（全部在 RollSystem.TryRoll 里）
            if (TurnInput.RollPressed) _ctx.Roll.TryRoll();

            float step = Mathf.Max(0.005f, simStep);
            _accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            int steps = 0;
            while (_accumulator >= step && steps < maxStepsPerFrame)
            {
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
            if (_ctx == null) return;
            if (_views != null) _views.Sync();
            phase = _ctx.Session.Phase;
            activeMap = _ctx.Plane.Active;
            monsterCount = _ctx.Monsters.AliveCount;
            sharkCount = _ctx.Sharks.Active.Count;
        }
    }
}
