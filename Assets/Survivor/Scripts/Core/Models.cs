using System.Collections.Generic;

namespace Survivor.Core
{
    // ==================================================================
    // 配置数据：由 Data 层（ScriptableObject）或 Content 层（代码）创建，运行时只读。
    // ==================================================================

    /// <summary>武器某一级的数值（未经过角色属性加成）。</summary>
    public sealed class WeaponStats
    {
        public float Damage = 10f;
        /// <summary>两次发动之间的秒数。</summary>
        public float Cooldown = 1f;
        public int Amount = 1;
        /// <summary>范围倍率（判定与图像一起缩放）。</summary>
        public float Area = 1f;
        /// <summary>速度倍率。</summary>
        public float Speed = 1f;
        /// <summary>持续时间（秒）：鞭子挥动 / 圣经环绕。</summary>
        public float Duration = 1f;
        /// <summary>可击中的敌人数；&lt;= 0 表示无限。</summary>
        public int Pierce = 1;
        public float Knockback = 1f;
        public string Description = "";
    }

    public sealed class WeaponData
    {
        public string Id;
        public string Name;
        /// <summary>界面图标的素材键。</summary>
        public string ArtKey;
        /// <summary>投射物的素材键。</summary>
        public string VisualKey;
        public WeaponKind Kind;
        public IWeaponBehavior Behavior;
        public readonly List<WeaponStats> Levels = new List<WeaponStats>();

        public int MaxLevel { get { return Levels.Count; } }

        /// <summary>level 从 1 开始；越界时取最近的一级。</summary>
        public WeaponStats GetLevel(int level)
        {
            if (Levels.Count == 0) return new WeaponStats();
            int index = level - 1;
            if (index < 0) index = 0;
            if (index >= Levels.Count) index = Levels.Count - 1;
            return Levels[index];
        }
    }

    public sealed class PassiveData
    {
        public string Id;
        public string Name;
        public string ArtKey;
        public StatId Stat;
        /// <summary>每级加成（0.1 = 10%；Amount 为整数个数）。</summary>
        public float AmountPerLevel = 0.1f;
        public int MaxLevel = 5;
        public string Description = "";
    }

    public sealed class CharacterData
    {
        public string Name;
        public string SpriteKey;
        public float MaxHp = 100f;
        /// <summary>每秒移动的世界单位。</summary>
        public float MoveSpeed = 3f;
        /// <summary>经验宝石开始被吸过来的半径。</summary>
        public float PickupRadius = 1.6f;
        public WeaponData StartingWeapon;
    }

    public sealed class EnemyData
    {
        public string Id;
        public string Name;
        public string SpriteKey;
        public float MaxHp = 10f;
        public float Speed = 1.5f;
        /// <summary>接触伤害（每次）。</summary>
        public float Damage = 5f;
        /// <summary>碰撞半径（世界单位）。</summary>
        public float Radius = 0.4f;
        /// <summary>图像缩放。</summary>
        public float Scale = 1f;
        /// <summary>0 = 正常被击退，1 = 完全不吃击退。</summary>
        public float KnockbackResist;
        public int Xp = 1;
        public float HealDropChance;
        public bool Elite;
        public bool Boss;
    }

    /// <summary>一段持续刷怪：[Start, End) 内每 Interval 秒刷 Count 只，从 Pool 里等概率挑（重复放入 = 提高权重）。</summary>
    public sealed class SpawnWave
    {
        public float Start;
        public float End;
        public float Interval = 1f;
        public int Count = 1;
        public readonly List<EnemyData> Pool = new List<EnemyData>();
    }

    /// <summary>定时事件：到点一次性刷出（精英、首领、包围圈）。</summary>
    public sealed class SpawnEventData
    {
        public float Time;
        public EnemyData Enemy;
        public int Count = 1;
        public SpawnPattern Pattern = SpawnPattern.Scatter;
        /// <summary>非空时在屏幕中央播报。</summary>
        public string Announce;
    }

    public sealed class StageData
    {
        public string Name;
        /// <summary>WorldView 的主题下标。</summary>
        public int Theme;
        /// <summary>存活到这个秒数即胜利。</summary>
        public float Duration = 300f;
        public int MaxEnemies = 350;
        public readonly List<SpawnWave> Waves = new List<SpawnWave>();
        public readonly List<SpawnEventData> Events = new List<SpawnEventData>();
    }

    // ==================================================================
    // 运行时实体：字段公开是为了让表现层零开销地读取。
    // 每个字段只有注释里写明的那个系统可以写；表现层只读。
    // ==================================================================

    public sealed class PlayerState
    {
        /// <summary>PlayerSystem 写。</summary>
        public Vec2 Position;
        /// <summary>PlayerSystem 写：最近一次移动的方向（单位向量）。</summary>
        public Vec2 Facing = Vec2.Right;
        /// <summary>PlayerSystem 写：最近一次水平移动的朝向，1 或 -1（鞭子用）。</summary>
        public int FacingX = 1;
        /// <summary>PlayerSystem 写：本帧的移动输入。</summary>
        public Vec2 MoveDir;
        /// <summary>CombatSystem 写（初始化除外）。</summary>
        public float Hp;
        /// <summary>CombatSystem 写。</summary>
        public bool Alive = true;
    }

    /// <summary>WeaponSystem 写。</summary>
    public sealed class WeaponInstance
    {
        public WeaponData Data;
        public int Level = 1;
        /// <summary>距离下一次发动的秒数。</summary>
        public float Timer;

        public WeaponStats Stats { get { return Data.GetLevel(Level); } }
        public bool IsMaxLevel { get { return Level >= Data.MaxLevel; } }
    }

    /// <summary>PlayerSystem 写。</summary>
    public sealed class PassiveInstance
    {
        public PassiveData Data;
        public int Level = 1;
        public bool IsMaxLevel { get { return Level >= Data.MaxLevel; } }
    }

    public sealed class EnemyEntity
    {
        /// <summary>每次生成都分配新的 Id（实体对象会被对象池复用，表现层按 Id 区分）。</summary>
        public int Id;
        public EnemyData Data;
        /// <summary>EnemySystem 写。</summary>
        public Vec2 Position;
        /// <summary>EnemySystem 写：本帧追踪速度（表现层用来决定朝向）。</summary>
        public Vec2 Velocity;
        /// <summary>EnemySystem 写：击退速度，逐渐衰减。</summary>
        public Vec2 Knockback;
        /// <summary>CombatSystem 写。</summary>
        public float Hp;
        public float MaxHp;
        /// <summary>EnemySystem 写（死亡由 CombatSystem 通过 IEnemySystem.Despawn 发起）。</summary>
        public bool Alive;
        /// <summary>EnemySystem 写：接触伤害的冷却。</summary>
        public float AttackTimer;
    }

    /// <summary>武器产生的判定体。ProjectileSystem 写。</summary>
    public sealed class ProjectileEntity
    {
        public int Id;
        public string WeaponId;
        public string VisualKey;
        public ProjectileMotion Motion;
        public HitShape Shape;
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>Attached：相对玩家的偏移。</summary>
        public Vec2 Offset;
        public float Radius;
        public Vec2 HalfExtents;
        public float Life;
        public float MaxLife;
        /// <summary>剩余可击中数；-1 = 无限。</summary>
        public int PierceLeft;
        /// <summary>基础伤害（力量加成由 CombatSystem 在命中时计算）。</summary>
        public float Damage;
        public float Knockback;
        /// <summary>同一敌人再次受击的间隔；&lt;= 0 表示一生只打一次。</summary>
        public float RehitInterval;
        public float OrbitAngle;
        public float OrbitRadius;
        public float OrbitSpeed;
        /// <summary>图像朝向（弧度），只给表现层用。</summary>
        public float Rotation;
        /// <summary>图像缩放（FitVisual = false 时使用）。</summary>
        public float Scale = 1f;
        /// <summary>true：表现层把图拉伸到判定体大小（鞭子、大蒜）。</summary>
        public bool FitVisual;
        /// <summary>1 或 -1：图像水平翻转。</summary>
        public int Facing = 1;
        public bool Alive;

        public readonly List<int> HitIds = new List<int>();
        public readonly List<float> HitTimes = new List<float>();
    }

    /// <summary>PickupSystem 写。</summary>
    public sealed class PickupEntity
    {
        public int Id;
        public PickupKind Kind;
        public Vec2 Position;
        /// <summary>宝石 = 经验值；回复道具 = 回复量。</summary>
        public int Value;
        public bool Magnetized;
        public float Speed;
        public bool Alive;
    }

    /// <summary>升级时的一个选项。ProgressionSystem 生成。</summary>
    public sealed class UpgradeOption
    {
        public UpgradeKind Kind;
        public WeaponData Weapon;
        public PassiveData Passive;
        /// <summary>0 = 新获得。</summary>
        public int FromLevel;
        public int ToLevel;
        public string Name;
        public string Description;
        public string ArtKey;
    }

    // ==================================================================
    // 武器 → 投射物系统的参数包
    // ==================================================================

    /// <summary>角色属性加成之后的武器数值（伤害除外：力量加成在 CombatSystem 里算）。</summary>
    public struct EffectiveWeaponStats
    {
        public float Damage;
        public float Cooldown;
        public int Amount;
        public float Area;
        public float Speed;
        public float Duration;
        public int Pierce;
        public float Knockback;
    }

    /// <summary>生成一个投射物所需的全部参数。用 Create() 取得带默认值的实例再改字段。</summary>
    public struct ProjectileSpec
    {
        public string WeaponId;
        public string VisualKey;
        public ProjectileMotion Motion;
        public HitShape Shape;
        public Vec2 Position;
        public Vec2 Velocity;
        public Vec2 Offset;
        public float Radius;
        public Vec2 HalfExtents;
        public float Life;
        public int Pierce;
        public float Damage;
        public float Knockback;
        public float RehitInterval;
        public float OrbitAngle;
        public float OrbitRadius;
        public float OrbitSpeed;
        public float Rotation;
        public float Scale;
        public bool FitVisual;
        public int Facing;

        public static ProjectileSpec Create(WeaponInstance weapon)
        {
            var spec = new ProjectileSpec();
            spec.WeaponId = weapon != null ? weapon.Data.Id : "";
            spec.VisualKey = weapon != null ? weapon.Data.VisualKey : "";
            spec.Motion = ProjectileMotion.Linear;
            spec.Shape = HitShape.Circle;
            spec.Radius = 0.25f;
            spec.Life = 1f;
            spec.Pierce = 1;
            spec.Scale = 1f;
            spec.Facing = 1;
            return spec;
        }
    }
}
