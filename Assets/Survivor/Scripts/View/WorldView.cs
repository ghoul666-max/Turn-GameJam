using UnityEngine;
using Survivor.Art;

namespace Survivor.View
{
    /// <summary>
    /// 世界画面：一台俯视的透视相机 + 无限平铺的地面 + 两层靠近相机的树冠剪影。
    /// 角色、宝石、投射物都在 Z = 0 平面上；树冠在 Z = -2.6 / -5.2（更靠近相机），
    /// 相机跟随玩家移动时近处的树冠滑得更快 —— 和卡牌白模“2D 素材在 3D 空间里分层搭景”同一个视差原理。
    /// 另外负责：按主题给地面与树冠统一染色（角色不参与染色，保持可读性）、镜头跟随与震动。
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const float CameraDistance = 10f;
        public const float FieldOfView = 60f;
        /// <summary>地面贴图 64 像素 / PPU 16 = 4 世界单位。</summary>
        public const float TileSize = 4f;
        public const int TilesX = 8;
        public const int TilesY = 6;
        public const float FollowSharpness = 6f;
        /// <summary>镜头朝移动方向多看出去的距离。</summary>
        public const float LeadDistance = 0.8f;
        /// <summary>世界文字画布的缩放：1 世界单位 = 100 画布单位。</summary>
        public const float TextScale = 0.01f;

        private const float ShakeStep = 0.03f;
        private const float CanopyChunk = 7f;
        private const int CanopyPerLayer = 32;
        private static readonly float[] CanopyDepths = { -2.6f, -5.2f };
        private static readonly float[] CanopyChance = { 0.32f, 0.22f };
        private static readonly float[] CanopyAlpha = { 0.72f, 0.88f };

        private Camera _camera;
        private SpriteRenderer[] _tiles;
        private SpriteRenderer[][] _canopy;
        private Transform _entityRoot;
        private RectTransform _textRoot;
        private int _tileOriginX = int.MinValue;
        private int _tileOriginY = int.MinValue;

        private Vector2 _focus;
        private Vector2 _target;
        private float _shakeAmplitude;
        private float _shakeTimer;
        private Vector2 _shakeOffset;
        private Theme _theme;

        public Camera Camera { get { return _camera; } }
        /// <summary>实体视图的父节点。</summary>
        public Transform EntityRoot { get { return _entityRoot; } }
        /// <summary>世界空间文字（伤害数字）的父节点；用 WorldToText 换算坐标。</summary>
        public RectTransform TextRoot { get { return _textRoot; } }
        /// <summary>当前镜头中心（含震动）。</summary>
        public Vector2 CameraPosition
        {
            get { return _camera != null ? (Vector2)_camera.transform.position : _focus; }
        }

        private struct Theme
        {
            public Color Ground, Canopy, Background;
        }

        // 每个主题：地面是灰度图乘主题色；树冠几乎是纯剪影。
        private static readonly Theme[] Themes =
        {
            MakeTheme(C(96, 110, 92), C(8, 14, 14), C(14, 18, 18)),    // 月下墓园
            MakeTheme(C(122, 102, 82), C(20, 12, 10), C(22, 16, 14)),  // 荒原黄昏
            MakeTheme(C(82, 92, 122), C(6, 8, 18), C(10, 12, 20)),     // 冰窟
        };

        // ---------------- 创建 ----------------

        public static WorldView Create(Camera cam)
        {
            var go = new GameObject("[Survivor] World");
            var view = go.AddComponent<WorldView>();
            view.Build(cam);
            return view;
        }

        private void Build(Camera cam)
        {
            _camera = cam;
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = FieldOfView;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.transform.rotation = Quaternion.identity;
                cam.transform.position = new Vector3(0f, 0f, -CameraDistance);
            }

            var ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            _tiles = new SpriteRenderer[TilesX * TilesY];
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i] = SpriteKit.Create("Tile" + i, ground, SurvivorArt.Get(SurvivorArt.Ground0));
                _tiles[i].sortingOrder = ViewMath.GroundOrder;
            }

            _entityRoot = new GameObject("Entities").transform;
            _entityRoot.SetParent(transform, false);

            var textGo = new GameObject("WorldText", typeof(RectTransform), typeof(Canvas));
            textGo.transform.SetParent(transform, false);
            var canvas = textGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = ViewMath.TextOrder;
            _textRoot = (RectTransform)textGo.transform;
            _textRoot.sizeDelta = new Vector2(100f, 100f);
            _textRoot.localScale = new Vector3(TextScale, TextScale, TextScale);
            _textRoot.position = new Vector3(0f, 0f, -0.01f);

            var canopyRoot = new GameObject("Canopy").transform;
            canopyRoot.SetParent(transform, false);
            _canopy = new SpriteRenderer[CanopyDepths.Length][];
            for (int layer = 0; layer < CanopyDepths.Length; layer++)
            {
                _canopy[layer] = new SpriteRenderer[CanopyPerLayer];
                for (int i = 0; i < CanopyPerLayer; i++)
                {
                    SpriteRenderer r = SpriteKit.Create("Canopy" + layer + "_" + i, canopyRoot, SurvivorArt.Get(SurvivorArt.Canopy0));
                    r.sortingOrder = ViewMath.CanopyOrder + layer;
                    r.enabled = false;
                    _canopy[layer][i] = r;
                }
            }

            ApplyTheme(0);
        }

        // ---------------- 对外 ----------------

        public void ApplyTheme(int index)
        {
            if (index < 0) index = 0;
            _theme = Themes[index % Themes.Length];
            if (_camera != null) _camera.backgroundColor = _theme.Background;
            for (int i = 0; i < _tiles.Length; i++) _tiles[i].color = _theme.Ground;
            for (int layer = 0; layer < _canopy.Length; layer++)
            {
                Color c = _theme.Canopy;
                c.a = CanopyAlpha[layer];
                for (int i = 0; i < _canopy[layer].Length; i++) _canopy[layer][i].color = c;
            }
        }

        /// <summary>镜头跟随目标；lead 是移动方向（长度 ≤ 1），镜头会往那边多看一点。</summary>
        public void SetFollow(Vector2 position, Vector2 lead)
        {
            _target = position + lead * LeadDistance;
        }

        public void SnapTo(Vector2 position)
        {
            _target = position;
            _focus = position;
            _tileOriginX = int.MinValue;
        }

        /// <summary>镜头震动。amplitude 为世界单位（0.08 轻、0.3 重）。</summary>
        public void Shake(float amplitude)
        {
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
        }

        public static Vector2 WorldToText(Vector2 world)
        {
            return world / TextScale;
        }

        // ---------------- 每帧 ----------------

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _focus = Vector2.Lerp(_focus, _target, 1f - Mathf.Exp(-FollowSharpness * dt));

            if (_shakeAmplitude > 0.001f)
            {
                _shakeTimer += dt;
                if (_shakeTimer >= ShakeStep)
                {
                    _shakeTimer = 0f;
                    _shakeOffset = Random.insideUnitCircle * _shakeAmplitude;
                }
                _shakeAmplitude *= Mathf.Exp(-9f * dt);
            }
            else
            {
                _shakeAmplitude = 0f;
                _shakeOffset = Vector2.zero;
            }

            Vector2 cam = _focus + _shakeOffset;
            if (_camera != null) _camera.transform.position = new Vector3(cam.x, cam.y, -CameraDistance);

            UpdateGround(_focus);
            UpdateCanopy(cam);
        }

        /// <summary>地面只在镜头跨过一格时重新摆放；每格的花样由格子坐标哈希决定，来回走看到的是同一片地。</summary>
        private void UpdateGround(Vector2 focus)
        {
            int ox = Mathf.FloorToInt(focus.x / TileSize) - TilesX / 2;
            int oy = Mathf.FloorToInt(focus.y / TileSize) - TilesY / 2;
            if (ox == _tileOriginX && oy == _tileOriginY) return;
            _tileOriginX = ox;
            _tileOriginY = oy;

            for (int j = 0; j < TilesY; j++)
            {
                for (int i = 0; i < TilesX; i++)
                {
                    int cx = ox + i;
                    int cy = oy + j;
                    SpriteRenderer tile = _tiles[j * TilesX + i];
                    tile.transform.position = new Vector3((cx + 0.5f) * TileSize, (cy + 0.5f) * TileSize, 0f);

                    float h = SurvivorArt.Hash(cx, cy, 5);
                    int variant = h < 0.55f ? 0 : (h < 0.85f ? 1 : 2);
                    tile.sprite = SurvivorArt.Get(SurvivorArt.GroundKeys[variant]);
                }
            }
        }

        /// <summary>树冠按“块”哈希决定有没有、在哪、多大。每层只处理该层平面上看得见的块。</summary>
        private void UpdateCanopy(Vector2 cam)
        {
            float tanHalf = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = _camera != null ? _camera.aspect : 16f / 9f;

            for (int layer = 0; layer < CanopyDepths.Length; layer++)
            {
                float depth = CanopyDepths[layer];
                float dist = CameraDistance + depth;
                float halfH = dist * tanHalf + CanopyChunk * 0.5f;
                float halfW = halfH * aspect + CanopyChunk * 0.5f;

                int minX = Mathf.FloorToInt((cam.x - halfW) / CanopyChunk) - 1;
                int maxX = Mathf.FloorToInt((cam.x + halfW) / CanopyChunk) + 1;
                int minY = Mathf.FloorToInt((cam.y - halfH) / CanopyChunk) - 1;
                int maxY = Mathf.FloorToInt((cam.y + halfH) / CanopyChunk) + 1;

                SpriteRenderer[] pool = _canopy[layer];
                int used = 0;
                for (int cy = minY; cy <= maxY && used < pool.Length; cy++)
                {
                    for (int cx = minX; cx <= maxX && used < pool.Length; cx++)
                    {
                        int seed = 50 + layer * 10;
                        if (SurvivorArt.Hash(cx, cy, seed) > CanopyChance[layer]) continue;

                        float px = (cx + SurvivorArt.Hash(cx, cy, seed + 1)) * CanopyChunk;
                        float py = (cy + SurvivorArt.Hash(cx, cy, seed + 2)) * CanopyChunk;
                        float scale = 1.6f + SurvivorArt.Hash(cx, cy, seed + 3) * 1.2f;
                        int variant = SurvivorArt.Hash(cx, cy, seed + 4) < 0.5f ? 0 : 1;

                        SpriteRenderer r = pool[used++];
                        r.enabled = true;
                        r.sprite = SurvivorArt.Get(SurvivorArt.CanopyKeys[variant]);
                        r.transform.position = new Vector3(px, py, depth);
                        r.transform.localScale = new Vector3(scale, scale, 1f);
                        r.transform.rotation = Quaternion.Euler(0f, 0f, SurvivorArt.Hash(cx, cy, seed + 5) * 360f);
                    }
                }
                for (int i = used; i < pool.Length; i++) pool[i].enabled = false;
            }
        }

        private static Theme MakeTheme(Color ground, Color canopy, Color background)
        {
            var theme = new Theme();
            theme.Ground = ground;
            theme.Canopy = canopy;
            theme.Background = background;
            return theme;
        }

        private static Color C(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }
    }
}
