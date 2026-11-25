using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 空间锚（11/25 双武器策划案）。
    /// 每次翻滚（在主武器爆发之后）：如果已经有锚点，玩家回溯到锚点，并沿「翻滚处 → 锚点」的连线斩击路径上所有敌人；
    /// 然后把这次翻滚的位置记录为新锚点。于是连续翻滚会在两个位置之间来回折返。
    /// 「双锚」：最多储存 2 个锚点，回溯时沿「翻滚处 → 最新锚点 → 更早的锚点」两段斩击，回到更早的那个。
    /// 斩击默认被障碍物挡住（「虚空斩」可穿透）；「时隙」：每命中 1 名敌人，本次翻滚冷却缩短（回到常态后结算）。
    /// </summary>
    public sealed class SpaceAnchorSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _anchors = new List<Vec2>();
        private readonly List<Vec2> _path = new List<Vec2>();
        private float _cooldownCut;
        private float _boostLeft;

        public SpaceAnchorSubWeapon(WeaponKit kit) : base(kit) { }

        private SpaceAnchorConfig Cfg { get { return Kit.Arsenal.Anchor; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.SpaceAnchor; } }
        public override IReadOnlyList<Vec2> Anchors { get { return _anchors; } }

        private int Capacity { get { return Kit.Has(BuildId.AnchorDoubleAnchor) ? 2 : 1; } }

        public override void AfterBurst(Vec2 rollOrigin)
        {
            if (_anchors.Count > 0)
            {
                _path.Clear();
                _path.Add(rollOrigin);
                int segments = Math.Min(Capacity, _anchors.Count);
                for (int i = 0; i < segments; i++) _path.Add(_anchors[_anchors.Count - 1 - i]);

                float damage = Cfg.SlashDamage * (1f + Kit.Value(BuildId.AnchorDamage));
                float width = MathF.Max(0.05f, Cfg.Width * (1f + Kit.Value(BuildId.AnchorWidth)));
                int totalHits = 0;
                for (int i = 0; i + 1 < _path.Count; i++) totalHits += Slash(_path[i], _path[i + 1], damage, width);

                // 时隙：命中越多，本次翻滚冷却越短（回到常态、冷却开始后结算）
                _cooldownCut = MathF.Min(Kit.Value2(BuildId.AnchorCooldownCut), totalHits * Kit.Value(BuildId.AnchorCooldownCut));

                // 回溯
                Kit.Ctx.Player.Teleport(_path[_path.Count - 1]);

                // 轻身：回溯后短时间加速
                float boost = Kit.Value(BuildId.AnchorSpeedBoost);
                if (boost > 0f)
                {
                    _boostLeft = MathF.Max(0.1f, Kit.Value2(BuildId.AnchorSpeedBoost));
                    Kit.Ctx.Player.SetSpeedModifier(PlayerSpeedSource.AnchorBoost, 1f + boost);
                }
            }

            _anchors.Add(rollOrigin);
            while (_anchors.Count > Capacity) _anchors.RemoveAt(0);
            Kit.Ctx.Events.Publish(new AnchorRecordedEvent { Position = rollOrigin });
        }

        private int Slash(Vec2 a, Vec2 b, float damage, float width)
        {
            bool throughWalls = Kit.Has(BuildId.AnchorThroughWalls);
            float stun = Kit.Value(BuildId.AnchorStun);
            Kit.Ctx.Monsters.QuerySegment(a, b, width, Kit.Hits);
            int hits = 0;
            for (int i = 0; i < Kit.Hits.Count; i++)
            {
                MonsterEntity m = Kit.Hits[i];
                if (!m.Alive) continue;
                if (!throughWalls && Kit.Ctx.Obstacles.SegmentBlocked(a, m.Position, 0f, ObstacleMask.Projectiles)
                                  && Kit.Ctx.Obstacles.SegmentBlocked(b, m.Position, 0f, ObstacleMask.Projectiles)) continue;
                Kit.Hit(m, damage, DamageSource.SubWeapon);
                if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
                hits++;
            }
            Kit.Hits.Clear();
            Kit.Ctx.Events.Publish(new AnchorSlashedEvent { From = a, To = b, Width = width, Hits = hits });
            return hits;
        }

        public override void OnSubspaceExited()
        {
            if (_cooldownCut > 0f) Kit.Ctx.Roll.ReduceCooldown(_cooldownCut);
            _cooldownCut = 0f;
        }

        public override void Tick(float dt)
        {
            if (_boostLeft <= 0f) return;
            _boostLeft -= dt;
            if (_boostLeft <= 0f) Kit.Ctx.Player.SetSpeedModifier(PlayerSpeedSource.AnchorBoost, 1f);
        }
    }
}
