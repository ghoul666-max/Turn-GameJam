using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 弩（11/25 双武器策划案）。
    /// 常态：基础 1 发弹药，自动向最近的敌人发射重型弩箭；可穿透（每穿透一个衰减），命中强制击退（冲锋中的疾冲兽免疫）。
    /// 杀气：被击退的敌人撞上障碍物 / 地图边缘 → 立即眩晕，玩家 +1 层杀气（「嗜杀」额外叠层）。
    ///       杀气只提升弩自身的所有伤害（每层 +IntentBonus），无限叠加、没有持续时间，主武器切换时强制清零。
    /// 翻滚：没有爆发；翻滚只补满弹药（+ 方案 A 切换 / 方案 B 驱动副武器），由 RollSystem 完成。
    /// </summary>
    public sealed class CrossbowWeapon : WeaponBehavior
    {
        private int _intent;

        public CrossbowWeapon(WeaponKit kit) : base(kit) { }

        private CrossbowConfig Cfg { get { return Kit.Arsenal.Crossbow; } }

        /// <summary>当前杀气层数。</summary>
        public int Intent { get { return _intent; } }

        public override WeaponKind Kind { get { return WeaponKind.Crossbow; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.Range; } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Shoot(origin, WeaponKit.Direction(origin, target.Position));
        }

        private void Shoot(Vec2 origin, Vec2 dir)
        {
            float damage = Cfg.Damage * (1f + Kit.Value(BuildId.BowDamage));
            ProjectileEntity p = Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Crossbow, origin, dir * MathF.Max(0.5f, Cfg.Speed),
                Cfg.Radius, damage, Cfg.Range);
            p.PierceLeft = Math.Max(1, Cfg.Pierce + (int)MathF.Round(Kit.Value(BuildId.BowPierce)));
        }

        /// <summary>杀气倍率 = 1 + 层数 × 每层加成（只作用于弩）。</summary>
        private float IntentMultiplier
        {
            get { return 1f + _intent * MathF.Max(0f, Cfg.IntentBonus + Kit.Value(BuildId.BowIntentBonus)); }
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            float falloff = MathF.Max(0f, MathF.Min(1f, Cfg.PierceFalloff - Kit.Value(BuildId.BowFalloff)));
            bool crit;
            float mult = Kit.Crit(BuildId.BowCrit, out crit);
            float damage = p.Damage * IntentMultiplier * MathF.Pow(1f - falloff, p.HitCount) * mult;
            p.HitCount++;
            Kit.Hit(m, damage, DamageSource.Weapon, crit);

            if (!m.Alive)
            {
                // 连弩：常态击杀有概率直接补满弹药
                if (Kit.Chance(Kit.Value(BuildId.BowKillReload))) Kit.PendingRefund += 999;
            }
            else
            {
                Vec2 dir = p.Velocity.SqrMagnitude > 0.0001f ? p.Velocity.Normalized : Vec2.Right;
                float knock = MathF.Max(0f, Cfg.Knockback * (1f + Kit.Value(BuildId.BowKnockback)));
                if (Kit.Knockback(m, dir, knock)) WallSlam(m);
            }

            // 杀气溢出：高杀气时命中额外小范围溅射
            if (Kit.Has(BuildId.BowSplash) && _intent >= Kit.Value2(BuildId.BowSplash))
            {
                Kit.Ctx.Monsters.QueryCircle(m.Position, MathF.Max(0.1f, Cfg.SplashRadius), Kit.Hits2);
                float splash = damage * Kit.Value(BuildId.BowSplash);
                for (int i = 0; i < Kit.Hits2.Count; i++)
                    if (Kit.Hits2[i] != m && Kit.Hits2[i].Alive) Kit.Hit(Kit.Hits2[i], splash, DamageSource.Weapon);
                Kit.Hits2.Clear();
            }

            p.PierceLeft--;
            return p.PierceLeft > 0;
        }

        private void WallSlam(MonsterEntity m)
        {
            Kit.Ctx.Monsters.ApplyStun(m, MathF.Max(0.05f, Cfg.WallStun + Kit.Value(BuildId.BowStunTime)));
            _intent += 1 + (int)MathF.Round(Kit.Value(BuildId.BowExtraIntent));
            float heal = Kit.Value(BuildId.BowWallHeal);
            if (heal > 0f) Kit.Ctx.Combat.HealPlayer(heal);
            Kit.Ctx.Events.Publish(new WallSlammedEvent { MonsterId = m.Id, Position = m.Position, Intent = _intent });
        }

        public override void Burst() { }

        public override void OnUnequipped(bool byRoll)
        {
            _intent = 0; // 主武器切换时强制清零
        }

        public override void OnHurt()
        {
            if (!Kit.Has(BuildId.BowHurtShot)) return;
            Vec2 origin = Kit.PlayerPosition;
            MonsterEntity nearest = Kit.Ctx.Monsters.GetNearest(origin, Cfg.Range);
            Vec2 facing = Kit.Ctx.Player.State.Facing;
            Vec2 dir = nearest != null ? WeaponKit.Direction(origin, nearest.Position) : (facing.SqrMagnitude > 0.01f ? facing.Normalized : Vec2.Right);
            Shoot(origin, dir);
        }
    }
}
