using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Monsters;

namespace Turn.Systems
{
    /// <summary>
    /// 怪物（新策划案 11/24：五种基础怪物，数值见 HordeConfig / MonsterCatalog.CreateHordeDefaults）。
    ///   · 游荡尸：直线追玩家，接触造成碰撞伤害；
    ///   · 弹幕孢：缓慢追玩家，每 3 秒朝玩家当前位置发射一枚弹幕（11/26 改：原来是向四周一圈 8 发）；
    ///   · 疾冲兽：远处正常移动，进入冲锋范围后原地蓄力（锁定方向），随后高速直线冲锋，路径上撞到玩家造成高额伤害；
    ///   · 毒腐体：近战追玩家；死亡时原地留下毒潭（站在里面持续掉血）；
    ///   · 狙击械：保持中远距离，锁定玩家方向蓄力，蓄力结束发射一道横穿全屏的穿透激光（不被遮挡物挡住）。
    /// 通用：每推进 1 波血量 +10%、伤害 +5%（从该怪物首次出现的波次算起）；精英 / Boss 再乘倍率。
    /// 接触伤害有单位时间上限：同一只怪每 AttackInterval 秒（游荡尸 / 毒腐体 0.5 秒）最多一次。
    /// 宝箱代价「狂怒 / 疾行」：怪物伤害 / 移速按层数提高。
    /// 位置 / 存活 / 计时 / 状态字段只有这里能写；生命由 CombatSystem 写；印记由 WeaponSystem 写。
    /// 亚空间里 SessionSystem 不调用 Tick：敌人停止移动、停止攻击，毒潭也暂停。
    /// </summary>
    public sealed class MonsterSystem : IMonsterSystem
    {
        public const float SeparationStrength = 0.5f;
        public const float MaxSeparationStep = 0.2f;

        private readonly ITurnContext _ctx;
        private readonly HordeConfig _horde;
        private readonly List<MonsterEntity> _active = new List<MonsterEntity>();
        private readonly List<PoisonPool> _pools = new List<PoisonPool>();
        private float _timeSlow = 1f;
        private int _nextId = 1;
        private int _nextPoolId = 1;
        private float _poolTimer;

        public IReadOnlyList<MonsterEntity> Active { get { return _active; } }
        public IReadOnlyList<PoisonPool> Pools { get { return _pools; } }
        public int AliveCount { get; private set; }

        public MonsterSystem(ITurnContext ctx, HordeConfig horde)
        {
            _ctx = ctx;
            _horde = horde ?? new HordeConfig();
            _horde.Kinds = MonsterCatalog.NormalizeHorde(_horde.Kinds);
        }

        private HordeKindConfig Stats(MonsterKind kind)
        {
            int i = MonsterKinds.HordeIndex(kind);
            HordeKindConfig[] kinds = _horde.Kinds;
            return i >= 0 && i < kinds.Length && kinds[i] != null ? kinds[i] : kinds[0];
        }

        private int Wave { get { return Math.Max(1, _ctx.Session.Wave); } }

        // ---------------- 生成 / 死亡 / 状态 ----------------

        public MonsterEntity Spawn(Vec2 position, bool penalty)
        {
            MonsterKind kind = MonsterSpawnPool.Pick(_horde, Wave, _ctx.Random);
            MonsterEntity m = SpawnKind(position, kind, 0);
            m.Penalty = penalty;
            return m;
        }

        public MonsterEntity SpawnKind(Vec2 position, MonsterKind kind, int rank)
        {
            if (MonsterKinds.HordeIndex(kind) < 0 || MonsterKinds.HordeIndex(kind) >= MonsterKinds.HordeCount) kind = MonsterKinds.First;
            HordeKindConfig k = Stats(kind);
            int waves = Math.Max(0, Wave - Math.Max(1, k.DebutWave));
            float hp = k.MaxHp * (1f + _horde.HpPerWave * waves);
            float damage = 1f + _horde.DamagePerWave * waves;
            float radius = MathF.Max(0.1f, _horde.Radius) * (k.Scale > 0f ? k.Scale : 1f); // 按种类的体型倍率
            int xp = Math.Max(0, k.Xp);
            if (rank == 1)
            {
                hp *= MathF.Max(1f, _horde.EliteHp);
                damage *= MathF.Max(1f, _horde.EliteDamage);
                radius *= MathF.Max(1f, _horde.EliteScale);
                xp *= Math.Max(1, _horde.EliteXp);
            }
            else if (rank >= 2)
            {
                rank = 2;
                hp *= MathF.Max(1f, _horde.BossHp) * MathF.Max(0f, _horde.BossHpMultiplier);
                damage *= MathF.Max(1f, _horde.BossDamage);
                radius *= MathF.Max(1f, _horde.BossScale);
                xp *= Math.Max(1, _horde.BossXp);
            }

            var m = new MonsterEntity();
            m.Id = _nextId++;
            m.Kind = kind;
            m.Rank = rank;
            m.Form = _ctx.Plane.Active;
            m.SpawnPlane = m.Form;
            m.Radius = radius;
            m.DamageScale = damage;
            m.MaxHp = MathF.Max(1f, hp);
            m.Hp = m.MaxHp;
            m.XpValue = xp;
            m.Position = _ctx.Bounds.Clamp(position, radius);
            m.Position = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(m.Position, m.Position, radius, ObstacleMask.Units), radius);
            m.Terrain = _ctx.Terrain.Sample(m.Position);
            m.Alive = true;
            // 远程怪错开第一次攻击，避免同时开火
            if (kind == MonsterKind.Spore) m.AiTimer = (float)_ctx.Random.NextDouble() * MathF.Max(0.1f, k.AttackInterval);
            else if (kind == MonsterKind.Sniper) m.AiTimer = MathF.Max(0.1f, k.AttackInterval) * (0.4f + 0.6f * (float)_ctx.Random.NextDouble());
            else if (kind == MonsterKind.Charger) m.AiTimer = 1f;
            _active.Add(m);
            AliveCount++;
            _ctx.Events.Publish(new MonsterSpawnedEvent { MonsterId = m.Id, Penalty = false, Rank = rank });
            return m;
        }

        public void Despawn(MonsterEntity monster)
        {
            if (monster == null || !monster.Alive) return;
            monster.Alive = false;
            AliveCount--;
            if (monster.Kind == MonsterKind.Rotter) CreatePool(monster);
        }

        /// <summary>毒腐体亡语：原地留下毒潭。</summary>
        private void CreatePool(MonsterEntity m)
        {
            float radius = MathF.Max(0.1f, _horde.PoolRadius) * (m.Rank > 0 ? MathF.Sqrt(m.Radius / MathF.Max(0.1f, _horde.Radius)) : 1f);
            var pool = new PoisonPool
            {
                Id = _nextPoolId++,
                Position = m.Position,
                Radius = radius,
                Duration = MathF.Max(0.1f, _horde.PoolDuration),
                TimeLeft = MathF.Max(0.1f, _horde.PoolDuration),
                Damage = _horde.PoolDamage * MathF.Max(1f, m.DamageScale),
            };
            _pools.Add(pool);
            _ctx.Events.Publish(new PoisonPoolCreatedEvent { PoolId = pool.Id, Position = pool.Position, Radius = pool.Radius, Duration = pool.Duration });
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

        public void ApplyStun(MonsterEntity monster, float duration)
        {
            if (monster == null || !monster.Alive || duration <= 0f) return;
            monster.StunLeft = MathF.Max(monster.StunLeft, duration);
        }

        public float Knockback(MonsterEntity monster, Vec2 direction, float distance)
        {
            if (monster == null || !monster.Alive || distance <= 0f || direction.SqrMagnitude < 0.0001f) return 0f;
            Vec2 dir = direction.Normalized;
            int steps = Math.Max(1, (int)MathF.Ceiling(distance / 0.25f));
            float step = distance / steps;
            Vec2 start = monster.Position;
            Vec2 p = start;
            for (int i = 0; i < steps; i++)
            {
                Vec2 next = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(p, p + dir * step, monster.Radius, ObstacleMask.Units), monster.Radius);
                // 被挡住（贴边滑动后在击退方向上几乎没前进）：停在这里
                if (((next.X - p.X) * dir.X + (next.Y - p.Y) * dir.Y) < step * 0.5f)
                {
                    p = next;
                    break;
                }
                p = next;
            }
            monster.Position = p;
            Vec2 moved = p - start;
            return MathF.Max(0f, moved.X * dir.X + moved.Y * dir.Y);
        }

        // ---------------- 每步更新 ----------------

        public void Tick(float dt)
        {
            Compact();
            PlayerState player = _ctx.Player.State;
            BuildConfig builds = _ctx.Config.Builds;
            // 时间沙漏：全场敌人移速倍率（其他副武器为 1）
            _timeSlow = _ctx.SubWeapons != null ? MathF.Max(0f, _ctx.SubWeapons.MonsterSpeedMultiplier) : 1f;
            float speedCurse = (1f + MathF.Max(0f, builds.HasteSpeed) * _ctx.Progression.CurseStacks(ChestCurse.Haste)) * _timeSlow;
            float damageCurse = 1f + MathF.Max(0f, builds.FuryDamage) * _ctx.Progression.CurseStacks(ChestCurse.Fury);

            int count = _active.Count;
            for (int i = 0; i < count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                HordeKindConfig k = Stats(m.Kind);
                TickStatus(m, dt);
                if (m.ContactTimer > 0f) m.ContactTimer = MathF.Max(0f, m.ContactTimer - dt);

                Vec2 to = player.Position - m.Position;
                float dist = to.Magnitude;
                Vec2 toward = dist > 0.05f ? to / dist : Vec2.Zero;
                float speed = MathF.Max(0f, k.Speed) * m.SlowFactor * speedCurse;
                if (m.Rank == 0) speed *= MathF.Max(0f, _horde.NormalChaseSpeedMultiplier);
                Vec2 velocity = toward * speed;

                if (m.StunLeft > 0f) velocity = Vec2.Zero;
                else if (player.Alive)
                {
                    switch (m.Kind)
                    {
                        case MonsterKind.Spore: TickSpore(m, k, damageCurse, dt); break;
                        case MonsterKind.Charger: velocity = TickCharger(m, k, player, toward, dist, velocity, damageCurse, dt); break;
                        case MonsterKind.Sniper: velocity = TickSniper(m, k, player, toward, dist, speed, damageCurse, dt); break;
                    }
                }

                m.Velocity = velocity;
                Vec2 desired = m.Position + velocity * dt;
                Vec2 resolved = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(m.Position, desired, m.Radius, ObstacleMask.Units), m.Radius);
                if (m.Kind == MonsterKind.Charger && m.AiPhase == 2)
                {
                    m.ChargeTravelled += Vec2.Distance(m.Position, resolved);
                    // 撞墙 / 撞边界或走满距离：冲锋结束
                    if (Vec2.SqrDistance(resolved, desired) > 0.0004f || m.ChargeTravelled >= _horde.ChargeMaxDistance) EndCharge(m, k);
                }
                m.Position = resolved;
            }

            Separate();
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                m.Position = _ctx.Bounds.Clamp(_ctx.Obstacles.ResolveMove(m.Position, m.Position, m.Radius, ObstacleMask.Units), m.Radius);
                m.Terrain = _ctx.Terrain.Sample(m.Position);
            }

            // 接触伤害（游荡尸 / 毒腐体）：每只怪每 AttackInterval 秒最多一次
            if (player.Alive)
            {
                float playerRadius = _ctx.Player.Config.Radius;
                for (int i = 0; i < _active.Count; i++)
                {
                    MonsterEntity m = _active[i];
                    if (!m.Alive || m.StunLeft > 0f || m.ContactTimer > 0f) continue;
                    if (m.Kind != MonsterKind.Walker && m.Kind != MonsterKind.Rotter) continue;
                    float reach = m.Radius + playerRadius;
                    if (Vec2.SqrDistance(m.Position, player.Position) > reach * reach) continue;
                    HordeKindConfig k = Stats(m.Kind);
                    m.ContactTimer = MathF.Max(0.1f, k.AttackInterval);
                    _ctx.Combat.DamagePlayer(k.Damage * m.DamageScale * damageCurse, DamageSource.Monster);
                    if (!player.Alive) break;
                }
            }

            TickPools(dt, player, damageCurse);
            TickBurn(dt);
        }

        private static void TickStatus(MonsterEntity m, float dt)
        {
            if (m.SlowLeft > 0f)
            {
                m.SlowLeft = MathF.Max(0f, m.SlowLeft - dt);
                if (m.SlowLeft <= 0f) m.SlowFactor = 1f;
            }
            if (m.StunLeft > 0f) m.StunLeft = MathF.Max(0f, m.StunLeft - dt);
        }

        /// <summary>
        /// 弹幕孢：每 AttackInterval 秒朝玩家当前位置发射一枚弹幕（直线飞行，不追踪）。
        /// 精英 / Boss 也只发一枚（伤害 / 体型按倍率）。玩家和它完全重合时随机一个方向。
        /// </summary>
        private void TickSpore(MonsterEntity m, HordeKindConfig k, float damageCurse, float dt)
        {
            m.AiTimer -= dt;
            if (m.AiTimer > 0f) return;
            m.AiTimer = MathF.Max(0.1f, k.AttackInterval);
            Vec2 to = _ctx.Player.State.Position - m.Position;
            Vec2 dir = to.SqrMagnitude > 0.0001f ? to.Normalized : Vec2.FromAngle((float)_ctx.Random.NextDouble() * MathF.PI * 2f);
            float speed = MathF.Max(0.1f, _horde.SporeBulletSpeed);
            float life = MathF.Max(0.1f, _horde.SporeBulletLifetime);
            _ctx.Projectiles.SpawnEnemy(m.Position + dir * m.Radius, dir * speed, _horde.SporeBulletRadius,
                k.Damage * m.DamageScale * damageCurse, life, speed * life, EnemyProjectileKind.Bubble);
        }

        /// <summary>疾冲兽：0 追击（冷却）→ 1 蓄力（锁定方向，不动）→ 2 冲锋（一次冲锋最多撞一次）。</summary>
        private Vec2 TickCharger(MonsterEntity m, HordeKindConfig k, PlayerState player, Vec2 toward, float dist, Vec2 chase, float damageCurse, float dt)
        {
            if (m.AiPhase == 0)
            {
                if (m.AiTimer > 0f) m.AiTimer -= dt;
                if (m.AiTimer <= 0f && dist <= k.Range + m.Radius)
                {
                    m.AiPhase = 1;
                    m.AiTimer = MathF.Max(0.01f, k.Windup);
                    m.AttackDirection = toward.SqrMagnitude > 0.5f ? toward : Vec2.Right;
                    return Vec2.Zero;
                }
                return chase;
            }
            if (m.AiPhase == 1)
            {
                m.AiTimer -= dt;
                if (m.AiTimer > 0f) return Vec2.Zero;
                m.AiPhase = 2;
                m.ChargeTravelled = 0f;
                m.ContactTimer = 0f;
                m.TargetPosition = Vec2.Zero; // X 用作「本次冲锋已撞过」的标记
            }
            // 冲锋
            Vec2 velocity = m.AttackDirection * MathF.Max(0f, _horde.ChargeSpeed) * m.SlowFactor * _timeSlow;
            float reach = m.Radius + _ctx.Player.Config.Radius;
            if (m.TargetPosition.X < 0.5f && Vec2.SqrDistance(m.Position, player.Position) <= reach * reach)
            {
                m.TargetPosition = new Vec2(1f, 0f);
                _ctx.Combat.DamagePlayer(k.Damage * m.DamageScale * damageCurse, DamageSource.Monster);
            }
            return velocity;
        }

        private static void EndCharge(MonsterEntity m, HordeKindConfig k)
        {
            m.AiPhase = 0;
            m.AiTimer = MathF.Max(0.1f, k.AttackInterval);
            m.ChargeTravelled = 0f;
        }

        /// <summary>狙击械：保持 [SniperMinRange, SniperMaxRange] 的距离；冷却好了就锁定方向蓄力，蓄力结束发射全屏激光。</summary>
        private Vec2 TickSniper(MonsterEntity m, HordeKindConfig k, PlayerState player, Vec2 toward, float dist, float speed, float damageCurse, float dt)
        {
            if (m.AiPhase == 1)
            {
                m.AiTimer -= dt;
                if (m.AiTimer > 0f) return Vec2.Zero;
                FireLaser(m, k, player, damageCurse);
                m.AiPhase = 0;
                m.AiTimer = MathF.Max(0.1f, k.AttackInterval);
                return Vec2.Zero;
            }

            m.AiTimer -= dt;
            if (m.AiTimer <= 0f && dist <= MathF.Max(k.Range, _horde.SniperMaxRange) + 1f)
            {
                m.AiPhase = 1;
                m.AiTimer = MathF.Max(0.01f, k.Windup);
                m.AttackDirection = toward.SqrMagnitude > 0.5f ? toward : Vec2.Right;
                _ctx.Events.Publish(new LaserAttackWarnedEvent
                {
                    Origin = m.Position, Direction = m.AttackDirection, Plane = _ctx.Plane.Active,
                    WarningSeconds = m.AiTimer, Range = LaserLength,
                });
                return Vec2.Zero;
            }
            if (dist < _horde.SniperMinRange) return toward * -speed;
            if (dist > _horde.SniperMaxRange) return toward * speed;
            // 距离合适：绕着玩家慢慢横移
            return new Vec2(-toward.Y, toward.X) * (speed * 0.35f * ((m.Id & 1) == 0 ? 1f : -1f));
        }

        /// <summary>激光长度：边界对角线（= 横穿全屏）。</summary>
        private float LaserLength
        {
            get
            {
                Vec2 size = _ctx.Bounds.Rect.Size;
                return MathF.Sqrt(size.X * size.X + size.Y * size.Y);
            }
        }

        private void FireLaser(MonsterEntity m, HordeKindConfig k, PlayerState player, float damageCurse)
        {
            float length = LaserLength;
            Vec2 end = m.Position + m.AttackDirection * length;
            float reach = MathF.Max(0f, _horde.LaserWidth) * MathF.Max(1f, m.Radius / MathF.Max(0.1f, _horde.Radius)) + _ctx.Player.Config.Radius;
            bool hit = player.Alive && SegmentSqrDistance(player.Position, m.Position, end) <= reach * reach;
            if (hit) _ctx.Combat.DamagePlayer(k.Damage * m.DamageScale * damageCurse, DamageSource.Laser);
            _ctx.Events.Publish(new LaserFiredEvent { Origin = m.Position, Direction = m.AttackDirection, Range = length, Width = reach - _ctx.Player.Config.Radius, HitPlayer = hit });
        }

        /// <summary>毒潭：按 PoolInterval 结算；玩家站在任意一个毒潭里就掉一次血（多个毒潭不叠加，取最大）。</summary>
        private void TickPools(float dt, PlayerState player, float damageCurse)
        {
            for (int i = _pools.Count - 1; i >= 0; i--)
            {
                _pools[i].TimeLeft -= dt;
                if (_pools[i].TimeLeft <= 0f) _pools.RemoveAt(i);
            }
            if (_pools.Count == 0)
            {
                _poolTimer = 0f;
                return;
            }
            float interval = MathF.Max(0.05f, _horde.PoolInterval);
            _poolTimer += dt;
            while (_poolTimer >= interval)
            {
                _poolTimer -= interval;
                if (!player.Alive) return;
                float damage = 0f;
                for (int i = 0; i < _pools.Count; i++)
                {
                    PoisonPool pool = _pools[i];
                    float r = pool.Radius + _ctx.Player.Config.Radius * 0.5f;
                    if (Vec2.SqrDistance(pool.Position, player.Position) <= r * r) damage = MathF.Max(damage, pool.Damage);
                }
                if (damage > 0f) _ctx.Combat.DamagePlayer(damage * damageCurse, DamageSource.Poison);
            }
        }

        private void TickBurn(float dt)
        {
            float interval = MathF.Max(0.02f, _ctx.Config.Environment.DotTickInterval);
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive || !m.Burning) continue;
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
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity a = _active[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < _active.Count; j++)
                {
                    MonsterEntity b = _active[j];
                    if (!b.Alive) continue;
                    float minDist = a.Radius + b.Radius;
                    Vec2 d = a.Position - b.Position;
                    float sq = d.SqrMagnitude;
                    if (sq >= minDist * minDist) continue;

                    float dist = MathF.Sqrt(sq);
                    Vec2 dir = dist > 0.0001f ? d / dist : Vec2.FromAngle(a.Id * 2.399f); // 完全重合时按 Id 给固定方向
                    float push = MathF.Min(MaxSeparationStep, (minDist - dist) * 0.5f * SeparationStrength);
                    // 大体型（精英 / Boss）推小怪更多、自己被推得更少
                    float total = a.Radius + b.Radius;
                    a.Position += dir * (push * 2f * b.Radius / total);
                    b.Position -= dir * (push * 2f * a.Radius / total);
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
                float r = radius + m.Radius;
                if (Vec2.SqrDistance(center, m.Position) <= r * r) results.Add(m);
            }
        }

        public void QuerySegment(Vec2 a, Vec2 b, float halfWidth, List<MonsterEntity> results)
        {
            results.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                MonsterEntity m = _active[i];
                if (!m.Alive) continue;
                float r = halfWidth + m.Radius;
                if (SegmentSqrDistance(m.Position, a, b) <= r * r) results.Add(m);
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
    }
}
