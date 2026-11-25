using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Weapons;

namespace Turn.Systems
{
    /// <summary>
    /// 经验、等级、击杀数、词条等级表、升级三选一与宝箱三选一（新策划案 11/24）。
    ///   · 升级：初始 50 经验，每级所需经验递增 20%；经验满 → PendingLevelUps +1，Session 弹窗（全局暂停）。
    ///     选项 = 当前装备武器的词条池里随机 3 个未满级的（不重复）。
    ///   · 宝箱：打开 → PendingChests +1；选项 = 2 个强化（一次 +ChestLevels 层）+ 1 个代价选项
    ///     （随机一种代价 +1 层，换取随机 CurseBonusLevels 层当前武器的强化）—— 策划：选项不全为正向强化。
    /// 词条等级表全局一份（三把武器的词条分开计），武器每次现读，选择后立即生效。
    /// </summary>
    public sealed class ProgressionSystem : IProgressionSystem
    {
        private readonly ITurnContext _ctx;
        private readonly BuildConfig _builds;
        private readonly BuildDef[] _defs;
        private readonly ExperienceConfig _xp;
        private readonly int[] _levels = new int[BuildIds.Count];
        private readonly int[] _curses = new int[ChestCurses.Count];
        private readonly List<UpgradeOption> _options = new List<UpgradeOption>();
        private readonly List<BuildId> _candidates = new List<BuildId>();
        private readonly Queue<int> _chestRanks = new Queue<int>();

        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int Kills { get; private set; }
        public int PendingLevelUps { get; private set; }
        public int PendingChests { get; private set; }
        public bool OptionsFromChest { get; private set; }
        public IReadOnlyList<UpgradeOption> Options { get { return _options; } }

        /// <summary>升到下一级所需经验 = 初始 × (1 + 递增比例)^(等级 − 1)。</summary>
        public int XpToNext
        {
            get
            {
                double need = Math.Max(1, _xp.FirstLevelXp) * Math.Pow(1.0 + Math.Max(0f, _xp.XpGrowthRate), Level - 1);
                return (int)Math.Min(int.MaxValue / 2, Math.Max(1.0, Math.Round(need)));
            }
        }

        public ProgressionSystem(ITurnContext ctx, BuildConfig builds, ExperienceConfig xp)
        {
            _ctx = ctx;
            _builds = builds ?? new BuildConfig();
            _defs = BuildCatalog.Normalize(_builds.Builds);
            _builds.Builds = _defs;
            _xp = xp ?? new ExperienceConfig();
            Level = 1;
            ctx.Events.Subscribe<ChestOpenedEvent>(evt => _chestRanks.Enqueue(evt.Rank));
        }

        public int GetLevel(BuildId id)
        {
            int i = (int)id;
            return i >= 0 && i < _levels.Length ? _levels[i] : 0;
        }

        public float Value(BuildId id)
        {
            int i = (int)id;
            if (i < 0 || i >= _levels.Length || _levels[i] <= 0) return 0f;
            return _levels[i] * _defs[i].Value;
        }

        public float Value2(BuildId id)
        {
            int i = (int)id;
            return i >= 0 && i < _defs.Length ? _defs[i].Value2 : 0f;
        }

        public int CurseStacks(ChestCurse curse)
        {
            int i = (int)curse;
            return i > 0 && i < _curses.Length ? _curses[i] : 0;
        }

        public int MaxLevel(BuildId id)
        {
            int i = (int)id;
            return i >= 0 && i < _defs.Length ? Math.Max(0, _defs[i].MaxLevel) : 0;
        }

        public void AddKill() { Kills++; }

        public void AddChest() { PendingChests++; }

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

        // ---------------- 选项 ----------------

        /// <summary>
        /// 候选池 = 这一局带着的全部装备的词条（11/25 双武器策划案）：
        /// 方案 A 两把主武器的词条混在一起；方案 B 主武器 + 副武器的词条混在一起。只收未满级的。
        /// </summary>
        private void CollectCandidates()
        {
            _candidates.Clear();
            for (int i = 0; i < BuildIds.Count; i++)
            {
                var id = (BuildId)i;
                if (id == BuildId.PalmAngle || id == BuildId.PalmPierce) continue;
                if (_levels[i] >= MaxLevel(id)) continue;
                if (IsCarried(id)) _candidates.Add(id);
            }
        }

        public bool IsCarried(BuildId id)
        {
            ILoadoutSystem loadout = _ctx.Loadout;
            return BuildIds.IsSub(id)
                ? loadout.Sub != SubWeaponKind.None && BuildIds.SubOf(id) == loadout.Sub
                : loadout.Carries(BuildIds.WeaponOf(id));
        }

        public void DebugSetLevel(BuildId id, int level)
        {
            int i = (int)id;
            if (i < 0 || i >= _levels.Length) return;
            int next = Math.Max(0, Math.Min(MaxLevel(id), level));
            if (next == _levels[i]) return;
            _levels[i] = next;
            _ctx.Events.Publish(new UpgradeAppliedEvent { Build = id, Level = next, Curse = ChestCurse.None });
        }

        /// <summary>出战装备的显示名（「水枪 + 投矛」「掌 + 引力黑洞」）。</summary>
        private string LoadoutName()
        {
            ILoadoutSystem loadout = _ctx.Loadout;
            if (loadout.Scheme == LoadoutScheme.DualMain)
                return WeaponKinds.DisplayName(loadout.Equipped) + " / " + WeaponKinds.DisplayName(loadout.Standby);
            return loadout.Sub != SubWeaponKind.None
                ? WeaponKinds.DisplayName(loadout.Equipped) + " / " + SubWeaponKinds.DisplayName(loadout.Sub)
                : WeaponKinds.DisplayName(loadout.Equipped);
        }

        private BuildId TakeRandomCandidate()
        {
            int pick = _ctx.Random.Next(_candidates.Count);
            BuildId id = _candidates[pick];
            _candidates.RemoveAt(pick);
            return id;
        }

        public void RollOptions()
        {
            _options.Clear();
            OptionsFromChest = false;
            CollectCandidates();
            int take = Math.Min(Math.Max(1, _builds.OptionCount), _candidates.Count);
            for (int i = 0; i < take; i++) _options.Add(Describe(TakeRandomCandidate(), 1, "强化"));
        }

        public void RollChestOptions()
        {
            _options.Clear();
            OptionsFromChest = true;
            WeaponKind weapon = _ctx.Loadout.Equipped;
            CollectCandidates();
            bool bossChest = _chestRanks.Count > 0 && _chestRanks.Peek() >= 2;
            if (bossChest)
            {
                int take = Math.Min(3, _candidates.Count);
                for (int i = 0; i < take; i++) _options.Add(Describe(TakeRandomCandidate(), Math.Max(1, _builds.ChestLevels), "Boss宝箱·定向"));
                return; // 品质表和全满补偿尚待用户/A提供，不臆造稀有度。
            }
            int total = Math.Max(1, _builds.OptionCount);
            int normal = Math.Min(Math.Max(0, total - 1), _candidates.Count);
            for (int i = 0; i < normal; i++) _options.Add(Describe(TakeRandomCandidate(), Math.Max(1, _builds.ChestLevels), "宝箱"));
            CollectCandidates();
            if (_candidates.Count > 0) _options.Add(DescribeCurse((ChestCurse)(1 + _ctx.Random.Next(ChestCurses.Count - 1)), weapon));
        }

        public bool TryApply(int index)
        {
            int pending = OptionsFromChest ? PendingChests : PendingLevelUps;
            if (pending <= 0 || index < 0 || index >= _options.Count) return false;
            UpgradeOption o = _options[index];

            if (o.Curse != ChestCurse.None)
            {
                ApplyCurse(o.Curse);
                GrantRandomLevels(o.Weapon, o.BonusLevels);
            }
            else
            {
                int i = (int)o.Build;
                if (i < 0 || i >= _levels.Length || _levels[i] >= MaxLevel(o.Build)) return false;
                _levels[i] = Math.Min(MaxLevel(o.Build), _levels[i] + Math.Max(1, o.ToLevel - o.FromLevel));
            }

            if (OptionsFromChest)
            {
                PendingChests--;
                if (_chestRanks.Count > 0) _chestRanks.Dequeue();
            }
            else PendingLevelUps--;
            _options.Clear();
            _ctx.Events.Publish(new UpgradeAppliedEvent { Build = o.Build, Level = GetLevel(o.Build), Curse = o.Curse });
            return true;
        }

        public void SkipPending()
        {
            if (PendingLevelUps > 0) PendingLevelUps--;
            else if (PendingChests > 0)
            {
                PendingChests--;
                if (_chestRanks.Count > 0) _chestRanks.Dequeue();
            }
            _options.Clear();
        }

        private void ApplyCurse(ChestCurse curse)
        {
            int i = (int)curse;
            if (i <= 0 || i >= _curses.Length) return;
            _curses[i]++;
            if (curse == ChestCurse.Frail)
            {
                PlayerState p = _ctx.Player.State;
                _ctx.Player.SetLevelStats(MathF.Max(10f, p.MaxHp - MathF.Max(0f, _builds.FrailMaxHp)), 0f);
            }
        }

        private void GrantRandomLevels(WeaponKind weapon, int levels)
        {
            for (int n = 0; n < levels; n++)
            {
                CollectCandidates();
                if (_candidates.Count == 0) return;
                BuildId id = _candidates[_ctx.Random.Next(_candidates.Count)];
                _levels[(int)id]++;
            }
        }

        private UpgradeOption Describe(BuildId id, int levels, string tag)
        {
            int from = GetLevel(id);
            int to = Math.Min(MaxLevel(id), from + levels);
            return new UpgradeOption
            {
                Build = id,
                Weapon = BuildIds.IsSub(id) ? _ctx.Loadout.Equipped : BuildIds.WeaponOf(id),
                FromLevel = from,
                ToLevel = to,
                Name = BuildCatalog.Name(id),
                Description = BuildCatalog.Describe(id, _defs[(int)id], to) + (to - from > 1 ? "（一次 +" + (to - from) + " 层）" : ""),
                Tag = tag + "·" + BuildIds.OwnerName(id),
            };
        }

        private UpgradeOption DescribeCurse(ChestCurse curse, WeaponKind weapon)
        {
            string cost;
            string name;
            switch (curse)
            {
                case ChestCurse.Frail:
                    name = "血契";
                    cost = "最大生命 −" + MathF.Round(_builds.FrailMaxHp).ToString("0");
                    break;
                case ChestCurse.Fury:
                    name = "狂怒";
                    cost = "怪物伤害 +" + MathF.Round(_builds.FuryDamage * 100f).ToString("0") + "%";
                    break;
                default:
                    name = "疾行";
                    cost = "怪物移速 +" + MathF.Round(_builds.HasteSpeed * 100f).ToString("0") + "%";
                    break;
            }
            int bonus = Math.Max(1, _builds.CurseBonusLevels);
            return new UpgradeOption
            {
                Weapon = weapon,
                Curse = curse,
                BonusLevels = bonus,
                Name = name,
                Description = "随机获得 " + bonus + " 层强化（" + LoadoutName() + "）；代价：" + cost + "（本局永久，可叠加）",
                Tag = "代价",
                FromLevel = CurseStacks(curse),
                ToLevel = CurseStacks(curse) + 1,
            };
        }
    }
}
