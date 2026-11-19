using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class UpgradeSettings
    {
        [Tooltip("每次升级提供的候选数（互不相同）")]
        [Min(1)] public int optionCount = 3;

        [Header("1. 扩容弹匣（火球与鱼叉共用）")]
        [Min(0)] public int magazinePerLevel = 2;
        [Min(0)] public int magazineMaxLevel = 5;

        [Header("2. 攻击附加灼烧（火球与鱼叉共用）")]
        [Tooltip("每级灼烧每秒伤害")]
        public float burnDpsPerLevel = 4f;
        [Tooltip("灼烧持续时间（秒）")]
        public float burnDuration = 3f;
        [Min(0)] public int burnMaxLevel = 5;

        [Header("3. 翻滚 CD 缩短")]
        public float rollCooldownPerLevel = 0.4f;
        [Min(0)] public int rollCooldownMaxLevel = 5;

        [Header("4. 翻滚无敌时间延长")]
        public float rollInvinciblePerLevel = 0.15f;
        [Min(0)] public int rollInvincibleMaxLevel = 5;

        [Header("5. 翻滚落地冲击波")]
        public float shockwaveBaseRadius = 2.5f;
        public float shockwaveRadiusPerLevel = 0.5f;
        public float shockwaveBaseDamage = 20f;
        public float shockwaveDamagePerLevel = 10f;
        [Min(0)] public int shockwaveMaxLevel = 5;

        public UpgradeConfig ToConfig()
        {
            var c = new UpgradeConfig
            {
                OptionCount = optionCount,
                MagazinePerLevel = magazinePerLevel,
                BurnDpsPerLevel = burnDpsPerLevel,
                BurnDuration = burnDuration,
                RollCooldownPerLevel = rollCooldownPerLevel,
                RollInvinciblePerLevel = rollInvinciblePerLevel,
                ShockwaveBaseRadius = shockwaveBaseRadius,
                ShockwaveRadiusPerLevel = shockwaveRadiusPerLevel,
                ShockwaveBaseDamage = shockwaveBaseDamage,
                ShockwaveDamagePerLevel = shockwaveDamagePerLevel,
            };
            c.MaxLevels[(int)UpgradeId.Magazine] = magazineMaxLevel;
            c.MaxLevels[(int)UpgradeId.Burn] = burnMaxLevel;
            c.MaxLevels[(int)UpgradeId.RollCooldown] = rollCooldownMaxLevel;
            c.MaxLevels[(int)UpgradeId.RollInvincible] = rollInvincibleMaxLevel;
            c.MaxLevels[(int)UpgradeId.Shockwave] = shockwaveMaxLevel;
            return c;
        }
    }

    [Serializable]
    public sealed class ExperienceSettings
    {
        [Tooltip("升级经验阈值：1 级升 2 级所需经验")]
        [Min(1)] public int baseXpToLevel = 5;
        [Tooltip("经验成长参数：每升一级，下一级多需要的经验")]
        [Min(0)] public int xpGrowthPerLevel = 5;
        [Tooltip("拾取半径")]
        public float pickupRadius = 0.5f;
        [Tooltip("吸附半径：进入后经验球飞向玩家")]
        public float magnetRadius = 2f;
        public float magnetSpeed = 10f;

        public ExperienceConfig ToConfig()
        {
            return new ExperienceConfig
            {
                BaseXpToLevel = baseXpToLevel,
                XpGrowthPerLevel = xpGrowthPerLevel,
                PickupRadius = pickupRadius,
                MagnetRadius = magnetRadius,
                MagnetSpeed = magnetSpeed,
            };
        }
    }

    /// <summary>
    /// 升级与经验配置（节点 UpgradeManager）：五个词条的强化数值、经验阈值与成长、经验球 Prefab。
    /// 升级等级表在模拟层的 ProgressionSystem 里全局只有一份，火球与鱼叉共用。
    /// </summary>
    public sealed class UpgradeManager : MonoBehaviour
    {
        [Header("Prefab（生成器自动拖好）")]
        [SerializeField] private ExperienceOrb experienceOrbPrefab;

        [Header("升级词条")]
        [SerializeField] private UpgradeSettings upgrades = new UpgradeSettings();
        [Header("经验")]
        [SerializeField] private ExperienceSettings experience = new ExperienceSettings();

        public ExperienceOrb ExperienceOrbPrefab { get { return experienceOrbPrefab; } }
        public UpgradeSettings Upgrades { get { return upgrades; } }
        public ExperienceSettings Experience { get { return experience; } }
    }
}
