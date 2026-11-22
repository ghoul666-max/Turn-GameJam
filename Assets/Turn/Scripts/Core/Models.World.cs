using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ==================================================================
    // 世界数据（程序A）：玩家、翻滚、鲨鱼、环境、位面、地形。战斗数据在 Models.Combat.cs（程序B）。
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
        /// <summary>岩浆 / 浅水 / 灼烧的结算间隔（秒）。按间隔结算，与帧率无关。</summary>
        public float DotTickInterval = 0.25f;
        public float PlayerLavaDps = 10f;
        public float ShallowRegenPerSecond = 3f;
    }

    /// <summary>一局的全部配置（世界部分）。战斗部分在 Models.Combat.cs 的同名 partial 里。</summary>
    public sealed partial class TurnConfig
    {
        public PlayerConfig Player = new PlayerConfig();
        public RollConfig Roll = new RollConfig();
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
        /// <summary>RollSystem 写：无敌剩余时间。&gt; 0 时 CombatSystem 拒绝一切对玩家的伤害。</summary>
        public float InvincibleLeft;

        public bool Invincible { get { return InvincibleLeft > 0f; } }
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
