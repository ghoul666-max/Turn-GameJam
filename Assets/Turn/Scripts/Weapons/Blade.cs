using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 陌刀（策划：武器 3）。
    /// 常态（11/27 起）：朝最近的敌人发射一道月牙剑气弹（速度 WaveSpeed，射程 7.5 = 300 像素，伤害 6），
    ///       命中第一个敌人即消失（「破阵」每层 +1 穿透），被命中的敌人叠 1 层剑气印记；撞到遮挡物照常消失。
    ///       月牙宽度 = 半宽 WaveHalfWidth ×（1 + 开阖 + 长刀）。
    /// 爆发：以全场剑气印记总层数为增伤系数（每层 +8%），释放一道横贯屏幕的横向巨型斩击（以玩家为中心的水平带），
    ///       对带内全部敌人造成伤害（基础 25）。印记在回到常态空间时清空。
    /// </summary>
    public sealed class BladeWeapon : WeaponBehavior
    {
        public BladeWeapon(WeaponKit kit) : base(kit) { }

        private BladeConfig Cfg { get { return Kit.Arsenal.Blade; } }

        public override WeaponKind Kind { get { return WeaponKind.Blade; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.BladeAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.BladeRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.BladePostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.BladeBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Range; } }

        /// <summary>射程 = 基础 × (1 + 远斩 + 长刀)。</summary>
        public float Range { get { return Cfg.Range * (1f + Kit.Value(BuildId.BladeRange) + Kit.Value(BuildId.BladeLength)); } }

        /// <summary>月牙半宽 = 基础 × (1 + 开阖 + 长刀)。</summary>
        public float HalfWidth { get { return MathF.Max(0.05f, Cfg.WaveHalfWidth * (1f + Kit.Value(BuildId.BladeAngle) + Kit.Value(BuildId.BladeLength))); } }

        /// <summary>可命中敌人数 = 1 + 破阵层数（默认不穿透）。</summary>
        public int Pierce { get { return 1 + Math.Max(0, (int)MathF.Round(Kit.Value(BuildId.BladePierce))); } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            if (target == null || !target.Alive) return;
            Vec2 dir = WeaponKit.Direction(origin, target.Position);
            ProjectileEntity p = Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Blade, origin, dir * MathF.Max(0.5f, Cfg.WaveSpeed),
                HalfWidth, Cfg.Damage, Range);
            p.PierceLeft = Pierce;
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            p.HitCount++;
            Kit.Hit(m, p.Damage, DamageSource.Weapon);
            if (m.Alive) m.BladeMarks++;
            p.PierceLeft--;
            return p.PierceLeft > 0;
        }

        public override void Burst()
        {
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            int marks = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Alive) marks += Math.Max(0, all[i].BladeMarks);

            float perMark = MathF.Max(0f, Cfg.MarkBonus + Kit.Value(BuildId.BladeMarkBonus));
            float damage = Cfg.SlashDamage * (1f + perMark * marks);
            float halfHeight = MathF.Max(0.1f, Cfg.SlashHalfHeight * (1f + Kit.Value(BuildId.BladeSlashWidth)));
            float centerY = Kit.PlayerPosition.Y;
            float execute = Kit.Value(BuildId.BladeExecute);
            float stun = Kit.Value(BuildId.BladeStun);
            int hits = 0;

            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (!m.Alive || MathF.Abs(m.Position.Y - centerY) > halfHeight + m.Radius) continue;
                hits++;
                if (execute > 0f && m.Hp <= m.MaxHp * execute)
                {
                    Kit.Hit(m, m.Hp + 1f, DamageSource.Burst, true); // 斩杀
                    continue;
                }
                bool crit;
                float mult = Kit.Crit(BuildId.BladeCrit, out crit);
                Kit.Hit(m, damage * mult, DamageSource.Burst, crit);
                if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
            }
            Kit.Ctx.Events.Publish(new BladeSlashedEvent { CenterY = centerY, HalfHeight = halfHeight, Marks = marks, Damage = damage, Hits = hits });
        }

        public override void OnHurt()
        {
            int layers = (int)MathF.Round(Kit.Value(BuildId.BladeHurtMarks));
            if (layers <= 0) return;
            int count = Math.Max(1, (int)MathF.Round(Kit.Value2(BuildId.BladeHurtMarks)));
            Kit.Nearest(Kit.PlayerPosition, Kit.Arsenal.HurtMarkRange, count, Kit.Hits2);
            for (int i = 0; i < Kit.Hits2.Count; i++) Kit.Hits2[i].BladeMarks += layers;
            Kit.Hits2.Clear();
        }
    }
}