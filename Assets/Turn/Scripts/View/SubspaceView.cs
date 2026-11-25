using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 亚空间画面（程序A，新策划案 11/24）：翻滚进入亚空间时画面整体切成灰白，回到常态空间时恢复。
    ///
    /// 做法（不需要在场景里摆任何东西，GameManager 会自动挂上本组件）：
    ///   · 运行时建一个全局 Volume（ColorAdjustments：饱和度 −100、对比度可调），权重在 0 ↔ 1 之间按现实时间淡入淡出；
    ///   · 打开主相机的 Post Processing（URP 2D Renderer 已带 PostProcessData）；
    ///   · 「保留原色」：如果项目里有名为 <see cref="ColorLayerName"/> 的 Layer，运行时在主相机上叠一个 Overlay 相机，
    ///     只渲染这个 Layer 且不做后处理 —— 爆发特效、敌人受击特效挂上 <see cref="KeepColorInSubspace"/>（或调 <see cref="KeepColor"/>）
    ///     就会在灰白画面上保持原色高亮。没有这个 Layer 时整屏都变灰，并在 Console 提示一次。
    /// 只订阅事件 / 每帧读 PlayerState.InSubspace，不改任何模拟层状态。慢放（Time.timeScale）由 GameManager 统一设置。
    /// </summary>
    public sealed class SubspaceView : MonoBehaviour
    {
        /// <summary>保留原色的 Layer 名。在 Project Settings → Tags and Layers 里加一个同名 Layer 即可启用。</summary>
        public const string ColorLayerName = "SubspaceColor";

        [Tooltip("亚空间饱和度（−100 = 完全灰白）")]
        [UnityEngine.Range(-100f, 0f)] [SerializeField] private float saturation = -100f;
        [Tooltip("亚空间对比度（0 = 不变；调高让灰白更有线稿感）")]
        [UnityEngine.Range(-100f, 100f)] [SerializeField] private float contrast = 15f;
        [Tooltip("灰白淡入 / 淡出时长（现实秒）")]
        [UnityEngine.Min(0f)] [SerializeField] private float fadeTime = 0.08f;

        private static int _colorLayer = int.MinValue;
        private static bool _warned;

        private ITurnContext _ctx;
        private Camera _camera;
        private Camera _overlay;
        private Volume _volume;
        private VolumeProfile _profile;
        private ColorAdjustments _adjust;

        /// <summary>保留原色 Layer 的编号；项目里没有这个 Layer 时为 −1。</summary>
        public static int ColorLayer
        {
            get
            {
                if (_colorLayer == int.MinValue) _colorLayer = LayerMask.NameToLayer(ColorLayerName);
                return _colorLayer;
            }
        }

        /// <summary>把一个物体（默认连同子物体）放到保留原色的 Layer 上。给代码生成的特效用。</summary>
        public static void KeepColor(GameObject go, bool includeChildren = true)
        {
            if (go == null) return;
            int layer = ColorLayer;
            if (layer < 0)
            {
                WarnMissingLayer();
                return;
            }
            if (!includeChildren)
            {
                go.layer = layer;
                return;
            }
            Transform[] all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
        }

        private static void WarnMissingLayer()
        {
            if (_warned) return;
            _warned = true;
            UnityEngine.Debug.LogWarning("[Turn] 没有名为 " + ColorLayerName + " 的 Layer：亚空间里爆发 / 受击特效也会变灰。" +
                             "在 Project Settings → Tags and Layers 里加一个 " + ColorLayerName + " Layer 即可保留原色。");
        }

        public void Bind(ITurnContext ctx, Camera cam)
        {
            _ctx = ctx;
            if (cam != null && cam != _camera)
            {
                _camera = cam;
                SetupCamera();
            }
            EnsureVolume();
            if (_volume != null) _volume.weight = 0f;
        }

        private void EnsureVolume()
        {
            if (_volume != null) return;
            var go = new GameObject("SubspaceVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 0f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            _adjust = _profile.Add<ColorAdjustments>(true);
            _adjust.saturation.Override(saturation);
            _adjust.contrast.Override(contrast);
            _volume.sharedProfile = _profile;
        }

        private void SetupCamera()
        {
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;

            int layer = ColorLayer;
            if (layer < 0)
            {
                WarnMissingLayer();
                return;
            }
            if (_overlay != null) return;

            // 主相机不再画保留原色的 Layer，改由不做后处理的 Overlay 相机画在最上面
            _camera.cullingMask &= ~(1 << layer);
            var go = new GameObject("SubspaceColorCamera");
            go.transform.SetParent(_camera.transform, false);
            _overlay = go.AddComponent<Camera>();
            _overlay.cullingMask = 1 << layer;
            _overlay.clearFlags = CameraClearFlags.Nothing;
            SyncOverlay();
            UniversalAdditionalCameraData od = _overlay.GetUniversalAdditionalCameraData();
            od.renderType = CameraRenderType.Overlay;
            od.renderPostProcessing = false;
            if (!data.cameraStack.Contains(_overlay)) data.cameraStack.Add(_overlay);
        }

        private void SyncOverlay()
        {
            if (_overlay == null || _camera == null) return;
            _overlay.orthographic = _camera.orthographic;
            _overlay.orthographicSize = _camera.orthographicSize;
            _overlay.fieldOfView = _camera.fieldOfView;
            _overlay.nearClipPlane = _camera.nearClipPlane;
            _overlay.farClipPlane = _camera.farClipPlane;
        }

        private void LateUpdate()
        {
            SyncOverlay();
            if (_volume == null) return;
            if (_adjust != null)
            {
                _adjust.saturation.value = saturation;
                _adjust.contrast.value = contrast;
            }
            float target = _ctx != null && _ctx.Player.State.InSubspace ? 1f : 0f;
            float speed = fadeTime > 0.001f ? Time.unscaledDeltaTime / fadeTime : 1f;
            _volume.weight = Mathf.MoveTowards(_volume.weight, target, speed);
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }
    }
}
