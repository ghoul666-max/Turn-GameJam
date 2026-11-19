using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 武器产生的所有判定体：魔弹、飞刀、鞭痕、大蒜光环、环绕的圣经、闪电落点。
    /// 统一处理运动、寿命、穿透次数、对同一敌人的重复命中间隔，命中后交给 CombatSystem 结算。
    /// </summary>
    public sealed class ProjectileSystem : IProjectileSystem
    {
        public const int MaxProjectiles = 600;

        private readonly ISurvivorContext _ctx;
        private readonly List<ProjectileEntity> _active = new List<ProjectileEntity>();
        private readonly Stack<ProjectileEntity> _pool = new Stack<ProjectileEntity>();
        private readonly List<EnemyEntity> _hits = new List<EnemyEntity>();
        private int _nextId = 1;
        private float _clock;

        public IReadOnlyList<ProjectileEntity> Active { get { return _active; } }

        public ProjectileSystem(ISurvivorContext ctx)
        {
            _ctx = ctx;
        }

        public ProjectileEntity Spawn(ProjectileSpec s)
        {
            if (_active.Count >= MaxProjectiles) return null;

            ProjectileEntity p = _pool.Count > 0 ? _pool.Pop() : new ProjectileEntity();
            p.Id = _nextId++;
            p.WeaponId = s.WeaponId;
            p.VisualKey = s.VisualKey;
            p.Motion = s.Motion;
            p.Shape = s.Shape;
            p.Position = s.Position;
            p.Velocity = s.Velocity;
            p.Offset = s.Offset;
            p.Radius = s.Radius;
            p.HalfExtents = s.HalfExtents;
            p.Life = s.Life > 0f ? s.Life : 0.01f;
            p.MaxLife = p.Life;
            p.PierceLeft = s.Pierce > 0 ? s.Pierce : -1;
            p.Damage = s.Damage;
            p.Knockback = s.Knockback;
            p.RehitInterval = s.RehitInterval;
            p.OrbitAngle = s.OrbitAngle;
            p.OrbitRadius = s.OrbitRadius;
            p.OrbitSpeed = s.OrbitSpeed;
            p.Rotation = s.Rotation;
            p.Scale = s.Scale > 0f ? s.Scale : 1f;
            p.FitVisual = s.FitVisual;
            p.Facing = s.Facing < 0 ? -1 : 1;
            p.Alive = true;
            p.HitIds.Clear();
            p.HitTimes.Clear();

            Move(p, 0f, _ctx.Player.State.Position); // 跟随类的位置在生成当帧就要正确
            _active.Add(p);
            return p;
        }

        public void Tick(float dt)
        {
            _clock += dt;
            Vec2 player = _ctx.Player.State.Position;

            for (int i = 0; i < _active.Count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    continue;
                }

                Move(p, dt, player);
                HitEnemies(p);
            }

            Compact();
        }

        private static void Move(ProjectileEntity p, float dt, Vec2 player)
        {
            switch (p.Motion)
            {
                case ProjectileMotion.Linear:
                    p.Position += p.Velocity * dt;
                    break;
                case ProjectileMotion.Attached:
                    p.Position = player + p.Offset;
                    break;
                case ProjectileMotion.Orbit:
                    p.OrbitAngle += p.OrbitSpeed * dt;
                    p.Position = player + Vec2.FromAngle(p.OrbitAngle) * p.OrbitRadius;
                    break;
                case ProjectileMotion.Static:
                    break;
            }
        }

        private void HitEnemies(ProjectileEntity p)
        {
            float reach = p.Shape == HitShape.Box ? p.HalfExtents.Magnitude : p.Radius;
            _ctx.Enemies.QueryCircle(p.Position, reach, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                EnemyEntity e = _hits[i];
                if (!e.Alive) continue;
                if (p.Shape == HitShape.Box && !BoxOverlaps(p, e)) continue;
                if (!CanHit(p, e.Id)) continue;

                RecordHit(p, e.Id);
                Vec2 knockDir = p.Velocity.SqrMagnitude > 0.01f ? p.Velocity : e.Position - p.Position;
                if (p.Shape == HitShape.Box) knockDir = new Vec2(p.Facing, 0f);
                _ctx.Combat.DamageEnemy(e, p.Damage, knockDir, p.Knockback);

                if (p.PierceLeft > 0)
                {
                    p.PierceLeft--;
                    if (p.PierceLeft == 0)
                    {
                        p.Alive = false;
                        return;
                    }
                }
            }
        }

        /// <summary>轴对齐矩形与敌人圆的相交判定。</summary>
        private static bool BoxOverlaps(ProjectileEntity p, EnemyEntity e)
        {
            float dx = e.Position.X - p.Position.X;
            float dy = e.Position.Y - p.Position.Y;
            float cx = MathF.Max(-p.HalfExtents.X, MathF.Min(p.HalfExtents.X, dx));
            float cy = MathF.Max(-p.HalfExtents.Y, MathF.Min(p.HalfExtents.Y, dy));
            float ox = dx - cx;
            float oy = dy - cy;
            float r = e.Data.Radius;
            return ox * ox + oy * oy <= r * r;
        }

        private bool CanHit(ProjectileEntity p, int enemyId)
        {
            int index = p.HitIds.IndexOf(enemyId);
            if (index < 0) return true;
            if (p.RehitInterval <= 0f) return false;
            return _clock - p.HitTimes[index] >= p.RehitInterval;
        }

        private void RecordHit(ProjectileEntity p, int enemyId)
        {
            int index = p.HitIds.IndexOf(enemyId);
            if (index < 0)
            {
                p.HitIds.Add(enemyId);
                p.HitTimes.Add(_clock);
            }
            else
            {
                p.HitTimes[index] = _clock;
            }
        }

        private void Compact()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ProjectileEntity p = _active[i];
                if (p.Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
                _pool.Push(p);
            }
        }
    }
}
