using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>遮挡物挡什么（Inspector 用；转换成模拟层的 ObstacleMask）。</summary>
    public enum ObstacleKind
    {
        /// <summary>实体：挡人也挡子弹（熔岩巨石、办公桌、集装箱……）。</summary>
        Solid = 0,
        /// <summary>软遮挡 · 只挡人：子弹能穿过（文件堆、灌木……）。</summary>
        BlocksUnitsOnly = 1,
        /// <summary>软遮挡 · 只挡弹：人能走过（云朵平台、全息挡板……）。</summary>
        BlocksProjectilesOnly = 2,
    }

    /// <summary>
    /// 遮挡物（程序A）。挂在某个位面 Root 下面的任意物体上，开局时 MapManager 按位面收集、
    /// TurnConfigReader 转成纯数据交给模拟层的 ObstacleSystem —— 之后 Root 是否被隐藏都不影响判定。
    /// 判定形状由这里的参数决定，与美术图大小无关：美术换图后在 Scene 视图里对照 Gizmo 调 radius / size。
    /// 物体缩放会乘进判定尺寸；旋转不生效（矩形永远是轴对齐的）。
    /// 运行时调试：F2 或 F1 数值面板里的开关显示所有判定框（红 = 实体，黄 = 只挡人，蓝 = 只挡弹）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Obstacle : MonoBehaviour
    {
        /// <summary>运行时是否显示判定框（F2 / F1 面板开关，不保存）。</summary>
        public static bool ShowDebug;
        /// <summary>判定框线条用的材质（MapManager 绑定时从 WorldBounds 的边界线取）。</summary>
        public static Material DebugMaterial;

        private const int CircleSegments = 32;
        private const float DebugWidth = 0.06f;

        [Tooltip("挡什么：实体 = 人和子弹都挡；只挡人 = 子弹能穿过；只挡弹 = 人能走过")]
        [SerializeField] private ObstacleKind kind = ObstacleKind.Solid;
        [Tooltip("判定形状。矩形是轴对齐的，物体旋转不影响判定。")]
        [SerializeField] private ObstacleShape shape = ObstacleShape.Circle;
        [Tooltip("圆形判定半径（世界单位，会乘以物体缩放）")]
        [Min(0.05f)] [SerializeField] private float radius = 0.5f;
        [Tooltip("矩形判定宽高（世界单位，会乘以物体缩放）")]
        [SerializeField] private Vector2 size = Vector2.one;
        [Tooltip("判定中心相对物体位置的偏移")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        private LineRenderer _debugLine;
        private bool _debugShown;

        public ObstacleKind Kind { get { return kind; } }

        public ObstacleMask Mask
        {
            get
            {
                switch (kind)
                {
                    case ObstacleKind.BlocksUnitsOnly: return ObstacleMask.Units;
                    case ObstacleKind.BlocksProjectilesOnly: return ObstacleMask.Projectiles;
                    default: return ObstacleMask.Solid;
                }
            }
        }

        /// <summary>编辑器工具用：一次性设置全部参数。</summary>
        public void Configure(ObstacleKind newKind, ObstacleShape newShape, float newRadius, Vector2 newSize)
        {
            kind = newKind;
            shape = newShape;
            radius = Mathf.Max(0.05f, newRadius);
            size = newSize;
            offset = Vector2.zero;
        }

        /// <summary>交给模拟层的纯数据（世界坐标，已乘缩放）。</summary>
        public ObstacleData ToData()
        {
            Vector3 s = transform.lossyScale;
            Vector2 center = (Vector2)transform.position + new Vector2(offset.x * s.x, offset.y * s.y);
            var data = new ObstacleData
            {
                Shape = shape,
                Center = ViewMath.ToVec2(center),
                Mask = Mask,
            };
            if (shape == ObstacleShape.Circle)
            {
                data.Radius = radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            }
            else
            {
                data.HalfSize = new Vec2(Mathf.Abs(size.x * s.x) * 0.5f, Mathf.Abs(size.y * s.y) * 0.5f);
            }
            return data;
        }

        public static Color KindColor(ObstacleKind k)
        {
            switch (k)
            {
                case ObstacleKind.BlocksUnitsOnly: return new Color(1f, 0.82f, 0.2f, 1f);
                case ObstacleKind.BlocksProjectilesOnly: return new Color(0.3f, 0.8f, 1f, 1f);
                default: return new Color(1f, 0.25f, 0.25f, 1f);
            }
        }

        // ---------------- 运行时调试显示 ----------------

        private void LateUpdate()
        {
            bool want = ShowDebug;
            if (want == _debugShown) return;
            _debugShown = want;
            if (want) BuildDebugLine();
            if (_debugLine != null) _debugLine.enabled = want;
        }

        private void BuildDebugLine()
        {
            if (_debugLine == null)
            {
                var go = new GameObject("ObstacleDebug");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);
                _debugLine = go.AddComponent<LineRenderer>();
                _debugLine.useWorldSpace = true;
                _debugLine.loop = true;
                _debugLine.sortingOrder = ViewMath.ObstacleDebugOrder;
            }
            if (DebugMaterial != null) _debugLine.sharedMaterial = DebugMaterial;
            Color c = KindColor(kind);
            _debugLine.startColor = c;
            _debugLine.endColor = c;
            _debugLine.startWidth = DebugWidth;
            _debugLine.endWidth = DebugWidth;

            ObstacleData d = ToData();
            Vector3 center = ViewMath.ToWorld(d.Center);
            if (d.Shape == ObstacleShape.Circle)
            {
                _debugLine.positionCount = CircleSegments;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / CircleSegments;
                    _debugLine.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * d.Radius);
                }
            }
            else
            {
                float hx = d.HalfSize.X;
                float hy = d.HalfSize.Y;
                _debugLine.positionCount = 4;
                _debugLine.SetPosition(0, center + new Vector3(-hx, -hy, 0f));
                _debugLine.SetPosition(1, center + new Vector3(hx, -hy, 0f));
                _debugLine.SetPosition(2, center + new Vector3(hx, hy, 0f));
                _debugLine.SetPosition(3, center + new Vector3(-hx, hy, 0f));
            }
        }

        // ---------------- Scene 视图 ----------------

        private void OnDrawGizmos()
        {
            Gizmos.color = KindColor(kind);
            ObstacleData d = ToData();
            Vector3 center = ViewMath.ToWorld(d.Center);
            if (d.Shape == ObstacleShape.Circle) Gizmos.DrawWireSphere(center, d.Radius);
            else Gizmos.DrawWireCube(center, new Vector3(d.HalfSize.X * 2f, d.HalfSize.Y * 2f, 0f));
        }
    }
}
