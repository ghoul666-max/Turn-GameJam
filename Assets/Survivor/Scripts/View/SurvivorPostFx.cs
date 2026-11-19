using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Survivor.View
{
    /// <summary>
    /// 场景后期处理：Bloom + 色差 + 暗角（URP 全局 Volume，运行时创建），数值与卡牌白模一致。
    /// 只作用于透视主相机拍到的世界；HUD 是 Overlay 画布，不受影响，文字保持清晰。
    /// “亮色才发光”：普通颜色低于阈值，刀光 / 宝石 / 闪电 / 受击闪白 / 伤害数字越过阈值。
    /// </summary>
    public static class SurvivorPostFx
    {
        public const float BloomThreshold = 0.9f;
        public const float BloomIntensity = 1.4f;
        public const float BloomScatter = 0.65f;
        public const float ChromaticIntensity = 0.15f;
        public const float VignetteIntensity = 0.36f;
        public const float VignetteSmoothness = 0.41f;

        private static GameObject _root;

        public static void Ensure(Camera cam)
        {
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            if (_root != null) return;

            _root = new GameObject("[Survivor] PostFx");
            var volume = _root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SurvivorPostFx";

            Bloom bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);

            ChromaticAberration chromatic = profile.Add<ChromaticAberration>(false);
            chromatic.intensity.Override(ChromaticIntensity);

            Vignette vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);

            volume.sharedProfile = profile;
        }
    }
}
