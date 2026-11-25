using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 战斗数据（程序B）：武器、怪物、刷怪、升级、经验。世界数据在 Models.World.cs（程序A）。
    // 配置数据：由 Unity 层的 TurnConfigReader 从 Inspector 参数转换而来，运行时只读。
    // 新策划案（11/24）用的是下面的 ArsenalConfig / HordeConfig / BuildConfig；
    // 旧方案的 WeaponConfig / WeaponFormConfig / MonsterConfig / SpawnConfig / PenaltyConfig / UpgradeConfig
    // 只为兼容场景与 Prefab 上已序列化的 Inspector 字段而保留，新系统不再读取（MonsterConfig 的岩浆伤害除外）。
    // 像素 → 世界单位：1 单位 = 40 像素。
    // ==================================================================

    // ---------------- 新策划案：三把武器 ----------------

    /// <summary>水枪。常态：水弹落地 / 命中敌人处留下水渍；爆发：全图水渍同时喷发喷泉。</summary>
    [Serializable]
    public sealed class WaterGunConfig
    {
        public int Ammo = 30;
        /// <summary>攻击间隔（秒 / 发）。</summary>
        public float FireInterval = 0.2f;
        /// <summary>水弹飞行速度（单位/秒）。策划书没给，暂定 10（400 像素/秒）。</summary>
        public float BulletSpeed = 10f;
        public float Damage = 5f;
        public float BulletRadius = 0.15f;
        /// <summary>自动索敌 / 射程（单位）。策划书没给，暂定 8。</summary>
        public float Range = 8f;
        /// <summary>水渍半径（单位，策划 40 像素 = 1）。</summary>
        public float PuddleRadius = 1f;
        /// <summary>单滩水渍的喷泉伤害。</summary>
        public float FountainDamage = 12f;
        /// <summary>重叠加成：同一敌人被 n 滩喷泉覆盖时，每滩伤害 ×(1 + OverlapBonus × (n − 1))。默认 0 = 每滩独立判定、直接相加。</summary>
        public float OverlapBonus = 0f;
        /// <summary>全图水渍上限（防止极端情况下卡顿）。</summary>
        public int MaxPuddles = 400;
        /// <summary>散射词条：两枚子弹之间的夹角（度）。</summary>
        public float SpreadDegrees = 12f;
    }

    /// <summary>投矛。常态：命中叠矛印记，击杀则穿透下一个并传递印记；爆发：全部长矛向玩家回收，路径穿刺。</summary>
    [Serializable]
    public sealed class SpearConfig
    {
        public int Ammo = 20;
        public float FireInterval = 0.4f;
        /// <summary>长矛飞行速度（单位/秒）。策划书没给，暂定 12。</summary>
        public float Speed = 12f;
        public float Damage = 8f;
        public float Radius = 0.18f;
        /// <summary>索敌 / 射程（单位）。暂定 9。</summary>
        public float Range = 9f;
        /// <summary>每穿透一次伤害衰减比例（策划 20%）。</summary>
        public float PierceFalloff = 0.2f;
        /// <summary>每根长矛的回收伤害。</summary>
        public float RecallDamage = 15f;
        /// <summary>回收路径判定半宽（单位）。暂定 0.5。</summary>
        public float RecallWidth = 0.5f;
        /// <summary>每多一层矛，这只敌人身上每根矛的回收伤害加成（默认 0，词条提升）。</summary>
        public float StackBonus = 0f;
        /// <summary>双矛词条：两根长矛之间的夹角（度）。</summary>
        public float SpreadDegrees = 14f;
    }

    /// <summary>陌刀。常态：前方扇形剑气，命中的敌人各叠 1 层剑气印记；爆发：横贯屏幕的巨型斩击，按全场印记总层数增伤。</summary>
    [Serializable]
    public sealed class BladeConfig
    {
        public int Ammo = 25;
        public float FireInterval = 0.5f;
        public float Damage = 6f;
        /// <summary>扇形总角度（度）。</summary>
        public float Angle = 60f;
        /// <summary>剑气射程（单位，策划 300 像素 = 7.5）。</summary>
        public float Range = 7.5f;
        /// <summary>剑气厚度：默认只命中最近一圈敌人（离最近目标不超过这个距离），穿透词条每层多推进一圈。</summary>
        public float RingThickness = 0.9f;
        /// <summary>一刀斩基础伤害。</summary>
        public float SlashDamage = 25f;
        /// <summary>每层印记增伤（策划 8%）。</summary>
        public float MarkBonus = 0.08f;
        /// <summary>横向斩击的半高（单位，以玩家为中心上下各这么多）。暂定 1.5。</summary>
        public float SlashHalfHeight = 1.5f;
    }

    /// <summary>三把武器 + 共用参数。</summary>
    [Serializable]
    public sealed class ArsenalConfig
    {
        public WaterGunConfig WaterGun = new WaterGunConfig();
        public SpearConfig Spear = new SpearConfig();
        public BladeConfig Blade = new BladeConfig();
        /// <summary>暴击伤害倍率（暴击词条共用）。</summary>
        public float CritMultiplier = 2f;
        /// <summary>「翻滚爆发时对周身一圈敌人造成百分比伤害」的半径（单位）。</summary>
        public float BurstRingRadius = 3f;
        /// <summary>「受到伤害时给附近敌人叠印记」的搜索半径（单位）。</summary>
        public float HurtMarkRange = 8f;
    }

    // ---------------- 新策划案：五种怪物 + 波次 ----------------

    /// <summary>一种怪物的基础数值（= 它首次出现那一波的数值）。</summary>
    [Serializable]
    public sealed class HordeKindConfig
    {
        public float MaxHp;
        /// <summary>移动速度（单位/秒）。</summary>
        public float Speed;
        /// <summary>碰撞 / 弹幕 / 冲锋 / 激光伤害（按种类）。</summary>
        public float Damage;
        /// <summary>攻击间隔或接触伤害间隔（秒）。</summary>
        public float AttackInterval;
        /// <summary>蓄力时间（秒）。</summary>
        public float Windup;
        /// <summary>进入攻击的距离（冲锋范围 / 狙击射程，单位）。</summary>
        public float Range;
        public int Xp;
        /// <summary>第几波开始出现。</summary>
        public int DebutWave;
        /// <summary>刷新权重。</summary>
        public float Weight;
    }

    /// <summary>怪物与波次（新策划案）。Kinds 按 MonsterKind.Walker..Sniper 的顺序。</summary>
    [Serializable]
    public sealed class HordeConfig
    {
        public HordeKindConfig[] Kinds = Turn.Monsters.MonsterCatalog.CreateHordeDefaults();
        /// <summary>每推进 1 波：血量 +10%、伤害 +5%（从该怪物首次出现的波次算起）。</summary>
        public float HpPerWave = 0.10f;
        public float DamagePerWave = 0.05f;

        /// <summary>常规刷怪间隔（秒）。</summary>
        public float SpawnInterval = 1.5f;
        /// <summary>第 1 波每批刷几只。</summary>
        public float BaseCount = 2f;
        /// <summary>每推进 1 波，每批多刷几只（线性）。</summary>
        public float CountPerWave = 0.5f;
        public int MaxAlive = 160;
        /// <summary>从地图边缘往里缩多少刷新（单位）。</summary>
        public float EdgeInset = 0.6f;
        /// <summary>刷新点离玩家的最小距离（太近就重新随机）。</summary>
        public float MinPlayerDistance = 6f;
        public float Radius = 0.4f;

        // 弹幕孢
        public int SporeBullets = 8;
        public float SporeBulletSpeed = 3.75f;
        public float SporeBulletRadius = 0.2f;
        public float SporeBulletLifetime = 6f;
        // 疾冲兽
        public float ChargeSpeed = 7.5f;
        public float ChargeMaxDistance = 9f;
        // 毒腐体
        public float PoolRadius = 1.5f;
        public float PoolDuration = 5f;
        public float PoolDamage = 2f;
        public float PoolInterval = 0.5f;
        // 狙击械
        public float SniperMinRange = 6f;
        public float SniperMaxRange = 10f;
        public float LaserWidth = 0.3f;

        // 精英 / Boss（策划书没给规则，暂定：第 2 波起每波开始时刷 1 只精英；每 10 波刷 1 只 Boss）
        public int EliteFromWave = 2;
        public float EliteHp = 5f, EliteDamage = 1.5f, EliteScale = 1.5f;
        public int EliteXp = 5;
        public int BossEveryWaves = 10;
        public float BossHp = 25f, BossDamage = 2f, BossScale = 2.2f;
        public int BossXp = 20;
    }

    // ---------------- 新策划案：词条与宝箱 ----------------

    /// <summary>一条词条的数值：每层效果 Value（含义见 BuildCatalog 的说明），Value2 为附加参数（持续时间 / 半径等），MaxLevel 最大层数。</summary>
    [Serializable]
    public sealed class BuildDef
    {
        public int MaxLevel;
        public float Value;
        public float Value2;
    }

    [Serializable]
    public sealed class BuildConfig
    {
        /// <summary>升级 / 宝箱的候选数。</summary>
        public int OptionCount = 3;
        /// <summary>按 (int)BuildId 下标。</summary>
        public BuildDef[] Builds = Turn.Weapons.BuildCatalog.CreateDefaults();
        /// <summary>宝箱的强化选项一次给几层。</summary>
        public int ChestLevels = 1;
        /// <summary>宝箱代价选项：选择后随机获得几层强化。</summary>
        public int CurseBonusLevels = 3;
        /// <summary>每层代价：最大生命 −15 / 怪物伤害 +15% / 怪物移速 +10%。</summary>
        public float FrailMaxHp = 15f;
        public float FuryDamage = 0.15f;
        public float HasteSpeed = 0.10f;
    }

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
        RockThrower, VineDemon, Boar, FutureSoldier, RepairRobot, DataWorm,
        /// <summary>新策划案：近战炮灰 · 游荡尸。</summary>
        Walker,
        /// <summary>新策划案：远程弹幕 · 弹幕孢。</summary>
        Spore,
        /// <summary>新策划案：冲锋 · 疾冲兽。</summary>
        Charger,
        /// <summary>新策划案：亡语 · 毒腐体。</summary>
        Rotter,
        /// <summary>新策划案：激光枪 · 狙击械。</summary>
        Sniper,
    }

    public static class MonsterKinds
    {
        /// <summary>新策划案的五种怪物（HordeConfig.Kinds 的下标 = Kind − Walker）。</summary>
        public const MonsterKind First = MonsterKind.Walker;
        public const int HordeCount = 5;

        public static int HordeIndex(MonsterKind kind) { return (int)kind - (int)First; }

        public static string DisplayName(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Walker: return "游荡尸";
                case MonsterKind.Spore: return "弹幕孢";
                case MonsterKind.Charger: return "疾冲兽";
                case MonsterKind.Rotter: return "毒腐体";
                case MonsterKind.Sniper: return "狙击械";
                default: return kind.ToString();
            }
        }
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
        /// <summary>按 B01..B19 顺序配置的最大叠层。</summary>
        public int[] BuildMaxLevels = { 5, 4, 2, 3, 3, 2, 3, 2, 3, 2, 2, 3, 3, 2, 2, 1, 2, 1, 2 };
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
        /// <summary>新策划案：1 级升 2 级所需经验（策划 50）。</summary>
        public int FirstLevelXp = 50;
        /// <summary>新策划案：每级所需经验递增比例（策划 20%）。</summary>
        public float XpGrowthRate = 0.2f;
        /// <summary>新策划案：经验拾取范围（单位，策划 100 像素 = 2.5）。进入后经验球飞向玩家。</summary>
        public float PickupRange = 2.5f;
        /// <summary>宝箱：玩家靠近到这个距离自动打开（单位）。</summary>
        public float ChestOpenRadius = 1f;
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
        /// <summary>新策划案：三把武器。</summary>
        public ArsenalConfig Arsenal = new ArsenalConfig();
        /// <summary>新策划案：五种怪物 + 波次 + 精英 / Boss。</summary>
        public HordeConfig Horde = new HordeConfig();
        /// <summary>新策划案：词条表 + 宝箱。</summary>
        public BuildConfig Builds = new BuildConfig();
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

        /// <summary>WeaponSystem 写：身上的矛印记层数（= 插着几根矛）。爆发后 / 回到常态空间时清零。</summary>
        public int SpearMarks;
        /// <summary>WeaponSystem 写：剑气印记层数。回到常态空间时清零。</summary>
        public int BladeMarks;
        /// <summary>MonsterSystem 写（生成时）：0 普通 / 1 精英 / 2 Boss。精英与 Boss 死亡掉落宝箱。</summary>
        public int Rank;
        /// <summary>MonsterSystem 写（生成时）：碰撞半径（精英 / Boss 更大）。</summary>
        public float Radius = 0.4f;
        /// <summary>MonsterSystem 写（生成时）：伤害倍率（波次成长 × 精英 / Boss）。</summary>
        public float DamageScale = 1f;
        /// <summary>MonsterSystem 写：疾冲兽本次冲锋已经走过的距离。</summary>
        public float ChargeTravelled;

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
        /// <summary>ProjectileSystem 写（生成时）：玩家子弹属于哪把武器（敌方弹幕忽略）。</summary>
        public WeaponKind Weapon;
        /// <summary>WeaponSystem 写：长矛携带的矛印记层数（击杀穿透时累加被击杀者身上的矛）。</summary>
        public int CarriedMarks;
        /// <summary>WeaponSystem 写：水弹是否已经留下过水渍（穿透词条：只在首个命中处留一滩）。</summary>
        public bool MarkLeft;
    }

    /// <summary>水枪的水渍（铺垫痕迹）。永久存在，直到翻滚爆发后回到常态空间时清空。WeaponSystem 写。</summary>
    public struct WaterMark
    {
        public Vec2 Position;
        public float Radius;
    }

    /// <summary>毒腐体死亡留下的毒潭。MonsterSystem 写全部字段。</summary>
    public sealed class PoisonPool
    {
        public int Id;
        public Vec2 Position;
        public float Radius;
        public float TimeLeft;
        public float Duration;
        /// <summary>每次结算对玩家的伤害（已含波次成长）。</summary>
        public float Damage;
    }

    /// <summary>精英 / Boss 掉落的宝箱。ExperienceSystem 写全部字段。玩家靠近自动打开。</summary>
    public sealed class ChestEntity
    {
        public int Id;
        public Vec2 Position;
        /// <summary>掉落者的等级（1 精英 / 2 Boss），表现层据此区分外观。</summary>
        public int Rank;
        public bool Alive;
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

    /// <summary>升级 / 宝箱面板上的一个候选项。ProgressionSystem 生成。</summary>
    public sealed class UpgradeOption
    {
        public BuildId Build;
        public WeaponKind Weapon;
        public int FromLevel;
        public int ToLevel;
        public string Name = "";
        public string Description = "";
        /// <summary>标签：「强化」「宝箱」「代价」。</summary>
        public string Tag = "强化";
        /// <summary>宝箱代价选项：Curse ≠ None 时，选择后该代价 +1 层，并随机获得 BonusLevels 层当前武器的强化（此时 Build 无意义）。</summary>
        public ChestCurse Curse;
        public int BonusLevels;
    }
}
