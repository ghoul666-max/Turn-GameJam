using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 光标（11/25 第三批策划案）。策略精准型：两次翻滚 = 按下 / 松开光标框选。
    ///   · 第一次翻滚：以翻滚前站位为起点开始绘制，玩家移动时身后拖出选区边界（最长 MaxLength，超出丢掉最早的点）；
    ///   · 之后每次翻滚（爆发前）：确认选区 = 轨迹首尾相连围成的多边形 + 边界 Thickness 范围，
    ///     范围内敌人受到降维打击并定身（PinnedLeft，表现层压扁贴地）；然后以当前站位为新起点无缝开始下一轮绘制。
    /// </summary>
    public sealed class CursorSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _trail = new List<Vec2>();
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private bool _drawing;
        private float _length;

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
            Vec2 p = Kit.PlayerPosition;
            Vec2 last = _trail[_trail.Count - 1];
            float d = Vec2.Distance(last, p);
            if (d < MathF.Max(0.05f, Cfg.SampleSpacing)) return;
            _trail.Add(p);
            _length += d;
            float max = MaxLength;
            while (_length > max && _trail.Count > 2)
            {
                _length -= Vec2.Distance(_trail[0], _trail[1]);
                _trail.RemoveAt(0);
            }
        }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            if (_drawing)
            {
                _trail.Add(rollOrigin);
                Strike();
            }
            StartDrawing(rollOrigin);
        }

        private void StartDrawing(Vec2 at)
        {
            _drawing = true;
            _trail.Clear();
            _trail.Add(at);
            _length = 0f;
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

        /// <summary>在轨迹首尾相连的多边形里，或者离边界不超过 margin。</summary>
        private bool Inside(Vec2 p, float margin)
        {
            int n = _trail.Count;
            if (n == 0) return false;
            float marginSq = margin * margin;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vec2 a = _trail[i], b = _trail[j];
                if (SegmentSqrDistance(p, a, b) <= marginSq) return true;
                if (n >= 3 && ((a.Y > p.Y) != (b.Y > p.Y)) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        private static float SegmentSqrDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len = ab.SqrMagnitude;
            float t = len > 0.0001f ? MathF.Max(0f, MathF.Min(1f, WeaponKit.Dot(p - a, ab) / len)) : 0f;
            return Vec2.SqrDistance(p, a + ab * t);
        }

        public override float DamageTakenMultiplier(MonsterEntity m)
        {
            float v = Kit.Value(BuildId.CursorPinVulnerable);
            return m != null && v > 0f && m.PinnedLeft > 0f ? 1f + v : 1f;
        }
    }
}
