using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 引力黑洞（11/25 双武器策划案）。
    /// 每次翻滚，在主武器爆发之前：以翻滚前的站位为中心生成瞬时引力场，把半径内的怪物向中心拉拢（无基础伤害），
    /// 于是这些怪物正好吃到接下来的主武器爆发。冲锋中的疾冲兽默认免疫（「锁定」词条可拉）。
    /// 词条：范围 / 力度 / 拉扯百分比伤害 / 聚拢后中心爆炸 / 被拉过的敌人受本次爆发增伤（失衡）/ 次级引力点。
    /// </summary>
    public sealed class GravityWellSubWeapon : SubWeaponBehavior
    {
        private readonly List<MonsterEntity> _pulled = new List<MonsterEntity>();

        public GravityWellSubWeapon(WeaponKit kit) : base(kit) { }

        private GravityWellConfig Cfg { get { return Kit.Arsenal.Gravity; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.GravityWell; } }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            float radius = MathF.Max(0.5f, Cfg.Radius * (1f + Kit.Value(BuildId.GravityRadius)));
            float pull = MathF.Max(0f, Cfg.PullDistance * (1f + Kit.Value(BuildId.GravityPull)));
            Pull(rollOrigin, radius, pull, false);

            // 次级引力点：在主中心周围，小幅拉扯
            int secondary = (int)MathF.Round(Kit.Value(BuildId.GravitySecondary));
            float start = (float)Kit.Ctx.Random.NextDouble() * MathF.PI * 2f;
            for (int i = 0; i < secondary; i++)
            {
                Vec2 point = Kit.Ctx.Bounds.Clamp(rollOrigin + Vec2.FromAngle(start + i * MathF.PI * 2f / secondary) * Cfg.SecondaryOffset, 0.5f);
                Pull(point, Cfg.SecondaryRadius, Cfg.SecondaryPull, true);
            }

            // 坍缩：聚拢结束时中心爆炸
            if (Kit.Has(BuildId.GravityBlast))
            {
                float blastRadius = MathF.Max(0.3f, Kit.Value2(BuildId.GravityBlast));
                Kit.Ctx.Monsters.QueryCircle(rollOrigin, blastRadius, Kit.Hits);
                int hits = 0;
                for (int i = 0; i < Kit.Hits.Count; i++)
                {
                    if (!Kit.Hits[i].Alive) continue;
                    Kit.Hit(Kit.Hits[i], Kit.Value(BuildId.GravityBlast), DamageSource.SubWeapon);
                    hits++;
                }
                Kit.Hits.Clear();
                Kit.Ctx.Events.Publish(new GravityBlastEvent { Center = rollOrigin, Radius = blastRadius, Hits = hits });
            }
        }

        private void Pull(Vec2 center, float radius, float pull, bool secondary)
        {
            bool grabCharging = Kit.Has(BuildId.GravityPullCharging);
            float damageRatio = Kit.Value(BuildId.GravityPullDamage);
            float vulnerable = Kit.Value(BuildId.GravityVulnerable);
            Kit.Ctx.Monsters.QueryCircle(center, radius, _pulled);
            int count = 0;
            for (int i = 0; i < _pulled.Count; i++)
            {
                MonsterEntity m = _pulled[i];
                if (!m.Alive || (!grabCharging && !WeaponKit.CanDisplace(m))) continue;
                float dist = Vec2.Distance(center, m.Position);
                float move = MathF.Min(pull, dist - MathF.Max(0f, Cfg.MinGap));
                if (move > 0.01f)
                {
                    float moved = Kit.Ctx.Monsters.Knockback(m, WeaponKit.Direction(m.Position, center), move);
                    if (moved > .01f) Kit.Ctx.Events.Publish(new GravityDisplacedEvent { Moved = 1 });
                }
                if (damageRatio > 0f) Kit.Hit(m, m.MaxHp * damageRatio, DamageSource.SubWeapon, percentageDamage: true);
                if (vulnerable > 0f && m.Alive) m.BurstVulnerability = MathF.Max(m.BurstVulnerability, vulnerable);
                count++;
            }
            _pulled.Clear();
            Kit.Ctx.Events.Publish(new GravityPulledEvent { Center = center, Radius = radius, Pulled = count, Secondary = secondary });
        }

        public override void OnSubspaceExited()
        {
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++) all[i].BurstVulnerability = 0f;
        }
    }
}
