using UnityEngine;

namespace Survivor.View
{
    /// <summary>
    /// 输入读取：键盘 WASD / 方向键、手柄左摇杆。工程只启用了新 Input System，不能用 Input.GetAxis。
    /// 输入只在这里被读取，然后由 SurvivorRoot 转成对系统接口的调用（UI 单向）。
    /// </summary>
    public static class SurvivorInput
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
                UnityEngine.InputSystem.Gamepad pad = UnityEngine.InputSystem.Gamepad.current;
                if (pad != null)
                {
                    Vector2 stick = pad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > 0.04f) v += stick;
                }
                return Vector2.ClampMagnitude(v, 1f);
#else
                return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
            }
        }

        public static bool PausePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;
                UnityEngine.InputSystem.Gamepad pad = UnityEngine.InputSystem.Gamepad.current;
                return pad != null && pad.startButton.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
            }
        }

        public static bool ConfirmPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
                UnityEngine.InputSystem.Gamepad pad = UnityEngine.InputSystem.Gamepad.current;
                return pad != null && pad.buttonSouth.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
            }
        }

        /// <summary>升级面板的快捷键：按 1/2/3 返回 0/1/2，否则 -1。</summary>
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
