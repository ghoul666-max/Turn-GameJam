using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 一局的流程编排。每个模拟步的固定顺序：
    /// 定时刷怪 → 玩家移动/环境 → 位面环境 → 翻滚计时（落地冲击波）→ 怪物 → 鲨鱼 → 武器 → 子弹 → 经验球。
    /// 之后依次检查：失败 → 过关（存活满 LevelConfig.SurviveSeconds）→ 是否有待处理的升级。
    /// 翻滚不在这里：它由输入层直接调用 IRollSystem.TryRoll，在一次调用里原子完成。
    /// </summary>
    public sealed class SessionSystem : ISessionSystem
    {
        private readonly ITurnContext _ctx;
        private bool _started;

        public RunPhase Phase { get; private set; }
        public float Time { get; private set; }
        public bool IsOver { get { return Phase == RunPhase.Defeat || Phase == RunPhase.Victory; } }

        public float LevelTimeLeft
        {
            get
            {
                float total = _ctx.Config.Level.SurviveSeconds;
                return total > 0f && total > Time ? total - Time : 0f;
            }
        }

        public SessionSystem(ITurnContext ctx)
        {
            _ctx = ctx;
            Phase = RunPhase.Playing;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            _ctx.Spawner.SpawnInitial();
            PlaneId start = _ctx.Plane.Active;
            _ctx.Environment.OnPlaneEntered(start); // 起始位面的进入结算（火山：岩浆喷发一次后消失）
            _ctx.Sharks.SyncWithPlane();            // 起始位面是海洋时立刻生成鲨鱼
            _ctx.Events.Publish(new RunStartedEvent { Plane = start, Pair = _ctx.Plane.ActivePair });
            _ctx.Events.Publish(new PhaseChangedEvent { Phase = Phase });
        }

        public void Tick(float dt)
        {
            if (!_started || Phase != RunPhase.Playing) return;

            Time += dt;
            _ctx.Spawner.Tick(dt, Time);
            _ctx.Player.Tick(dt);
            _ctx.Environment.Tick(dt);
            _ctx.Roll.Tick(dt);
            _ctx.Monsters.Tick(dt);
            _ctx.Sharks.Tick(dt);
            _ctx.Weapons.Tick(dt);
            _ctx.Projectiles.Tick(dt);
            _ctx.Experience.Tick(dt);

            if (!_ctx.Player.State.Alive)
            {
                SetPhase(RunPhase.Defeat);
                return;
            }

            // 过关：存活够时间（死亡优先判定）。表现层收到后翻页，开下一关的新一局。
            LevelConfig level = _ctx.Config.Level;
            if (level.SurviveSeconds > 0f && Time >= level.SurviveSeconds)
            {
                PlanePairId pair = _ctx.Plane.ActivePair;
                bool victory = pair == level.FinalPair;
                _ctx.Events.Publish(new LevelClearedEvent { Pair = pair, Time = Time, Victory = victory });
                SetPhase(victory ? RunPhase.Victory : RunPhase.LevelCleared);
                return;
            }
            OpenLevelUpIfPending();
        }

        public bool TryChooseUpgrade(int index)
        {
            if (Phase != RunPhase.LevelUp) return false;
            if (!_ctx.Progression.TryApply(index)) return false;

            if (_ctx.Progression.PendingLevelUps > 0)
            {
                // 一次吃到大量经验会连升几级：继续停在升级阶段，换一组新选项。
                _ctx.Progression.RollOptions();
                if (_ctx.Progression.Options.Count > 0)
                {
                    _ctx.Events.Publish(new PhaseChangedEvent { Phase = RunPhase.LevelUp });
                    return true;
                }
                while (_ctx.Progression.PendingLevelUps > 0) _ctx.Progression.SkipPending();
            }
            SetPhase(RunPhase.Playing);
            return true;
        }

        private void OpenLevelUpIfPending()
        {
            if (_ctx.Progression.PendingLevelUps <= 0) return;
            _ctx.Progression.RollOptions();
            if (_ctx.Progression.Options.Count == 0)
            {
                // 五个词条全部满级：没有可选项，直接消耗掉，不打断游戏。
                while (_ctx.Progression.PendingLevelUps > 0) _ctx.Progression.SkipPending();
                return;
            }
            SetPhase(RunPhase.LevelUp);
        }

        private void SetPhase(RunPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _ctx.Events.Publish(new PhaseChangedEvent { Phase = phase });
        }
    }
}
