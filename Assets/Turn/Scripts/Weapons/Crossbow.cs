using System;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 重弩：大型高速弩箭，沿固定射程贯穿敌人并击退；单弹同一敌人只命中一次（ProjectileSystem.HitIds）。
    /// 杀气改为本局所有玩家来源击杀累计的尺寸成长，不再增加伤害，切换武器不清零。
    /// 翻滚没有固有爆发，只按 RollSystem 的规则装填。
    /// </summary>
    public sealed class CrossbowWeapon : WeaponBehavior
    {
        private long _kills;
        private int _wallIntent;

        public CrossbowWeapon(WeaponKit kit) : base(kit)
        {
            Kit.Ctx.Events.Subscribe<WeaponKillConfirmedEvent>(OnPlayerKill);
        }

        private CrossbowConfig Cfg { get { return Kit.Arsenal.Crossbow; } }

        /// <summary>当前尺寸成长档数（每档所需击杀数由配置决定），切枪保留。</summary>
        public int Intent { get { return (int)Math.Min(int.MaxValue, _kills / Math.Max(1, Cfg.KillsPerSizeStep)); } }
        public float SizeMultiplier
        {
            get { return MathF.Min(MathF.Max(1f, Cfg.MaxSizeMultiplier), 1f + Intent * MathF.Max(0f, Cfg.SizePerKillStep) * MathF.Max(0f, 1f + Kit.Value(BuildId.BowIntentBonus))); }
        }
        private float FlightRange { get { return MathF.Max(0f, Kit.Arsenal.Blade.Range * Cfg.SwordWaveRangeMultiplier); } }

        public override WeaponKind Kind { get { return WeaponKind.Crossbow; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return FlightRange; } }

        private void OnPlayerKill(WeaponKillConfirmedEvent evt)
        {
            // CombatSystem 仅为 Weapon/Burst/SubWeapon 来源发布该事件，不依赖当前手持哪把武器。
            if (_kills < long.MaxValue) _kills++;
        }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            Shoot(origin, WeaponKit.Direction(origin, target.Position));
        }

        private void Shoot(Vec2 origin, Vec2 dir)
        {
            float damage = Cfg.Damage * (1f + Kit.Value(BuildId.BowDamage));
            Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Crossbow, origin, dir * MathF.Max(0.5f, Cfg.Speed),
                Cfg.Radius * SizeMultiplier, damage, FlightRange);
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            float falloff = MathF.Max(0f, MathF.Min(1f, Cfg.PierceFalloff - Kit.Value(BuildId.BowFalloff)));
            bool crit;
            float mult = Kit.Crit(BuildId.BowCrit, out crit);
            float damage = p.Damage * MathF.Pow(1f - falloff, p.HitCount) * mult;
            p.HitCount++;
            Kit.Hit(m, damage, DamageSource.Weapon, crit);

            if (!m.Alive)
            {
                // 连弩：保留既有击杀装填词条，只返还给弩自己的弹匣。
                if (Kit.Chance(Kit.Value(BuildId.BowKillReload))) Kit.AddRefund(WeaponKind.Crossbow, BaseAmmo);
            }
            else
            {
                Vec2 dir = p.Velocity.SqrMagnitude > 0.0001f ? p.Velocity.Normalized : Vec2.Right;
                float knock = MathF.Max(0f, Cfg.Knockback * (1f + Kit.Value(BuildId.BowKnockback) + Kit.Value(BuildId.BowPierce)));
                if (Kit.Knockback(m, dir, knock)) WallSlam(m);
            }

            // 保留既有撞墙累计触发的溅射 Build；不把杀气换算成额外伤害倍率。
            if (Kit.Has(BuildId.BowSplash) && _wallIntent >= Kit.Value2(BuildId.BowSplash))
            {
                Kit.Ctx.Monsters.QueryCircle(m.Position, MathF.Max(0.1f, Cfg.SplashRadius), Kit.Hits2);
                float splash = damage * Kit.Value(BuildId.BowSplash);
                for (int i = 0; i < Kit.Hits2.Count; i++)
                    if (Kit.Hits2[i] != m && Kit.Hits2[i].Alive) Kit.Hit(Kit.Hits2[i], splash, DamageSource.Weapon);
                Kit.Hits2.Clear();
            }

            // 贯穿不再受旧 Pierce 次数限制；ProjectileSystem 记录 HitIds，保证单弹单目标一次。
            return true;
        }

        private void WallSlam(MonsterEntity m)
        {
            Kit.Ctx.Monsters.ApplyStun(m, MathF.Max(0.05f, Cfg.WallStun + Kit.Value(BuildId.BowStunTime)));
            _wallIntent += 1 + (int)MathF.Round(Kit.Value(BuildId.BowExtraIntent));
            float heal = Kit.Value(BuildId.BowWallHeal);
            if (heal > 0f) Kit.Ctx.Combat.HealPlayer(heal);
            Kit.Ctx.Events.Publish(new WallSlammedEvent { MonsterId = m.Id, Position = m.Position, Intent = Intent });
        }

        public override void Burst() { }

        public override void OnUnequipped(bool byRoll)
        {
            _wallIntent = 0; // 仅既有撞墙 Build 的计数清零；本局尺寸成长保留。
        }

        public override void OnHurt()
        {
            if (!Kit.Has(BuildId.BowHurtShot)) return;
            Vec2 origin = Kit.PlayerPosition;
            MonsterEntity nearest = Kit.Ctx.Monsters.GetNearest(origin, FlightRange);
            Vec2 facing = Kit.Ctx.Player.State.Facing;
            Vec2 dir = nearest != null ? WeaponKit.Direction(origin, nearest.Position) : (facing.SqrMagnitude > 0.01f ? facing.Normalized : Vec2.Right);
            Shoot(origin, dir);
        }
    }
}
