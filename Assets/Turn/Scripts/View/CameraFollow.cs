using UnityEngine;

namespace Turn.View
{
    /// <summary>正交相机跟随玩家，并把视野限制在 WorldBounds 内（地图比屏幕大时不露出边界外）。</summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // 在 PlayerController.LateUpdate 同步完玩家位置之后再跟随
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private WorldBounds bounds;
        [SerializeField] private float sharpness = 8f;
        [SerializeField] private float orthographicSize = 7f;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 p = Clamp(target.position);
            transform.position = new Vector3(p.x, p.y, -10f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = Clamp(target.position);
            Vector3 current = transform.position;
            Vector3 next = Vector3.Lerp(current, goal, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            transform.position = new Vector3(next.x, next.y, -10f);
        }

        private Vector3 Clamp(Vector3 p)
        {
            if (bounds == null || _camera == null) return p;
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            Rect r = bounds.Rect;
            float x = r.width <= halfW * 2f ? r.center.x : Mathf.Clamp(p.x, r.xMin + halfW, r.xMax - halfW);
            float y = r.height <= halfH * 2f ? r.center.y : Mathf.Clamp(p.y, r.yMin + halfH, r.yMax - halfH);
            return new Vector3(x, y, p.z);
        }
    }
}
