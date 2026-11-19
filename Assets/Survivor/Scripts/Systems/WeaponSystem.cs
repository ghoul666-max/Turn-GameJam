using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 已装备的武器：冷却计时与发动。发动的具体形式交给 WeaponData.Behavior（无状态）。
    /// 这里负责把角色属性（冷却 / 范围 / 数量）折算进武器数值；伤害的力量加成留给 CombatSystem。
    /// </summary>
    public sealed class WeaponSystem : IWeaponSystem
    {
        public const int MaxSlots = 6;
        /// <summary>新武器第一次发动前的等待，避免拿到手的同一帧就出手。</summary>
        public const float FirstFireDelay = 0.3f;

        private readonly ISurvivorContext _ctx;
        private readonly List<WeaponInstance> _owned = new List<WeaponInstance>();

        public IReadOnlyList<WeaponInstance> Owned { get { return _owned; } }
        public bool CanAddWeapon { get { return _owned.Count < MaxSlots; } }

        public WeaponSystem(ISurvivorContext ctx)
        {
            _ctx = ctx;
        }

        public WeaponInstance Get(string id)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Data.Id == id) return _owned[i];
            }
            return null;
        }

        public bool TryAdd(WeaponData data)
        {
            if (data == null || data.MaxLevel == 0 || !CanAddWeapon || Get(data.Id) != null) return false;
            _owned.Add(new WeaponInstance { Data = data, Level = 1, Timer = FirstFireDelay });
            return true;
        }

        public bool TryLevelUp(string id)
        {
            WeaponInstance weapon = Get(id);
            if (weapon == null || weapon.IsMaxLevel) return false;
            weapon.Level++;
            return true;
        }

        public EffectiveWeaponStats GetEffectiveStats(WeaponInstance weapon)
        {
            WeaponStats s = weapon.Stats;
            IPlayerSystem player = _ctx.Player;

            var e = new EffectiveWeaponStats();
            e.Damage = s.Damage;
            e.Cooldown = MathF.Max(0.1f, s.Cooldown * player.GetStat(StatId.Cooldown));
            e.Amount = Math.Max(1, s.Amount + (int)MathF.Round(player.GetStat(StatId.Amount)));
            e.Area = s.Area * player.GetStat(StatId.Area);
            e.Speed = s.Speed;
            e.Duration = s.Duration;
            e.Pierce = s.Pierce;
            e.Knockback = s.Knockback;
            return e;
        }

        public void Tick(float dt)
        {
            if (!_ctx.Player.State.Alive) return;

            for (int i = 0; i < _owned.Count; i++)
            {
                WeaponInstance weapon = _owned[i];
                weapon.Timer -= dt;
                if (weapon.Timer > 0f) continue;

                EffectiveWeaponStats stats = GetEffectiveStats(weapon);
                weapon.Timer += stats.Cooldown;
                if (weapon.Timer < 0f) weapon.Timer = stats.Cooldown; // 不补发积压的次数

                if (weapon.Data.Behavior == null) continue;
                weapon.Data.Behavior.Fire(new WeaponFireContext { World = _ctx, Weapon = weapon, Stats = stats });
            }
        }
    }
}
