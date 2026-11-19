using System;
using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>地图环境参数（Inspector）。</summary>
    [Serializable]
    public sealed class EnvironmentSettings
    {
        [Tooltip("开局激活的地图")]
        public MapId startMap = MapId.Volcano;
        [Tooltip("地形网格尺寸：每格边长（世界单位）。两张地图共用。")]
        [Min(0.25f)] public float terrainCellSize = 1f;
        [Tooltip("岩浆 / 浅水 / 灼烧的结算间隔（秒）。按间隔结算，与帧率无关。")]
        [Min(0.02f)] public float dotTickInterval = 0.25f;
        [Tooltip("玩家踩岩浆每秒受到的伤害（怪物的岩浆伤害在 MonsterPrefab 的 MonsterController 上）")]
        public float playerLavaDps = 10f;
        [Tooltip("玩家站在海洋浅水区每秒回复的生命（普通怪物不回血）")]
        public float shallowRegenPerSecond = 3f;

        public EnvironmentConfig ToConfig()
        {
            return new EnvironmentConfig
            {
                StartMap = startMap,
                DotTickInterval = dotTickInterval,
                PlayerLavaDps = playerLavaDps,
                ShallowRegenPerSecond = shallowRegenPerSecond,
            };
        }
    }

    /// <summary>
    /// 双地图管理（节点 MapManager）。
    /// VolcanoRoot 与 OceanRoot 在同一个 Scene、同一个世界坐标空间：两个根节点都固定在原点、不旋转、不缩放，
    /// 两张地面都按 WorldBounds 铺满，两张地形网格都覆盖 WorldBounds —— 尺寸与坐标范围完全一致。
    /// 切换：收到 MapSwitchedEvent 后用 SetActive 互斥显隐（同一时刻只有一个 Root 激活）。
    /// 不加载 Scene、不叠 Tilemap、不移动任何单位。
    /// </summary>
    public sealed class MapManager : MonoBehaviour
    {
        [Header("两套地图根节点（生成器自动拖好）")]
        [SerializeField] private GameObject volcanoRoot;
        [SerializeField] private GameObject oceanRoot;
        [SerializeField] private SpriteRenderer volcanoGround;
        [SerializeField] private SpriteRenderer oceanGround;
        [SerializeField] private TerrainGrid lavaGrid;
        [SerializeField] private TerrainGrid shallowWaterGrid;
        [SerializeField] private Transform volcanoSpawnPoints;
        [SerializeField] private Transform oceanSpawnPoints;
        [SerializeField] private WorldBounds worldBounds;

        [Header("环境参数")]
        [SerializeField] private EnvironmentSettings environment = new EnvironmentSettings();

        [Header("运行时只读")]
        [SerializeField] private MapId activeMap;

        private ITurnContext _ctx;

        public EnvironmentSettings Environment { get { return environment; } }
        public float TerrainCellSize { get { return environment.terrainCellSize; } }
        public WorldBounds Bounds { get { return worldBounds; } }

        public TerrainGrid GetTerrain(MapId map)
        {
            return map == MapId.Volcano ? lavaGrid : shallowWaterGrid;
        }

        public Transform GetSpawnPointRoot(MapId map)
        {
            return map == MapId.Volcano ? volcanoSpawnPoints : oceanSpawnPoints;
        }

        public void CollectSpawnPoints(MapId map, List<Vec2> results)
        {
            results.Clear();
            Transform root = GetSpawnPointRoot(map);
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                Vector3 p = root.GetChild(i).position;
                results.Add(new Vec2(p.x, p.y));
            }
        }

        // ---------------- 绑定 ----------------

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            AlignRoots();
            ctx.Events.Subscribe<MapSwitchedEvent>(OnMapSwitched);
            Apply(ctx.Map.Active);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<MapSwitchedEvent>(OnMapSwitched);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnMapSwitched(MapSwitchedEvent evt)
        {
            Apply(evt.To);
        }

        /// <summary>互斥显隐：先关旧的再开新的，任何时刻都不会两张同时激活。</summary>
        public void Apply(MapId map)
        {
            activeMap = map;
            if (map == MapId.Volcano)
            {
                if (oceanRoot != null) oceanRoot.SetActive(false);
                if (volcanoRoot != null) volcanoRoot.SetActive(true);
            }
            else
            {
                if (volcanoRoot != null) volcanoRoot.SetActive(false);
                if (oceanRoot != null) oceanRoot.SetActive(true);
            }
        }

        // ---------------- 同空间对齐 ----------------

        /// <summary>两个根节点固定在原点；两张地面按边界铺满。保证“同一空间、同一尺寸”。</summary>
        public void AlignRoots()
        {
            ResetTransform(volcanoRoot);
            ResetTransform(oceanRoot);
            if (worldBounds == null) return;
            FitGround(volcanoGround, worldBounds);
            FitGround(oceanGround, worldBounds);
        }

        private static void ResetTransform(GameObject root)
        {
            if (root == null) return;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        private static void FitGround(SpriteRenderer ground, WorldBounds bounds)
        {
            if (ground == null || ground.sprite == null) return;
            Vector2 spriteSize = ground.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
            ground.transform.position = new Vector3(bounds.Center.x, bounds.Center.y, 0f);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(bounds.Size.x / spriteSize.x, bounds.Size.y / spriteSize.y, 1f);
            ground.sortingOrder = ViewMath.GroundOrder;
        }
    }
}
