using UnityEngine;

namespace Turn.View
{
    /// <summary>只附着于全屏Canvas的内容容器，不改变相机或战斗世界视野。</summary>
    [DisallowMultipleComponent]
    public sealed class MobileSafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _last;
        private Vector2Int _size;
        public static void Attach(RectTransform target)
        {
            if (target != null && target.GetComponent<MobileSafeArea>() == null) target.gameObject.AddComponent<MobileSafeArea>();
        }
        private void Awake() { _rect = (RectTransform)transform; Apply(); }
        private void OnEnable() { if (_rect == null) _rect = (RectTransform)transform; Apply(); }
        private void LateUpdate()
        {
            if (_last != Screen.safeArea || _size.x != Screen.width || _size.y != Screen.height) Apply();
        }
        private void Apply()
        {
            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;
            _last = Screen.safeArea;
            _size = new Vector2Int(Screen.width, Screen.height);
            Rect safe = MobilePlatform.IsTouchUI ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            _rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}
