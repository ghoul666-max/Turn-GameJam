using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 手里剑（11/25 第二批策划案）。定点持续铺伤 + 环绕站撸。
    /// 常态：朝最近敌人掷出，飞行 FlyDistance（撞到遮挡物提前停）后停在原地高速旋转，每 SpinInterval 秒伤害范围内敌人
    ///       （飞行途中也在转、也有伤害）。手里剑不会被消除，一直留到翻滚爆发回收（或切走主武器时消失）。
    /// 爆发：全部手里剑高速飞回玩家，路径穿刺。
    ///   · 方案 A（双主武器）：回收的手里剑环绕玩家旋转、持续伤害，直到下一次切换主武器（切回手里剑时）消失；
    ///   · 方案 B（主 + 副）：只造成路径伤害，回收后清空。
    /// </summary>
    public sealed class ShurikenWeapon : WeaponBehavior
    {
        private sealed class Star
        {
            public Vec2 Position;
            public Vec2 Velocity;
            public float FlyLeft;
        }

        private readonly List<Star> _stars = new List<Star>();
        private readonly List<Vec2> _orbit = new List<Vec2>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private int _orbitCount;
        private float _orbitAngle;
        private float _spinTimer;

        public ShurikenWeapon(WeaponKit kit) : base(kit) { }

        private ShurikenConfig Cfg { get { return Kit.Arsenal.Shuriken; } }

        public override WeaponKind Kind { get { return WeaponKind.Shuriken; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? RateBuild { get { return BuildId.ShurikenRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.ShurikenPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.ShurikenBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.TargetRange; } }

        /// <summary>场上（停住 / 飞行中）的手里剑数量。</summary>
        public int OnField { get { return _stars.Count; } }
        /// <summary>环绕中的手里剑数量（方案 A）。</summary>
        public int Orbiting { get { return _orbitCount; } }

        private float SpinRadius { get { return MathF.Max(0.1f, Cfg.SpinRadius * (1f + Kit.Value(BuildId.ShurikenSpinArea))); } }
        private float SpinDamage { get { return Cfg.SpinDamage * (1f + Kit.Value(BuildId.ShurikenSpinDamage)); } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Vec2 dir = target != null ? WeaponKit.Direction(origin, target.Position) : Vec2.Right;
            float fly = MathF.Max(0.2f, Cfg.FlyDistance * (1f + Kit.Value(BuildId.ShurikenRange)));
            if (Kit.Has(BuildId.ShurikenDouble))
            {
                float half = Cfg.DoubleAngle * 0.5f;
                Throw(origin, WeaponKit.Rotate(dir, -half), fly);
                Throw(origin, WeaponKit.Rotate(dir, half), fly);
            }
            else Throw(origin, dir, fly);
        }

        private void Throw(Vec2 origin, Vec2 dir, float fly)
        {
            int max = Math.Max(1, Cfg.MaxOnField);
            while (_stars.Count >= max) _stars.RemoveAt(0);
            _stars.Add(new Star { Position = origin, Velocity = dir * MathF.Max(0.5f, Cfg.Speed), FlyLeft = fly });
        }

        public override void TickAlways(float dt)
        {
            // 飞行
            for (int i = 0; i < _stars.Count; i++)
            {
                Star s = _stars[i];
                if (s.FlyLeft <= 0f) continue;
                Vec2 step = s.Velocity * dt;
                float len = step.Magnitude;
                if (len > s.FlyLeft) step = step * (s.FlyLeft / len);
                Vec2 to = s.Position + step;
                if (Kit.Ctx.Obstacles.SegmentBlocked(s.Position, to, 0.1f, ObstacleMask.Projectiles) || !Kit.Ctx.Bounds.Contains(to, 0.3f))
                {
                    s.FlyLeft = 0f; // 撞墙 / 出界：停在这里转
                    continue;
                }
                s.Position = to;
                s.FlyLeft -= MathF.Min(len, s.FlyLeft);
            }

            // 环绕位置
            _orbit.Clear();
            if (_orbitCount > 0)
            {
                _orbitAngle += Cfg.OrbitSpeed * dt;
                int shown = Math.Min(_orbitCount, Math.Max(1, Cfg.MaxOnField));
                Vec2 player = Kit.PlayerPosition;
                for (int i = 0; i < shown; i++)
                    _orbit.Add(player + Vec2.FromAngle(_orbitAngle + i * MathF.PI * 2f / shown) * Cfg.OrbitRadius);
            }

            // 旋转伤害（全部手里剑共用一个节拍）
            _spinTimer -= dt;
            if (_spinTimer > 0f) return;
            _spinTimer += MathF.Max(0.05f, Cfg.SpinInterval);
            if (_stars.Count == 0 && _orbit.Count == 0) return;
            float radius = SpinRadius;
            float damage = SpinDamage;
            for (int i = 0; i < _stars.Count; i++) SpinHit(_stars[i].Position, radius, damage);
            if (_orbit.Count > 0)
            {
                // 护身：环绕状态下旋转伤害额外加成；环绕的数量超过显示上限时伤害折算进显示的那些
                float orbitDamage = damage * (1f + Kit.Value(BuildId.ShurikenOrbitBonus)) * ((float)_orbitCount / _orbit.Count);
                for (int i = 0; i < _orbit.Count; i++) SpinHit(_orbit[i], radius, orbitDamage);
            }
        }

        private void SpinHit(Vec2 at, float radius, float damage)
        {
            Kit.Ctx.Monsters.QueryCircle(at, radius, _query);
            for (int k = 0; k < _query.Count; k++)
                if (_query[k].Alive) Kit.Hit(_query[k], damage, DamageSource.Weapon);
            _query.Clear();
        }

        public override void Burst()
        {
            Vec2 player = Kit.PlayerPosition;
            float width = MathF.Max(0.05f, Cfg.RecallWidth * (1f + Kit.Value(BuildId.ShurikenRecallWidth)));
            float damage = Cfg.RecallDamage * (1f + Kit.Value(BuildId.ShurikenRecallDamage));
            float dealt = 0f;
            int recalled = _stars.Count;
            for (int i = 0; i < _stars.Count; i++)
            {
                Vec2 from = _stars[i].Position;
                Kit.Ctx.Monsters.QuerySegment(from, player, width, _query);
                int hits = 0;
                for (int k = 0; k < _query.Count; k++)
                {
                    MonsterEntity m = _query[k];
                    if (!m.Alive) continue;
                    bool crit;
                    float mult = Kit.Crit(BuildId.ShurikenCrit, out crit);
                    dealt += Kit.Hit(m, damage * mult, DamageSource.Burst, crit);
                    hits++;
                }
                _query.Clear();
                Kit.Ctx.Events.Publish(new ShurikenRecalledEvent { From = from, To = player, Width = width, Hits = hits });
            }
            _stars.Clear();
            Kit.Lifesteal(dealt, BuildId.ShurikenLifesteal);
            // 方案 A：回收后环绕玩家（直到下一次切换主武器）；方案 B：清空
            _orbitCount = Kit.Ctx.Loadout.Scheme == LoadoutScheme.DualMain ? recalled : 0;
        }

        public override void OnEquipped(bool byRoll)
        {
            _orbitCount = 0; // 切回手里剑：上一轮的环绕消失
            _orbit.Clear();
        }

        public override void OnUnequipped(bool byRoll)
        {
            _stars.Clear(); // 切走：场上的手里剑全部消失（翻滚切走时已经回收成环绕）
        }

        public override void CollectFields(List<WeaponField> results)
        {
            float r = SpinRadius;
            for (int i = 0; i < _stars.Count; i++)
                results.Add(new WeaponField { Kind = WeaponFieldKind.Shuriken, Position = _stars[i].Position, End = _stars[i].Position, Radius = r, Life = 1f });
            for (int i = 0; i < _orbit.Count; i++)
                results.Add(new WeaponField { Kind = WeaponFieldKind.OrbitShuriken, Position = _orbit[i], End = _orbit[i], Radius = r, Life = 1f });
        }
    }
}
