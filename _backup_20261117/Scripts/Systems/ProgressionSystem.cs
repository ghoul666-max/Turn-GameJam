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
        private readonly int[] _levels = new int[UpgradeIds.Count];
        private readonly List<UpgradeOption> _options = new List<UpgradeOption>();
        private readonly List<UpgradeId> _candidates = new List<UpgradeId>();

        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int Kills { get; private set; }
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<UpgradeOption> Options { get { return _options; } }

        /// <summary>升到下一级所需经验 = 基础 + (等级 − 1) × 成长。</summary>
        public int XpToNext { get { return Math.Max(1, _xp.BaseXpToLevel + (Level - 1) * _xp.XpGrowthPerLevel); } }

        public ProgressionSystem(ITurnContext ctx, UpgradeConfig upgrades, ExperienceConfig xp)
        {
            _ctx = ctx;
            _upgrades = upgrades;
            _xp = xp;
            Level = 1;
        }

        public int GetLevel(UpgradeId id)
        {
            return _levels[(int)id];
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

        /// <summary>从未满级的词条里随机抽 OptionCount 个互不相同的。</summary>
        public void RollOptions()
        {
            _options.Clear();
            _candidates.Clear();
            for (int i = 0; i < UpgradeIds.Count; i++)
            {
                if (_levels[i] < MaxLevel((UpgradeId)i)) _candidates.Add((UpgradeId)i);
            }

            int take = Math.Min(Math.Max(1, _upgrades.OptionCount), _candidates.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = i + _ctx.Random.Next(_candidates.Count - i);
                UpgradeId chosen = _candidates[pick];
                _candidates[pick] = _candidates[i];
                _candidates[i] = chosen;
                _options.Add(Describe(chosen));
            }
        }

        public bool TryApply(int index)
        {
            if (PendingLevelUps <= 0 || index < 0 || index >= _options.Count) return false;

            UpgradeOption option = _options[index];
            int i = (int)option.Id;
            if (_levels[i] >= MaxLevel(option.Id)) return false;

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

        private int MaxLevel(UpgradeId id)
        {
            int i = (int)id;
            return i < _upgrades.MaxLevels.Length ? Math.Max(0, _upgrades.MaxLevels[i]) : 0;
        }

        private UpgradeOption Describe(UpgradeId id)
        {
            int from = _levels[(int)id];
            int to = from + 1;
            var o = new UpgradeOption { Id = id, FromLevel = from, ToLevel = to };
            switch (id)
            {
                case UpgradeId.Magazine:
                    o.Name = "扩容弹匣";
                    o.Description = "火球与鱼叉的弹匣容量 +" + _upgrades.MagazinePerLevel + "（下次翻滚时装满）";
                    break;
                case UpgradeId.Burn:
                    o.Name = "灼烧弹头";
                    o.Description = "火球与鱼叉命中附加灼烧：每秒 " + (to * _upgrades.BurnDpsPerLevel).ToString("0.#") +
                                    " 伤害，持续 " + _upgrades.BurnDuration.ToString("0.#") + " 秒";
                    break;
                case UpgradeId.RollCooldown:
                    o.Name = "疾速翻滚";
                    o.Description = "翻滚冷却 -" + _upgrades.RollCooldownPerLevel.ToString("0.##") + " 秒（最低 " +
                                    _ctx.Config.Roll.MinCooldown.ToString("0.##") + " 秒）";
                    break;
                case UpgradeId.RollInvincible:
                    o.Name = "残影";
                    o.Description = "翻滚无敌时间 +" + _upgrades.RollInvinciblePerLevel.ToString("0.##") + " 秒";
                    break;
                default:
                    float radius = _upgrades.ShockwaveBaseRadius + from * _upgrades.ShockwaveRadiusPerLevel;
                    float damage = _upgrades.ShockwaveBaseDamage + from * _upgrades.ShockwaveDamagePerLevel;
                    o.Name = "落地冲击波";
                    o.Description = "翻滚落地时对半径 " + radius.ToString("0.#") + " 内的怪物造成 " + damage.ToString("0") + " 伤害";
                    break;
            }
            return o;
        }
    }
}
