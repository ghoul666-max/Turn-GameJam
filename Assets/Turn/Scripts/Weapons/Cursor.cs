using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 光标（11/25 第三批策划案）。策略精准型：两次翻滚 = 按下 / 松开光标框选。
    ///   · 第一次翻滚：固定翻滚前站位为选区锚点；玩家当前位置为对角，实时显示轴对齐矩形。
    ///   · MaxLength 保留为选区对角线最长距离，CursorArea 仍增加此上限；超限裁剪当前对角而不移动锚点。
    ///   · 之后每次翻滚（爆发前）：确认矩形内部 + 边界 Thickness 范围，
    ///     范围内敌人受到降维打击并定身（PinnedLeft，表现层压扁贴地）；然后以当前站位为新起点无缝开始下一轮绘制。
    /// </summary>
    public sealed class CursorSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _trail = new List<Vec2>();
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private bool _drawing;
        private Vec2 _anchor;
        private Vec2 _opposite;

        public CursorSubWeapon(WeaponKit kit) : base(kit) { }

        private CursorConfig Cfg { get { return Kit.Arsenal.Cursor; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.Cursor; } }
        public override IReadOnlyList<Vec2> Trail { get { return _trail; } }
        public bool Drawing { get { return _drawing; } }

        private float MaxLength { get { return MathF.Max(1f, Cfg.MaxLength * (1f + Kit.Value(BuildId.CursorArea))); } }

        public override void Tick(float dt)
        {
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
                if (all[i].PinnedLeft > 0f) all[i].PinnedLeft = MathF.Max(0f, all[i].PinnedLeft - dt);

            if (!_drawing) return;
            UpdateRectangle(Kit.PlayerPosition);
        }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            if (_drawing)
            {
                UpdateRectangle(rollOrigin);
                Strike();
            }
            StartDrawing(rollOrigin);
        }

        private void StartDrawing(Vec2 at)
        {
            _drawing = true;
            _anchor = at;
            UpdateRectangle(at);
            float haste = Kit.Value(BuildId.CursorDrawHaste);
            Kit.Ctx.Player.SetSpeedModifier(PlayerSpeedSource.CursorHaste, 1f + MathF.Max(0f, haste));
        }

        private void Strike()
        {
            float thickness = MathF.Max(0.05f, Cfg.Thickness * (1f + Kit.Value(BuildId.CursorThickness)));
            _hits.Clear();
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (m.Alive && Inside(m.Position, thickness + m.Radius)) _hits.Add(m);
            }

            float damage = Cfg.Damage * (1f + Kit.Value(BuildId.CursorDamage)) * (1f + Kit.Value(BuildId.CursorCrowdBonus) * _hits.Count);
            float pin = MathF.Max(0f, Cfg.PinTime + Kit.Value(BuildId.CursorPinTime));
            float slow = Kit.Value(BuildId.CursorAfterSlow);
            for (int i = 0; i < _hits.Count; i++)
            {
                MonsterEntity m = _hits[i];
                if (!m.Alive) continue;
                bool crit;
                float mult = Kit.Crit(BuildId.CursorCrit, out crit);
                if (pin > 0f) m.PinnedLeft = MathF.Max(m.PinnedLeft, pin); // 先定身，让「定身易伤」吃到这一下
                Kit.Hit(m, damage * mult, DamageSource.SubWeapon, crit);
                if (!m.Alive) continue;
                if (pin > 0f) Kit.Ctx.Monsters.ApplyStun(m, pin);
                if (slow > 0f) Kit.Ctx.Monsters.ApplySlow(m, MathF.Max(0.05f, 1f - slow), pin + MathF.Max(0.1f, Kit.Value2(BuildId.CursorAfterSlow)));
            }
            Kit.Ctx.Events.Publish(new CursorStrikeEvent { Outline = _trail.ToArray(), Thickness = thickness, Hits = _hits.Count });
            _hits.Clear();
        }

        private void UpdateRectangle(Vec2 opposite)
        {
            _opposite = _anchor + Vec2.ClampMagnitude(opposite - _anchor, MaxLength);
            _trail.Clear();
            _trail.Add(_anchor);
            _trail.Add(new Vec2(_opposite.X, _anchor.Y));
            _trail.Add(_opposite);
            _trail.Add(new Vec2(_anchor.X, _opposite.Y));
        }

        /// <summary>矩形内部或距矩形边界不超过 margin；拐角采用圆角半径，保留旧厚度语义。</summary>
        private bool Inside(Vec2 p, float margin)
        {
            float minX = MathF.Min(_anchor.X, _opposite.X), maxX = MathF.Max(_anchor.X, _opposite.X);
            float minY = MathF.Min(_anchor.Y, _opposite.Y), maxY = MathF.Max(_anchor.Y, _opposite.Y);
            float closestX = MathF.Max(minX, MathF.Min(maxX, p.X));
            float closestY = MathF.Max(minY, MathF.Min(maxY, p.Y));
            return Vec2.SqrDistance(p, new Vec2(closestX, closestY)) <= margin * margin;
        }

        public override float DamageTakenMultiplier(MonsterEntity m)
        {
            float v = Kit.Value(BuildId.CursorPinVulnerable);
            return m != null && v > 0f && m.PinnedLeft > 0f ? 1f + v : 1f;
        }
    }
}
