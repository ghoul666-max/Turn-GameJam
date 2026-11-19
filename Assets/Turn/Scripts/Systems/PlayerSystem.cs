using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 玩家移动与环境结算。
    /// 移动：平时按 WASD（长度限制在 1，斜向不更快）；翻滚中忽略输入，沿翻滚方向匀速位移。
    /// 每次移动后都经过 IBoundsSystem.Clamp。
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

        public PlayerState State { get; private set; }
        public PlayerConfig Config { get; private set; }

        public PlayerSystem(ITurnContext ctx, PlayerConfig config, EnvironmentConfig env)
        {
            _ctx = ctx;
            Config = config;
            _env = env;
            State = new PlayerState();
        }

        public void Setup()
        {
            State = new PlayerState();
            State.MaxHp = Config.MaxHp;
            State.Hp = Config.MaxHp;
            State.Alive = true;
            State.Position = _ctx.Bounds.Clamp(_ctx.Bounds.Rect.Center, Config.Radius);
            State.Terrain = _ctx.Terrain.Sample(State.Position);
            _envTerrain = State.Terrain;
            _envTimer = 0f;
        }

        public void SetMoveInput(Vec2 input)
        {
            _input = Vec2.ClampMagnitude(input, 1f);
        }

        public void Tick(float dt)
        {
            PlayerState s = State;
            if (!s.Alive)
            {
                s.MoveDir = Vec2.Zero;
                return;
            }

            // 1) 移动
            if (s.IsRolling)
            {
                s.MoveDir = s.RollDirection;
                s.Position += s.RollDirection * (s.RollSpeed * dt);
            }
            else
            {
                s.MoveDir = _input;
                if (_input.SqrMagnitude > 0.0001f)
                {
                    s.Position += _input * (Config.MoveSpeed * dt);
                    s.Facing = _input.Normalized;
                }
            }

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
