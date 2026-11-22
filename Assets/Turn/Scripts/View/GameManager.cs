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
            StartNewRun();
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
            int seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;
            _ctx = TurnFactory.Create(config, seed);

            if (mapManager != null) mapManager.Bind(_ctx);
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

            // 数值面板打开时：暂停模拟、屏蔽玩法输入（面板里打字不会触发翻滚 / 选升级）
            if (_tuning != null && _tuning.IsOpen)
            {
                _ctx.Player.SetMoveInput(Vec2.Zero);
                _accumulator = 0f;
                return;
            }
            ISessionSystem session = _ctx.Session;

            if (session.IsOver)
            {
                if (TurnInput.RestartPressed) StartNewRun();
                return;
            }

            if (TurnInput.PausePressed && session.Phase == RunPhase.Playing)
            {
                _paused = !_paused;
                if (hud != null) hud.SetPaused(_paused);
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
