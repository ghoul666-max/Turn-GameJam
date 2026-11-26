using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 陌刀（策划：武器 3）。
    /// 常态：朝最近的敌人方向挥出扇形剑气（60°、射程 7.5 = 300 像素，伤害 6），剑气命中的所有敌人统一叠 1 层剑气印记。
    ///       剑气 = 以玩家为圆心、落在最近敌人处的一道弧形带（厚 RingThickness，「破阵」词条每层多一圈厚度）；
    ///       特效画的就是这道带，带上（与怪物圆形相交）的敌人全部同帧受到伤害；被遮挡物挡住的敌人打不到。
    /// 爆发：以全场剑气印记总层数为增伤系数（每层 +8%），释放一道横贯屏幕的横向巨型斩击（以玩家为中心的水平带），
    ///       对带内全部敌人造成伤害（基础 25）。印记在回到常态空间时清空。
    /// </summary>
    public sealed class BladeWeapon : WeaponBehavior
    {
        private readonly List<MonsterEntity> _candidates = new List<MonsterEntity>();

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

        /// <summary>扇形总角度 = 基础 × (1 + 开阖 + 长刀)，上限 360°。</summary>
        public float Angle { get { return MathF.Min(360f, Cfg.Angle * (1f + Kit.Value(BuildId.BladeAngle) + Kit.Value(BuildId.BladeLength))); } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            if (target == null || !target.Alive) return;
            Vec2 targetPosition = target.Position;
            Vec2 dir = WeaponKit.Direction(origin, targetPosition);
            float range = Range;
            float halfAngle = Angle * 0.5f * (MathF.PI / 180f);

            // 1) 剑气落点：扇形里、射程内、视线没被挡住的最近一只敌人到玩家的距离
            float nearest = float.MaxValue;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                float dist;
                if (!InFan(origin, dir, halfAngle, m, out dist) || dist > range + m.Radius) continue;
                if (Kit.Ctx.Obstacles.SegmentBlocked(origin, m.Position, 0f, ObstacleMask.Projectiles)) continue;
                if (dist < nearest) nearest = dist;
            }
            if (nearest == float.MaxValue) return;

            // 2) 剑气 = 以玩家为圆心的一道弧形带：从最近敌人往内半个厚度开始，往外 厚度 × 圈数（「破阵」每层 +1 圈）。
            //    特效画的就是这道带；带上（与怪物圆形相交）且视线没被挡住的敌人全部受到伤害，和特效同一帧结算。
            int rings = 1 + (int)MathF.Round(Kit.Value(BuildId.BladePierce));
            float thickness = MathF.Max(0.1f, Cfg.RingThickness);
            float inner = MathF.Max(0f, nearest - thickness * 0.5f);
            float outer = inner + thickness * rings;

            _candidates.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                float dist;
                if (!InFan(origin, dir, halfAngle, m, out dist)) continue;
                if (dist + m.Radius < inner || dist - m.Radius > outer) continue;
                if (Kit.Ctx.Obstacles.SegmentBlocked(origin, m.Position, 0f, ObstacleMask.Projectiles)) continue;
                _candidates.Add(m);
            }
            int hits = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                MonsterEntity m = _candidates[i];
                if (!m.Alive) continue;
                Kit.Hit(m, Cfg.Damage, DamageSource.Weapon);
                if (m.Alive) m.BladeMarks++;
                hits++;
            }
            _candidates.Clear();
            Kit.Ctx.Events.Publish(new BladeWaveFiredEvent { Origin = origin, TargetPosition = targetPosition,
                Direction = dir, HalfAngle = halfAngle, Range = range, Inner = inner, Outer = outer, Hits = hits });
        }

        /// <summary>怪物（按圆形算，碰到扇形边缘也算）在不在 dir 两侧 halfAngle 的扇形里。dist = 圆心到 origin 的距离。</summary>
        private static bool InFan(Vec2 origin, Vec2 dir, float halfAngle, MonsterEntity m, out float dist)
        {
            dist = 0f;
            if (m == null || !m.Alive) return false;
            Vec2 to = m.Position - origin;
            dist = to.Magnitude;
            if (dist <= m.Radius + 0.05f || halfAngle >= MathF.PI) return true;
            float cos = MathF.Max(-1f, MathF.Min(1f, WeaponKit.Dot(to / dist, dir)));
            float margin = MathF.Asin(MathF.Min(1f, m.Radius / dist));
            return MathF.Acos(cos) <= halfAngle + margin;
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
