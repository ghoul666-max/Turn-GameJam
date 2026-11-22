using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 普通怪物。跨地图保留的实体：地图切换时这里什么都不做 —— 不销毁、不重建、不改坐标 / 血量 / Buff。
    /// （皮肤是纯表现，由 MonsterController 按当前地图切换。）
    /// 每步：移出死亡实体 → 直线追踪玩家 → 互相挤开 → 统一边界 → 接触伤害 → 灼烧。
    /// 岩浆伤害不在这里：切到火山时由 EnvironmentSystem 一次性结算。
    /// 注意：怪物在浅水区没有任何回血逻辑（这是规则，不是遗漏）。
    /// 位置 / 存活 / 计时 / 灼烧字段只有这里能写；生命由 CombatSystem 写。
    /// </summary>
    public sealed class MonsterSystem : IMonsterSystem
    {
        /// <summary>互相挤开的力度（每步最多推开的距离）。</summary>
        public const float SeparationStrength = 0.5f;
        public const float MaxSeparationStep = 0.2f;

        private readonly ITurnContext _ctx;
        private readonly MonsterConfig _config;
        private readonly EnvironmentConfig _env;
        private readonly List<MonsterEntity> _active = new List<MonsterEntity>();
        private int _nextId = 1;

        public IReadOnlyList<MonsterEntity> Active { get { return _active; } }
        public int AliveCount { get; private set; }

        public MonsterSystem(ITurnContext ctx, MonsterConfig config, EnvironmentConfig env)
        {
            _ctx = ctx;
            _config = config;
            _env = env;
        }

        // ---------------- 生成 / 死亡 / Buff ----------------

        public MonsterEntity Spawn(Vec2 position, bool penalty)
        {
            var m = new MonsterEntity();
            m.Id = _nextId++;
            m.Position = _ctx.Bounds.Clamp(position, _config.Radius); // 生成点也必须在边界内
            // 不生成在遮挡物里（程序A 补）
            m.Position = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(m.Position, m.Position, _config.Radius, ObstacleMask.Units), _config.Radius);
            m.Form = _ctx.Plane.Active;
            m.MaxHp = MaxHpFor(m.Form);
            m.Hp = m.MaxHp;
            m.Alive = true;
            m.Penalty = penalty;
            m.Terrain = _ctx.Terrain.Sample(m.Position);
            _active.Add(m);
            AliveCount++;
            _ctx.Events.Publish(new MonsterSpawnedEvent { MonsterId = m.Id, Penalty = penalty });
            return m;
        }

        public void Despawn(MonsterEntity monster)
        {
            if (monster == null || !monster.Alive) return;
            monster.Alive = false;
            AliveCount--;
        }

        public void ApplyBurn(MonsterEntity monster, float dps, float duration)
        {
            if (monster == null || !monster.Alive || dps <= 0f || duration <= 0f) return;
            if (!monster.Burning) monster.BurnTimer = 0f;
            monster.BurnDps = MathF.Max(monster.BurnDps, dps);
            monster.BurnLeft = MathF.Max(monster.BurnLeft, duration);
        }

        // ---------------- 每步更新 ----------------

        public void Tick(float dt)
        {
            Compact();

            PlayerState player = _ctx.Player.State;
            Vec2 target = player.Position;

            // 1) 直线追踪
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                Vec2 to = target - m.Position;
                float dist = to.Magnitude;
                m.Velocity = dist > 0.05f ? to / dist * _config.Speed : Vec2.Zero;
                m.Position += m.Velocity * dt;
            }

            // 2) 互相挤开（数量在一两百以内，两两检测足够便宜）
            Separate();

            // 3) 遮挡物 + 统一边界（程序A 补）。没动也要过一次：切位面后站在新位面遮挡物里的怪会被推到外侧。
            //    这里只做“推出去 / 贴边滑动”，没有寻路：绕障碍属于怪物 AI，由程序B 决定要不要做。
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                Vec2 resolved = _ctx.Obstacles.ResolveMove(m.Position, m.Position, _config.Radius, ObstacleMask.Units);
                m.Position = _ctx.Bounds.Clamp(resolved, _config.Radius);
            }

            float interval = MathF.Max(0.02f, _env.DotTickInterval);
            float contactRange = _config.Radius + _ctx.Player.Config.Radius;

            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;

                // 4) 接触伤害（无敌判定在 CombatSystem 里）
                if (m.ContactTimer > 0f) m.ContactTimer -= dt;
                if (player.Alive && m.ContactTimer <= 0f &&
                    Vec2.SqrDistance(m.Position, target) <= contactRange * contactRange)
                {
                    m.ContactTimer = _config.ContactInterval;
                    _ctx.Combat.DamagePlayer(_config.ContactDamage, DamageSource.Monster);
                }

                // 5) 脚下地形（只记录；岩浆伤害由 EnvironmentSystem 在切图时一次性结算，浅水什么都不做）
                m.Terrain = _ctx.Terrain.Sample(m.Position);

                // 6) 灼烧（持续 Buff，跨地图保留）
                if (m.Burning)
                {
                    m.BurnLeft -= dt;
                    m.BurnTimer += dt;
                    while (m.BurnTimer >= interval && m.Alive)
                    {
                        m.BurnTimer -= interval;
                        _ctx.Combat.DamageMonster(m, m.BurnDps * interval, DamageSource.Burn);
                    }
                    if (m.BurnLeft <= 0f)
                    {
                        m.BurnLeft = 0f;
                        m.BurnDps = 0f;
                        m.BurnTimer = 0f;
                    }
                }
            }
        }

        private void Separate()
        {
            float minDist = _config.Radius * 2f;
            float minSq = minDist * minDist;
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity a = _active[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < _active.Count; j++)
                {
                    MonsterEntity b = _active[j];
                    if (!b.Alive) continue;
                    Vec2 d = a.Position - b.Position;
                    float sq = d.SqrMagnitude;
                    if (sq >= minSq) continue;

                    float dist = MathF.Sqrt(sq);
                    Vec2 dir = dist > 0.0001f ? d / dist : Vec2.FromAngle(a.Id * 2.399f); // 完全重合时按 Id 给固定方向
                    float push = MathF.Min(MaxSeparationStep, (minDist - dist) * 0.5f * SeparationStrength);
                    a.Position += dir * push;
                    b.Position -= dir * push;
                }
            }
        }

        /// <summary>把死亡的实体移出列表（交换删除，不保持顺序）。</summary>
        private void Compact()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
        }

        // ---------------- 空间查询 ----------------

        public void QueryCircle(Vec2 center, float radius, List<MonsterEntity> results)
        {
            results.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                float r = radius + _config.Radius;
                if (Vec2.SqrDistance(center, m.Position) <= r * r) results.Add(m);
            }
        }

        public MonsterEntity GetNearest(Vec2 from, float range)
        {
            MonsterEntity best = null;
            float bestSq = range * range;
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                float sq = Vec2.SqrDistance(from, m.Position);
                if (sq > bestSq) continue;
                bestSq = sq;
                best = m;
            }
            return best;
        }

        // ---------------- 翻滚第 6 步（程序B） ----------------

        public void ShiftAll(Vec2 offset)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                // 平移后落在新位面的遮挡物里就推到外侧（程序A 补）
                Vec2 shifted = _ctx.Obstacles.ResolveMove(m.Position, m.Position + offset, _config.Radius, ObstacleMask.Units);
                m.Position = _ctx.Bounds.Clamp(shifted, _config.Radius);
                m.Terrain = _ctx.Terrain.Sample(m.Position);
            }
        }

        public void SwapPlane(PlaneId from, PlaneId to)
        {
            if (PlaneIds.PairOf(from) != PlaneIds.PairOf(to)) return;
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive || m.Form != from) continue;
                float ratio = m.MaxHp > 0f ? MathF.Max(0f, MathF.Min(1f, m.Hp / m.MaxHp)) : 0f;
                m.Form = to;
                m.MaxHp = MaxHpFor(to);
                m.Hp = m.MaxHp * ratio;
                m.Velocity = Vec2.Zero; // 换形态中断当前移动；后续行为状态由各怪物行为实现。
                m.ContactTimer = 0f;
                m.Terrain = _ctx.Terrain.Sample(m.Position);
            }
        }

        private float MaxHpFor(PlaneId plane)
        {
            if (plane == PlaneId.Volcano) return MathF.Max(1f, _config.VolcanoMaxHp);
            if (plane == PlaneId.Ocean) return MathF.Max(1f, _config.OceanMaxHp);
            return MathF.Max(1f, _config.MaxHp);
        }
    }
}
