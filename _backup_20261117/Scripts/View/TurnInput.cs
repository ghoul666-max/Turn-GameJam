using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 输入读取：WASD / 方向键移动，Space 翻滚，1/2/3 选升级，R / Enter 重开，Esc / P 暂停。
    /// 工程只启用了新 Input System（activeInputHandler = 1），所以不能用 Input.GetAxis。
    /// 输入只在这里被读取，然后由 GameManager 转成对系统接口的调用（UI 单向）。
    /// </summary>
    public static class TurnInput
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 v = Vector2.zero;
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                }
                // 标准化：斜向移动不会更快
                return v.sqrMagnitude > 1f ? v.normalized : v;
#else
                Vector2 v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return v.sqrMagnitude > 1f ? v.normalized : v;
#endif
            }
        }

        public static bool RollPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        public static bool RestartPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return);
#endif
            }
        }

        public static bool PausePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
            }
        }

        /// <summary>升级面板快捷键：按 1/2/3 返回 0/1/2，否则 -1。</summary>
        public static int ChoicePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return -1;
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
                return -1;
#else
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                return -1;
#endif
            }
        }
    }
}
