using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 闪电（11/27 定稿）。
    /// 常态：朝最近的敌人发射一道闪电，在怪物之间超快速连锁跳跃（默认一共命中 5 只，「连锁」词条加），
    ///       每一跳选离上一只最近、且在跳跃距离内的怪；同一条链里同一只怪不会被重复命中；命中的怪物各叠 1 层雷痕。
    ///       视线被遮挡物挡住的那一跳不会发生（链在这里断掉）。
    /// 翻滚：逐个引爆全场雷痕，伤害 = 每层引爆伤害 × 层数。
    ///       传染：被引爆打死的怪物，向距离最近的敌人再发一道闪电，规格与常态完全一致（同样连锁、同样叠雷痕）；
    ///       传染打上的雷痕在这次引爆里接着结算（每只怪每次翻滚最多被引爆一次，所以不会无限循环）。
    ///       结算后全场雷痕清空；弹药补满由 RollSystem 统一处理。
    /// </summary>
    public sealed class LightningWeapon : WeaponBehavior
    {
        private readonly HashSet<int> _chainHit = new HashSet<int>();
        private readonly List<MonsterEntity> _chainTargets = new List<MonsterEntity>();
        private readonly List<Vec2> _points = new List<Vec2>();
        private readonly Queue<MonsterEntity> _queue = new Queue<MonsterEntity>();
        private readonly HashSet<int> _detonated = new HashSet<int>();

        public LightningWeapon(WeaponKit kit) : base(kit) { }

        private LightningConfig Cfg { get { return Kit.Arsenal.Lightning; } }

        public override WeaponKind Kind { get { return WeaponKind.Lightning; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.LightningAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.LightningRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.LightningPostHaste; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return MathF.Max(0.5f, Cfg.Range); } }

        /// <summary>一条闪电最多命中几只 = 基础 + 连锁词条。</summary>
        private int ChainCount { get { return Math.Max(1, Cfg.ChainCount + (int)MathF.Round(Kit.Value(BuildId.LightningJumps))); } }

        private float JumpRange { get { return MathF.Max(0.1f, Cfg.JumpRange * (1f + Kit.Value(BuildId.LightningJumpRange))); } }

        private float HitDamage { get { return Cfg.Damage * (1f + Kit.Value(BuildId.LightningDamage)); } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            if (target == null || !target.Alive) return;
            Chain(origin, target, false);
        }

        /// <summary>从 origin 打出一条连锁闪电，第一下打 first。命中的怪记进 _chainTargets。</summary>
        private void Chain(Vec2 origin, MonsterEntity first, bool infection)
        {
            _chainHit.Clear();
            _chainTargets.Clear();
            _points.Clear();
            _points.Add(origin);

            DamageSource source = infection ? DamageSource.Burst : DamageSource.Weapon;
            float damage = HitDamage;
            float extraMark = Kit.Value(BuildId.LightningExtraMark);
            int count = ChainCount;
            Vec2 from = origin;
            MonsterEntity current = first;

            for (int n = 0; n < count && current != null; n++)
            {
                if (Kit.Ctx.Obstacles.SegmentBlocked(from, current.Position, 0f, ObstacleMask.Projectiles)) break;
                _chainHit.Add(current.Id);
                _chainTargets.Add(current);
                _points.Add(current.Position);
                Kit.Hit(current, damage, source);
                if (current.Alive) current.LightningMarks += 1 + (extraMark > 0f && Kit.Chance(extraMark) ? 1 : 0);
                from = current.Position;
                current = NextJump(from);
            }

            Kit.Ctx.Events.Publish(new LightningChainedEvent { Points = _points.ToArray(), Hits = _chainTargets.Count, Infection = infection });
        }

        /// <summary>离 from 最近、在跳跃距离内、还活着、这条链没打过的怪。</summary>
        private MonsterEntity NextJump(Vec2 from)
        {
            float range = JumpRange;
            float best = range * range;
            MonsterEntity pick = null;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (m == null || !m.Alive || _chainHit.Contains(m.Id)) continue;
                float d = (m.Position - from).SqrMagnitude;
                if (d <= best)
                {
                    best = d;
                    pick = m;
                }
            }
            return pick;
        }

        /// <summary>离 from 最近、在传染范围内、还活着的怪（不含 exclude）。</summary>
        private MonsterEntity NearestAlive(Vec2 from, float range, MonsterEntity exclude)
        {
            float best = range * range;
            MonsterEntity pick = null;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (m == null || m == exclude || !m.Alive) continue;
                float d = (m.Position - from).SqrMagnitude;
                if (d <= best)
                {
                    best = d;
                    pick = m;
                }
            }
            return pick;
        }

        public override void Burst()
        {
            _queue.Clear();
            _detonated.Clear();
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Alive && all[i].LightningMarks > 0) _queue.Enqueue(all[i]);

            float perMark = Cfg.DetonateDamage * (1f + Kit.Value(BuildId.LightningDetonateDamage));
            float stun = Kit.Value(BuildId.LightningStun);
            int maxDetonations = Math.Max(1, Cfg.MaxDetonations);
            int maxInfections = Math.Max(0, Cfg.MaxInfections);
            int detonations = 0, infections = 0;

            while (_queue.Count > 0 && detonations < maxDetonations)
            {
                MonsterEntity m = _queue.Dequeue();
                if (m == null || !m.Alive || m.LightningMarks <= 0 || _detonated.Contains(m.Id)) continue;
                _detonated.Add(m.Id);
                detonations++;

                int marks = m.LightningMarks;
                m.LightningMarks = 0;
                bool crit;
                float mult = Kit.Crit(BuildId.LightningCrit, out crit);
                float damage = perMark * marks * mult;
                float dealt = Kit.Hit(m, damage, DamageSource.Burst, crit);
                Kit.Lifesteal(dealt, BuildId.LightningLifesteal);
                bool killed = !m.Alive;
                Kit.Ctx.Events.Publish(new LightningDetonatedEvent { Position = m.Position, Marks = marks, Damage = damage, Killed = killed });

                if (!killed)
                {
                    if (stun > 0f) Kit.Ctx.Monsters.ApplyStun(m, stun);
                    continue;
                }

                // 传染：引爆致死 → 向最近的敌人再发一道同规格闪电，新打上的雷痕排进这次引爆
                if (infections >= maxInfections) continue;
                MonsterEntity next = NearestAlive(m.Position, MathF.Max(0.1f, Cfg.InfectionRange), m);
                if (next == null) continue;
                infections++;
                Chain(m.Position, next, true);
                for (int i = 0; i < _chainTargets.Count; i++)
                {
                    MonsterEntity t = _chainTargets[i];
                    if (t.Alive && t.LightningMarks > 0 && !_detonated.Contains(t.Id)) _queue.Enqueue(t);
                }
            }

            // 结算后全场雷痕清空（包括被引爆过的怪又被传染打上的那些）
            for (int i = 0; i < all.Count; i++) all[i].LightningMarks = 0;
            _queue.Clear();
        }
    }
}
