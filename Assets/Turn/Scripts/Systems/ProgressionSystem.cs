using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 经验、等级、击杀数、三选一升级，以及“全局升级等级表”。
    /// 等级表全局只有一份（_levels），武器 / 翻滚 / 子弹系统每次都现读它，
    /// 所以选择词条后立即生效，并且火球和鱼叉天然共享同一套强化 —— 不存在两套武器升级数据。
    /// </summary>
    public sealed class ProgressionSystem : IProgressionSystem
    {
        private readonly ITurnContext _ctx;
        private readonly UpgradeConfig _upgrades;
        private readonly ExperienceConfig _xp;
        private readonly int[] _levels = new int[BuildIds.Count];
        private readonly List<UpgradeOption> _options = new List<UpgradeOption>();
        private readonly List<BuildId> _candidates = new List<BuildId>();
        private static readonly int[] Rarities = { 0, 0, 1, 0, 0, 1, 1, 1, 0, 2, 1, 0, 0, 1, 1, 2, 1, 2, 0 };

        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int Kills { get; private set; }
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<UpgradeOption> Options { get { return _options; } }

        /// <summary>升到下一级所需经验 = 基础 + (等级 − 1) × 成长。</summary>
        public int XpToNext { get { return Level >= _xp.MaxLevel ? 0 : Math.Max(1, _xp.BaseXpToLevel + (Level - 1) * _xp.XpGrowthPerLevel); } }

        public ProgressionSystem(ITurnContext ctx, UpgradeConfig upgrades, ExperienceConfig xp)
        {
            _ctx = ctx;
            _upgrades = upgrades;
            _xp = xp;
            Level = 1;
        }

        public int GetLevel(UpgradeId id)
        {
            int raw = (int)id;
            // A 的 RollSystem 仍查询旧枚举；映射到 B13 / B14 / B15。
            if (id == UpgradeId.RollCooldown) return GetBuildLevel(BuildId.B13);
            if (id == UpgradeId.RollInvincible) return GetBuildLevel(BuildId.B14);
            if (id == UpgradeId.Shockwave) return GetBuildLevel(BuildId.B15);
            if (raw >= (int)BuildId.B01 && raw <= (int)BuildId.B19) return GetBuildLevel((BuildId)raw);
            return 0; // 旧扩容 / 灼烧不进入 Build 池。
        }

        private int GetBuildLevel(BuildId id)
        {
            int index = BuildIds.Index(id);
            return index >= 0 && index < _levels.Length ? _levels[index] : 0;
        }

        public void AddKill()
        {
            Kills++;
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            while (XpToNext > 0 && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                _ctx.Player.SetLevelStats(_xp.LevelOneMaxHp + (Level - 1) * _xp.MaxHpPerLevel,
                    _xp.LevelOneMoveSpeed + (Level - 1) * _xp.MoveSpeedPerLevel);
                PendingLevelUps++;
                _ctx.Events.Publish(new LeveledUpEvent { Level = Level });
            }
            _ctx.Events.Publish(new XpChangedEvent { Xp = Xp, XpToNext = XpToNext, Level = Level });
        }

        /// <summary>从未满级的词条里随机抽 OptionCount 个互不相同的。</summary>
        public void RollOptions()
        {
            _options.Clear();
            _candidates.Clear();
            for (int i = 0; i < BuildIds.Count; i++)
            {
                BuildId id = (BuildId)((int)BuildId.B01 + i);
                if (_levels[i] < MaxLevel(id)) _candidates.Add(id);
            }

            int take = Math.Min(Math.Max(1, _upgrades.OptionCount), _candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int total = 0;
                for (int j = 0; j < _candidates.Count; j++) total += Weight(_candidates[j]);
                if (total <= 0) break;
                int roll = _ctx.Random.Next(total);
                int pick = 0;
                for (; pick < _candidates.Count - 1; pick++)
                {
                    roll -= Weight(_candidates[pick]);
                    if (roll < 0) break;
                }
                BuildId chosen = _candidates[pick];
                _candidates.RemoveAt(pick);
                _options.Add(Describe(chosen));
            }
        }

        public bool TryApply(int index)
        {
            if (PendingLevelUps <= 0 || index < 0 || index >= _options.Count) return false;

            UpgradeOption option = _options[index];
            int i = BuildIds.Index(option.Build);
            if (i < 0 || i >= _levels.Length || _levels[i] >= MaxLevel(option.Build)) return false;

            _levels[i]++;
            PendingLevelUps--;
            _options.Clear();
            _ctx.Events.Publish(new UpgradeAppliedEvent { Id = option.Id, Level = _levels[i] });
            return true;
        }

        public void SkipPending()
        {
            if (PendingLevelUps > 0) PendingLevelUps--;
            _options.Clear();
        }

        private int MaxLevel(BuildId id)
        {
            int i = BuildIds.Index(id);
            return i >= 0 && i < _upgrades.BuildMaxLevels.Length ? Math.Max(0, _upgrades.BuildMaxLevels[i]) : 0;
        }

        private int Weight(BuildId id)
        {
            int rarity = Rarities[BuildIds.Index(id)];
            return Math.Max(0, rarity == 0 ? _upgrades.CommonWeight : rarity == 1 ? _upgrades.RareWeight : _upgrades.EpicWeight);
        }

        private UpgradeOption Describe(BuildId id)
        {
            int from = GetBuildLevel(id);
            var o = new UpgradeOption { Id = BuildIds.Query(id), Build = id, FromLevel = from, ToLevel = from + 1,
                Rarity = Rarities[BuildIds.Index(id)] == 0 ? "普通" : Rarities[BuildIds.Index(id)] == 1 ? "稀有" : "史诗" };
            switch (id)
            {
                case BuildId.B01: o.Name = "伤害强化"; o.Description = "两侧武器伤害 +20% / 层"; break;
                case BuildId.B02: o.Name = "攻速强化"; o.Description = "开火间隔 -12% / 层（下限 0.2 秒）"; break;
                case BuildId.B03: o.Name = "多重投射"; o.Description = "每次发射额外 +1 发投射物 / 层"; break;
                case BuildId.B04: o.Name = "弹速提升"; o.Description = "两侧弹速 +40% / 层"; break;
                case BuildId.B05: o.Name = "射程延长"; o.Description = "索敌射程 +25% / 层"; break;
                case BuildId.B06: o.Name = "命中减速"; o.Description = "命中目标减速 30%，持续 2 秒"; break;
                case BuildId.B07: o.Name = "生命汲取"; o.Description = "玩家武器伤害的 5% 回血 / 层"; break;
                case BuildId.B08: o.Name = "暴击"; o.Description = "暴击率 +15% / 层，暴击伤害 ×2"; break;
                case BuildId.B09: o.Name = "范围扩大"; o.Description = "爆炸、毒区、灼烧半径 +30% / 层"; break;
                case BuildId.B10: o.Name = "地形增伤"; o.Description = "目标在伤害地形中时武器伤害 +50% / 层"; break;
                case BuildId.B11: o.Name = "击杀回弹"; o.Description = "玩家武器击杀返还 1 发 / 层（弩与激光不生效）"; break;
                case BuildId.B12: o.Name = "经验吸引"; o.Description = "经验球拾取范围 +50% / 层"; break;
                case BuildId.B13: o.Name = "翻滚加速"; o.Description = "翻滚冷却 -15% / 层（下限 0.9 秒）"; break;
                case BuildId.B14: o.Name = "无敌延长"; o.Description = "翻滚无敌时间 +0.15 秒 / 层"; break;
                case BuildId.B15: o.Name = "翻滚冲击"; o.Description = "落地范围伤害 20，半径 2.5，并击退"; break;
                case BuildId.B16: o.Name = "传送余波"; o.Description = "切换后 0.5 秒内已存在弹幕伤害 +30%"; break;
                case BuildId.B17: o.Name = "惩罚减免"; o.Description = "翻滚刷怪 -40% / 层，至少保留 1 只"; break;
                case BuildId.B18: o.Name = "双程翻滚"; o.Description = "0.8 秒内可免费再翻一次；冷却 10 秒"; break;
                case BuildId.B19: o.Name = "弹幕留驻"; o.Description = "切换保留弹幕寿命再 +60% / 层"; break;
            }
            return o;
        }
    }
}
