using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 浮游炮（11/25 双武器策划案）。
    /// 常驻：环绕玩家飞行，各自自动索敌、持续小额攻击。
    /// 翻滚（回到常态空间后）盲盒：按概率分裂（+SplitAdd 台）或合体（−MergeRemove 台），全部按加法结算；
    /// 合体最少保留 MinCount 台（「双核」：至少 2 台），MaxCount &gt; 0 时台数封顶。数值都在 F1 面板可调。
    /// 性能：同时真正开火的台数最多 MaxActive，超出的台数按比例折算进这些炮的伤害（总输出不变）。
    /// </summary>
    public sealed class DronesSubWeapon : SubWeaponBehavior
    {
        private readonly List<Vec2> _satellites = new List<Vec2>();
        private float[] _timers;
        private int _count;
        private float _angle;
        private float _haste;
        private float _hasteLeft;

        public DronesSubWeapon(WeaponKit kit) : base(kit)
        {
            _count = Clamp(Math.Max(Math.Max(1, Cfg.StartCount), Cfg.MinCount));
            _timers = new float[Math.Max(1, Cfg.MaxActive)];
        }

        private DronesConfig Cfg { get { return Kit.Arsenal.Drones; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.Drones; } }
        public override int UnitCount { get { return _count; } }
        public override IReadOnlyList<Vec2> Satellites { get { return _satellites; } }

        private int Active { get { return Math.Min(_count, _timers.Length); } }

        /// <summary>合体保底：MinCount，「双核」至少 2。</summary>
        private int Floor
        {
            get
            {
                int floor = Math.Max(1, Cfg.MinCount);
                if (Kit.Has(BuildId.DroneMinTwo)) floor = Math.Max(floor, 2);
                return floor;
            }
        }

        private int Clamp(int count)
        {
            if (Cfg.MaxCount > 0) count = Math.Min(count, Math.Max(1, Cfg.MaxCount));
            return Math.Max(1, count);
        }

        public override void Tick(float dt)
        {
            if (_hasteLeft > 0f)
            {
                _hasteLeft -= dt;
                if (_hasteLeft <= 0f) _haste = 0f;
            }
            int active = Active;
            _angle += Cfg.OrbitSpeed * dt;
            Vec2 player = Kit.PlayerPosition;
            _satellites.Clear();
            for (int i = 0; i < active; i++)
                _satellites.Add(player + Vec2.FromAngle(_angle + i * MathF.PI * 2f / active) * Cfg.OrbitRadius);

            if (!Kit.Ctx.Player.State.Alive) return;
            float interval = MathF.Max(0.05f, Cfg.FireInterval / MathF.Max(0.1f, 1f + Kit.Value(BuildId.DroneRate) + _haste));
            float damage = Cfg.Damage * (1f + Kit.Value(BuildId.DroneDamage)) * ((float)_count / active);
            if (Kit.Has(BuildId.DroneSwarmBonus) && _count >= Kit.Value2(BuildId.DroneSwarmBonus))
                damage *= 1f + Kit.Value(BuildId.DroneSwarmBonus);
            for (int i = 0; i < active; i++)
            {
                _timers[i] -= dt;
                if (_timers[i] > 0f) continue;
                MonsterEntity target = Kit.Ctx.Monsters.GetNearest(_satellites[i], Cfg.Range);
                if (target == null)
                {
                    _timers[i] = 0f;
                    continue;
                }
                Vec2 dir = WeaponKit.Direction(_satellites[i], target.Position);
                Kit.Ctx.Projectiles.SpawnSubWeapon(SubWeaponKind.Drones, _satellites[i], dir * MathF.Max(0.5f, Cfg.BulletSpeed),
                    Cfg.BulletRadius, damage, Cfg.Range + 1f);
                // 错开开火时间，避免所有炮同一帧齐射
                _timers[i] = interval * (0.9f + 0.2f * (float)Kit.Ctx.Random.NextDouble());
            }
        }

        public override bool OnProjectileHit(ProjectileEntity p, MonsterEntity m)
        {
            Kit.Hit(m, p.Damage, DamageSource.SubWeapon);
            if (!m.Alive && Kit.Chance(Kit.Value(BuildId.DroneKillSplit))) Split();
            if (Kit.Has(BuildId.DronePierce) && p.HitCount < 1)
            {
                p.HitCount++;
                return true;
            }
            return false;
        }

        public override void OnSubspaceExited()
        {
            float chance = MathF.Min(1f, Cfg.SplitChance + Kit.Value(BuildId.DroneSplitChance));
            if (Kit.Chance(chance)) Split();
            else Merge();
            float haste = Kit.Value(BuildId.DronePostHaste);
            if (haste > 0f)
            {
                _haste = haste;
                _hasteLeft = MathF.Max(0.1f, Kit.Value2(BuildId.DronePostHaste));
            }
        }

        private void Split()
        {
            int from = _count;
            _count = Clamp((int)Math.Min(int.MaxValue / 2, (long)_count + Math.Max(0, Cfg.SplitAdd)));
            if (_count == from) return; // 已到上限：不算分裂
            // 分裂冲击：分裂到新的数量时，对周身敌人造成一次范围伤害
            if (Kit.Has(BuildId.DroneSplitBlast))
            {
                float radius = MathF.Max(0.3f, Kit.Value2(BuildId.DroneSplitBlast));
                Kit.Ctx.Monsters.QueryCircle(Kit.PlayerPosition, radius, Kit.Hits2);
                for (int i = 0; i < Kit.Hits2.Count; i++)
                    if (Kit.Hits2[i].Alive) Kit.Hit(Kit.Hits2[i], Kit.Value(BuildId.DroneSplitBlast), DamageSource.SubWeapon);
                Kit.Hits2.Clear();
            }
            Kit.Ctx.Events.Publish(new DronesChangedEvent { From = from, To = _count, Split = true });
        }

        private void Merge()
        {
            int from = _count;
            int floor = Math.Min(Floor, from); // 本来就不到保底的不会被合体加回去
            _count = Clamp(Math.Max(floor, _count - Math.Max(0, Cfg.MergeRemove)));
            Kit.Ctx.Events.Publish(new DronesChangedEvent { From = from, To = _count, Split = false });
        }
    }
}
