using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 玩家移动与环境结算。
    /// 移动：平时按 WASD（长度限制在 1，斜向不更快）；翻滚中忽略输入，沿翻滚方向匀速位移。
    /// 每次移动后先经过 IObstacleSystem.ResolveMove（遮挡物），再经过 IBoundsSystem.Clamp。
    /// 移动速度 = 等级基础速度（SetLevelStats 写入，开局 = Config.MoveSpeed）× 环境修正（M2：梦境减速层）。
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

        public PlayerState State { get; private set; }
        public PlayerConfig Config { get; private set; }

        /// <summary>当前生效的移动速度。环境减速（M2）接入后在这里乘上修正系数。</summary>
        public float MoveSpeed { get { return _baseMoveSpeed; } }

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
            _baseMoveSpeed = Config.MoveSpeed;
            State.Position = _ctx.Bounds.Clamp(_ctx.Bounds.Rect.Center, Config.Radius);
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
            if (Vec2.SqrDistance(from, to) > 0f) s.Position = _ctx.Obstacles.ResolveMove(from, to, Config.Radius, ObstacleMask.Units);

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
