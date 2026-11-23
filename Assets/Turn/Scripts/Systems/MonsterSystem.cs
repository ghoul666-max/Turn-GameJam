using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Monsters;

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
        private static readonly MonsterArchetype[] DefaultStats = MonsterCatalog.CreateDefaults();
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
            MonsterKind kind = MonsterSpawnPool.Pick(_ctx.Plane.Active, _ctx.Config.Spawn,
                _ctx.Random, _ctx.Session.Time);
            return SpawnKind(position, penalty, kind, 0);
        }

        private MonsterEntity SpawnKind(Vec2 position, bool penalty, MonsterKind kind, int parentId)
        {
            var m = new MonsterEntity();
            m.Id = _nextId++;
            m.Kind = kind;
            m.Form = _ctx.Plane.Active;
            m.SpawnPlane = m.Form;
            m.ParentId = parentId;
            m.SplitTimer = MathF.Max(.01f, _config.SplitInterval);
            m.Position = _ctx.Bounds.Clamp(position, _config.Radius);
            m.Position = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(m.Position, m.Position, _config.Radius, ObstacleMask.Units), _config.Radius);
            MonsterArchetype stats = Stats(m);
            m.MaxHp = MaxHpFor(m.Form, stats);
            m.Hp = m.MaxHp;
            m.XpValue = stats.Xp;
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

        public void ApplySlow(MonsterEntity monster, float factor, float duration)
        {
            if (monster == null || !monster.Alive || duration <= 0f) return;
            monster.SlowFactor = MathF.Min(monster.SlowFactor, MathF.Max(0f, MathF.Min(1f, factor)));
            monster.SlowLeft = MathF.Max(monster.SlowLeft, duration);
        }

        // ---------------- 每步更新 ----------------

        public void Tick(float dt)
        {
            Compact();

            PlayerState player = _ctx.Player.State;
            Vec2 target = player.Position;

            // 1) 按形态寻敌 / 站桩 / 冲锋；存活列表可能在攻击阶段产生虫崽，故计数快照。
            int originalCount = _active.Count;
            for (int i = 0; i < originalCount; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                MonsterArchetype stats = Stats(m);
                if (m.SlowLeft > 0f)
                {
                    m.SlowLeft = MathF.Max(0f, m.SlowLeft - dt);
                    if (m.SlowLeft <= 0f) m.SlowFactor = 1f;
                }
                Vec2 to = target - m.Position;
                float dist = to.Magnitude;
                m.Velocity = dist > .05f && stats.Speed > 0f ? to / dist * stats.Speed * m.SlowFactor : Vec2.Zero;
                if (m.AiPhase == 2 && IsCharger(m.Kind))
                    m.Velocity = m.AttackDirection * ChargeSpeed(m.Kind) * m.SlowFactor;
                if (m.AiPhase == 2 && m.Kind == MonsterKind.StoneGolem)
                    m.Velocity = Vec2.Zero;
                if (m.StunLeft > 0f || m.AiPhase == 1 || (m.AiPhase == 5 && m.Kind == MonsterKind.FutureSoldier) || m.Kind == MonsterKind.Alarm || m.Kind == MonsterKind.VineDemon)
                    m.Velocity = Vec2.Zero;
                Vec2 desired = m.Position + m.Velocity * dt;
                Vec2 resolved = _ctx.Obstacles.ResolveMove(m.Position, desired, _config.Radius, ObstacleMask.Units);
                m.Position = _ctx.Bounds.Clamp(resolved, _config.Radius);
                if (m.AiPhase == 2 && IsCharger(m.Kind) && Vec2.SqrDistance(m.Position, desired) > .01f)
                {
                    m.AiPhase = 0;
                    m.AiTimer = 0f;
                    if (m.Kind == MonsterKind.Boar) m.StunLeft = _config.BoarStunDuration;
                }
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

            int attackCount = _active.Count;
            for (int i = 0; i < attackCount; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                MonsterArchetype stats = Stats(m);
                if (m.StunLeft > 0f) m.StunLeft = MathF.Max(0f, m.StunLeft - dt);
                if (m.Kind == MonsterKind.NullPointer) TickSplit(m, dt);
                TickAttack(m, stats, player, contactRange, dt);

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

        private MonsterArchetype Stats(MonsterEntity m)
        {
            MonsterArchetype[] all = _config.Archetypes;
            int index = (int)m.Kind;
            MonsterArchetype stats = all != null && index >= 0 && index < all.Length ? all[index] : null;
            return stats ?? DefaultStats[index];
        }

        private static bool IsCharger(MonsterKind kind)
        {
            return kind == MonsterKind.BlackPot || kind == MonsterKind.Boar || kind == MonsterKind.RepairRobot || kind == MonsterKind.DataWorm;
        }

        private float ChargeSpeed(MonsterKind kind)
        {
            if (kind == MonsterKind.RepairRobot) return _config.RobotChargeSpeed;
            if (kind == MonsterKind.Boar) return _config.BoarChargeSpeed;
            if (kind == MonsterKind.DataWorm) return StatsFor(kind).Speed;
            return _config.ChargeSpeed;
        }

        private MonsterArchetype StatsFor(MonsterKind kind)
        {
            var all = _config.Archetypes;
            int index = (int)kind;
            return all != null && index >= 0 && index < all.Length && all[index] != null
                ? all[index] : DefaultStats[index];
        }

        private void TickSplit(MonsterEntity parent, float dt)
        {
            parent.SplitTimer -= dt;
            if (parent.SplitTimer > 0f) return;
            parent.SplitTimer += MathF.Max(.1f, _config.SplitInterval);
            int children = 0;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Alive && _active[i].Kind == MonsterKind.Buglet && _active[i].ParentId == parent.Id) children++;
            if (children >= _config.BugletLimitPerParent) return;
            Vec2 delta = Vec2.FromAngle((float)(_ctx.Random.NextDouble() * Math.PI * 2));
            SpawnKind(parent.Position + delta, false, MonsterKind.Buglet, parent.Id);
        }

        private void TickAttack(MonsterEntity m, MonsterArchetype stats, PlayerState player, float contactRange, float dt)
        {
            if (!player.Alive || m.StunLeft > 0f) return;
            if (m.ContactTimer > 0f) m.ContactTimer = MathF.Max(0f, m.ContactTimer - dt);
            float dist = Vec2.Distance(m.Position, player.Position);
            if (m.Kind == MonsterKind.Alarm)
            {
                if (dist <= _config.AlarmAuraRadius + _config.Radius)
                    _ctx.Combat.DamagePlayer(stats.Damage * dt, DamageSource.Monster);
                return;
            }
            if (m.Kind == MonsterKind.NullPointer && dist <= contactRange && m.ContactTimer <= 0f)
            {
                m.ContactTimer = MathF.Max(.1f, stats.AttackInterval);
                _ctx.Combat.DamagePlayer(stats.Damage, DamageSource.Monster);
            }
            if (m.AiPhase == 2)
            {
                m.AiTimer -= dt;
                if (IsCharger(m.Kind) && dist <= contactRange && m.ContactTimer <= 0f)
                {
                    m.ContactTimer = MathF.Max(.1f, stats.AttackInterval);
                    bool damaged = _ctx.Combat.DamagePlayer(stats.Damage, DamageSource.Monster);
                    if (damaged && m.Kind == MonsterKind.RepairRobot)
                    {
                        Vec2 away = (player.Position - m.Position).Normalized;
                        _ctx.Events.Publish(new RobotKnockbackHitEvent
                        {
                            MonsterId = m.Id, Plane = m.Form,
                            Direction = away.SqrMagnitude > .01f ? away : m.AttackDirection,
                            Distance = MathF.Max(0f, _config.RobotKnockbackDistance),
                        });
                    }
                    if (m.Kind == MonsterKind.DataWorm) Despawn(m);
                }
                if (m.AiTimer <= 0f)
                {
                    if (m.Kind == MonsterKind.DataWorm && m.Alive)
                    {
                        if (dist <= _config.DataWormExplosionRadius) _ctx.Combat.DamagePlayer(stats.Damage, DamageSource.Monster);
                        Despawn(m); // 一次性自爆；不会产经验（设计待确认）。
                        return;
                    }
                    m.AiPhase = 0;
                    m.AiTimer = MathF.Max(0f, stats.AttackInterval - stats.Windup);
                }
                return;
            }
            if (m.AiPhase == 5 && m.Kind == MonsterKind.FutureSoldier)
            {
                float active = MathF.Min(dt, MathF.Max(0f, m.AiTimer));
                m.AiTimer -= dt;
                if (active > 0f && m.Form == _ctx.Plane.Active && player.Alive)
                {
                    Vec2 ray = m.AttackDirection.SqrMagnitude > .01f ? m.AttackDirection : Vec2.Right;
                    Vec2 end = m.Position + ray * MathF.Max(0f, stats.Range);
                    float width = MathF.Max(0f, _config.LaserWidth) + _ctx.Player.Config.Radius;
                    if (SegmentSqrDistance(player.Position, m.Position, end) <= width * width &&
                        !_ctx.Obstacles.SegmentBlocked(m.Position, player.Position, 0f, ObstacleMask.Projectiles))
                        _ctx.Combat.DamagePlayer(stats.Damage * active, DamageSource.Monster);
                }
                if (m.AiTimer <= 0f)
                {
                    m.AiPhase = 0;
                    m.AiTimer = MathF.Max(0f, stats.AttackInterval - stats.Windup);
                }
                return;
            }
            if (m.AiPhase == 4)
            {
                m.AiTimer -= dt;
                if (m.AiTimer > 0f) return;
                float radius = MathF.Max(0f, _config.RockImpactRadius) + _ctx.Player.Config.Radius;
                if (m.Form == _ctx.Plane.Active && player.Alive &&
                    Vec2.SqrDistance(player.Position, m.TargetPosition) <= radius * radius)
                    _ctx.Combat.DamagePlayer(stats.Damage, DamageSource.Monster);
                m.AiPhase = 0;
                m.AiTimer = MathF.Max(0f, stats.AttackInterval - stats.Windup);
                return;
            }
            if (m.AiPhase == 3)
            {
                m.AiTimer -= dt;
                if (m.AiTimer > 0f) return;
                FireAttack(m, stats, player, contactRange);
                m.BurstShotsLeft--;
                if (m.BurstShotsLeft <= 0) { m.AiPhase = 0; m.AiTimer = stats.AttackInterval; }
                else m.AiTimer = MathF.Max(.01f, _config.CodeShotInterval);
                return;
            }
            if (m.AiPhase == 1)
            {
                m.AiTimer -= dt;
                if (m.AiTimer > 0f) return;
                if (m.Kind == MonsterKind.Bug)
                {
                    m.AiPhase = 3;
                    m.AiTimer = MathF.Max(.01f, _config.CodeShotInterval);
                    m.BurstShotsLeft = Math.Max(1, _config.CodeBurstShots) - 1;
                    FireAttack(m, stats, player, contactRange);
                    if (m.BurstShotsLeft <= 0) { m.AiPhase = 0; m.AiTimer = stats.AttackInterval; }
                }
                else if (m.Kind == MonsterKind.RockThrower)
                {
                    m.TargetPosition = player.Position;
                    m.AiPhase = 4;
                    m.AiTimer = MathF.Max(0f, _config.RockWarning);
                    _ctx.Events.Publish(new RockImpactWarnedEvent
                    {
                        Position = m.TargetPosition, Plane = m.Form, WarningSeconds = m.AiTimer,
                    });
                }
                else if (m.Kind == MonsterKind.FutureSoldier)
                {
                    m.AiPhase = 5;
                    m.AiTimer = MathF.Max(.01f, _config.LaserDuration);
                }
                else
                {
                    m.AiPhase = IsCharger(m.Kind) || m.Kind == MonsterKind.StoneGolem ? 2 : 0;
                    m.AiTimer = m.AiPhase == 2 ? (m.Kind == MonsterKind.StoneGolem ? _config.StoneRecovery : _config.ChargeDuration) : stats.AttackInterval;
                    FireAttack(m, stats, player, contactRange);
                }
                return;
            }
            m.AiTimer -= dt;
            if (m.AiTimer > 0f || dist > stats.Range + _config.Radius) return;
            m.AiPhase = 1;
            m.AiTimer = MathF.Max(0.01f, m.Kind == MonsterKind.FutureSoldier ? _config.LaserWarning : stats.Windup);
            m.AttackDirection = (player.Position - m.Position).Normalized;
            if (m.Kind == MonsterKind.FutureSoldier)
                _ctx.Events.Publish(new LaserAttackWarnedEvent
                {
                    Origin = m.Position, Direction = m.AttackDirection,
                    Plane = m.Form, WarningSeconds = m.AiTimer, Range = stats.Range,
                });
        }

        private void FireAttack(MonsterEntity m, MonsterArchetype stats, PlayerState player, float contactRange)
        {
            Vec2 direction = m.AttackDirection.SqrMagnitude > .01f ? m.AttackDirection : (player.Position - m.Position).Normalized;
            if (m.Kind == MonsterKind.StoneGolem || m.Kind == MonsterKind.NullPointer || m.Kind == MonsterKind.Buglet || m.Kind == MonsterKind.VineDemon)
            {
                if (Vec2.Distance(m.Position, player.Position) <= MathF.Max(contactRange, stats.Range))
                    _ctx.Combat.DamagePlayer(stats.Damage, DamageSource.Monster);
                return;
            }
            if (IsCharger(m.Kind) || m.Kind == MonsterKind.RockThrower || m.Kind == MonsterKind.FutureSoldier) return;
            EnemyProjectileKind kind = m.Kind == MonsterKind.Anglerfish ? EnemyProjectileKind.Bubble
                : m.Kind == MonsterKind.Bug ? EnemyProjectileKind.Code
                : m.Kind == MonsterKind.RockThrower ? EnemyProjectileKind.Rock : EnemyProjectileKind.Laser;
            float speed = m.Kind == MonsterKind.Anglerfish ? _config.BubbleSpeed : MathF.Max(1f, stats.Speed * 2f);
            _ctx.Projectiles.SpawnEnemy(m.Form, m.Position, direction * speed, _config.EnemyProjectileRadius,
                stats.Damage, _config.EnemyProjectileLifetime, stats.Range, kind);
        }

        private static float SegmentSqrDistance(Vec2 point, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float lengthSq = ab.SqrMagnitude;
            if (lengthSq <= 0.000001f) return Vec2.SqrDistance(point, a);
            Vec2 ap = point - a;
            float t = MathF.Max(0f, MathF.Min(1f, (ap.X * ab.X + ap.Y * ab.Y) / lengthSq));
            return Vec2.SqrDistance(point, a + ab * t);
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
                if (m.Kind == MonsterKind.Buglet && to != PlaneId.Office)
                {
                    Despawn(m); // 只属于工位；不能映射到闹钟。
                    continue;
                }
                float ratio = m.MaxHp > 0f ? MathF.Max(0f, MathF.Min(1f, m.Hp / m.MaxHp)) : 0f;
                m.Kind = MonsterCatalog.ForPlane(m.Kind, to);
                m.Form = to;
                MonsterArchetype stats = Stats(m);
                m.MaxHp = MaxHpFor(to, stats);
                m.Hp = m.MaxHp * ratio;
                m.XpValue = stats.Xp;
                m.Velocity = Vec2.Zero;
                m.AiPhase = 0;
                m.AiTimer = 0f;
                m.TargetPosition = Vec2.Zero;
                m.ContactTimer = 0f;
                m.Terrain = _ctx.Terrain.Sample(m.Position);
            }
        }

        private float MaxHpFor(PlaneId plane, MonsterArchetype stats)
        {
            if (plane == PlaneId.Volcano) return MathF.Max(1f, _config.VolcanoMaxHp);
            if (plane == PlaneId.Ocean) return MathF.Max(1f, _config.OceanMaxHp);
            return MathF.Max(1f, stats.MaxHp);
        }
    }
}
