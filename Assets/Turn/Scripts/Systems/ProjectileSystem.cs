using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 子弹（火球 / 鱼叉）：直线飞行、最远距离、穿透次数、离开边界即消失。
    /// 命中只检测普通怪物（鲨鱼不在怪物系统里，打不到）。
    /// 命中效果全部读全局升级等级：灼烧词条对两种形态同时生效。
    /// </summary>
    public sealed class ProjectileSystem : IProjectileSystem
    {
        private readonly ITurnContext _ctx;
        private readonly UpgradeConfig _upgrades;
        private readonly float _lifetime;
        private readonly float _planeSwitchLifetimeMultiplier;
        private readonly List<ProjectileEntity> _active = new List<ProjectileEntity>();
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private int _nextId = 1;

        public IReadOnlyList<ProjectileEntity> Active { get { return _active; } }

        public ProjectileSystem(ITurnContext ctx, UpgradeConfig upgrades)
        {
            _ctx = ctx;
            _upgrades = upgrades;
            _lifetime = MathF.Max(0.1f, ctx.Config.Weapon.ProjectileLifetime);
            _planeSwitchLifetimeMultiplier = MathF.Max(1f, ctx.Config.Weapon.PlaneSwitchLifetimeMultiplier);
        }

        public ProjectileEntity Spawn(PlaneId form, Vec2 position, Vec2 velocity, float radius, float damage, int pierce, float maxDistance)
        {
            var p = new ProjectileEntity();
            p.Id = _nextId++;
            p.Form = form;
            p.LifetimeLeft = _lifetime;
            p.Position = position;
            p.Velocity = velocity;
            p.Radius = MathF.Max(0.02f, radius);
            p.Damage = damage;
            p.PierceLeft = Math.Max(1, pierce);
            p.DistanceLeft = MathF.Max(0.1f, maxDistance);
            p.Alive = true;
            _active.Add(p);
            return p;
        }

        public void OnPlaneSwitched(PlaneId from, PlaneId to)
        {
            if (from == to) return;
            for (int i = 0; i < _active.Count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive || p.Form != from || p.LifetimeExtended) continue;
                p.LifetimeLeft *= _planeSwitchLifetimeMultiplier;
                p.LifetimeExtended = true;
            }
        }

        public void Tick(float dt)
        {
            int burnLevel = _ctx.Progression.GetLevel(UpgradeId.Burn);
            float burnDps = burnLevel * _upgrades.BurnDpsPerLevel;

            for (int i = 0; i < _active.Count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive) continue;

                Vec2 from = p.Position;
                Vec2 step = p.Velocity * dt;
                p.Position += step;
                p.DistanceLeft -= step.Magnitude;
                p.LifetimeLeft -= dt;
                if (p.LifetimeLeft <= 0f || p.DistanceLeft <= 0f || !_ctx.Bounds.Contains(p.Position, 0.5f))
                {
                    p.Alive = false;
                    continue;
                }

                if (p.Form != _ctx.Plane.Active) continue;

                // 遮挡物（实体 / 只挡弹）：这一步扫过的线段碰到就消失（程序A 补）。只检查当前位面的子弹，
                // 隐藏在另一层的子弹等切回来后再检查。
                if (_ctx.Obstacles.SegmentBlocked(from, p.Position, p.Radius, ObstacleMask.Projectiles))
                {
                    p.Alive = false;
                    continue;
                }

                _ctx.Monsters.QueryCircle(p.Position, p.Radius, _hits);
                for (int k = 0; k < _hits.Count; k++)
                {
                    MonsterEntity m = _hits[k];
                    if (!m.Alive || m.Form != p.Form || p.HitIds.Contains(m.Id)) continue;
                    p.HitIds.Add(m.Id);

                    _ctx.Combat.DamageMonster(m, p.Damage, DamageSource.Weapon);
                    if (burnDps > 0f && m.Alive) _ctx.Monsters.ApplyBurn(m, burnDps, _upgrades.BurnDuration);

                    p.PierceLeft--;
                    if (p.PierceLeft <= 0)
                    {
                        p.Alive = false;
                        break;
                    }
                }
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
        }
    }
}
