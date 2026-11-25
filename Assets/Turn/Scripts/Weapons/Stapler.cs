using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 订书机（11/25 第三批策划案）。群体连锁控场。
    /// 常态：订书钉射向最近敌人，命中 = 伤害 + 1 层钉痕 + 击退（冲锋中的疾冲兽免疫）。
    ///       被击退的敌人沿途撞到的其他怪物（还没被钉合的）和它钉合成一组：全组各 +1 层钉痕并减速；同一组不会再被钉合，
    ///       之后打中组里任意一只，全组一起 +1 层钉痕。
    /// 爆发：全图带钉痕的怪各向四周散射 5 枚次级钉（伤害随钉痕层数提升）；原有钉痕与钉合组随即清空。
    ///       次级钉与常态钉逻辑一致（击退、钉合、叠钉痕），它们在回到常态后才飞出，留下的新钉痕留到下一次爆发。
    /// </summary>
    public sealed class StaplerWeapon : WeaponBehavior
    {
        private struct Marked
        {
            public int Id;
            public Vec2 Position;
            public float Radius;
            public int Marks;
        }

        private readonly List<Marked> _marked = new List<Marked>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private readonly List<MonsterEntity> _group = new List<MonsterEntity>();
        private int _nextGroup = 1;

        public StaplerWeapon(WeaponKit kit) : base(kit) { }

        private StaplerConfig Cfg { get { return Kit.Arsenal.Stapler; } }

        public override WeaponKind Kind { get { return WeaponKind.Stapler; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.StaplerAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.StaplerRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.StaplerPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.StaplerBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.Range; } }

        private int Pierce { get { return Math.Max(1, Cfg.Pierce + (int)MathF.Round(Kit.Value(BuildId.StaplerPierce))); } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Vec2 dir = target != null ? WeaponKit.Direction(origin, target.Position) : Vec2.Right;
            if (Kit.Has(BuildId.StaplerDouble))
            {
                Shoot(origin, WeaponKit.Rotate(dir, -8f), false, Cfg.Damage, Cfg.Range, -1);
                Shoot(origin, WeaponKit.Rotate(dir, 8f), false, Cfg.Damage, Cfg.Range, -1);
            }
            else Shoot(origin, dir, false, Cfg.Damage, Cfg.Range, -1);
        }

        /// <summary>secondary = 爆发散射出的次级钉（CarriedMarks = 1 标记）；ignoreId = 不打发射它的那只怪。</summary>
        private void Shoot(Vec2 origin, Vec2 dir, bool secondary, float damage, float range, int ignoreId)
        {
            ProjectileEntity p = Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Stapler, origin, dir * MathF.Max(0.5f, Cfg.Speed),
                Cfg.Radius, damage, range);
            if (p == null) return;
            p.PierceLeft = Pierce;
            p.CarriedMarks = secondary ? 1 : 0;
            if (ignoreId >= 0) p.HitIds.Add(ignoreId);
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            bool secondary = p.CarriedMarks == 1;
            bool crit = false;
            float mult = secondary ? Kit.Crit(BuildId.StaplerCrit, out crit) : 1f;
            float dealt = Kit.Hit(m, p.Damage * mult, DamageSource.Weapon, crit);
            if (secondary) Kit.Lifesteal(dealt, BuildId.StaplerLifesteal);

            if (m.Alive)
            {
                AddMark(m);
                if (secondary)
                {
                    float stun = Kit.Value(BuildId.StaplerStun);
                    if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
                }
                // 击退：沿途撞到的怪钉合成一组
                Vec2 start = m.Position;
                Vec2 dir = p.Velocity.SqrMagnitude > 0.0001f ? p.Velocity.Normalized : Vec2.Right;
                float knock = MathF.Max(0f, Cfg.Knockback * (1f + Kit.Value(BuildId.StaplerKnockback)));
                if (knock > 0f && WeaponKit.CanDisplace(m))
                {
                    Kit.Knockback(m, dir, knock);
                    if (m.StapleGroup == 0) Staple(m, start);
                }
            }

            p.PierceLeft--;
            return p.PierceLeft > 0;
        }

        /// <summary>m +1 层钉痕；它在钉合组里的话，全组一起 +1。</summary>
        private void AddMark(MonsterEntity m)
        {
            if (m.StapleGroup == 0)
            {
                m.StapleMarks++;
                return;
            }
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Alive && all[i].StapleGroup == m.StapleGroup) all[i].StapleMarks++;
        }

        private void Staple(MonsterEntity m, Vec2 start)
        {
            int max = Math.Max(2, Cfg.GroupMax + (int)MathF.Round(Kit.Value(BuildId.StaplerGroupSize)));
            Kit.Ctx.Monsters.QuerySegment(start, m.Position, m.Radius, _query);
            _group.Clear();
            _group.Add(m);
            for (int i = 0; i < _query.Count && _group.Count < max; i++)
            {
                MonsterEntity o = _query[i];
                if (o == m || !o.Alive || o.StapleGroup != 0) continue;
                _group.Add(o);
            }
            _query.Clear();
            if (_group.Count < 2)
            {
                _group.Clear();
                return; // 没撞到别的怪
            }
            int id = _nextGroup++;
            float slow = MathF.Min(0.95f, MathF.Max(0f, Cfg.GroupSlow + Kit.Value(BuildId.StaplerSlow)));
            for (int i = 0; i < _group.Count; i++)
            {
                MonsterEntity g = _group[i];
                g.StapleGroup = id;
                if (g != m) g.StapleMarks++; // 被击退的那只这次已经叠过了
                if (slow > 0f) Kit.Ctx.Monsters.ApplySlow(g, 1f - slow, MathF.Max(0.1f, Cfg.GroupSlowTime));
            }
            Kit.Ctx.Events.Publish(new StapledEvent { Position = m.Position, Count = _group.Count });
            _group.Clear();
        }

        public override void Burst()
        {
            _marked.Clear();
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (m.Alive && m.StapleMarks > 0)
                    _marked.Add(new Marked { Id = m.Id, Position = m.Position, Radius = m.Radius, Marks = m.StapleMarks });
                // 原有钉痕与钉合组清空（散射出的次级钉打出的新钉痕留到下一次爆发）
                m.StapleMarks = 0;
                m.StapleGroup = 0;
            }
            if (_marked.Count == 0) return;

            int per = Math.Max(1, Cfg.SecondaryCount + (int)MathF.Round(Kit.Value(BuildId.StaplerScatterCount)));
            float baseDamage = Cfg.SecondaryDamage * (1f + Kit.Value(BuildId.StaplerScatterDamage));
            float perMark = MathF.Max(0f, Cfg.MarkBonus + Kit.Value(BuildId.StaplerMarkBonus));
            int budget = Math.Max(1, Cfg.MaxSecondary);
            Random rng = Kit.Ctx.Random;
            for (int i = 0; i < _marked.Count && budget > 0; i++)
            {
                Marked mk = _marked[i];
                float damage = baseDamage * (1f + perMark * mk.Marks);
                float start = (float)rng.NextDouble() * MathF.PI * 2f;
                int shots = Math.Min(per, budget);
                for (int s = 0; s < shots; s++)
                {
                    Vec2 dir = Vec2.FromAngle(start + s * MathF.PI * 2f / per);
                    Shoot(mk.Position + dir * (mk.Radius + Cfg.Radius + 0.05f), dir, true, damage, Cfg.SecondaryRange, mk.Id);
                }
                budget -= shots;
                Kit.Ctx.Events.Publish(new StapleScatterEvent { Position = mk.Position, Marks = mk.Marks, Count = shots });
            }
            _marked.Clear();
        }
    }
}
