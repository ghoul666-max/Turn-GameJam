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
        // B10 地形增伤暂设 0（不进池），等策划定危险地形规则后再开（原值 2）。
        public int[] buildMaxLevels = { 5, 4, 2, 3, 3, 2, 3, 2, 3, 0, 2, 3, 3, 2, 2, 1, 2, 1, 2 };
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

        [Header("3. 旧：翻滚 CD 缩短（已不使用，B13 改读 rollCooldownReductionPerLevel）")]
        public float rollCooldownPerLevel = 0.4f;
        [Min(0)] public int rollCooldownMaxLevel = 5;

        [Header("4. B14 无敌延长（每层秒数）")]
        public float rollInvinciblePerLevel = 0.15f;
        [Min(0)] public int rollInvincibleMaxLevel = 5;

        [Header("5. B15 翻滚冲击（1 层 = 基础值，之后每层递增）")]
        public float shockwaveBaseRadius = 2.5f;
        public float shockwaveRadiusPerLevel = 0.5f;
        public float shockwaveBaseDamage = 20f;
        public float shockwaveDamagePerLevel = 10f;
        [Tooltip("B15 击退距离（世界单位）")]
        [Min(0f)] public float shockwaveKnockback = 1f;
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
                ShockwaveKnockback = shockwaveKnockback,
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
        [Header("新策划案（11/24）")]
        [Tooltip("1 级升 2 级所需经验（策划 50）")]
        [Min(1)] public int firstLevelXp = 50;
        [Tooltip("每级所需经验递增比例（策划 0.2 = 20%）")]
        [Min(0f)] public float xpGrowthRate = 0.2f;
        [Tooltip("经验拾取范围（单位，策划 100 像素 = 2.5）：进入后经验球飞向玩家")]
        [Min(0.1f)] public float pickupRange = 2.5f;
        [Tooltip("宝箱：玩家靠近到这个距离自动打开（单位）")]
        [Min(0.1f)] public float chestOpenRadius = 1f;
        [Header("旧方案")]
        [Tooltip("升级经验阈值：1 级升 2 级所需经验")]
        [Min(1)] public int baseXpToLevel = 4;
        [Tooltip("经验成长参数：每升一级，下一级多需要的经验")]
        [Min(0)] public int xpGrowthPerLevel = 4;
        [Header("等级属性成长")]
        [Tooltip("旧方案字段，不再读取：1 级生命以主角的 maxHp（20）为准")]
        public float levelOneMaxHp = 20f;
        [Tooltip("每升 1 级生命上限 +（11/27：4）")]
        [Min(0f)] public float maxHpPerLevel = 4f;
        [Tooltip("生命上限封顶（11/27：100；0 = 不封顶）")]
        [Min(0f)] public float maxHpCap = 100f;
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
                LevelOneMaxHp = levelOneMaxHp, MaxHpPerLevel = maxHpPerLevel, MaxHpCap = maxHpCap,
                LevelOneMoveSpeed = levelOneMoveSpeed, MoveSpeedPerLevel = moveSpeedPerLevel,
                MaxLevel = maxLevel,
                PickupRadius = pickupRadius,
                MagnetRadius = magnetRadius,
                MagnetSpeed = magnetSpeed > 0f ? magnetSpeed : 10f,
                FirstLevelXp = firstLevelXp > 0 ? firstLevelXp : 50,
                XpGrowthRate = xpGrowthRate >= 0f ? xpGrowthRate : 0.2f,
                PickupRange = pickupRange > 0f ? pickupRange : 2.5f,
                ChestOpenRadius = chestOpenRadius > 0f ? chestOpenRadius : 1f,
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

        [Header("新策划案（11/24）· 三把武器的词条表 + 宝箱")]
        [Tooltip("Builds 按 BuildId 顺序（水枪 17 条 → 投矛 15 条 → 陌刀 14 条）：MaxLevel 最大层数、Value 每层效果、Value2 附加参数。名字和说明见 Weapons/WeaponBuilds.cs")]
        [SerializeField] private BuildConfig builds = new BuildConfig();

        [Header("旧方案（不再使用）· 升级词条")]
        [SerializeField] private UpgradeSettings upgrades = new UpgradeSettings();
        [Header("经验")]
        [SerializeField] private ExperienceSettings experience = new ExperienceSettings();

        public ExperienceOrb ExperienceOrbPrefab { get { return experienceOrbPrefab; } }
        public UpgradeSettings Upgrades { get { return upgrades; } }
        public BuildConfig Builds { get { return builds; } }
        public ExperienceSettings Experience { get { return experience; } }
    }
}