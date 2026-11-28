using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Turn.View
{
    /// <summary>移动端运行环境；桌面默认不改变帧率或显示方向。</summary>
    public static class MobilePlatform
    {
        public static bool SimulateTouch { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { SimulateTouch = false; }
        public static bool IsTouchUI { get { return Application.isMobilePlatform || SimulateTouch; } }

        public static void EnsureEventSystem()
        {
            EventSystem system = Object.FindAnyObjectByType<EventSystem>();
            if (system == null) system = new GameObject("Turn EventSystem").AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy != null) { legacy.enabled = false; Object.Destroy(legacy); }
            system.enabled = true;
            var input = system.GetComponent<InputSystemUIInputModule>();
            if (input == null)
            {
                input = system.gameObject.AddComponent<InputSystemUIInputModule>();
                input.AssignDefaultActions();
            }
            input.enabled = true;
#else
            if (system.GetComponent<StandaloneInputModule>() == null) system.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
