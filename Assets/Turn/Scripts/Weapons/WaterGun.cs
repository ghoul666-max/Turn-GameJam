using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 水枪（策划：武器 1）。
    /// 常态：自动向最近的敌人发射水弹；水弹飞到瞄准点落地，或先命中敌人（伤害 5），都在该处留下一滩水渍（圆形，半径 1 = 40 像素）。
    ///       水渍永久存在，直到翻滚爆发后回到常态空间时清空。
    /// 爆发：全图所有水渍同步喷发喷泉，对范围内敌人各造成 12 点伤害；水渍独立判定，重叠区域的伤害直接叠加
    ///       （「汇流」词条再给重叠加成）。
    /// </summary>
    public sealed class WaterGunWeapon : WeaponBehavior
    {
        private readonly Dictionary<int, int> _overlap = new Dictionary<int, int>();
        private readonly List<MonsterEntity> _touched = new List<MonsterEntity>();

        public WaterGunWeapon(WeaponKit kit) : base(kit) { }

        private WaterGunConfig Cfg { get { return Kit.Arsenal.WaterGun; } }

        public override WeaponKind Kind { get { return WeaponKind.WaterGun; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.WaterAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.WaterRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.WaterPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.WaterBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.Range; } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Vec2 dir = WeaponKit.Direction(origin, target.Position);
            float distance = MathF.Min(Cfg.Range, MathF.Max(0.3f, Vec2.Distance(origin, target.Position)));
            float speed = MathF.Max(0.5f, Cfg.BulletSpeed * (1f + Kit.Value(BuildId.WaterBulletSpeed)));
            int count = 1 + (int)MathF.Round(Kit.Value(BuildId.WaterSpread));
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * Cfg.SpreadDegrees;
                Vec2 d = WeaponKit.Rotate(dir, offset);
                Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.WaterGun, origin, d * speed, Cfg.BulletRadius, Cfg.Damage, distance);
            }
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            Kit.Hit(m, p.Damage, DamageSource.Weapon);
            if (!p.MarkLeft)
            {
                p.MarkLeft = true;
                Kit.AddPuddle(m.Position);
            }
            // 回流：常态击杀有概率补 1 颗子弹
            if (!m.Alive && Kit.Chance(Kit.Value(BuildId.WaterKillRefund))) Kit.AddRefund(WeaponKind.WaterGun, 1);
            return Kit.Has(BuildId.WaterPierce); // 贯穿水弹：继续飞，但不再留水渍
        }

        public override void OnEnded(ProjectileEntity p)
        {
            if (p.MarkLeft) return;
            p.MarkLeft = true;
            Kit.AddPuddle(p.Position);
        }

        public override void Burst()
        {
            List<WaterMark> puddles = Kit.Puddles;
            if (puddles.Count == 0) return;
            float areaScale = 1f + Kit.Value(BuildId.WaterFountainArea);
            float damage = Cfg.FountainDamage * (1f + Kit.Value(BuildId.WaterFountainDamage));
            float overlapBonus = MathF.Max(0f, Cfg.OverlapBonus + Kit.Value(BuildId.WaterOverlap));

            // 第一遍：每只敌人被几滩喷泉覆盖
            _overlap.Clear();
            _touched.Clear();
            for (int i = 0; i < puddles.Count; i++)
            {
                Kit.Ctx.Monsters.QueryCircle(puddles[i].Position, puddles[i].Radius * areaScale, Kit.Hits);
                for (int k = 0; k < Kit.Hits.Count; k++)
                {
                    MonsterEntity m = Kit.Hits[k];
                    int n;
                    if (_overlap.TryGetValue(m.Id, out n)) _overlap[m.Id] = n + 1;
                    else
                    {
                        _overlap[m.Id] = 1;
                        _touched.Add(m);
                    }
                }
            }

            // 第二遍：每滩独立结算（重叠直接相加，汇流再乘加成）
            float dealt = 0f;
            for (int i = 0; i < puddles.Count; i++)
            {
                WaterMark puddle = puddles[i];
                float radius = puddle.Radius * areaScale;
                Kit.Ctx.Monsters.QueryCircle(puddle.Position, radius, Kit.Hits);
                int hits = 0;
                for (int k = 0; k < Kit.Hits.Count; k++)
                {
                    MonsterEntity m = Kit.Hits[k];
                    if (!m.Alive) continue;
                    int n;
                    _overlap.TryGetValue(m.Id, out n);
                    bool crit;
                    float mult = Kit.Crit(BuildId.WaterFountainCrit, out crit);
                    dealt += Kit.Hit(m, damage * (1f + overlapBonus * MathF.Max(0, n - 1)) * mult, DamageSource.Burst, crit);
                    hits++;
                }
                Kit.Ctx.Events.Publish(new FountainBurstEvent { Position = puddle.Position, Radius = radius, Hits = hits });
            }
            Kit.Hits.Clear();

            // 控制：每只被喷到的敌人结算一次
            float slow = MathF.Min(0.8f, Kit.Value(BuildId.WaterFountainSlow));
            float stun = Kit.Value(BuildId.WaterFountainStun);
            for (int i = 0; i < _touched.Count; i++)
            {
                MonsterEntity m = _touched[i];
                if (!m.Alive) continue;
                if (slow > 0f) Kit.Ctx.Monsters.ApplySlow(m, 1f - slow, MathF.Max(0.1f, Kit.Value2(BuildId.WaterFountainSlow)));
                if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
            }
            _touched.Clear();
            Kit.Lifesteal(dealt, BuildId.WaterLifesteal);
        }

        public override void OnHurt()
        {
            int count = (int)MathF.Round(Kit.Value(BuildId.WaterHurtPuddle));
            Vec2 at = Kit.PlayerPosition;
            for (int i = 0; i < count; i++)
                Kit.AddPuddle(at + Vec2.FromAngle(i * 2.4f) * (i == 0 ? 0f : Kit.PuddleRadius * 0.8f));
        }
    }
}
