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
        public int AmmoPerShot = 1;
        /// <summary>开局弹药；&lt; 0 表示满弹匣。</summary>
        public int InitialAmmo = -1;
        public float FireInterval = 0.6f;
        /// <summary>自动索敌半径。</summary>
        public float TargetRange = 8f;
        /// <summary>攻击范围：子弹最远飞行距离。</summary>
        public float AttackRange = 11f;
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
    }

    public sealed class MonsterConfig
    {
        public float MaxHp = 30f;
        public float Speed = 2f;
        public float ContactDamage = 8f;
        public float ContactInterval = 0.6f;
        public float Radius = 0.4f;
        /// <summary>切到火山地图时，站在岩浆范围内的怪物受到的一次性伤害（之后岩浆消失）。</summary>
        public float LavaHitDamage = 20f;
        public int Xp = 1;
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
        /// <summary>按 (int)UpgradeId 下标的最高等级。</summary>
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
        public int BaseXpToLevel = 5;
        /// <summary>每升一级，下一级多需要的经验。</summary>
        public int XpGrowthPerLevel = 5;
        public float PickupRadius = 0.5f;
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
        /// <summary>CombatSystem 写。</summary>
        public float Hp;
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

    /// <summary>子弹。ProjectileSystem 写全部字段。</summary>
    public sealed class ProjectileEntity
    {
        public int Id;
        /// <summary>由哪张地图的武器形态发射（决定外观：火球 / 鱼叉）。</summary>
        public PlaneId Form;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
        public float Damage;
        public int PierceLeft;
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
        public int FromLevel;
        public int ToLevel;
        public string Name = "";
        public string Description = "";
    }
}
