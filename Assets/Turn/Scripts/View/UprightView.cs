using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Turn.View
{
    /// <summary>
    /// 2.5D：倾斜相机时让玩家 / 怪物立起来面向镜头（billboard）。PlayerController / MonsterController 自动挂上。
    ///
    /// 做法：运行时在根节点下建一个 Upright 子节点，把「该立起来」的直接子物体（身体、贴图、血条、头顶印记…）
    /// 挪进去（局部坐标不变），每帧把 Upright 的世界旋转设成 <see cref="CameraRig.Upright"/>。
    /// 贴地的子物体（阴影、地形标记、朝向箭头、护盾圈、武器节点）留在根节点上，继续平铺在地面。
    /// 之后才创建的子物体（怪物的 Art / Marks）下一帧自动收进去。
    /// 前后遮挡：倾斜时给 Upright 打开 SortingGroup（统一 sortingOrder = MonsterOrder），
    /// 由相机深度决定前后（画面靠下的挡住靠上的），组内各部件的先后不变。倾斜角 = 0 时关闭，和原来完全一样。
    /// </summary>
    [DefaultExecutionOrder(200)] // 在控制器写完子物体动画、CameraFollow 更新完姿态之后
    public sealed class UprightView : MonoBehaviour
    {
        public const string PivotName = "Upright";

        /// <summary>留在地面上、不跟着立起来的子物体名字。</summary>
        private static readonly HashSet<string> GroundChildren = new HashSet<string>
        {
            "Shadow", "FootShadow", "TerrainMarker", "FacingArrow", "ShieldRing", "Weapon",
        };

        private Transform _pivot;
        private SortingGroup _group;
        private int _childCount = -1;

        /// <summary>给一个根节点挂上（已经有就直接返回）。</summary>
        public static UprightView Ensure(GameObject go)
        {
            if (go == null) return null;
            UprightView u = go.GetComponent<UprightView>();
            if (u == null) u = go.AddComponent<UprightView>();
            u.EnsurePivot();
            u.Collect();
            return u;
        }

        private void EnsurePivot()
        {
            if (_pivot != null) return;
            Transform existing = transform.Find(PivotName);
            if (existing != null) _pivot = existing;
            else
            {
                var go = new GameObject(PivotName);
                _pivot = go.transform;
                _pivot.SetParent(transform, false);
            }
            _group = _pivot.GetComponent<SortingGroup>();
            if (_group == null) _group = _pivot.gameObject.AddComponent<SortingGroup>();
            _group.sortingOrder = ViewMath.MonsterOrder;
            _group.enabled = false;
        }

        /// <summary>把根节点下新出现的、该立起来的子物体收进 Upright（局部坐标原样保留）。</summary>
        private void Collect()
        {
            if (_pivot == null) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c == _pivot || GroundChildren.Contains(c.name)) continue;
                c.SetParent(_pivot, false);
            }
            _childCount = transform.childCount;
        }

        private void LateUpdate()
        {
            EnsurePivot();
            if (transform.childCount != _childCount) Collect();

            if (CameraRig.Standing)
            {
                _pivot.rotation = CameraRig.Upright;
                Vector3 lift = new Vector3(0f, 0f, -CameraRig.Lift);
                _pivot.localPosition = transform.InverseTransformVector(lift);
            }
            else
            {
                _pivot.localRotation = Quaternion.identity;
                _pivot.localPosition = Vector3.zero;
            }

            bool group = CameraRig.DepthSort;
            if (_group.enabled != group) _group.enabled = group;
        }
    }
}
