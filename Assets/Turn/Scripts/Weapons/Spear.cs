using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 投矛（策划：武器 2）。
    /// 常态：自动向最近的敌人投矛（伤害 8）。命中后长矛插在敌人身上 = 叠 1 层矛印记；
    ///       若这一矛直接击杀目标，停止飞行；当前携带的矛与目标原有的矛一起留在死亡位置。
    ///       飞完射程没插中任何敌人的长矛直接消失。
    /// 爆发：地面与活怪身上的长矛同时向玩家方向高速回收，每根矛沿回收路径穿刺沿途所有敌人（回收伤害由配置决定），
    ///       结算后矛印记清空（参考男刀大招的回收逻辑）。
    /// </summary>
    public sealed class SpearWeapon : WeaponBehavior
    {
        private struct Host
        {
            public Vec2 Position;
            public int Spears;
        }

        private readonly List<Host> _hosts = new List<Host>();
        private readonly List<Host> _ground = new List<Host>();
        private readonly List<MonsterEntity> _targets = new List<MonsterEntity>();

        public SpearWeapon(WeaponKit kit) : base(kit)
        {
            Kit.Ctx.Events.Subscribe<SubspaceExitedEvent>(_ => _ground.Clear());
        }

        /// <summary>当前留在地面的矛总数；爆发回收或退出亚空间后清空。</summary>
        public int GroundSpearCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _ground.Count; i++) count += _ground[i].Spears;
                return count;
            }
        }

        private SpearConfig Cfg { get { return Kit.Arsenal.Spear; } }

        public override WeaponKind Kind { get { return WeaponKind.Spear; } }
        public override int BaseAmmo { get { return Cfg.Ammo; } }
        public override BuildId? AmmoBuild { get { return BuildId.SpearAmmo; } }
        public override BuildId? RateBuild { get { return BuildId.SpearRate; } }
        public override BuildId? PostHasteBuild { get { return BuildId.SpearPostHaste; } }
        public override BuildId? BurstRingBuild { get { return BuildId.SpearBurstRing; } }
        public override float BaseInterval { get { return Cfg.FireInterval; } }
        public override float TargetRange { get { return Cfg.Range; } }

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            int count = 1 + (int)MathF.Round(Kit.Value(BuildId.SpearDouble));
            // 双矛：尽量分别瞄准不同的敌人；敌人不够时按夹角散射
            Kit.Nearest(origin, Cfg.Range, count, _targets);
            if (_targets.Count == 0) _targets.Add(target);
            Vec2 baseDir = WeaponKit.Direction(origin, _targets[0].Position);
            for (int i = 0; i < count; i++)
            {
                Vec2 dir = i < _targets.Count
                    ? WeaponKit.Direction(origin, _targets[i].Position)
                    : WeaponKit.Rotate(baseDir, ((i % 2 == 0) ? 1f : -1f) * Cfg.SpreadDegrees * ((i + 1) / 2));
                ProjectileEntity p = Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Spear, origin, dir * MathF.Max(0.5f, Cfg.Speed),
                    Cfg.Radius, Cfg.Damage, Cfg.Range);
                p.CarriedMarks = 1;
            }
            _targets.Clear();
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            float falloff = MathF.Max(0f, MathF.Min(1f, Cfg.PierceFalloff - Kit.Value(BuildId.SpearFalloff)));
            float damage = p.Damage * MathF.Pow(1f - falloff, p.HitCount);
            p.HitCount++;
            // 死亡事件可能触发其它武器效果，先保存命中前已经插在目标身上的矛。
            int existing = Math.Max(0, m.SpearMarks);
            Kit.Hit(m, damage, DamageSource.Weapon);
            if (!m.Alive)
            {
                _ground.Add(new Host { Position = m.Position, Spears = Math.Max(1, p.CarriedMarks) + existing });
                m.SpearMarks = 0;
                p.CarriedMarks = 0;
                return false;
            }
            m.SpearMarks += Math.Max(1, p.CarriedMarks);
            // 连锁：有概率在相邻 1 名敌人身上也叠 1 层
            if (Kit.Chance(Kit.Value(BuildId.SpearChainMark)))
            {
                Kit.Nearest(m.Position, 2.5f, 1, Kit.Hits2, m);
                if (Kit.Hits2.Count > 0) Kit.Hits2[0].SpearMarks++;
                Kit.Hits2.Clear();
            }
            return false;
        }

        public override void Burst()
        {
            Vec2 player = Kit.PlayerPosition;
            _hosts.Clear();
            _hosts.AddRange(_ground);
            _ground.Clear();
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (m.Alive && m.SpearMarks > 0) _hosts.Add(new Host { Position = m.Position, Spears = m.SpearMarks });
            }
            if (_hosts.Count == 0) return;

            float width = MathF.Max(0.05f, Cfg.RecallWidth * (1f + Kit.Value(BuildId.SpearRecallWidth)));
            float baseDamage = Cfg.RecallDamage * (1f + Kit.Value(BuildId.SpearRecallDamage));
            float stackBonus = MathF.Max(0f, Cfg.StackBonus + Kit.Value(BuildId.SpearStackBonus));
            float ramp = Kit.Value(BuildId.SpearPathRamp);
            float stun = Kit.Value(BuildId.SpearStun);
            float dealt = 0f;

            for (int h = 0; h < _hosts.Count; h++)
            {
                Host host = _hosts[h];
                float perSpear = baseDamage * (1f + stackBonus * (host.Spears - 1));
                for (int s = 0; s < host.Spears; s++)
                {
                    Kit.Ctx.Monsters.QuerySegment(host.Position, player, width, Kit.Hits);
                    Vec2 from = host.Position;
                    Kit.Hits.Sort((a, b) => Vec2.SqrDistance(from, a.Position).CompareTo(Vec2.SqrDistance(from, b.Position)));
                    int hits = 0;
                    for (int k = 0; k < Kit.Hits.Count; k++)
                    {
                        MonsterEntity m = Kit.Hits[k];
                        if (!m.Alive) continue;
                        bool crit;
                        float mult = Kit.Crit(BuildId.SpearCrit, out crit);
                        // 串刺：同一根矛路径上每多命中 1 名敌人，伤害递增
                        dealt += Kit.Hit(m, perSpear * (1f + ramp * hits) * mult, DamageSource.Burst, crit);
                        if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
                        hits++;
                    }
                    Kit.Ctx.Events.Publish(new SpearRecalledEvent { From = host.Position, To = player, Width = width, Hits = hits });
                }
            }
            Kit.Hits.Clear();
            _hosts.Clear();

            // 结算后矛印记清空
            for (int i = 0; i < all.Count; i++) all[i].SpearMarks = 0;
            Kit.Lifesteal(dealt, BuildId.SpearLifesteal);
        }

        public override void CollectFields(List<WeaponField> results)
        {
            float radius = MathF.Max(.05f, Cfg.Radius);
            for (int i = 0; i < _ground.Count; i++)
            {
                Host host = _ground[i];
                for (int s = 0; s < host.Spears; s++)
                    results.Add(new WeaponField { Kind = WeaponFieldKind.GroundSpear, Position = host.Position,
                        End = host.Position + new Vec2(.15f * s + .1f, .65f), Radius = radius, Life = 1f });
            }
        }

        public override void OnHurt()
        {
            int marks = (int)MathF.Round(Kit.Value(BuildId.SpearHurtMarks));
            if (marks <= 0) return;
            Kit.Nearest(Kit.PlayerPosition, Kit.Arsenal.HurtMarkRange, 1, Kit.Hits2);
            if (Kit.Hits2.Count > 0) Kit.Hits2[0].SpearMarks += marks;
            Kit.Hits2.Clear();
        }
    }
}
