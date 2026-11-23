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
        [Tooltip("开局激活的位面（目前只有火山 / 海洋搭好了场景）")]
        public PlaneId startMap = PlaneId.Volcano;
        [Tooltip("地形网格尺寸：每格边长（世界单位）。两张地图共用。")]
        [Min(0.25f)] public float terrainCellSize = 1f;
        [Tooltip("岩浆 / 浅水 / 灼烧的结算间隔（秒）。按间隔结算，与帧率无关。")]
        [Min(0.02f)] public float dotTickInterval = 0.25f;
        [Tooltip("玩家踩岩浆每秒受到的伤害。现在岩浆切图时喷发一次就消失，玩家实际踩不到岩浆，此项保留备用。" +
                 "（怪物的岩浆一次性伤害在 MonsterPrefab 的 MonsterController 上）")]
        public float playerLavaDps = 10f;
        [Tooltip("玩家站在海洋浅水区每秒回复的生命（普通怪物不回血）")]
        public float shallowRegenPerSecond = 3f;
        [Tooltip("工位困意 / 梦境减速 / 老板来电的参数（F1 数值面板的「位面机制」分组同步这些值）")]
        public PlaneMechanicsSettings mechanics = new PlaneMechanicsSettings();

        public EnvironmentConfig ToConfig()
        {
            var c = new EnvironmentConfig
            {
                StartPlane = startMap,
                DotTickInterval = dotTickInterval,
                PlayerLavaDps = playerLavaDps,
                ShallowRegenPerSecond = shallowRegenPerSecond,
            };
            if (mechanics != null) mechanics.ApplyTo(c);
            return c;
        }
    }

    /// <summary>关卡参数（Inspector + F1 数值面板）。一关 = 一组位面，存活够时间翻页进下一关，最后一组通过 = 胜利。</summary>
    [Serializable]
    public sealed class LevelSettings
    {
        [Tooltip("数据版本。旧存档里没有这一组时读出来是 0，此时忽略、使用 Inspector 数值。")]
        [HideInInspector] public int version = 1;
        [Tooltip("每关（每组位面）需要存活的秒数。0 = 不会过关（无尽测试）。")]
        [Min(0f)] public float surviveSeconds = 300f;

        public LevelConfig ToConfig()
        {
            return new LevelConfig { SurviveSeconds = surviveSeconds };
        }
    }

    /// <summary>工位 / 梦境的位面机制参数（Inspector + F1 数值面板）。默认值 = 策划书。</summary>
    [Serializable]
    public sealed class PlaneMechanicsSettings
    {
        [Tooltip("数据版本。旧存档里没有这一组时读出来是 0，此时整组忽略、使用 Inspector 数值。")]
        [HideInInspector] public int version = 1;

        [Header("工位 · 困意")]
        [Tooltip("连续停留超过多少秒开始掉血")]
        [Min(0f)] public float drowsyDelay = 5f;
        [Tooltip("困意每秒伤害")]
        [Min(0f)] public float drowsyDps = 4f;
        [Tooltip("从进入工位到画面最暗 / 眼皮合到最紧的秒数（纯表现）")]
        [Min(0.1f)] public float drowsyDarkenTime = 10f;

        [Header("梦境 · 减速")]
        [Tooltip("每隔多少秒叠一层减速")]
        [Min(0.05f)] public float dreamSlowInterval = 2f;
        [Tooltip("每层减速比例（0.05 = 5%）")]
        [UnityEngine.Range(0f, 0.5f)] public float dreamSlowPerStack = 0.05f;
        [Tooltip("减速上限（0.4 = 40%，移速 4.2 → 2.52）")]
        [UnityEngine.Range(0f, 0.95f)] public float dreamSlowMax = 0.4f;

        [Header("梦境 · 老板来电")]
        [Tooltip("预警时长（秒）")]
        [Min(0.05f)] public float phoneWarning = 0.8f;
        [Tooltip("砸落半径")]
        [Min(0.1f)] public float phoneRadius = 2.5f;
        [Tooltip("砸中伤害（只伤玩家）")]
        [Min(0f)] public float phoneDamage = 25f;
        [Tooltip("两次来电的最短间隔（秒）")]
        [Min(0.5f)] public float phoneIntervalMin = 4f;
        [Tooltip("两次来电的最长间隔（秒）")]
        [Min(0.5f)] public float phoneIntervalMax = 6f;

        public void ApplyTo(EnvironmentConfig c)
        {
            if (c == null || version <= 0) return;
            c.DrowsyDelay = drowsyDelay;
            c.DrowsyDps = drowsyDps;
            c.DrowsyDarkenTime = drowsyDarkenTime;
            c.DreamSlowInterval = dreamSlowInterval;
            c.DreamSlowPerStack = dreamSlowPerStack;
            c.DreamSlowMax = dreamSlowMax;
            c.PhoneWarning = phoneWarning;
            c.PhoneRadius = phoneRadius;
            c.PhoneDamage = phoneDamage;
            c.PhoneIntervalMin = phoneIntervalMin;
            c.PhoneIntervalMax = Mathf.Max(phoneIntervalMin, phoneIntervalMax);
        }
    }

    /// <summary>一个位面在场景里的部件（火山 / 海洋之外的位面用它，M1 起在场景里建好后拖进来）。</summary>
    [Serializable]
    public sealed class PlaneSceneSlot
    {
        public GameObject root;
        public SpriteRenderer ground;
        public TerrainGrid terrain;
        public Transform spawnPoints;
    }

    /// <summary>
    /// 位面管理（节点 MapManager）。火山 / 海洋沿用原来的字段；工位 / 梦境 / 原始 / 未来用 PlaneSceneSlot，没拖的为空，安全跳过。
    /// VolcanoRoot 与 OceanRoot 在同一个 Scene、同一个世界坐标空间：两个根节点都固定在原点、不旋转、不缩放，
    /// 两张地面都按 WorldBounds 铺满，两张地形网格都覆盖 WorldBounds —— 尺寸与坐标范围完全一致。
    /// 切换：收到 PlaneSwitchedEvent 后用 SetActive 互斥显隐（同一时刻只有一个 Root 激活）。
    /// 水池与岩浆池一一对应：ShallowWaterGrid 的形状总是从 LavaGrid 复制（shareTerrainShape）。
    /// 岩浆：收到 LavaEruptedEvent 后亮一下再淡出（数据层面岩浆已经消失）；浅水一直显示。
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

        [Header("其他位面（场景搭好后拖进来；为空时该位面没有画面 / 地形 / 刷怪点）")]
        [SerializeField] private PlaneSceneSlot office = new PlaneSceneSlot();
        [SerializeField] private PlaneSceneSlot dream = new PlaneSceneSlot();
        [SerializeField] private PlaneSceneSlot primal = new PlaneSceneSlot();
        [SerializeField] private PlaneSceneSlot future = new PlaneSceneSlot();

        [Header("环境参数")]
        [SerializeField] private EnvironmentSettings environment = new EnvironmentSettings();

        [Header("关卡")]
        [SerializeField] private LevelSettings level = new LevelSettings();

        [Header("水池 / 岩浆池")]
        [Tooltip("勾选：海洋的浅水池完全复制火山岩浆池的大小、形状、位置（一一对应）。只需要编辑 / 随机生成 LavaGrid。")]
        [SerializeField] private bool shareTerrainShape = true;
        [Tooltip("岩浆喷发后保持完全可见的时间（秒，纯表现）")]
        [SerializeField] private float lavaHoldTime = 0.25f;
        [Tooltip("岩浆随后淡出消失的时间（秒，纯表现）")]
        [SerializeField] private float lavaFadeTime = 0.5f;

        [Header("运行时只读")]
        [SerializeField] private PlaneId activeMap;

        private ITurnContext _ctx;
        private float _lavaShowTimer = -1f;

        public EnvironmentSettings Environment { get { return environment; } }
        public LevelSettings Level { get { return level; } }
        public float TerrainCellSize { get { return environment.terrainCellSize; } }
        public WorldBounds Bounds { get { return worldBounds; } }

        private PlaneSceneSlot Slot(PlaneId plane)
        {
            switch (plane)
            {
                case PlaneId.Office: return office;
                case PlaneId.Dream: return dream;
                case PlaneId.Primal: return primal;
                case PlaneId.Future: return future;
                default: return null;
            }
        }

        public GameObject GetRoot(PlaneId plane)
        {
            if (plane == PlaneId.Volcano) return volcanoRoot;
            if (plane == PlaneId.Ocean) return oceanRoot;
            PlaneSceneSlot slot = Slot(plane);
            return slot != null ? slot.root : null;
        }

        public SpriteRenderer GetGround(PlaneId plane)
        {
            if (plane == PlaneId.Volcano) return volcanoGround;
            if (plane == PlaneId.Ocean) return oceanGround;
            PlaneSceneSlot slot = Slot(plane);
            return slot != null ? slot.ground : null;
        }

        public TerrainGrid GetTerrain(PlaneId plane)
        {
            if (plane == PlaneId.Volcano) return lavaGrid;
            if (plane == PlaneId.Ocean) return shallowWaterGrid;
            PlaneSceneSlot slot = Slot(plane);
            return slot != null ? slot.terrain : null;
        }

        public Transform GetSpawnPointRoot(PlaneId plane)
        {
            if (plane == PlaneId.Volcano) return volcanoSpawnPoints;
            if (plane == PlaneId.Ocean) return oceanSpawnPoints;
            PlaneSceneSlot slot = Slot(plane);
            return slot != null ? slot.spawnPoints : null;
        }

        /// <summary>收集某个位面 Root 下所有启用的遮挡物（Root 本身隐藏也能收集；子物体被单独关掉的不算）。</summary>
        public void CollectObstacles(PlaneId plane, List<ObstacleData> results)
        {
            results.Clear();
            GameObject root = GetRoot(plane);
            if (root == null) return;
            Obstacle[] all = root.GetComponentsInChildren<Obstacle>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Obstacle o = all[i];
                if (o.enabled && ActiveUnder(o.transform, root.transform)) results.Add(o.ToData());
            }
        }

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                if (!p.gameObject.activeSelf) return false;
            }
            return true;
        }

        public void CollectSpawnPoints(PlaneId map, List<Vec2> results)
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

        // ---------------- 水池与岩浆池一一对应 ----------------

        /// <summary>按边界确保 LavaGrid 尺寸正确，然后让 ShallowWaterGrid 复制它的形状。</summary>
        public void SyncTerrainShape(Rect bounds)
        {
            if (!shareTerrainShape || lavaGrid == null || shallowWaterGrid == null) return;
            if (!lavaGrid.Matches(bounds, TerrainCellSize)) lavaGrid.Generate(bounds, TerrainCellSize);
            shallowWaterGrid.CopyShapeFrom(lavaGrid);
        }

        /// <summary>编辑器里用：按场景里的 WorldBounds 同步一次。</summary>
        public void SyncTerrainShape()
        {
            if (worldBounds != null) SyncTerrainShape(worldBounds.Rect);
            else if (shareTerrainShape && lavaGrid != null && shallowWaterGrid != null) shallowWaterGrid.CopyShapeFrom(lavaGrid);
#if UNITY_EDITOR
            if (!Application.isPlaying && shallowWaterGrid != null) UnityEditor.EditorUtility.SetDirty(shallowWaterGrid);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 编辑器里打开场景 / 改参数时，让水池的显示也立刻和岩浆池一致（OnValidate 里不能直接改别的组件，延后一帧）
            if (Application.isPlaying || !shareTerrainShape) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && shallowWaterGrid != null && lavaGrid != null) shallowWaterGrid.CopyShapeFrom(lavaGrid);
            };
        }
#endif

        // ---------------- 绑定 ----------------

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            AlignRoots();
            if (worldBounds != null && worldBounds.DebugLineMaterial != null) Obstacle.DebugMaterial = worldBounds.DebugLineMaterial;
            ctx.Events.Subscribe<PlaneSwitchedEvent>(OnMapSwitched);
            ctx.Events.Subscribe<LavaEruptedEvent>(OnLavaErupted);
            _lavaShowTimer = -1f;
            if (lavaGrid != null) lavaGrid.SetAlpha(0f); // 岩浆平时不存在，只在喷发时亮一下
            Apply(ctx.Plane.Active);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<PlaneSwitchedEvent>(OnMapSwitched);
            _ctx.Events.Unsubscribe<LavaEruptedEvent>(OnLavaErupted);
            _ctx = null;
        }

        private void OnLavaErupted(LavaEruptedEvent evt)
        {
            _lavaShowTimer = 0f;
            if (lavaGrid != null) lavaGrid.SetAlpha(1f);
        }

        private void Update()
        {
            if (_lavaShowTimer < 0f || lavaGrid == null) return;
            _lavaShowTimer += Time.deltaTime;
            float t = _lavaShowTimer - Mathf.Max(0f, lavaHoldTime);
            float alpha = t <= 0f ? 1f : 1f - t / Mathf.Max(0.01f, lavaFadeTime);
            if (alpha <= 0f)
            {
                alpha = 0f;
                _lavaShowTimer = -1f;
            }
            lavaGrid.SetAlpha(alpha);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnMapSwitched(PlaneSwitchedEvent evt)
        {
            Apply(evt.To);
            if (evt.To != PlaneId.Volcano && lavaGrid != null)
            {
                _lavaShowTimer = -1f;
                lavaGrid.SetAlpha(0f); // 离开火山时岩浆已经消失
            }
        }

        /// <summary>互斥显隐：先关掉其他所有位面再开目标位面，任何时刻都不会两张同时激活。</summary>
        public void Apply(PlaneId map)
        {
            activeMap = map;
            GameObject target = GetRoot(map);
            for (int i = 0; i < PlaneIds.Count; i++)
            {
                GameObject root = GetRoot((PlaneId)i);
                if (root != null && root != target) root.SetActive(false);
            }
            if (target != null) target.SetActive(true);
        }

        // ---------------- 同空间对齐 ----------------

        /// <summary>两个根节点固定在原点；两张地面按边界铺满。保证“同一空间、同一尺寸”。</summary>
        public void AlignRoots()
        {
            for (int i = 0; i < PlaneIds.Count; i++) ResetTransform(GetRoot((PlaneId)i));
            if (worldBounds == null) return;
            for (int i = 0; i < PlaneIds.Count; i++) FitGround(GetGround((PlaneId)i), worldBounds);
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
