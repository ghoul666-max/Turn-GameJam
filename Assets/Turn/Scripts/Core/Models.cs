using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 配置数据：由 Unity 层的 TurnConfigReader 从 Inspector 参数转换而来，运行时只读。
    // 默认值 = 题目给出的推荐手感参数。
    // ==================================================================

    public sealed class PlayerConfig
    {
        public float MoveSpeed = 5f;
        public float MaxHp = 100f;
        /// <summary>碰撞半径（世界单位）。</summary>
        public float Radius = 0.35f;
    }

    public sealed class RollConfig
    {
        /// <summary>全局冷却（秒）。</summary>
        public float Cooldown = 3f;
        /// <summary>升级缩短冷却后的下限（秒）。</summary>
        public float MinCooldown = 0.8f;
        public float InvincibleTime = 0.5f;
        /// <summary>翻滚动画 / 位移时长（秒）。</summary>
        public float Duration = 0.35f;
        /// <summary>翻滚位移距离（世界单位）。</summary>
        public float Distance = 2.5f;
    }

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
        public MapId Map;
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

    public sealed class SharkConfig
    {
        public float Speed = 3f;
        public float ContactDamage = 7.5f;
        public float AttackInterval = 0.8f;
        public float Radius = 0.55f;
        /// <summary>每次海洋地图激活时生成的数量。</summary>
        public int SpawnCount = 1;
        /// <summary>生成时离玩家的最小距离。</summary>
        public float MinSpawnDistance = 7f;
    }

    /// <summary>地图环境：起始地图、DOT 结算间隔、岩浆伤害、浅水回血。</summary>
    public sealed class EnvironmentConfig
    {
        public MapId StartMap = MapId.Volcano;
        /// <summary>岩浆 / 浅水 / 灼烧的结算间隔（秒）。按间隔结算，与帧率无关。</summary>
        public float DotTickInterval = 0.25f;
        public float PlayerLavaDps = 10f;
        public float ShallowRegenPerSecond = 3f;
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

    /// <summary>一局的全部配置。</summary>
    public sealed class TurnConfig
    {
        public PlayerConfig Player = new PlayerConfig();
        public RollConfig Roll = new RollConfig();
        public WeaponConfig Weapon = new WeaponConfig();
        /// <summary>按 (int)MapId 下标：每张地图绑定的武器形态。</summary>
        public readonly WeaponFormConfig[] Forms = new WeaponFormConfig[MapIds.Count];
        public MonsterConfig Monster = new MonsterConfig();
        public SharkConfig Shark = new SharkConfig();
        public EnvironmentConfig Environment = new EnvironmentConfig();
        public SpawnConfig Spawn = new SpawnConfig();
        public PenaltyConfig Penalty = new PenaltyConfig();
        public UpgradeConfig Upgrades = new UpgradeConfig();
        public ExperienceConfig Experience = new ExperienceConfig();

        /// <summary>两张地图共用的唯一移动边界。</summary>
        public BoundsRect Bounds = new BoundsRect(Vec2.Zero, new Vec2(40f, 24f));
        /// <summary>按 (int)MapId 下标：每张地图自己的地形网格。</summary>
        public readonly TerrainGridData[] Terrain = new TerrainGridData[MapIds.Count];
        /// <summary>按 (int)MapId 下标：每张地图的刷怪点（世界坐标）。</summary>
        public readonly List<Vec2>[] SpawnPoints = { new List<Vec2>(), new List<Vec2>() };
    }

    /// <summary>
    /// 一张地图的二维地形网格。原点 = 网格左下角的世界坐标，格子边长 CellSize。
    /// 网格之外一律视为 Normal。
    /// </summary>
    public sealed class TerrainGridData
    {
        public readonly int Columns;
        public readonly int Rows;
        public readonly float CellSize;
        public readonly Vec2 Origin;
        private readonly TerrainType[] _cells;

        public TerrainGridData(int columns, int rows, float cellSize, Vec2 origin, TerrainType[] cells)
        {
            Columns = Math.Max(1, columns);
            Rows = Math.Max(1, rows);
            CellSize = cellSize > 0.01f ? cellSize : 1f;
            Origin = origin;
            _cells = new TerrainType[Columns * Rows];
            if (cells != null) Array.Copy(cells, _cells, Math.Min(cells.Length, _cells.Length));
        }

        /// <summary>全是 Normal 的空网格（地图缺少地形组件时的兜底）。</summary>
        public static TerrainGridData Empty(BoundsRect bounds, float cellSize)
        {
            float size = cellSize > 0.01f ? cellSize : 1f;
            int cols = (int)MathF.Ceiling(bounds.Size.X / size);
            int rows = (int)MathF.Ceiling(bounds.Size.Y / size);
            return new TerrainGridData(cols, rows, size, bounds.Min, null);
        }

        public TerrainType Get(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= Columns || cy >= Rows) return TerrainType.Normal;
            return _cells[cy * Columns + cx];
        }

        public TerrainType Sample(Vec2 world)
        {
            int cx = (int)MathF.Floor((world.X - Origin.X) / CellSize);
            int cy = (int)MathF.Floor((world.Y - Origin.Y) / CellSize);
            return Get(cx, cy);
        }

        public int Count(TerrainType type)
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == type) n++;
            }
            return n;
        }
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
        /// <summary>PlayerSystem 写：本帧的移动输入（长度 ≤ 1）。</summary>
        public Vec2 MoveDir;
        /// <summary>PlayerSystem 写：脚下地形（读当前激活地图的网格）。</summary>
        public TerrainType Terrain;
        /// <summary>PlayerSystem 写（初始化时）。</summary>
        public float MaxHp;

        /// <summary>CombatSystem 写（初始化除外）。</summary>
        public float Hp;
        /// <summary>CombatSystem 写。</summary>
        public bool Alive = true;

        /// <summary>RollSystem 写。</summary>
        public bool IsRolling;
        /// <summary>RollSystem 写：翻滚位移剩余时间。</summary>
        public float RollTimer;
        /// <summary>RollSystem 写：翻滚方向（单位向量）。</summary>
        public Vec2 RollDirection;
        /// <summary>RollSystem 写：本次翻滚的位移速度。</summary>
        public float RollSpeed;
        /// <summary>RollSystem 写：翻滚冷却剩余。</summary>
        public float RollCooldownLeft;
        /// <summary>RollSystem 写：无敌剩余时间。&gt; 0 时 CombatSystem 拒绝一切对玩家的伤害。</summary>
        public float InvincibleLeft;

        public bool Invincible { get { return InvincibleLeft > 0f; } }
    }

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

    /// <summary>鲨鱼：海洋专属，没有生命值，不能被击杀。SharkSystem 写全部字段。</summary>
    public sealed class SharkEntity
    {
        public int Id;
        public Vec2 Position;
        public Vec2 Velocity;
        public float AttackTimer;
        public bool Alive;
    }

    /// <summary>子弹。ProjectileSystem 写全部字段。</summary>
    public sealed class ProjectileEntity
    {
        public int Id;
        /// <summary>由哪张地图的武器形态发射（决定外观：火球 / 鱼叉）。</summary>
        public MapId Form;
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
