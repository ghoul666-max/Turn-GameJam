using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 经验、等级、击杀数与升级三选一（对应卡牌白模 RunSystem 的战后选卡）。
    /// 候选项：已有武器升级 / 新武器（有空位时）/ 已有被动升级 / 新被动（有空位时）；全部满级时给“回复生命”。
    /// </summary>
    public sealed class ProgressionSystem : IProgressionSystem
    {
        public const int OptionCount = 3;
        public const int HealOptionAmount = 30;

        private readonly ISurvivorContext _ctx;
        private readonly List<WeaponData> _weaponPool = new List<WeaponData>();
        private readonly List<PassiveData> _passivePool = new List<PassiveData>();
        private readonly List<UpgradeOption> _options = new List<UpgradeOption>();
        private readonly List<UpgradeOption> _candidates = new List<UpgradeOption>();

        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int Kills { get; private set; }
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<UpgradeOption> Options { get { return _options; } }

        /// <summary>升到下一级所需经验：5、15、25、35……</summary>
        public int XpToNext { get { return 5 + (Level - 1) * 10; } }

        public ProgressionSystem(ISurvivorContext ctx, IEnumerable<WeaponData> weaponPool, IEnumerable<PassiveData> passivePool)
        {
            _ctx = ctx;
            Level = 1;
            if (weaponPool != null) _weaponPool.AddRange(weaponPool);
            if (passivePool != null) _passivePool.AddRange(passivePool);
        }

        public void AddKill()
        {
            Kills++;
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingLevelUps++;
                _ctx.Events.Publish(new LeveledUpEvent { Level = Level });
            }
            _ctx.Events.Publish(new XpChangedEvent { Xp = Xp, XpToNext = XpToNext, Level = Level });
        }

        public void RollOptions()
        {
            _options.Clear();
            BuildCandidates();

            int take = Math.Min(OptionCount, _candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = i + _ctx.Random.Next(_candidates.Count - i);
                UpgradeOption chosen = _candidates[pick];
                _candidates[pick] = _candidates[i];
                _candidates[i] = chosen;
                _options.Add(chosen);
            }

            if (_options.Count == 0)
            {
                _options.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Heal,
                    Name = "烤鸡",
                    Description = "回复 " + HealOptionAmount + " 点生命。",
                    ArtKey = "heal",
                });
            }
        }

        public bool TryApply(int index)
        {
            if (PendingLevelUps <= 0 || index < 0 || index >= _options.Count) return false;

            UpgradeOption option = _options[index];
            bool ok;
            switch (option.Kind)
            {
                case UpgradeKind.NewWeapon: ok = _ctx.Weapons.TryAdd(option.Weapon); break;
                case UpgradeKind.WeaponLevel: ok = _ctx.Weapons.TryLevelUp(option.Weapon.Id); break;
                case UpgradeKind.NewPassive:
                case UpgradeKind.PassiveLevel: ok = _ctx.Player.TryAddOrLevelPassive(option.Passive); break;
                case UpgradeKind.Heal: _ctx.Combat.HealPlayer(HealOptionAmount); ok = true; break;
                default: ok = false; break;
            }
            if (!ok) return false;

            PendingLevelUps--;
            _options.Clear();
            _ctx.Events.Publish(new UpgradeAppliedEvent { Option = option });
            return true;
        }

        private void BuildCandidates()
        {
            _candidates.Clear();

            for (int i = 0; i < _weaponPool.Count; i++)
            {
                WeaponData data = _weaponPool[i];
                if (data == null || data.MaxLevel == 0) continue;
                WeaponInstance owned = _ctx.Weapons.Get(data.Id);
                if (owned != null)
                {
                    if (owned.IsMaxLevel) continue;
                    _candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.WeaponLevel,
                        Weapon = data,
                        FromLevel = owned.Level,
                        ToLevel = owned.Level + 1,
                        Name = data.Name,
                        Description = data.GetLevel(owned.Level + 1).Description,
                        ArtKey = data.ArtKey,
                    });
                }
                else if (_ctx.Weapons.CanAddWeapon)
                {
                    _candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.NewWeapon,
                        Weapon = data,
                        FromLevel = 0,
                        ToLevel = 1,
                        Name = data.Name,
                        Description = data.GetLevel(1).Description,
                        ArtKey = data.ArtKey,
                    });
                }
            }

            for (int i = 0; i < _passivePool.Count; i++)
            {
                PassiveData data = _passivePool[i];
                if (data == null) continue;
                PassiveInstance owned = _ctx.Player.GetPassive(data.Id);
                if (owned != null && owned.IsMaxLevel) continue;
                if (owned == null && !_ctx.Player.CanAddPassive) continue;

                int from = owned != null ? owned.Level : 0;
                _candidates.Add(new UpgradeOption
                {
                    Kind = owned != null ? UpgradeKind.PassiveLevel : UpgradeKind.NewPassive,
                    Passive = data,
                    FromLevel = from,
                    ToLevel = from + 1,
                    Name = data.Name,
                    Description = data.Description,
                    ArtKey = data.ArtKey,
                });
            }
        }
    }
}
