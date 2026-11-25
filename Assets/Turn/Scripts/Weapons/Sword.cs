using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 剑（11/25 第三批策划案）。突进生存型。
    /// 常态：面向方向（扇形 TargetAngle、距离 TargetRange）有敌人时，朝面向方向突进刺击 DashDistance；
    ///       突进过程中玩家完全无敌（PlayerState.WeaponInvincible），只在两次突进之间的停留期会受伤；路径留下剑痕（铺垫）。
    /// 爆发：影子以 ShadowSpeed 倍速沿全部剑痕复刻刺击（结算是瞬时的，表现按顺序播放），伤害随剑痕条数提升；
    ///       影刺每击杀 KillsPerCharge 名敌人，永久 +1 突进次数上限（= 弹药上限），切换到其他主武器时清零。结算后剑痕清空。
    /// </summary>
    public sealed class SwordWeapon : WeaponBehavior
    {
        private struct Trail
        {
            public Vec2 From;
            public Vec2 To;
        }

        private readonly List<Trail> _trails = new List<Trail>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private readonly List<int> _dashHits = new List<int>();
        private bool _dashing;
        private Vec2 _dashFrom;
        private Vec2 _dashDir;
        private float _dashLeft;
        private float _dashSpeed;
        private float _killProgress;

        public SwordWeapon(WeaponKit kit) : base(kit) { }

        private SwordConfig Cfg { get { return Kit.Arsenal.Sword; } }

        public override WeaponKind Kind { get { return WeaponKind.Sword; } }
        public override int BaseAmmo { get { return Math.Max(1, Cfg.Ammo + BonusCharges); } }
        public override BuildId? AmmoBuild { get { return BuildId.SwordAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.SwordRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.SwordPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.SwordBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.TargetRange; } }

        /// <summary>影刺击杀累计的突进次数上限加成。</summary>
        public int BonusCharges { get; private set; }
        public int TrailCount { get { return _trails.Count; } }
        public bool Dashing { get { return _dashing; } }

        private float Width { get { return MathF.Max(0.05f, Cfg.Width * (1f + Kit.Value(BuildId.SwordWidth))); } }

        private Vec2 Facing
        {
            get
            {
                Vec2 f = Kit.Ctx.Player.State.Facing;
                return f.SqrMagnitude > 0.01f ? f.Normalized : Vec2.Right;
            }
        }

        public override bool WantsToFire(Vec2 origin, MonsterEntity nearest)
        {
            if (_dashing) return false;
            Vec2 facing = Facing;
            float cos = MathF.Cos(MathF.Min(180f, Cfg.TargetAngle * 0.5f) * MathF.PI / 180f);
            float rangeSq = Cfg.TargetRange * Cfg.TargetRange;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (!m.Alive) continue;
                Vec2 d = m.Position - origin;
                float sq = d.SqrMagnitude;
                if (sq > rangeSq) continue;
                if (sq < 0.0001f || WeaponKit.Dot(d / MathF.Sqrt(sq), facing) >= cos) return true;
            }
            return false;
        }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            StartDash(origin, Facing, Cfg.DashDistance * (1f + Kit.Value(BuildId.SwordRange)));
        }

        private void StartDash(Vec2 origin, Vec2 dir, float distance)
        {
            if (_dashing) EndDash();
            _dashing = true;
            _dashFrom = origin;
            _dashDir = dir;
            _dashLeft = MathF.Max(0.1f, distance);
            _dashSpeed = _dashLeft / MathF.Max(0.02f, Cfg.DashTime);
            _dashHits.Clear();
            Kit.Ctx.Player.State.WeaponInvincible = true;
        }

        public override void TickAlways(float dt)
        {
            if (!_dashing) return;
            PlayerState s = Kit.Ctx.Player.State;
            Vec2 from = s.Position;
            float step = MathF.Min(_dashLeft, _dashSpeed * dt);
            float radius = Kit.Ctx.Player.Config.Radius;
            Vec2 wanted = from + _dashDir * step;
            Vec2 to = Kit.Ctx.Bounds.Clamp(Kit.Ctx.Obstacles.ResolveMove(from, wanted, radius, ObstacleMask.Units), radius);
            Kit.Ctx.Player.Teleport(to);
            to = s.Position;
            StrikeAlong(from, to);
            _dashLeft -= step;
            float moved = WeaponKit.Dot(to - from, _dashDir);
            if (_dashLeft <= 0.001f || moved < step * 0.5f) EndDash();
        }

        private void StrikeAlong(Vec2 from, Vec2 to)
        {
            Kit.Ctx.Monsters.QuerySegment(from, to, Width, _query);
            float ramp = Kit.Value(BuildId.SwordHitRamp);
            float refund = Kit.Value(BuildId.SwordKillRefund);
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive || _dashHits.Contains(m.Id)) continue;
                _dashHits.Add(m.Id);
                // 突进路径上每多命中 1 名敌人，伤害递增
                Kit.Hit(m, Cfg.Damage * (1f + ramp * (_dashHits.Count - 1)), DamageSource.Weapon);
                if (!m.Alive && Kit.Chance(refund)) Kit.PendingRefund += 1;
            }
            _query.Clear();
        }

        private void EndDash()
        {
            if (!_dashing) return;
            _dashing = false;
            Kit.Ctx.Player.State.WeaponInvincible = false;
            Vec2 end = Kit.PlayerPosition;
            if (Vec2.SqrDistance(_dashFrom, end) > 0.04f)
            {
                int max = Math.Max(1, Cfg.MaxTrails);
                while (_trails.Count >= max) _trails.RemoveAt(0);
                _trails.Add(new Trail { From = _dashFrom, To = end });
            }
            Kit.Ctx.Events.Publish(new SwordDashEvent { From = _dashFrom, To = end, Width = Width, Hits = _dashHits.Count });
        }

        public override void Burst()
        {
            EndDash();
            if (_trails.Count == 0) return;
            float width = Width;
            float perTrail = MathF.Max(0f, Cfg.TrailBonus + Kit.Value(BuildId.SwordTrailBonus));
            float damage = Cfg.ShadowDamage * (1f + Kit.Value(BuildId.SwordShadowDamage)) * (1f + perTrail * _trails.Count);
            float stun = Kit.Value(BuildId.SwordStun);
            float step = MathF.Max(0.02f, Cfg.DashTime) / MathF.Max(0.1f, Cfg.ShadowSpeed);
            float dealt = 0f;
            int kills = 0;
            for (int t = 0; t < _trails.Count; t++)
            {
                Trail tr = _trails[t];
                Kit.Ctx.Monsters.QuerySegment(tr.From, tr.To, width, _query);
                int hits = 0;
                for (int i = 0; i < _query.Count; i++)
                {
                    MonsterEntity m = _query[i];
                    if (!m.Alive) continue;
                    bool crit;
                    float mult = Kit.Crit(BuildId.SwordCrit, out crit);
                    dealt += Kit.Hit(m, damage * mult, DamageSource.Burst, crit);
                    if (!m.Alive) kills++;
                    else if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
                    hits++;
                }
                _query.Clear();
                Kit.Ctx.Events.Publish(new ShadowStrikeEvent { From = tr.From, To = tr.To, Width = width, Index = t, Delay = t * step, Hits = hits });
            }
            _trails.Clear();
            Kit.Lifesteal(dealt, BuildId.SwordLifesteal);

            // 影刺击杀 → 永久 +突进次数上限（阈值词条：每次所需击杀数降低）
            float threshold = MathF.Max(0.2f, Cfg.KillsPerCharge * (1f - Kit.Value(BuildId.SwordKillThreshold)));
            _killProgress += kills;
            while (_killProgress >= threshold - 0.0001f)
            {
                _killProgress -= threshold;
                BonusCharges++;
            }
        }

        public override void OnHurt()
        {
            if (!Kit.Has(BuildId.SwordHurtDash) || _dashing) return;
            StartDash(Kit.PlayerPosition, Facing * -1f, Cfg.DashDistance * 0.7f); // 向后突进并留下剑痕（不耗弹药）
        }

        public override void OnUnequipped(bool byRoll)
        {
            EndDash();
            BonusCharges = 0; // 切换到其他主武器：突进次数加成强制清零
            _killProgress = 0f;
        }

        public override void CollectFields(List<WeaponField> results)
        {
            float w = Width;
            for (int i = 0; i < _trails.Count; i++)
                results.Add(new WeaponField { Kind = WeaponFieldKind.SwordTrail, Position = _trails[i].From, End = _trails[i].To, Radius = w, Life = 1f });
        }
    }
}
