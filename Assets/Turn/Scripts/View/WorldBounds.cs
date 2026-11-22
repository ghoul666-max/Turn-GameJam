using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 统一移动边界（节点 WorldBounds）。两张地图、玩家、普通怪物、鲨鱼共用这一个矩形。
    /// 这里只是“配置 + 可视化”：开局时转成 BoundsRect 交给模拟层的 BoundsSystem，真正的限制在那里做。
    /// 子节点：BoundsCollider（BoxCollider2D，isTrigger，只作为 Scene 视图里可见 / 可选中的边框，不参与物理）、
    ///         BoundsDebug（LineRenderer，运行时也能看到边界线）。
    /// 在 Inspector 里改 center / size 会同步到两个子节点，以及 MapManager 管理的两张地图。
    /// </summary>
    [ExecuteAlways]
    public sealed class WorldBounds : MonoBehaviour
    {
        [Tooltip("边界中心（世界坐标）")]
        [SerializeField] private Vector2 center = Vector2.zero;
        [Tooltip("地图尺寸 = 移动边界尺寸（世界单位）。两张地图完全一致。")]
        [SerializeField] private Vector2 size = new Vector2(40f, 24f);

        [Header("可视化（生成器自动拖好）")]
        [SerializeField] private BoxCollider2D boundsCollider;
        [SerializeField] private LineRenderer boundsDebug;
        [SerializeField] private Color debugColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        [SerializeField] private float debugWidth = 0.12f;

        public Vector2 Center { get { return center; } }
        public Vector2 Size { get { return size; } }
        public Rect Rect { get { return new Rect(center - size * 0.5f, size); } }
        /// <summary>边界线的材质（遮挡物调试线复用它）。</summary>
        public Material DebugLineMaterial { get { return boundsDebug != null ? boundsDebug.sharedMaterial : null; } }

        public BoundsRect ToData()
        {
            return new BoundsRect(ViewMath.ToVec2(center), ViewMath.ToVec2(size));
        }

        private void OnEnable()
        {
            SyncHelpers();
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(2f, size.x);
            size.y = Mathf.Max(2f, size.y);
            SyncHelpers();
        }

        /// <summary>把尺寸同步到 BoxCollider2D 与边界线。</summary>
        public void SyncHelpers()
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (boundsCollider != null)
            {
                boundsCollider.isTrigger = true;
                boundsCollider.offset = center;
                boundsCollider.size = size;
            }

            if (boundsDebug != null)
            {
                Vector2 h = size * 0.5f;
                boundsDebug.useWorldSpace = true;
                boundsDebug.loop = true;
                boundsDebug.positionCount = 4;
                boundsDebug.SetPosition(0, new Vector3(center.x - h.x, center.y - h.y, 0f));
                boundsDebug.SetPosition(1, new Vector3(center.x + h.x, center.y - h.y, 0f));
                boundsDebug.SetPosition(2, new Vector3(center.x + h.x, center.y + h.y, 0f));
                boundsDebug.SetPosition(3, new Vector3(center.x - h.x, center.y + h.y, 0f));
                boundsDebug.startWidth = debugWidth;
                boundsDebug.endWidth = debugWidth;
                boundsDebug.startColor = debugColor;
                boundsDebug.endColor = debugColor;
                boundsDebug.sortingOrder = ViewMath.BoundsOrder;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = debugColor;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
        }
    }
}
