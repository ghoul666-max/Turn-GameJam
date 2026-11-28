using UnityEngine;

namespace Turn.View
{
    /// <summary>只在手机上应用的表现层预算，不改怪物数量、伤害或模拟时间。</summary>
    [DisallowMultipleComponent]
    public sealed class MobileRuntimeSettings : MonoBehaviour
    {
        [Tooltip("手机目标帧率；真机Profile后可降为30，不改变模拟步长")]
        [Range(30, 120)] public int targetFrameRate = 60;
        [Tooltip("允许横屏左右翻转，禁止竖屏")]
        public bool rotateLandscape = true;
        [Tooltip("游玩时保持屏幕唤醒，离开本组件时恢复")]
        public bool keepScreenAwake = true;
        private int _oldRate;
        private int _oldSleep;
        private bool _applied;
        private void OnEnable()
        {
            if (!Application.isMobilePlatform) return;
            _oldRate = Application.targetFrameRate;
            _oldSleep = Screen.sleepTimeout;
            Application.targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 120);
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = rotateLandscape;
            Screen.orientation = rotateLandscape ? ScreenOrientation.AutoRotation : ScreenOrientation.LandscapeLeft;
            if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _applied = true;
        }
        private void OnDisable()
        {
            if (!_applied) return;
            Application.targetFrameRate = _oldRate;
            Screen.sleepTimeout = _oldSleep;
            _applied = false;
        }
    }
}
