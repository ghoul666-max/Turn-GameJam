using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 世界数据（程序A）：玩家、翻滚、鲨鱼、环境、位面、地形。战斗数据在 Models.Combat.cs（程序B）。
    // 配置数据：由 Unity 层的 TurnConfigReader 从 Inspector 参数转换而来，运行时只读。
    // 默认值 = 题目给出的推荐手感参数。
    // 新策划案（11/24）的数值写的是像素：统一按 1 世界单位 = 40 像素换算（200 像素/秒 = 5 单位/秒）。
    // ==================================================================

    public sealed class PlayerConfig
    {
        /// <summary>移动速度（单位/秒）。策划 200 像素/秒 = 5。</summary>
        public float MoveSpeed = 5f;
        public float MaxHp = 100f;
        /// <summary>碰撞半径（世界单位）。</summary>
        public float Radius = 0.35f;
        /// <summary>受伤后的全局无敌帧（秒，本轮调整为 0.5）。</summary>
        public float HurtInvincibleTime = 0.5f;
        public float GravityExitProtectionTime = 0.5f;
    }

    public sealed class RollConfig
    {
        /// <summary>
        /// 按 (int)WeaponKind：每把主武器独立的翻滚冷却（秒）。从回到常态空间开始计，按「回到常态时正在用的那把」算
        /// （双主武器方案里 = 切换后的那把）。策划：水枪 2 / 投矛 2.5 / 陌刀 3；掌 2.5、弩 1.2 为暂定（弩「显著短于其他」）。
        /// </summary>
        public float[] WeaponCooldowns = { 2f, 2.5f, 3f, 2.5f, 1.2f, 2.5f, 2.5f, 2.5f, 2.5f };
        /// <summary>数组缺项时的兜底冷却（秒）。</summary>
        public float Cooldown = 2f;
        /// <summary>亚空间持续时长（现实秒，策划 1）。全程无敌。</summary>
        public float SubspaceDuration = 1f;
        /// <summary>亚空间里的游戏时间倍率（策划 0.3）。</summary>
        public float SubspaceTimeScale = 0.3f;
        /// <summary>翻滚位移时长（游戏秒；亚空间里按慢放推进）。</summary>
        public float Duration = 0.2f;
        /// <summary>翻滚位移距离（世界单位）。</summary>
        public float Distance = 0.8f;
    }

    /// <summary>
    /// 出战配置（11/25 双武器策划案）：出战前在选择页选定，局内不再按波次解锁。
    /// DualMain：Main = 开局生效的主武器，SecondMain = 待切换的主武器（必须不同）；
    /// MainAndSub：Main = 唯一主武器，Sub = 副武器（None = 不带，相当于单武器）。
    /// 默认值（纯模拟 / 测试用）= 方案 B + 水枪 + 不带副武器。
    /// </summary>
    public sealed class LoadoutConfig
    {
        public LoadoutScheme Scheme = LoadoutScheme.MainAndSub;
        public WeaponKind Main = WeaponKind.WaterGun;
        public WeaponKind SecondMain = WeaponKind.Spear;
        public SubWeaponKind Sub = SubWeaponKind.None;
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
        public PlaneId StartPlane = PlaneId.Volcano;
        /// <summary>
        /// 旧位面机制总开关（岩浆喷发、浅水回血、工位困意、梦境减速 / 来电、海洋鲨鱼）。
        /// 新策划案是单地图 + 亚空间，默认关闭；关闭后地图只保留地面与遮挡物。
        /// </summary>
        public bool MechanicsEnabled;
        /// <summary>岩浆 / 浅水 / 灼烧的结算间隔（秒）。按间隔结算，与帧率无关。</summary>
        public float DotTickInterval = 0.25f;
        public float PlayerLavaDps = 10f;
        public float ShallowRegenPerSecond = 3f;

        // ---- 工位：困意 ----
        /// <summary>在工位连续停留超过这么多秒后开始掉血。</summary>
        public float DrowsyDelay = 5f;
        /// <summary>困意每秒伤害（玩家）。</summary>
        public float DrowsyDps = 4f;
        /// <summary>从进入工位到画面暗到最深、眼皮合到最紧所需的秒数（纯表现进度）。</summary>
        public float DrowsyDarkenTime = 10f;

        // ---- 梦境：减速层 ----
        /// <summary>每隔多少秒叠一层减速。</summary>
        public float DreamSlowInterval = 2f;
        /// <summary>每层减速比例（0.05 = 5%）。</summary>
        public float DreamSlowPerStack = 0.05f;
        /// <summary>减速上限（0.4 = 40%）。</summary>
        public float DreamSlowMax = 0.4f;

        // ---- 梦境：老板来电 ----
        /// <summary>预警时长（秒）：落点圈出现到电话砸下。</summary>
        public float PhoneWarning = 0.8f;
        public float PhoneRadius = 2.5f;
        public float PhoneDamage = 25f;
        /// <summary>两次来电之间的间隔（秒，在 [Min, Max] 之间随机）。</summary>
        public float PhoneIntervalMin = 4f;
        public float PhoneIntervalMax = 6f;
    }

    /// <summary>
    /// 关卡流程。新策划案：无尽生存（Endless），波次无限推进，没有通关终点。
    /// 旧流程（一关 = 一组位面，存活够时间翻页，最后一组过关 = 胜利）保留，Endless = false 时生效。
    /// </summary>
    public sealed class LevelConfig
    {
        /// <summary>无尽模式：永不过关。</summary>
        public bool Endless = true;
        /// <summary>每一波的时长（游戏秒）。策划书没给，暂定 30。≤ 0 = 永远第 1 波。</summary>
        public float WaveSeconds = 30f;
        /// <summary>每关需要存活的秒数；≤ 0 表示不会过关（无尽测试用）。</summary>
        public float SurviveSeconds = 300f;
        /// <summary>最后一关的位面组：这一组过关即胜利。</summary>
        public PlanePairId FinalPair = PlanePairId.PrimalFuture;
    }

    /// <summary>一局的全部配置（世界部分）。战斗部分在 Models.Combat.cs 的同名 partial 里。</summary>
    public sealed partial class TurnConfig
    {
        public LevelConfig Level = new LevelConfig();
        public PlayerConfig Player = new PlayerConfig();
        public RollConfig Roll = new RollConfig();
        public LoadoutConfig Loadout = new LoadoutConfig();
        public SharkConfig Shark = new SharkConfig();
        public EnvironmentConfig Environment = new EnvironmentConfig();

        /// <summary>同一组两个位面共用的唯一移动边界（所有组目前共用同一尺寸）。</summary>
        public BoundsRect Bounds = new BoundsRect(Vec2.Zero, new Vec2(40f, 24f));
        /// <summary>按 (int)PlaneId 下标：每个位面自己的地形网格（没有的为 null，TerrainSystem 会补空网格）。</summary>
        public readonly TerrainGridData[] Terrain = new TerrainGridData[PlaneIds.Count];
        /// <summary>按 (int)PlaneId 下标：每个位面的刷怪点（世界坐标）。</summary>
        public readonly List<Vec2>[] SpawnPoints = NewPlaneLists();
        /// <summary>按 (int)PlaneId 下标：每个位面的遮挡物（世界坐标）。</summary>
        public readonly List<ObstacleData>[] Obstacles = NewObstacleLists();

        private static List<Vec2>[] NewPlaneLists()
        {
            var lists = new List<Vec2>[PlaneIds.Count];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<Vec2>();
            return lists;
        }

        private static List<ObstacleData>[] NewObstacleLists()
        {
            var lists = new List<ObstacleData>[PlaneIds.Count];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<ObstacleData>();
            return lists;
        }
    }

    /// <summary>
    /// 一个遮挡物的判定数据（世界坐标）。开局由表现层的 Obstacle 组件转换而来，运行时只读。
    /// 圆：Center + Radius；矩形：Center ± HalfSize（轴对齐）。
    /// </summary>
    public struct ObstacleData
    {
        public ObstacleShape Shape;
        public Vec2 Center;
        public float Radius;
        public Vec2 HalfSize;
        public ObstacleMask Mask;

        public bool Blocks(ObstacleMask mask) { return (Mask & mask) != 0; }
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
        /// <summary>RollSystem 写：额外无敌剩余时间（目前只有调试无敌在用）。&gt; 0 时 CombatSystem 拒绝一切对玩家的伤害。</summary>
        public float InvincibleLeft;
        /// <summary>RollSystem 写：是否处在亚空间（翻滚后）。亚空间里全程无敌。</summary>
        public bool InSubspace;
        /// <summary>RollSystem 写：亚空间剩余现实秒数。</summary>
        public float SubspaceLeft;
        /// <summary>PlayerSystem 写：受伤后的无敌帧剩余时间（PlayerConfig.HurtInvincibleTime）。</summary>
        public float HurtInvincibleLeft;
        /// <summary>WeaponSystem 写：武器带来的无敌（剑的突进过程中）。</summary>
        public bool WeaponInvincible;

        /// <summary>任何一种无敌：调试无敌 / 亚空间 / 受伤无敌帧 / 剑突进中。CombatSystem 只看这一个。</summary>
        public bool Invincible { get { return InvincibleLeft > 0f || InSubspace || HurtInvincibleLeft > 0f || WeaponInvincible; } }
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
}
