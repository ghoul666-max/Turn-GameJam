using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 一局的流程编排（对应卡牌白模的 TurnSystem）。
    /// 每个模拟步的固定顺序：刷怪 → 玩家移动 → 敌人移动/接触伤害 → 武器发动 → 投射物命中 → 拾取。
    /// 之后依次检查：失败 → 胜利 → 是否有待处理的升级。
    /// </summary>
    public sealed class SessionSystem : ISessionSystem
    {
        private readonly ISurvivorContext _ctx;
        private bool _started;

        public RunPhase Phase { get; private set; }
        public float Time { get; private set; }
        public float Duration { get; private set; }
        public bool IsOver { get { return Phase == RunPhase.Victory || Phase == RunPhase.Defeat; } }

        public SessionSystem(ISurvivorContext ctx, float duration)
        {
            _ctx = ctx;
            Duration = duration > 0f ? duration : 300f;
            Phase = RunPhase.Playing;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _ctx.Events.Publish(new RunStartedEvent());
            _ctx.Events.Publish(new PhaseChangedEvent { Phase = Phase });
        }

        public void Tick(float dt)
        {
            if (!_started || Phase != RunPhase.Playing) return;

            Time += dt;
            _ctx.Spawner.Tick(dt, Time);
            _ctx.Player.Tick(dt);
            _ctx.Enemies.Tick(dt);
            _ctx.Weapons.Tick(dt);
            _ctx.Projectiles.Tick(dt);
            _ctx.Pickups.Tick(dt);

            if (!_ctx.Player.State.Alive)
            {
                End(false);
                return;
            }
            if (Time >= Duration)
            {
                Time = Duration;
                End(true);
                return;
            }
            if (_ctx.Progression.PendingLevelUps > 0)
            {
                _ctx.Progression.RollOptions();
                SetPhase(RunPhase.LevelUp);
            }
        }

        public bool TryChooseUpgrade(int index)
        {
            if (Phase != RunPhase.LevelUp) return false;
            if (!_ctx.Progression.TryApply(index)) return false;

            if (_ctx.Progression.PendingLevelUps > 0)
            {
                // 一次吃到大量经验会连升几级：继续停在升级阶段，换一组新选项。
                _ctx.Progression.RollOptions();
                _ctx.Events.Publish(new PhaseChangedEvent { Phase = RunPhase.LevelUp });
            }
            else
            {
                SetPhase(RunPhase.Playing);
            }
            return true;
        }

        private void End(bool victory)
        {
            SetPhase(victory ? RunPhase.Victory : RunPhase.Defeat);
            _ctx.Events.Publish(new RunEndedEvent { Victory = victory, Time = Time });
        }

        private void SetPhase(RunPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _ctx.Events.Publish(new PhaseChangedEvent { Phase = phase });
        }
    }
}
