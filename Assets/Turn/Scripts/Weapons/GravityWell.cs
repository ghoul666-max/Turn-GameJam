using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 引力黑洞与磁铁的统一行为。黑洞在当前位面永久存在；后续翻滚产生的黑洞
    /// 与旧洞向固定中点移动，碰撞爆炸后留下一个洞。合并期间的新起点按 FIFO 排队。
    /// MagnetSubWeapon 仅为旧存档 / 枚举的兼容别名，不应作为另一项独立技能装备。
    /// </summary>
    public class GravityWellSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _holes = new List<Vec2>(2);
        private readonly Queue<Vec2> _pending = new Queue<Vec2>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private Vec2 _pairFrom;
        private Vec2 _pairTo;
        private Vec2 _midpoint;

        public GravityWellSubWeapon(WeaponKit kit) : base(kit)
        {
            // 亚空间退出是每次翻滚的收尾，不是离开战斗位面，不能在此清掉永久洞。
            Kit.Ctx.Events.Subscribe<PlaneSwitchedEvent>(_ => Clear());
            Kit.Ctx.Events.Subscribe<RunStartedEvent>(_ => Clear());
            Kit.Ctx.Events.Subscribe<LevelClearedEvent>(_ => Clear());
            Kit.Ctx.Events.Subscribe<PlayerDiedEvent>(_ => Clear());
        }

        private GravityWellConfig Cfg { get { return Kit.Arsenal.Gravity; } }
        private MagnetConfig Magnet { get { return Kit.Arsenal.Magnet; } }
        private float Radius { get { return MathF.Max(0.5f, Cfg.Radius * (1f + Kit.Value(BuildId.GravityRadius))); } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.GravityWell; } }
        public override IReadOnlyList<Vec2> Anchors { get { return _holes; } }
        public override int UnitCount { get { return _holes.Count; } }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            // 尚未参与合并的起点不提前产生作用，保证最多两个活动洞。
            if (_holes.Count == 2) _pending.Enqueue(rollOrigin);
            else Spawn(rollOrigin);
        }

        private void Spawn(Vec2 origin)
        {
            bool first = _holes.Count == 0;
            _holes.Add(origin);
            Kit.Ctx.Events.Publish(new MagnetPoleEvent { Position = origin, Positive = first });
            Kit.Ctx.Events.Publish(new GravityPulledEvent { Center = origin, Radius = Radius, Pulled = 0, Secondary = false });
            ApplySpawnBuilds(origin);
            if (_holes.Count == 2)
            {
                _pairFrom = _holes[0];
                _pairTo = _holes[1];
                _midpoint = (_pairFrom + _pairTo) * 0.5f;
            }
        }

        public override void Tick(float dt)
        {
            if (dt <= 0f || _holes.Count == 0) return;
            float remaining = dt;
            // 消耗剩余时间以保证大时间步也不会跳过碰撞，或丢失排队的生成。
            while (remaining > 0f && _holes.Count > 0)
            {
                if (_holes.Count == 1)
                {
                    AffectArea(_holes[0], remaining);
                    break;
                }
                float speed = MathF.Max(0.01f, Cfg.MergeSpeed);
                float travel = MathF.Max(Vec2.Distance(_holes[0], _midpoint), Vec2.Distance(_holes[1], _midpoint)) / speed;
                float step = MathF.Min(remaining, travel);
                if (step > 0f)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        _holes[i] = MoveToward(_holes[i], _midpoint, speed * step);
                        AffectArea(_holes[i], step);
                    }
                    remaining -= step;
                }
                if (step < travel) break;
                Collide();
                if (_pending.Count > 0) Spawn(_pending.Dequeue());
            }
        }

        private static Vec2 MoveToward(Vec2 from, Vec2 to, float distance)
        {
            Vec2 delta = to - from;
            float length = delta.Magnitude;
            return length <= distance ? to : from + delta / length * distance;
        }

        private void AffectArea(Vec2 center, float dt)
        {
            float pull = MathF.Max(0f, Cfg.PullDistance * (1f + Kit.Value(BuildId.GravityPull))) * dt;
            float damage = MathF.Max(0f, Cfg.DamagePerSecond) * dt;
            Kit.Ctx.Monsters.QueryCircle(center, Radius, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive) continue;
                PullToward(m, center, pull);
                if (damage > 0f) Kit.Hit(m, damage, DamageSource.SubWeapon);
            }
            _query.Clear();
        }

        private void PullToward(MonsterEntity m, Vec2 center, float distance)
        {
            if (!m.Alive || (!Kit.Has(BuildId.GravityPullCharging) && !WeaponKit.CanDisplace(m))) return;
            float move = MathF.Min(distance, Vec2.Distance(m.Position, center) - MathF.Max(0f, Cfg.MinGap));
            if (move <= 0f) return;
            if (Kit.Ctx.Monsters.Knockback(m, WeaponKit.Direction(m.Position, center), move) > 0f)
                Kit.Ctx.Events.Publish(new GravityDisplacedEvent { Moved = 1 });
        }

        // 保留已有机制词条，但只在生成时触发一次，不把原本一次性的百分比伤害变成每帧伤害。
        private void ApplySpawnBuilds(Vec2 center)
        {
            float damageRatio = Kit.Value(BuildId.GravityPullDamage);
            float vulnerable = Kit.Value(BuildId.GravityVulnerable);
            float spawnPull = Kit.Value(BuildId.MagnetSpawnPull);
            Kit.Ctx.Monsters.QueryCircle(center, Radius, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive) continue;
                if (damageRatio > 0f) Kit.Hit(m, m.MaxHp * damageRatio, DamageSource.SubWeapon, percentageDamage: true);
                if (vulnerable > 0f && m.Alive) m.BurstVulnerability = MathF.Max(m.BurstVulnerability, vulnerable);
            }
            _query.Clear();
            if (spawnPull > 0f) PullArea(center, MathF.Max(0.3f, Magnet.SpawnPullRadius), spawnPull, false);
            int secondary = Math.Max(0, (int)MathF.Round(Kit.Value(BuildId.GravitySecondary)));
            float angle = secondary > 0 ? (float)Kit.Ctx.Random.NextDouble() * MathF.PI * 2f : 0f;
            for (int i = 0; i < secondary; i++)
            {
                Vec2 point = Kit.Ctx.Bounds.Clamp(center + Vec2.FromAngle(angle + i * MathF.PI * 2f / secondary) * Cfg.SecondaryOffset, 0.5f);
                PullArea(point, Cfg.SecondaryRadius, Cfg.SecondaryPull, true);
            }
            if (Kit.Has(BuildId.GravityBlast))
            {
                float radius = MathF.Max(0.3f, Kit.Value2(BuildId.GravityBlast));
                Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
                int hits = 0;
                for (int i = 0; i < _query.Count; i++)
                {
                    if (!_query[i].Alive) continue;
                    Kit.Hit(_query[i], Kit.Value(BuildId.GravityBlast), DamageSource.SubWeapon);
                    hits++;
                }
                _query.Clear();
                Kit.Ctx.Events.Publish(new GravityBlastEvent { Center = center, Radius = radius, Hits = hits });
            }
        }

        private void PullArea(Vec2 center, float radius, float distance, bool secondary)
        {
            Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
            int pulled = 0;
            for (int i = 0; i < _query.Count; i++)
            {
                if (!_query[i].Alive) continue;
                PullToward(_query[i], center, distance);
                pulled++;
            }
            _query.Clear();
            Kit.Ctx.Events.Publish(new GravityPulledEvent { Center = center, Radius = radius, Pulled = pulled, Secondary = secondary });
        }

        private void Collide()
        {
            bool throughWalls = Kit.Has(BuildId.MagnetThroughWalls);
            float pathPull = Kit.Value(BuildId.MagnetPathPull);
            if (pathPull > 0f)
            {
                Kit.Ctx.Monsters.QuerySegment(_pairFrom, _pairTo, MathF.Max(0.3f, Magnet.PathPullRadius), _query);
                for (int i = 0; i < _query.Count; i++) PullToward(_query[i], _midpoint, pathPull);
                _query.Clear();
            }
            float width = MathF.Max(0.05f, Magnet.PathWidth * (1f + Kit.Value(BuildId.MagnetPathWidth)));
            float pathDamage = Magnet.PathDamage * (1f + Kit.Value(BuildId.MagnetPathDamage));
            int pathHits = 0;
            Kit.Ctx.Monsters.QuerySegment(_pairFrom, _pairTo, width, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive || (!throughWalls && Blocked(_pairFrom, _pairTo, m.Position))) continue;
                Kit.Hit(m, pathDamage, DamageSource.SubWeapon);
                pathHits++;
            }
            _query.Clear();
            float radius = MathF.Max(0.3f, Magnet.BlastRadius * (1f + Kit.Value(BuildId.MagnetBlastArea)));
            float damage = Magnet.BlastDamage * (1f + Kit.Value(BuildId.MagnetBlastDamage)) * (1f + Kit.Value(BuildId.MagnetHitRamp) * pathHits);
            float stun = MathF.Max(0f, Magnet.StunTime + Kit.Value(BuildId.MagnetStun));
            float vulnerable = Kit.Value(BuildId.MagnetVulnerable);
            int blastHits = 0;
            Kit.Ctx.Monsters.QueryCircle(_midpoint, radius, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive || (!throughWalls && Kit.Ctx.Obstacles.SegmentBlocked(_midpoint, m.Position, 0f, ObstacleMask.Projectiles))) continue;
                Kit.Hit(m, damage, DamageSource.SubWeapon);
                if (m.Alive)
                {
                    if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
                    if (vulnerable > 0f) m.BurstVulnerability = MathF.Max(m.BurstVulnerability, vulnerable);
                }
                blastHits++;
            }
            _query.Clear();
            _holes.Clear();
            _holes.Add(_midpoint);
            Kit.Ctx.Events.Publish(new MagnetCollideEvent
            {
                From = _pairFrom, To = _pairTo, Center = _midpoint, Width = width,
                BlastRadius = radius, Hits = pathHits + blastHits,
            });
        }

        private bool Blocked(Vec2 a, Vec2 b, Vec2 target)
        {
            return Kit.Ctx.Obstacles.SegmentBlocked(a, target, 0f, ObstacleMask.Projectiles)
                && Kit.Ctx.Obstacles.SegmentBlocked(b, target, 0f, ObstacleMask.Projectiles);
        }

        public override void OnSubspaceExited()
        {
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++) all[i].BurstVulnerability = 0f;
        }

        private void Clear()
        {
            _holes.Clear();
            _pending.Clear();
        }
    }
}
