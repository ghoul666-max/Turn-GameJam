using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 投射物（新策划案）：玩家的水弹 / 长矛，怪物的弹幕。直线飞行、射程、存活时间、离开边界即消失。
    /// 静态障碍物遮挡投射物（ObstacleMask.Projectiles）：被挡住的投射物停在遮挡物前结束。
    /// 玩家子弹：沿本步线段找命中的怪物（由近到远，同一只只命中一次），命中与结束的效果全部交给 IWeaponSystem 的回调
    ///           （水弹留水渍、长矛叠印记 / 击杀穿透都在武器里）。
    /// 敌方弹幕：命中玩家（经 ICombatSystem，无敌时被挡下也会消失）。
    /// 亚空间里 SessionSystem 不调用 Tick：所有投射物原地定格。
    /// </summary>
    public sealed class ProjectileSystem : IProjectileSystem
    {
        private readonly ITurnContext _ctx;
        private readonly List<ProjectileEntity> _active = new List<ProjectileEntity>();
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private int _nextId = 1;

        public IReadOnlyList<ProjectileEntity> Active { get { return _active; } }

        public ProjectileSystem(ITurnContext ctx)
        {
            _ctx = ctx;
        }

        public ProjectileEntity SpawnPlayer(WeaponKind weapon, Vec2 position, Vec2 velocity, float radius, float damage, float maxDistance)
        {
            var p = Create(position, velocity, radius, damage, maxDistance);
            p.Weapon = weapon;
            float speed = MathF.Max(0.1f, velocity.Magnitude);
            p.LifetimeLeft = p.DistanceLeft / speed + 1f;
            return p;
        }

        public ProjectileEntity SpawnEnemy(Vec2 position, Vec2 velocity, float radius, float damage, float lifetime, float maxDistance, EnemyProjectileKind kind)
        {
            var p = Create(position, velocity, radius, damage, maxDistance);
            p.Enemy = true;
            p.EnemyKind = kind;
            p.LifetimeLeft = MathF.Max(0.01f, lifetime);
            return p;
        }

        private ProjectileEntity Create(Vec2 position, Vec2 velocity, float radius, float damage, float maxDistance)
        {
            var p = new ProjectileEntity();
            p.Id = _nextId++;
            p.Form = _ctx.Plane.Active;
            p.Position = position;
            p.Velocity = velocity;
            p.Radius = MathF.Max(0.02f, radius);
            p.Damage = damage;
            p.PierceLeft = 1;
            p.DistanceLeft = MathF.Max(0.05f, maxDistance);
            p.Alive = true;
            _active.Add(p);
            return p;
        }

        public void Tick(float dt)
        {
            int count = _active.Count; // 回调里可能生成新子弹，本步只推进已有的
            for (int i = 0; i < count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive) continue;

                Vec2 from = p.Position;
                Vec2 step = p.Velocity * dt;
                float length = step.Magnitude;
                bool lastStep = false;
                if (length >= p.DistanceLeft && length > 0f)
                {
                    step = step * (p.DistanceLeft / length);
                    length = p.DistanceLeft;
                    lastStep = true;
                }
                Vec2 to = from + step;

                // 遮挡物：停在遮挡物前结束
                if (_ctx.Obstacles.SegmentBlocked(from, to, p.Radius, ObstacleMask.Projectiles))
                {
                    End(p);
                    continue;
                }

                p.Position = to;
                p.DistanceLeft -= length;
                p.LifetimeLeft -= dt;

                if (p.Enemy) TickEnemy(p, from);
                else TickPlayer(p, from);
                if (!p.Alive) continue;

                if (lastStep || p.DistanceLeft <= 0f || p.LifetimeLeft <= 0f || !_ctx.Bounds.Contains(p.Position, 0.5f)) End(p);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
        }

        private void End(ProjectileEntity p)
        {
            if (!p.Alive) return;
            p.Alive = false;
            if (!p.Enemy) _ctx.Weapons.OnProjectileEnded(p);
        }

        private void TickEnemy(ProjectileEntity p, Vec2 from)
        {
            PlayerState player = _ctx.Player.State;
            float reach = p.Radius + _ctx.Player.Config.Radius;
            if (player.Alive && SegmentSqrDistance(player.Position, from, p.Position) <= reach * reach)
            {
                _ctx.Combat.DamagePlayer(p.Damage, DamageSource.Monster);
                p.Alive = false; // 被无敌帧挡下也消耗弹幕，避免反复命中。
            }
        }

        private void TickPlayer(ProjectileEntity p, Vec2 from)
        {
            _ctx.Monsters.QuerySegment(from, p.Position, p.Radius, _hits);
            if (_hits.Count == 0) return;
            _hits.Sort((a, b) => Vec2.SqrDistance(from, a.Position).CompareTo(Vec2.SqrDistance(from, b.Position)));
            for (int k = 0; k < _hits.Count; k++)
            {
                MonsterEntity m = _hits[k];
                if (!m.Alive || p.HitIds.Contains(m.Id)) continue;
                p.HitIds.Add(m.Id);
                bool keepFlying = _ctx.Weapons.OnProjectileHit(p, m);
                if (!keepFlying)
                {
                    p.Position = m.Position;
                    p.Alive = false; // 命中后消失：效果已由武器结算（不再调用 OnProjectileEnded）
                    break;
                }
            }
            _hits.Clear();
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
    }
}
