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
        [Header("B01-B19 最大层数")]
        public int[] buildMaxLevels = { 5, 4, 2, 3, 3, 2, 3, 2, 3, 2, 2, 3, 3, 2, 2, 1, 2, 1, 2 };
        [Header("普通 / 稀有 / 史诗权重")]
        [Min(0)] public int commonWeight = 70, rareWeight = 25, epicWeight = 5;
        [Header("通用武器词条数值")]
        public float damagePerLevel = .20f, fireIntervalReductionPerLevel = .12f, minFireInterval = .2f;
        public int extraProjectilePerLevel = 1;
        public float multiShotSpreadDegrees = 8f;
        public float projectileSpeedPerLevel = .40f, targetRangePerLevel = .25f;
        public float slowOnHit = .30f, slowDuration = 2f, lifestealPerLevel = .05f;
        public float criticalChancePerLevel = .15f, criticalMultiplier = 2f;
        public float areaRadiusPerLevel = .30f, hazardDamagePerLevel = .50f;
        public int killAmmoRefundPerLevel = 1;
        public float experienceMagnetPerLevel = .50f, rollCooldownReductionPerLevel = .15f;
        public float planeSwitchProjectileDamageBonus = .30f, planeSwitchBonusDuration = .5f;
        public float penaltyReductionPerLevel = .40f, doubleRollWindow = .8f, doubleRollCooldown = 10f;
        public float projectileLifetimeBonusPerLevel = .60f;

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
                CommonWeight = commonWeight, RareWeight = rareWeight, EpicWeight = epicWeight,
                DamagePerLevel = damagePerLevel, FireIntervalReductionPerLevel = fireIntervalReductionPerLevel,
                MinFireInterval = minFireInterval, ExtraProjectilePerLevel = extraProjectilePerLevel,
                MultiShotSpreadDegrees = multiShotSpreadDegrees,
                ProjectileSpeedPerLevel = projectileSpeedPerLevel, TargetRangePerLevel = targetRangePerLevel,
                SlowOnHit = slowOnHit, SlowDuration = slowDuration, LifestealPerLevel = lifestealPerLevel,
                CriticalChancePerLevel = criticalChancePerLevel, CriticalMultiplier = criticalMultiplier,
                AreaRadiusPerLevel = areaRadiusPerLevel, HazardDamagePerLevel = hazardDamagePerLevel,
                KillAmmoRefundPerLevel = killAmmoRefundPerLevel, ExperienceMagnetPerLevel = experienceMagnetPerLevel,
                RollCooldownReductionPerLevel = rollCooldownReductionPerLevel,
                PlaneSwitchProjectileDamageBonus = planeSwitchProjectileDamageBonus,
                PlaneSwitchBonusDuration = planeSwitchBonusDuration,
                PenaltyReductionPerLevel = penaltyReductionPerLevel,
                DoubleRollWindow = doubleRollWindow, DoubleRollCooldown = doubleRollCooldown,
                ProjectileLifetimeBonusPerLevel = projectileLifetimeBonusPerLevel,
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
            // 旧场景资源的新字段未被序列化时 Unity 可能提供 0，回退策划默认值。
            if (c.BuildMaxLevels == null || c.BuildMaxLevels.Length != BuildIds.Count)
                c.BuildMaxLevels = new UpgradeConfig().BuildMaxLevels;
            if (c.CommonWeight + c.RareWeight + c.EpicWeight <= 0)
            {
                c.CommonWeight = 70; c.RareWeight = 25; c.EpicWeight = 5;
            }
            if (c.MinFireInterval <= 0f) c.MinFireInterval = .2f;
            if (c.CriticalMultiplier <= 0f) c.CriticalMultiplier = 2f;
            if (c.DamagePerLevel <= 0f) c.DamagePerLevel = .2f;
            if (c.FireIntervalReductionPerLevel <= 0f) c.FireIntervalReductionPerLevel = .12f;
            if (c.ExtraProjectilePerLevel <= 0) c.ExtraProjectilePerLevel = 1;
            if (c.TargetRangePerLevel <= 0f) c.TargetRangePerLevel = .25f;
            if (c.ProjectileSpeedPerLevel <= 0f) c.ProjectileSpeedPerLevel = .4f;
            if (buildMaxLevels != null)
                for (int i = 0; i < c.BuildMaxLevels.Length && i < buildMaxLevels.Length; i++)
                    c.BuildMaxLevels[i] = Mathf.Max(0, buildMaxLevels[i]);
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
        [Min(1)] public int baseXpToLevel = 4;
        [Tooltip("经验成长参数：每升一级，下一级多需要的经验")]
        [Min(0)] public int xpGrowthPerLevel = 4;
        [Header("等级属性成长")]
        public float levelOneMaxHp = 100f, maxHpPerLevel = 10f;
        public float levelOneMoveSpeed = 4.2f, moveSpeedPerLevel = .08f;
        [Min(1)] public int maxLevel = 12;
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
                LevelOneMaxHp = levelOneMaxHp, MaxHpPerLevel = maxHpPerLevel,
                LevelOneMoveSpeed = levelOneMoveSpeed, MoveSpeedPerLevel = moveSpeedPerLevel,
                MaxLevel = maxLevel,
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
