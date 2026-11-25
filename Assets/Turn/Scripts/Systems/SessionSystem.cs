using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 一局的流程编排。每个模拟步的固定顺序：
    /// 波次 → 定时刷怪 → 玩家移动/环境 → 位面环境 → 翻滚计时（游戏时间）→ 亚空间计时（现实时间，到点退出 + 装填）
    /// → 怪物 → 鲨鱼 → 武器 → 子弹 → 经验球。
    /// 亚空间（这一步开始时 PlayerState.InSubspace）：游戏时间 = 现实时间 × RollConfig.SubspaceTimeScale；
    /// 刷怪 / 环境 / 怪物 / 鲨鱼 / 武器 / 子弹全部跳过（敌人停止移动和攻击，武器不再铺垫），只推进玩家、翻滚计时与经验球。
    /// 之后依次检查：失败 → 过关（非无尽模式且存活满 LevelConfig.SurviveSeconds）→ 是否有待处理的升级（亚空间里推迟）。
    /// 翻滚不在这里：它由输入层直接调用 IRollSystem.TryRoll，在一次调用里原子完成。
    /// </summary>
    public sealed class SessionSystem : ISessionSystem
    {
        private readonly ITurnContext _ctx;
        private bool _started;

        public RunPhase Phase { get; private set; }
        public float Time { get; private set; }
        public int Wave { get; private set; }
        public bool IsOver { get { return Phase == RunPhase.Defeat || Phase == RunPhase.Victory; } }

        public float WaveTimeLeft
        {
            get
            {
                float length = _ctx.Config.Level.WaveSeconds;
                if (length <= 0f) return 0f;
                return MathF.Max(0f, Wave * length - Time);
            }
        }

        public float LevelTimeLeft
        {
            get
            {
                LevelConfig level = _ctx.Config.Level;
                if (level.Endless) return 0f;
                float total = level.SurviveSeconds;
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
            Wave = 1;
            _ctx.Events.Publish(new WaveStartedEvent { Wave = Wave });
        }

        /// <summary>dt = 现实时间步长（GameManager 的固定步长；F5 调试加速已乘进去）。</summary>
        public void Tick(float dt)
        {
            if (!_started || Phase != RunPhase.Playing) return;

            bool subspace = _ctx.Player.State.InSubspace;
            float gameDt = subspace ? dt * SubspaceTimeScale : dt;

            Time += gameDt;
            UpdateWave();
            if (!subspace) _ctx.Spawner.Tick(gameDt, Time);
            _ctx.Player.Tick(gameDt);
            if (!subspace) _ctx.Environment.Tick(gameDt);
            _ctx.Roll.Tick(gameDt);
            _ctx.Roll.TickRealtime(dt);
            if (!subspace)
            {
                _ctx.Monsters.Tick(gameDt);
                _ctx.Sharks.Tick(gameDt);
                _ctx.Weapons.Tick(gameDt);
                _ctx.Projectiles.Tick(gameDt);
            }
            _ctx.Experience.Tick(gameDt);

            if (!_ctx.Player.State.Alive)
            {
                SetPhase(RunPhase.Defeat);
                return;
            }

            // 过关：非无尽模式下存活够时间（死亡优先判定）。表现层收到后翻页，开下一关的新一局。
            LevelConfig level = _ctx.Config.Level;
            if (!level.Endless && level.SurviveSeconds > 0f && Time >= level.SurviveSeconds)
            {
                ClearLevel();
                return;
            }
            // 亚空间里不弹升级窗：回到常态空间后的第一步再弹
            if (!_ctx.Player.State.InSubspace) OpenLevelUpIfPending();
        }

        private float SubspaceTimeScale
        {
            get
            {
                float s = _ctx.Config.Roll.SubspaceTimeScale;
                return s < 0f ? 0f : (s > 1f ? 1f : s);
            }
        }

        /// <summary>波次 = 1 + floor(游戏时间 / WaveSeconds)，无上限。每进一波发布一次 WaveStartedEvent（一步跨多波时逐波发布）。</summary>
        private void UpdateWave()
        {
            float length = _ctx.Config.Level.WaveSeconds;
            if (length <= 0f) return;
            int wave = 1 + (int)MathF.Floor(Time / length);
            while (Wave < wave)
            {
                Wave++;
                _ctx.Events.Publish(new WaveStartedEvent { Wave = Wave });
            }
        }

        public void DebugClearLevel()
        {
            if (!_started || (Phase != RunPhase.Playing && Phase != RunPhase.LevelUp)) return;
            if (_ctx.Config.Level.Endless) return; // 无尽模式没有过关
            ClearLevel();
        }

        /// <summary>本关通过：发布 LevelClearedEvent，最后一组 = 胜利，否则 LevelCleared（等表现层翻页）。</summary>
        private void ClearLevel()
        {
            PlanePairId pair = _ctx.Plane.ActivePair;
            bool victory = pair == _ctx.Config.Level.FinalPair;
            _ctx.Events.Publish(new LevelClearedEvent { Pair = pair, Time = Time, Victory = victory });
            SetPhase(victory ? RunPhase.Victory : RunPhase.LevelCleared);
        }

        /// <summary>升级 / 宝箱三选一：选择第 index 项。还有待处理的升级或宝箱就继续停在选择阶段（换一组新选项），否则回到 Playing。</summary>
        public bool TryChooseUpgrade(int index)
        {
            if (Phase != RunPhase.LevelUp && Phase != RunPhase.Chest) return false;
            if (!_ctx.Progression.TryApply(index)) return false;
            if (OpenSelection(true)) return true;
            SetPhase(RunPhase.Playing);
            return true;
        }

        private void OpenLevelUpIfPending()
        {
            OpenSelection(false);
        }

        /// <summary>
        /// 升级优先，其次宝箱。没有可选项（词条全部满级）时直接消耗掉，不打断游戏。
        /// republish：已经在选择阶段时，换了一组新选项也要重新发布 PhaseChangedEvent 让 UI 刷新。
        /// </summary>
        private bool OpenSelection(bool republish)
        {
            IProgressionSystem progression = _ctx.Progression;
            while (progression.PendingLevelUps > 0 || progression.PendingChests > 0)
            {
                bool levelUp = progression.PendingLevelUps > 0;
                if (levelUp) progression.RollOptions();
                else progression.RollChestOptions();
                if (progression.Options.Count == 0)
                {
                    progression.SkipPending();
                    continue;
                }
                RunPhase phase = levelUp ? RunPhase.LevelUp : RunPhase.Chest;
                if (Phase == phase && republish) _ctx.Events.Publish(new PhaseChangedEvent { Phase = phase });
                else SetPhase(phase);
                return true;
            }
            return false;
        }

        private void SetPhase(RunPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _ctx.Events.Publish(new PhaseChangedEvent { Phase = phase });
        }
    }
}
