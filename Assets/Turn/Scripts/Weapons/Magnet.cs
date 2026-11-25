using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 强磁铁（11/25 第三批策划案）。路径封锁 + 定点爆破。
    /// 翻滚（爆发前，以翻滚前站位为准）：奇数次留下正极；偶数次留下负极，两极沿直线高速对撞：
    ///   路径上所有敌人受穿刺伤害 → 两极在连线中点相遇，范围爆炸（高伤 + 眩晕）→ 两极消失，下一次翻滚重新从正极开始。
    /// 在主武器爆发之前结算，所以「磁暴」（被爆炸命中的敌人受到本次主武器爆发伤害增加）能吃到同一次爆发。
    /// </summary>
    public sealed class MagnetSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _poles = new List<Vec2>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();

        public MagnetSubWeapon(WeaponKit kit) : base(kit) { }

        private MagnetConfig Cfg { get { return Kit.Arsenal.Magnet; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.Magnet; } }
        public override IReadOnlyList<Vec2> Anchors { get { return _poles; } }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            bool positive = _poles.Count == 0;
            SpawnPull(rollOrigin);
            Kit.Ctx.Events.Publish(new MagnetPoleEvent { Position = rollOrigin, Positive = positive });
            if (positive)
            {
                _poles.Add(rollOrigin);
                return;
            }
            Vec2 from = _poles[0];
            _poles.Clear();
            Collide(from, rollOrigin);
        }

        /// <summary>「磁场」：磁极生成时吸附周身小范围敌人。</summary>
        private void SpawnPull(Vec2 at)
        {
            float pull = Kit.Value(BuildId.MagnetSpawnPull);
            if (pull <= 0f) return;
            Kit.Ctx.Monsters.QueryCircle(at, MathF.Max(0.3f, Cfg.SpawnPullRadius), _query);
            for (int i = 0; i < _query.Count; i++) PullToward(_query[i], at, pull);
            _query.Clear();
        }

        private void PullToward(MonsterEntity m, Vec2 at, float distance)
        {
            if (!WeaponKit.CanDisplace(m)) return;
            float move = MathF.Min(distance, Vec2.Distance(m.Position, at) - 0.4f);
            if (move > 0.01f) Kit.Ctx.Monsters.Knockback(m, WeaponKit.Direction(m.Position, at), move);
        }

        private void Collide(Vec2 a, Vec2 b)
        {
            Vec2 center = (a + b) * 0.5f;
            bool throughWalls = Kit.Has(BuildId.MagnetThroughWalls);

            // 磁流：对撞过程中把沿途怪物拉向路径中心
            float pathPull = Kit.Value(BuildId.MagnetPathPull);
            if (pathPull > 0f)
            {
                Kit.Ctx.Monsters.QuerySegment(a, b, MathF.Max(0.3f, Cfg.PathPullRadius), _query);
                for (int i = 0; i < _query.Count; i++) PullToward(_query[i], center, pathPull);
                _query.Clear();
            }

            // 路径穿刺
            float width = MathF.Max(0.05f, Cfg.PathWidth * (1f + Kit.Value(BuildId.MagnetPathWidth)));
            float pathDamage = Cfg.PathDamage * (1f + Kit.Value(BuildId.MagnetPathDamage));
            Kit.Ctx.Monsters.QuerySegment(a, b, width, _query);
            int pathHits = 0;
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive || (!throughWalls && Blocked(a, b, m.Position))) continue;
                Kit.Hit(m, pathDamage, DamageSource.SubWeapon);
                pathHits++;
            }
            _query.Clear();

            // 中点爆炸（路径每命中 1 名敌人，爆炸伤害递增）
            float radius = MathF.Max(0.3f, Cfg.BlastRadius * (1f + Kit.Value(BuildId.MagnetBlastArea)));
            float blast = Cfg.BlastDamage * (1f + Kit.Value(BuildId.MagnetBlastDamage)) * (1f + Kit.Value(BuildId.MagnetHitRamp) * pathHits);
            float stun = MathF.Max(0f, Cfg.StunTime + Kit.Value(BuildId.MagnetStun));
            float vulnerable = Kit.Value(BuildId.MagnetVulnerable);
            Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
            int blastHits = 0;
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive || (!throughWalls && Kit.Ctx.Obstacles.SegmentBlocked(center, m.Position, 0f, ObstacleMask.Projectiles))) continue;
                Kit.Hit(m, blast, DamageSource.SubWeapon);
                if (m.Alive)
                {
                    if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
                    if (vulnerable > 0f) m.BurstVulnerability = MathF.Max(m.BurstVulnerability, vulnerable);
                }
                blastHits++;
            }
            _query.Clear();
            Kit.Ctx.Events.Publish(new MagnetCollideEvent
            {
                From = a, To = b, Center = center, Width = width, BlastRadius = radius, Hits = pathHits + blastHits,
            });
        }

        /// <summary>两极都看不到这只怪（中间有遮挡物）。</summary>
        private bool Blocked(Vec2 a, Vec2 b, Vec2 p)
        {
            return Kit.Ctx.Obstacles.SegmentBlocked(a, p, 0f, ObstacleMask.Projectiles)
                && Kit.Ctx.Obstacles.SegmentBlocked(b, p, 0f, ObstacleMask.Projectiles);
        }

        public override void OnSubspaceExited()
        {
            if (!Kit.Has(BuildId.MagnetVulnerable)) return;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++) all[i].BurstVulnerability = 0f;
        }
    }
}
