using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 掌（11/25 双武器策划案）。
    /// 常态：以玩家为中心释放 360° 圆形掌击；范围内没有可见敌人时不出手。
    ///       圆内全部活怪均可命中，保留遮挡物视线判定；伤害 + 沿径向击退 + 叠掌印。
    /// 爆发：以自身为中心的圆形天降掌，每只敌人伤害 × (1 + 掌印层数 × 每层加成)；「二重掌」再落一段（第二段 50%）；结算后掌印清空。
    /// </summary>
    public sealed class PalmWeapon : WeaponBehavior
    {
        private readonly List<MonsterEntity> _candidates = new List<MonsterEntity>();

        public PalmWeapon(WeaponKit kit) : base(kit) { }

        private PalmConfig Cfg { get { return Kit.Arsenal.Palm; } }

        public override WeaponKind Kind { get { return WeaponKind.Palm; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.PalmAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.PalmRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.PalmPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.PalmBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Range; } }

        public float Range { get { return Cfg.Range * (1f + Kit.Value(BuildId.PalmRange)); } }
        public float Angle { get { return 360f; } }

        private Vec2 Facing
        {
            get
            {
                Vec2 f = Kit.Ctx.Player.State.Facing;
                return f.SqrMagnitude > 0.01f ? f.Normalized : Vec2.Right;
            }
        }

        public override bool WantsToFire(Vec2 origin, MonsterEntity nearest)
        {
            Collect(origin, Facing, Angle, Range);
            bool any = _candidates.Count > 0;
            _candidates.Clear();
            return any;
        }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Strike(origin, Facing, Angle, Range, true);
        }

        /// <summary>一次扇形掌击（angle ≥ 360 = 一整圈）。</summary>
        private void Strike(Vec2 origin, Vec2 dir, float angle, float range, bool refund)
        {
            Collect(origin, dir, angle, range);
            int marks = 1 + (int)MathF.Round(Kit.Value(BuildId.PalmExtraMark));
            float knock = MathF.Max(0f, Cfg.Knockback * (1f + Kit.Value(BuildId.PalmKnockback)));
            int hits = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                MonsterEntity m = _candidates[i];
                if (!m.Alive) continue;
                Kit.Hit(m, Cfg.Damage, DamageSource.Weapon);
                hits++;
                if (!m.Alive)
                {
                    if (refund && Kit.Chance(Kit.Value(BuildId.PalmKillRefund))) Kit.AddRefund(WeaponKind.Palm, 1);
                    continue;
                }
                m.PalmMarks += marks;
                Kit.Knockback(m, WeaponKit.Direction(origin, m.Position), knock);
            }
            _candidates.Clear();
            float half = angle >= 360f ? MathF.PI : angle * 0.5f * (MathF.PI / 180f);
            Kit.Ctx.Events.Publish(new PalmStrikeEvent { Origin = origin, Direction = dir, HalfAngle = half, Range = range, Hits = hits });
        }

        /// <summary>扇形里、视线没被挡住的敌人，按距离从近到远写进 _candidates。</summary>
        private void Collect(Vec2 origin, Vec2 dir, float angle, float range)
        {
            _candidates.Clear();
            float cosHalf = angle >= 360f ? -2f : MathF.Cos(angle * 0.5f * (MathF.PI / 180f));
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (!m.Alive) continue;
                Vec2 to = m.Position - origin;
                float dist = to.Magnitude;
                if (dist > range + m.Radius) continue;
                if (dist > 0.05f && WeaponKit.Dot(to / dist, dir) < cosHalf) continue;
                if (Kit.Ctx.Obstacles.SegmentBlocked(origin, m.Position, 0f, ObstacleMask.Projectiles)) continue;
                _candidates.Add(m);
            }
            _candidates.Sort((a, b) => Vec2.SqrDistance(origin, a.Position).CompareTo(Vec2.SqrDistance(origin, b.Position)));
        }

        public override void Burst()
        {
            Vec2 center = Kit.PlayerPosition;
            float radius = MathF.Max(0.5f, Cfg.SkyRadius * (1f + Kit.Value(BuildId.PalmSkyArea)));
            float damage = Cfg.SkyDamage * (1f + Kit.Value(BuildId.PalmSkyDamage));
            float perMark = MathF.Max(0f, Cfg.MarkBonus + Kit.Value(BuildId.PalmMarkBonus));
            float stun = Kit.Value(BuildId.PalmStun);

            float dealt = Drop(center, radius, damage, perMark, stun, 0f);
            if (Kit.Has(BuildId.PalmDoubleDrop))
                dealt += Drop(center, radius, damage * Kit.Value(BuildId.PalmDoubleDrop), perMark, stun, MathF.Max(0.05f, Kit.Value2(BuildId.PalmDoubleDrop)));

            // 结算后掌印清空
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++) all[i].PalmMarks = 0;
            Kit.Lifesteal(dealt, BuildId.PalmLifesteal);
        }

        private float Drop(Vec2 center, float radius, float damage, float perMark, float stun, float delay)
        {
            Kit.Ctx.Monsters.QueryCircle(center, radius, Kit.Hits);
            float dealt = 0f;
            int hits = 0;
            for (int i = 0; i < Kit.Hits.Count; i++)
            {
                MonsterEntity m = Kit.Hits[i];
                if (!m.Alive) continue;
                bool crit;
                float mult = Kit.Crit(BuildId.PalmCrit, out crit);
                dealt += Kit.Hit(m, damage * (1f + perMark * m.PalmMarks) * mult, DamageSource.Burst, crit);
                if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
                hits++;
            }
            Kit.Hits.Clear();
            Kit.Ctx.Events.Publish(new PalmSkyFellEvent { Center = center, Radius = radius, Delay = delay, Hits = hits });
            return dealt;
        }

        public override void OnHurt()
        {
            if (Kit.Has(BuildId.PalmHurtStrike)) Strike(Kit.PlayerPosition, Facing, 360f, Range, false);
        }
    }
}
