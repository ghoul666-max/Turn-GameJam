using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 玩家移动与环境结算。
    /// 移动：平时按 WASD（长度限制在 1，斜向不更快）；翻滚中忽略输入，沿翻滚方向匀速位移。
    /// 每次移动后先经过 IObstacleSystem.ResolveMove（遮挡物），再经过 IBoundsSystem.Clamp。
    /// 移动速度 = 等级基础速度（SetLevelStats 写入，开局 = Config.MoveSpeed）× 各来源移速倍率之积（SetSpeedModifier）。
    /// 每一步都经过一次 ResolveMove（即使没动）：翻滚切位面后如果正好站在新位面的遮挡物里，会被推到最近的外侧。
    /// 环境：读当前激活地图的地形 —— 岩浆按间隔扣血（经 CombatSystem，无敌时被拒绝），
    /// 浅水按间隔回血；火山地图没有任何回血来源。
    /// 生命由 CombatSystem 负责（初始化除外）；翻滚相关字段由 RollSystem 负责。
    /// 受伤无敌帧：收到 PlayerDamagedEvent（真的扣了血）后写 HurtInvincibleLeft = PlayerConfig.HurtInvincibleTime（策划 0.3 秒）。
    /// 位面机制总开关（EnvironmentConfig.MechanicsEnabled）关闭时不结算岩浆 / 浅水。
    /// </summary>
    public sealed class PlayerSystem : IPlayerSystem
    {
        private readonly ITurnContext _ctx;
        private readonly EnvironmentConfig _env;
        private Vec2 _input;
        private float _envTimer;
        private TerrainType _envTerrain;
        private float _baseMoveSpeed;
        private readonly float[] _speedModifiers = new float[PlayerSpeedSources.Count];
        private float _speedMultiplier = 1f;
        /// <summary>维修机器人命中后待结算的击退（下一次 Tick 开头经遮挡物 / 边界修正后应用）。</summary>
        private Vec2 _pendingKnockback;
        /// <summary>击退分步长度：每步都过一次 ResolveMove，避免一下穿过薄遮挡物。</summary>
        private const float KnockbackStep = 0.25f;

        public PlayerState State { get; private set; }
        public PlayerConfig Config { get; private set; }

        public float MoveSpeed { get { return _baseMoveSpeed * _speedMultiplier; } }
        public float SpeedMultiplier { get { return _speedMultiplier; } }

        public PlayerSystem(ITurnContext ctx, PlayerConfig config, EnvironmentConfig env)
        {
            _ctx = ctx;
            Config = config;
            _env = env;
            State = new PlayerState();
            _baseMoveSpeed = config.MoveSpeed;
            ClearSpeedModifiers();
            // B 的 MonsterSystem 只发“机器人已命中”的事实；玩家位置只在这里写。
            ctx.Events.Subscribe<RobotKnockbackHitEvent>(OnRobotKnockbackHit);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            PlayerState s = State;
            if (!s.Alive) return;
            s.HurtInvincibleLeft = MathF.Max(s.HurtInvincibleLeft, MathF.Max(0f, Config.HurtInvincibleTime));
        }

        private void OnRobotKnockbackHit(RobotKnockbackHitEvent evt)
        {
            if (evt.Plane != _ctx.Plane.Active || evt.Distance <= 0f || evt.Direction.SqrMagnitude < 0.0001f) return;
            _pendingKnockback = _pendingKnockback + evt.Direction.Normalized * evt.Distance;
        }

        public void Setup()
        {
            State = new PlayerState();
            State.MaxHp = Config.MaxHp;
            State.Hp = Config.MaxHp;
            State.Alive = true;
            _baseMoveSpeed = Config.MoveSpeed;
            ClearSpeedModifiers();
            State.Position = _ctx.Bounds.Clamp(_ctx.Bounds.Rect.Center, Config.Radius);
            // 出生点正好压在遮挡物上时推到外侧
            State.Position = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(State.Position, State.Position, Config.Radius, ObstacleMask.Units), Config.Radius);
            State.Terrain = _ctx.Terrain.Sample(State.Position);
            _envTerrain = State.Terrain;
            _envTimer = 0f;
            _pendingKnockback = Vec2.Zero;
        }

        public void SetMoveInput(Vec2 input)
        {
            _input = Vec2.ClampMagnitude(input, 1f);
        }

        public void Teleport(Vec2 position)
        {
            PlayerState s = State;
            Vec2 from = s.Position;
            Vec2 to = _ctx.Bounds.Clamp(position, Config.Radius);
            to = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(to, to, Config.Radius, ObstacleMask.Units), Config.Radius);
            s.Position = to;
            _pendingKnockback = Vec2.Zero;
            _ctx.Events.Publish(new PlayerTeleportedEvent { From = from, To = to });
        }

        public void SetLevelStats(float maxHp, float moveSpeed)
        {
            PlayerState s = State;
            if (maxHp > 0f)
            {
                float gained = maxHp - s.MaxHp;
                s.MaxHp = maxHp;
                // 只在提高时同步加血；降低时把当前生命压到新上限以内。生命的增减本应走 CombatSystem，
                // 这里属于“上限变化”的初始化性质写入，与 Setup 同等对待。
                if (gained > 0f && s.Alive) s.Hp += gained;
                if (s.Hp > s.MaxHp) s.Hp = s.MaxHp;
            }
            if (moveSpeed > 0f) _baseMoveSpeed = moveSpeed;
        }

        public void SetSpeedModifier(PlayerSpeedSource source, float multiplier)
        {
            int i = (int)source;
            if (i < 0 || i >= _speedModifiers.Length) return;
            _speedModifiers[i] = MathF.Max(0f, multiplier);
            RecalculateSpeed();
        }

        public void ClearSpeedModifiers()
        {
            for (int i = 0; i < _speedModifiers.Length; i++) _speedModifiers[i] = 1f;
            _speedMultiplier = 1f;
        }

        private void RecalculateSpeed()
        {
            float m = 1f;
            for (int i = 0; i < _speedModifiers.Length; i++) m *= _speedModifiers[i];
            _speedMultiplier = m;
        }

        public void Tick(float dt)
        {
            PlayerState s = State;
            if (!s.Alive)
            {
                s.MoveDir = Vec2.Zero;
                _pendingKnockback = Vec2.Zero;
                return;
            }
            if (s.HurtInvincibleLeft > 0f) s.HurtInvincibleLeft = MathF.Max(0f, s.HurtInvincibleLeft - dt);

            // 0) 维修机器人击退：分小步经遮挡物修正，再 Clamp 进边界
            if (_pendingKnockback.SqrMagnitude > 0.0001f)
            {
                float distance = _pendingKnockback.Magnitude;
                Vec2 dir = _pendingKnockback / distance;
                int steps = Math.Max(1, (int)MathF.Ceiling(distance / KnockbackStep));
                float step = distance / steps;
                Vec2 p = s.Position;
                for (int i = 0; i < steps; i++)
                    p = _ctx.Obstacles.ResolveMove(p, p + dir * step, Config.Radius, ObstacleMask.Units);
                s.Position = _ctx.Bounds.Clamp(p, Config.Radius);
                _pendingKnockback = Vec2.Zero;
            }

            // 1) 移动（先算想去的位置，再经遮挡物修正）
            Vec2 from = s.Position;
            Vec2 to = from;
            if (s.IsRolling)
            {
                s.MoveDir = s.RollDirection;
                to = from + s.RollDirection * (s.RollSpeed * dt);
            }
            else
            {
                s.MoveDir = _input;
                if (_input.SqrMagnitude > 0.0001f)
                {
                    to = from + _input * (MoveSpeed * dt);
                    s.Facing = _input.Normalized;
                }
            }
            s.Position = _ctx.Obstacles.ResolveMove(from, to, Config.Radius, ObstacleMask.Units);

            // 2) 统一边界
            s.Position = _ctx.Bounds.Clamp(s.Position, Config.Radius);

            // 3) 地形（当前激活地图）
            s.Terrain = _ctx.Terrain.Sample(s.Position);
            if (_env.MechanicsEnabled) TickEnvironment(dt);
        }

        /// <summary>按固定间隔结算岩浆 / 浅水。换了地形就从头计时，避免把上一块地形的计时带过来。</summary>
        private void TickEnvironment(float dt)
        {
            PlayerState s = State;
            if (s.Terrain != _envTerrain)
            {
                _envTerrain = s.Terrain;
                _envTimer = 0f;
            }
            if (s.Terrain == TerrainType.Normal) return;

            float interval = MathF.Max(0.02f, _env.DotTickInterval);
            _envTimer += dt;
            while (_envTimer >= interval)
            {
                _envTimer -= interval;
                if (s.Terrain == TerrainType.Lava)
                {
                    _ctx.Combat.DamagePlayer(_env.PlayerLavaDps * interval, DamageSource.Lava);
                    if (!s.Alive) return;
                }
                else if (s.Terrain == TerrainType.ShallowWater)
                {
                    _ctx.Combat.HealPlayer(_env.ShallowRegenPerSecond * interval);
                }
            }
        }
    }
}
