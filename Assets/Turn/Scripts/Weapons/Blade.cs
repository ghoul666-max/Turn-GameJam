using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 陌刀（策划：武器 3）。
    /// 常态：朝最近的敌人方向挥出扇形剑气（60°、射程 7.5 = 300 像素，伤害 6），剑气命中的所有敌人统一叠 1 层剑气印记。
    ///       剑气有厚度：只命中最近一圈敌人（离最近目标不超过 RingThickness），「破阵」词条每层多推进一圈；被遮挡物挡住的敌人打不到。
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
            float cosHalf = MathF.Cos(halfAngle);

            // 扇形内、视线没被遮挡的敌人，按距离从近到远
            _candidates.Clear();
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

            // 剑气厚度：一圈一圈往外推，默认 1 圈，破阵每层 +1 圈
            int rings = 1 + (int)MathF.Round(Kit.Value(BuildId.BladePierce));
            float thickness = MathF.Max(0.1f, Cfg.RingThickness);
            int ring = 0;
            float ringStart = -1f;
            int hits = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                MonsterEntity m = _candidates[i];
                float d = Vec2.Distance(origin, m.Position);
                if (ringStart < 0f) ringStart = d;
                else if (d > ringStart + thickness)
                {
                    ring++;
                    if (ring >= rings) break;
                    ringStart = d;
                }
                if (!m.Alive) continue;
                Kit.Hit(m, Cfg.Damage, DamageSource.Weapon);
                if (m.Alive) m.BladeMarks++;
                hits++;
            }
            _candidates.Clear();
            Kit.Ctx.Events.Publish(new BladeWaveFiredEvent { Origin = origin, TargetPosition = targetPosition,
                Direction = dir, HalfAngle = halfAngle, Range = range, Hits = hits });
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
