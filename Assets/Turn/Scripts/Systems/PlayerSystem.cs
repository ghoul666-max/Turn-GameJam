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
        }

        public void SetMoveInput(Vec2 input)
        {
            _input = Vec2.ClampMagnitude(input, 1f);
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
                return;
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
            TickEnvironment(dt);
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
