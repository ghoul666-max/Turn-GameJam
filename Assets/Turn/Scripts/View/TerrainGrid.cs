using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 一张地图的地形网格数据 + 显示（节点 VolcanoRoot/LavaGrid、OceanRoot/ShallowWaterGrid）。
    /// 网格覆盖整个 WorldBounds，两张地图的列数 / 行数 / 格子尺寸 / 原点完全一致，只有格子内容不同。
    /// 格子内容序列化在本组件里（cells）；可以用右键菜单“重新随机生成地形”按参数生成。
    /// 开局时 ToData() 拷贝一份纯数据交给模拟层的 TerrainSystem —— 之后 Root 是否被隐藏都不影响检测。
    /// 显示：每格 1 像素的贴图拉伸到整张地图（Point 过滤），只画 paintType 的格子。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TerrainGrid : MonoBehaviour
    {
        [Tooltip("这张网格画的地形：火山填 Lava，海洋填 ShallowWater")]
        [SerializeField] private TerrainType paintType = TerrainType.Lava;
        [SerializeField] private Color cellColor = new Color(1f, 0.4f, 0.05f, 0.85f);

        [Header("随机生成参数（右键组件 → 重新随机生成地形）")]
        [SerializeField] private int seed = 1;
        [Tooltip("圆形区域的数量")]
        [SerializeField] private int blobCount = 7;
        [SerializeField] private float blobRadiusMin = 1.5f;
        [SerializeField] private float blobRadiusMax = 3.2f;
        [Tooltip("地图中心（出生点）周围保持为普通地面的半径")]
        [SerializeField] private float safeRadius = 3f;

        [Header("网格（由生成器写入，请勿手改）")]
        [SerializeField] private int columns;
        [SerializeField] private int rows;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 origin;
        [SerializeField] private TerrainType[] cells = new TerrainType[0];

        [NonSerialized] private Texture2D _texture;
        [NonSerialized] private Sprite _sprite;

        public TerrainType PaintType { get { return paintType; } }
        public int Columns { get { return columns; } }
        public int Rows { get { return rows; } }
        public float CellSize { get { return cellSize; } }

        /// <summary>交给模拟层的纯数据（拷贝）。网格尺寸与边界不一致时按边界重新生成，保证两张图对齐。</summary>
        public TerrainGridData ToData(Rect bounds, float wantedCellSize)
        {
            if (!Matches(bounds, wantedCellSize)) Generate(bounds, wantedCellSize);
            return new TerrainGridData(columns, rows, cellSize, new Vec2(origin.x, origin.y), cells);
        }

        public bool Matches(Rect bounds, float wantedCellSize)
        {
            int cols = Mathf.Max(1, Mathf.CeilToInt(bounds.width / Mathf.Max(0.1f, wantedCellSize)));
            int rws = Mathf.Max(1, Mathf.CeilToInt(bounds.height / Mathf.Max(0.1f, wantedCellSize)));
            return cells != null && cells.Length == cols * rws && columns == cols && rows == rws &&
                   Mathf.Approximately(cellSize, wantedCellSize) && (origin - bounds.min).sqrMagnitude < 0.0001f;
        }

        /// <summary>按边界与格子尺寸重建网格，并用 seed 随机撒若干个圆形区域。</summary>
        public void Generate(Rect bounds, float wantedCellSize)
        {
            cellSize = Mathf.Max(0.1f, wantedCellSize);
            columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cellSize));
            rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cellSize));
            origin = bounds.min;
            cells = new TerrainType[columns * rows];

            var rng = new System.Random(seed);
            Vector2 c = bounds.center;
            for (int b = 0; b < blobCount; b++)
            {
                float r = Mathf.Lerp(blobRadiusMin, blobRadiusMax, (float)rng.NextDouble());
                Vector2 p = new Vector2(
                    Mathf.Lerp(bounds.xMin + r, bounds.xMax - r, (float)rng.NextDouble()),
                    Mathf.Lerp(bounds.yMin + r, bounds.yMax - r, (float)rng.NextDouble()));
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        Vector2 cell = origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
                        if ((cell - c).sqrMagnitude < safeRadius * safeRadius) continue;
                        // 边缘加一点噪声，看起来不那么像正圆
                        float wobble = Mathf.PerlinNoise(x * 0.35f + seed, y * 0.35f + b) * 0.8f;
                        if ((cell - p).magnitude <= r + wobble - 0.4f) cells[y * columns + x] = paintType;
                    }
                }
            }
            RefreshVisual();
        }

        public void SetSeed(int value)
        {
            seed = value;
        }

        /// <summary>
        /// 一一对应：完全复制 source 的网格尺寸、原点与形状，只把 source 的地形类型换成本网格的 paintType。
        /// 海洋的浅水池用它复制火山的岩浆池 —— 两张图的池子大小、形状、位置完全一样。
        /// </summary>
        public void CopyShapeFrom(TerrainGrid source)
        {
            if (source == null || source == this || source.cells == null) return;
            columns = source.columns;
            rows = source.rows;
            cellSize = source.cellSize;
            origin = source.origin;
            cells = new TerrainType[source.cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = source.cells[i] == source.paintType ? paintType : TerrainType.Normal;
            }
            RefreshVisual();
        }

        /// <summary>整张网格显示的透明度（岩浆喷发后淡出用；不影响数据）。</summary>
        public void SetAlpha(float alpha)
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        [ContextMenu("重新随机生成地形")]
        private void RegenerateFromScene()
        {
            WorldBounds bounds = null;
            MapManager map = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (bounds == null) bounds = root.GetComponentInChildren<WorldBounds>(true);
                if (map == null) map = root.GetComponentInChildren<MapManager>(true);
            }
            if (bounds == null)
            {
                Debug.LogWarning("[Turn] 场景里没有 WorldBounds，无法生成地形。");
                return;
            }
            float size = map != null ? map.TerrainCellSize : cellSize;
            seed = System.Environment.TickCount;
            Generate(bounds.Rect, size);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            if (map != null) map.SyncTerrainShape(); // 水池跟着岩浆池走
        }

        // ---------------- 显示 ----------------

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void OnDestroy()
        {
            ReleaseVisual();
        }

        /// <summary>重建一张“每格一像素”的贴图并铺满整张地图。</summary>
        public void RefreshVisual()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null || columns <= 0 || rows <= 0 || cells == null || cells.Length != columns * rows) return;

            ReleaseVisual();
            _texture = new Texture2D(columns, rows, TextureFormat.RGBA32, false);
            _texture.name = name + "_Cells";
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.hideFlags = HideFlags.DontSave;

            var pixels = new Color32[columns * rows];
            Color32 on = cellColor;
            var off = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = cells[i] == paintType ? on : off;
            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);

            // pixelsPerUnit = 1 / cellSize：贴图的 1 像素正好是 1 格
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, columns, rows), new Vector2(0f, 0f), 1f / cellSize);
            _sprite.name = name + "_Sprite";
            _sprite.hideFlags = HideFlags.DontSave;

            renderer.sprite = _sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = ViewMath.TerrainOrder;
            transform.position = new Vector3(origin.x, origin.y, 0f); // 枢轴在左下角 = 网格原点
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void ReleaseVisual()
        {
            if (_sprite != null) DestroyNow(_sprite);
            if (_texture != null) DestroyNow(_texture);
            _sprite = null;
            _texture = null;
        }

        private static void DestroyNow(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(cellColor.r, cellColor.g, cellColor.b, 1f);
            Vector3 c = new Vector3(origin.x + columns * cellSize * 0.5f, origin.y + rows * cellSize * 0.5f, 0f);
            Gizmos.DrawWireCube(c, new Vector3(columns * cellSize, rows * cellSize, 0f));
        }
    }
}
