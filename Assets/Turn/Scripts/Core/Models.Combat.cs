using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 战斗数据（程序B）：武器、怪物、刷怪、升级、经验。世界数据在 Models.World.cs（程序A）。
    // 配置数据：由 Unity 层的 TurnConfigReader 从 Inspector 参数转换而来，运行时只读。
    // 默认值 = 题目给出的推荐手感参数。
    // ==================================================================

    /// <summary>两种武器形态共用的弹匣与射击节奏（全局一份，升级直接作用于它）。</summary>
    public sealed class WeaponConfig
    {
        public int MagazineSize = 6;
        /// <summary>每次翻滚补足当前武器弹药；完整六位面可各自覆盖容量。</summary>
        public int[] PlaneCapacities = { 8, 8, 8, 8, 1, 1 };
        public int AmmoPerShot = 1;
        /// <summary>开局弹药；&lt; 0 表示满弹匣。</summary>
        public int InitialAmmo = -1;
        public float FireInterval = 0.6f;
        public float[] PlaneFireIntervals = { .7f, .9f, .45f, .6f, 2f, 2f };
        public float[] PlaneTargetRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        public float[] PlaneAttackRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        /// <summary>原始与未来每次成功翻滚只能重新获得一发。</summary>
        public int SingleShotCapacity = 1;
        /// <summary>自动索敌半径。</summary>
        public float TargetRange = 8f;
        /// <summary>攻击范围：子弹最远飞行距离。</summary>
        public float AttackRange = 11f;
        /// <summary>投射物最长存活秒数；射程上限仍由 AttackRange 独立控制。</summary>
        public float ProjectileLifetime = 3f;
        /// <summary>首次切离发射位面时剩余寿命的倍率。</summary>
        public float PlaneSwitchLifetimeMultiplier = 1.6f;
    }

    /// <summary>某张地图绑定的武器形态（火山 = 火球，海洋 = 鱼叉）。</summary>
    public sealed class WeaponFormConfig
    {
        public PlaneId Map;
        public string Name = "";
        public float ProjectileSpeed = 10f;
        public float Damage = 10f;
        public float Radius = 0.2f;
        /// <summary>可击中的怪物数（≥ 1）。</summary>
        public int Pierce = 1;
        public float SplashRadius = 1.2f;
        public float BurnDps = 6f, BurnDuration = 2f;
        public float PoisonDps = 5f, PoisonDuration = 3f;
        public int PoisonMaxStacks = 3;
        public float HarpoonFalloff = .30f;
    }

    /// <summary>怪物原型；数值稳定用于跨位面一一映射，不修改 PlaneId 的序列化值。</summary>
    public enum MonsterKind
    {
        StoneGolem, Anglerfish, NullPointer, Bug, Buglet, Alarm, BlackPot,
        RockThrower, VineDemon, Boar, FutureSoldier, RepairRobot, DataWorm
    }

    /// <summary>纯数据怪物原型；MonsterSystem 只读，Inspector/数值面板在开局写入配置。</summary>
    [Serializable]
    public sealed class MonsterArchetype
    {
        public float MaxHp;
        public float Speed;
        public float Damage;
        public float AttackInterval;
        public float Windup;
        public float Range;
        public int Xp;
    }

    public sealed class MonsterConfig
    {
        public float MaxHp = 30f;
        public float Speed = 2f;
        /// <summary>火山石头人的最大生命（Inspector：MonsterPrefab 的 MonsterSettings.volcanoMaxHp；F1 面板可调）。</summary>
        public float VolcanoMaxHp = 60f;
        /// <summary>海洋安康鱼的最大生命（Inspector：MonsterPrefab 的 MonsterSettings.oceanMaxHp；F1 面板可调）。</summary>
        public float OceanMaxHp = 45f;

        public float ContactDamage = 8f;
        public float ContactInterval = 0.6f;
        public float Radius = 0.4f;
        /// <summary>切到火山地图时，站在岩浆范围内的怪物受到的一次性伤害（之后岩浆消失）。</summary>
        public float LavaHitDamage = 20f;
        public int Xp = 1;
        /// <summary>各形态数值：MonsterCatalog.CreateDefaults 按设计表建立，改动需通过 Inspector/面板透传。</summary>
        public MonsterArchetype[] Archetypes = Turn.Monsters.MonsterCatalog.CreateDefaults();
        public float SplitInterval = 3f;
        public int BugletLimitPerParent = 6;
        public float BubbleSpeed = 3f;
        public float EnemyProjectileLifetime = 3f;
        public float EnemyProjectileRadius = .22f;
        public float StoneRecovery = .5f;
        public float ChargeSpeed = 5.5f;
        public float ChargeDuration = .8f;
        public float RobotChargeSpeed = 4.8f;
        public float RobotKnockbackDistance = 1.5f;
        public float BoarChargeSpeed = 5f;
        public float BoarStunDuration = 2f;
        public float DataWormExplosionRadius = 1f;
        public float LaserDuration = 1f;
        public float LaserWarning = .4f;
        public float LaserWidth = .18f;
        public float CodeShotInterval = .18f;
        public float RockWarning = .3f;
        public float RockImpactRadius = .7f;
        public float AlarmAuraRadius = 2f;
        public int CodeBurstShots = 3;
    }

    public sealed class SpawnConfig
    {
        public int InitialCount = 6;
        public float Interval = 2.5f;
        public int CountPerSpawn = 2;
        /// <summary>每隔多少秒每批多刷 RampAmount 只。</summary>
        public float RampInterval = 30f;
        public int RampAmount = 1;
        /// <summary>常规刷怪的存活上限（翻滚惩罚不受此限制）。</summary>
        public int MaxAlive = 120;
        public float MinDistance = 7f;
        public float MaxDistance = 11f;
        /// <summary>骨甲野猪/数据蠕虫 P2 进入常规刷新池的时间。</summary>
        public float P2StartSeconds = 90f;
        /// <summary>每张位面的怪物候选权重；负值表示禁用，0 表示采用默认等权。</summary>
        public float[] KindWeights = new float[13];
    }

    public sealed class PenaltyConfig
    {
        /// <summary>每次成功翻滚固定新增的怪物数量。</summary>
        public int Count = 3;
        public float MinDistance = 4f;
        public float MaxDistance = 8f;
        public PenaltyArea Area = PenaltyArea.AroundPlayer;
    }

    public sealed class UpgradeConfig
    {
        public int OptionCount = 3;
        /// <summary>按 B01..B19 顺序配置的最大叠层。B10 地形增伤暂设 0（不进池）：岩浆喷发即消失，等策划定危险地形规则后再开（原值 2）。</summary>
        public int[] BuildMaxLevels = { 5, 4, 2, 3, 3, 2, 3, 2, 3, 0, 2, 3, 3, 2, 2, 1, 2, 1, 2 };
        /// <summary>词条稀有度权重：普通、稀有、史诗。</summary>
        public int CommonWeight = 70, RareWeight = 25, EpicWeight = 5;
        public float DamagePerLevel = 0.20f;
        public float FireIntervalReductionPerLevel = 0.12f;
        public float MinFireInterval = 0.2f;
        public int ExtraProjectilePerLevel = 1;
        public float MultiShotSpreadDegrees = 8f;
        public float ProjectileSpeedPerLevel = 0.40f;
        public float TargetRangePerLevel = 0.25f;
        public float SlowOnHit = 0.30f, SlowDuration = 2f;
        public float LifestealPerLevel = 0.05f;
        public float CriticalChancePerLevel = 0.15f, CriticalMultiplier = 2f;
        public float AreaRadiusPerLevel = 0.30f;
        public float HazardDamagePerLevel = 0.50f;
        public int KillAmmoRefundPerLevel = 1;
        public float ExperienceMagnetPerLevel = 0.50f;
        public float RollCooldownReductionPerLevel = 0.15f;
        public float ShockwaveKnockback = 1f;
        public float PlaneSwitchProjectileDamageBonus = 0.30f, PlaneSwitchBonusDuration = 0.5f;
        public float PenaltyReductionPerLevel = 0.40f;
        public float DoubleRollWindow = 0.8f, DoubleRollCooldown = 10f;
        public float ProjectileLifetimeBonusPerLevel = 0.60f;
        /// <summary>按 (int)UpgradeId 下标保留旧配置，仅供旧场景的序列化字段兼容。</summary>
        public readonly int[] MaxLevels = { 5, 5, 5, 5, 5 };

        public int MagazinePerLevel = 2;
        public float BurnDpsPerLevel = 4f;
        public float BurnDuration = 3f;
        public float RollCooldownPerLevel = 0.4f;
        public float RollInvinciblePerLevel = 0.15f;
        public float ShockwaveBaseRadius = 2.5f;
        public float ShockwaveRadiusPerLevel = 0.5f;
        public float ShockwaveBaseDamage = 20f;
        public float ShockwaveDamagePerLevel = 10f;
    }

    public sealed class ExperienceConfig
    {
        /// <summary>1 级升 2 级所需经验。</summary>
        public int BaseXpToLevel = 4;
        /// <summary>每升一级所需经验每级增长 4。</summary>
        public int XpGrowthPerLevel = 4;
        public float PickupRadius = 0.5f;
        /// <summary>首级成长基础与每级固定增长。</summary>
        public float LevelOneMaxHp = 100f, MaxHpPerLevel = 10f;
        public float LevelOneMoveSpeed = 4.2f, MoveSpeedPerLevel = .08f;
        public int MaxLevel = 12;
        public float MagnetRadius = 2f;
        public float MagnetSpeed = 10f;
    }

    /// <summary>一局的全部配置（战斗部分）。世界部分在 Models.World.cs 的同名 partial 里。</summary>
    public sealed partial class TurnConfig
    {
        public WeaponConfig Weapon = new WeaponConfig();
        /// <summary>按 (int)PlaneId 下标：每个位面绑定的武器形态（没有的为 null）。</summary>
        public readonly WeaponFormConfig[] Forms = new WeaponFormConfig[PlaneIds.Count];
        public MonsterConfig Monster = new MonsterConfig();
        public SpawnConfig Spawn = new SpawnConfig();
        public PenaltyConfig Penalty = new PenaltyConfig();
        public UpgradeConfig Upgrades = new UpgradeConfig();
        public ExperienceConfig Experience = new ExperienceConfig();
    }

    // ==================================================================
    // 运行时实体：字段公开是为了让表现层零开销地读取。
    // 每个字段只有注释里写明的那个系统可以写；表现层只读。
    // ==================================================================

    /// <summary>普通怪物：跨地图保留的实体。地图切换不会碰这里的任何字段。</summary>
    public sealed class MonsterEntity
    {
        public int Id;
        /// <summary>MonsterSystem 写。</summary>
        public Vec2 Position;
        /// <summary>MonsterSystem 写：本步的追踪速度（表现层据此决定朝向）。</summary>
        public Vec2 Velocity;
        /// <summary>CombatSystem 写伤害与治疗；MonsterSystem 仅换形态时按血量比例重标定。</summary>
        public float Hp;
        /// <summary>MonsterSystem 写：怪物原型，位面转换时按成对表换型。</summary>
        public MonsterKind Kind;
        /// <summary>MonsterSystem 写：攻击阶段 0 空闲、1 前摇、2 冲锋/硬直。</summary>
        public int AiPhase;
        /// <summary>MonsterSystem 写：攻击或冲锋计时。</summary>
        public float AiTimer;
        /// <summary>MonsterSystem 写：冲锋方向。</summary>
        public Vec2 AttackDirection;
        /// <summary>MonsterSystem 写：投石野人预警时锁定的落点，不随玩家移动。</summary>
        public Vec2 TargetPosition;
        /// <summary>MonsterSystem 写：虫崽分裂父怪 ID，其他怪为零。</summary>
        public int ParentId;
        /// <summary>MonsterSystem 写：Bug 代码弹连射剩余数量。</summary>
        public int BurstShotsLeft;
        /// <summary>MonsterSystem 写：虫崽分裂计时。</summary>
        public float SplitTimer;
        /// <summary>MonsterSystem 写：减速剩余时间。</summary>
        public float SlowLeft;
        /// <summary>MonsterSystem 写：减速乘数，1 为正常速度。</summary>
        public float SlowFactor = 1f;
        /// <summary>MonsterSystem 写：击退/眩晕剩余秒数。</summary>
        public float StunLeft;
        /// <summary>MonsterSystem 写：经验值在生成时取怪物原型配置。</summary>
        public int XpValue;
        /// <summary>MonsterSystem 写：出生时是否被惩罚波生成。</summary>
        public PlaneId SpawnPlane;
        /// <summary>MonsterSystem 写：怪物当前行为形态；不依赖位面事件提前改变表现。</summary>
        public PlaneId Form;
        /// <summary>MonsterSystem 写：生成时赋值，切位面时按形态更新。</summary>
        public float MaxHp;
        /// <summary>MonsterSystem 写（死亡由 CombatSystem 通过 IMonsterSystem.Despawn 发起）。</summary>
        public bool Alive;
        /// <summary>是否为翻滚惩罚生成的（仅供调试显示）。</summary>
        public bool Penalty;

        /// <summary>MonsterSystem 写：接触伤害冷却。</summary>
        public float ContactTimer;
        /// <summary>MonsterSystem 写：脚下地形。</summary>
        public TerrainType Terrain;

        /// <summary>MonsterSystem 写：灼烧（持续 Buff）剩余时间。</summary>
        public float BurnLeft;
        /// <summary>MonsterSystem 写：灼烧每秒伤害。</summary>
        public float BurnDps;
        /// <summary>MonsterSystem 写：灼烧结算计时。</summary>
        public float BurnTimer;

        public bool Burning { get { return BurnLeft > 0f && BurnDps > 0f; } }
    }

    /// <summary>敌方弹幕表现类别；命中与伤害仍由 ProjectileSystem 统一结算。</summary>
    public enum EnemyProjectileKind { Normal, Bubble, Code, Rock, Laser, Explosion }

    /// <summary>子弹。ProjectileSystem 写全部字段。</summary>
    public sealed class ProjectileEntity
    {
        public int Id;
        /// <summary>ProjectileSystem 写：发射时所属位面，切换后不改变。</summary>
        public PlaneId Form;
        /// <summary>ProjectileSystem 写：剩余存活秒数，独立于射程。</summary>
        public float LifetimeLeft;
        /// <summary>ProjectileSystem 写：B16 传送余波剩余秒数，命中时仅加成一次。</summary>
        public float AftershockLeft;
        /// <summary>ProjectileSystem 写：每枚弹幕只允许首次切走时延寿。</summary>
        public bool LifetimeExtended;
        /// <summary>ProjectileSystem 写：敌方弹幕不伤怪物，玩家弹幕不伤玩家。</summary>
        public bool Enemy;
        /// <summary>ProjectileSystem 写：敌方弹幕的视觉和命中特效类型。</summary>
        public EnemyProjectileKind EnemyKind;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
        public float Damage;
        public int PierceLeft;
        /// <summary>ProjectileSystem 写：已经命中的怪物数，用于鱼叉穿透后伤害衰减。</summary>
        public int HitCount;
        public float DistanceLeft;
        public bool Alive;
        public readonly List<int> HitIds = new List<int>();
    }

    /// <summary>经验球。ExperienceSystem 写全部字段。</summary>
    public sealed class OrbEntity
    {
        public int Id;
        public Vec2 Position;
        public int Value;
        public bool Magnetized;
        public bool Alive;
    }

    /// <summary>升级面板上的一个候选项。ProgressionSystem 生成。</summary>
    public sealed class UpgradeOption
    {
        public UpgradeId Id;
        public BuildId Build;
        public string Rarity = "普通";
        public int FromLevel;
        public int ToLevel;
        public string Name = "";
        public string Description = "";
    }
}
