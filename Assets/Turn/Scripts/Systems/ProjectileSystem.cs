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
        private sealed class PoisonZone
        {
            public Vec2 Position;
            public float Radius;
            public float Dps;
            public float TimeLeft;
            public PlaneId Plane;
        }

        private readonly ITurnContext _ctx;
        private readonly UpgradeConfig _upgrades;
        private readonly float _lifetime;
        private readonly float _planeSwitchLifetimeMultiplier;
        private readonly List<ProjectileEntity> _active = new List<ProjectileEntity>();
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private readonly List<PoisonZone> _poisonZones = new List<PoisonZone>();
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

        public ProjectileEntity SpawnEnemy(PlaneId plane, Vec2 position, Vec2 velocity, float radius, float damage, float lifetime, float maxDistance, EnemyProjectileKind kind)
        {
            var p = Spawn(plane, position, velocity, radius, damage, 1, maxDistance);
            p.Enemy = true;
            p.EnemyKind = kind;
            p.LifetimeLeft = MathF.Max(0.01f, lifetime);
            return p;
        }

        public void OnPlaneSwitched(PlaneId from, PlaneId to)
        {
            if (from == to) return;
            for (int i = 0; i < _active.Count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive) continue;
                // B16 只强化切换前已存在的弹幕；目标位面的旧弹幕也能在接下来的 0.5 秒内命中。
                if (!p.Enemy && _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B16)) > 0)
                    p.AftershockLeft = MathF.Max(p.AftershockLeft, _upgrades.PlaneSwitchBonusDuration);
                if (p.Form != from || p.LifetimeExtended) continue;
                p.LifetimeLeft *= _planeSwitchLifetimeMultiplier;
                if (!p.Enemy)
                {
                    int linger = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B19));
                    p.LifetimeLeft *= 1f + linger * _upgrades.ProjectileLifetimeBonusPerLevel;
                }
                p.LifetimeExtended = true;
            }
        }

        public void Tick(float dt)
        {
            TickPoisonZones(dt);
            for (int i = 0; i < _active.Count; i++)
            {
                ProjectileEntity p = _active[i];
                if (!p.Alive) continue;

                Vec2 from = p.Position;
                Vec2 step = p.Velocity * dt;
                p.Position += step;
                p.DistanceLeft -= step.Magnitude;
                p.LifetimeLeft -= dt;
                if (p.AftershockLeft > 0f) p.AftershockLeft = MathF.Max(0f, p.AftershockLeft - dt);
                if (p.LifetimeLeft <= 0f || p.DistanceLeft <= 0f || !_ctx.Bounds.Contains(p.Position, 0.5f))
                {
                    p.Alive = false;
                    continue;
                }

                if (p.Form != _ctx.Plane.Active) continue;

                // 两侧投射物共用遮挡检测，离开所属位面时继续飞行但暂停碰撞判定。
                // 隐藏在另一层的子弹等切回来后再检查。
                if (_ctx.Obstacles.SegmentBlocked(from, p.Position, p.Radius, ObstacleMask.Projectiles))
                {
                    p.Alive = false;
                    continue;
                }

                if (p.Enemy)
                {
                    PlayerState player = _ctx.Player.State;
                    float reach = p.Radius + _ctx.Player.Config.Radius;
                    if (player.Alive && SegmentSqrDistance(player.Position, from, p.Position) <= reach * reach)
                    {
                        _ctx.Combat.DamagePlayer(p.Damage, DamageSource.Monster);
                        p.Alive = false; // 被无敌帧挡下也消耗弹幕，避免反复命中。
                    }
                    continue;
                }

                Vec2 midpoint = (from + p.Position) * .5f;
                float broadRadius = p.Radius + step.Magnitude * .5f;
                _ctx.Monsters.QueryCircle(midpoint, broadRadius, _hits);
                for (int k = 0; k < _hits.Count; k++)
                {
                    MonsterEntity m = _hits[k];
                    float reach = p.Radius + _ctx.Config.Monster.Radius;
                    if (!m.Alive || m.Form != p.Form || p.HitIds.Contains(m.Id) ||
                        SegmentSqrDistance(m.Position, from, p.Position) > reach * reach) continue;
                    p.HitIds.Add(m.Id);

                    float damage = p.Damage;
                    int crit = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B08));
                    if (crit > 0 && _ctx.Random.NextDouble() < MathF.Min(1f, crit * _upgrades.CriticalChancePerLevel))
                        damage *= _upgrades.CriticalMultiplier;
                    if (p.AftershockLeft > 0f)
                        damage *= 1f + _upgrades.PlaneSwitchProjectileDamageBonus;
                    _ctx.Combat.DamageMonster(m, damage, DamageSource.Weapon);
                    ApplyWeaponImpact(p, m, damage);
                    int slow = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B06));
                    if (slow > 0 && m.Alive)
                        _ctx.Monsters.ApplySlow(m, MathF.Max(0f, 1f - _upgrades.SlowOnHit), _upgrades.SlowDuration);

                    p.HitCount++;
                    if (p.Form == PlaneId.Ocean)
                    {
                        WeaponFormConfig form = FormConfig(p.Form);
                        p.Damage *= 1f - MathF.Max(0f, MathF.Min(1f, form.HarpoonFalloff));
                    }
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
        private static float SegmentSqrDistance(Vec2 point, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float lengthSq = ab.SqrMagnitude;
            if (lengthSq <= 0.000001f) return Vec2.SqrDistance(point, a);
            Vec2 ap = point - a;
            float t = MathF.Max(0f, MathF.Min(1f, (ap.X * ab.X + ap.Y * ab.Y) / lengthSq));
            return Vec2.SqrDistance(point, a + ab * t);
        }

        private WeaponFormConfig FormConfig(PlaneId plane)
        {
            int i = (int)plane;
            WeaponFormConfig[] forms = _ctx.Config.Forms;
            return i >= 0 && i < forms.Length && forms[i] != null ? forms[i] : _ctx.Weapons.CurrentForm;
        }

        private void ApplyWeaponImpact(ProjectileEntity p, MonsterEntity primary, float damage)
        {
            WeaponFormConfig form = FormConfig(p.Form);
            int areaLevel = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B09));
            float area = MathF.Max(0f, form.SplashRadius) * (1f + areaLevel * _upgrades.AreaRadiusPerLevel);
            if (p.Form == PlaneId.Volcano)
            {
                if (primary.Alive) _ctx.Monsters.ApplyBurn(primary, form.BurnDps, form.BurnDuration);
                if (area > 0f) Splash(p, primary, damage, area, form.BurnDps, form.BurnDuration);
            }
            else if (p.Form == PlaneId.Future && area > 0f)
                Splash(p, primary, damage, area, 0f, 0f);
            else if (p.Form == PlaneId.Dream)
            {
                float radius = MathF.Max(0f, form.SplashRadius) * (1f + areaLevel * _upgrades.AreaRadiusPerLevel);
                _poisonZones.Add(new PoisonZone { Plane = p.Form, Position = p.Position,
                    Radius = radius, Dps = form.PoisonDps, TimeLeft = form.PoisonDuration });
            }
        }

        private void Splash(ProjectileEntity p, MonsterEntity primary, float damage, float radius, float burnDps, float burnDuration)
        {
            // 使用独立列表，避免覆盖 Tick 正在遍历的 _hits。
            var splash = new List<MonsterEntity>();
            _ctx.Monsters.QueryCircle(p.Position, radius, splash);
            for (int i = 0; i < splash.Count; i++)
            {
                MonsterEntity m = splash[i];
                if (!m.Alive || m == primary || m.Form != p.Form) continue;
                _ctx.Combat.DamageMonster(m, damage, DamageSource.Weapon);
                if (burnDps > 0f && m.Alive) _ctx.Monsters.ApplyBurn(m, burnDps, burnDuration);
            }
        }

        private void TickPoisonZones(float dt)
        {
            if (dt <= 0f) return;
            WeaponFormConfig dream = FormConfig(PlaneId.Dream);
            int maxStacks = Math.Max(1, dream.PoisonMaxStacks);
            var perTarget = new Dictionary<int, int>();
            for (int i = _poisonZones.Count - 1; i >= 0; i--)
            {
                PoisonZone zone = _poisonZones[i];
                float active = MathF.Min(dt, MathF.Max(0f, zone.TimeLeft));
                zone.TimeLeft -= dt;
                if (active <= 0f) { _poisonZones.RemoveAt(i); continue; }
                if (_ctx.Plane.Active != zone.Plane) continue;
                var targets = new List<MonsterEntity>();
                _ctx.Monsters.QueryCircle(zone.Position, zone.Radius, targets);
                for (int k = 0; k < targets.Count; k++)
                {
                    MonsterEntity m = targets[k];
                    int stacks;
                    perTarget.TryGetValue(m.Id, out stacks);
                    if (!m.Alive || m.Form != zone.Plane || stacks >= maxStacks) continue;
                    perTarget[m.Id] = stacks + 1;
                    _ctx.Combat.DamageMonster(m, zone.Dps * active, DamageSource.Burn);
                }
                if (zone.TimeLeft <= 0f) _poisonZones.RemoveAt(i);
            }
        }
    }
}
