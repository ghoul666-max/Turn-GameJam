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

        /// <summary>F3：调试 · 立刻通过本关。</summary>
        public static bool DebugSkipLevelPressed { get { return FKey(3); } }
        /// <summary>F4：调试 · 无敌开关。</summary>
        public static bool DebugGodModePressed { get { return FKey(4); } }
        /// <summary>F5：调试 · 游戏速度 1× → 2× → 4× 循环。</summary>
        public static bool DebugSpeedPressed { get { return FKey(5); } }
        /// <summary>F6：调试 · 切到下一把已解锁的武器（正式的换武器入口由程序B 的升级 / 宝箱选项接入）。</summary>
        public static bool DebugNextWeaponPressed { get { return FKey(6); } }

        private static bool FKey(int n)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (n)
            {
                case 3: return kb.f3Key.wasPressedThisFrame;
                case 4: return kb.f4Key.wasPressedThisFrame;
                case 5: return kb.f5Key.wasPressedThisFrame;
                case 6: return kb.f6Key.wasPressedThisFrame;
                default: return false;
            }
#else
            switch (n)
            {
                case 3: return Input.GetKeyDown(KeyCode.F3);
                case 4: return Input.GetKeyDown(KeyCode.F4);
                case 5: return Input.GetKeyDown(KeyCode.F5);
                case 6: return Input.GetKeyDown(KeyCode.F6);
                default: return false;
            }
#endif
        }

        /// <summary>F2：切换遮挡物判定框显示（调试）。</summary>
        public static bool ObstacleDebugPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.f2Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F2);
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

        /// <summary>W：结算界面里「换武器重开」。</summary>
        public static bool SwitchWeaponPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.wKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.W);
#endif
            }
        }

        /// <summary>武器选择页切换：← / A 返回 -1，→ / D 返回 +1，否则 0。</summary>
        public static int SelectStepPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return 0;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return 1;
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;
                return 0;
#endif
            }
        }

        /// <summary>V：打开音量设置（标题页 / 暂停菜单里）。</summary>
        public static bool VolumePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.vKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.V);
#endif
            }
        }

        /// <summary>↑ / W = -1（上一行），↓ / S = +1（下一行），否则 0。菜单里用。</summary>
        public static int VerticalStepPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return 0;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return -1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return 1;
                return 0;
#else
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return -1;
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return 1;
                return 0;
#endif
            }
        }

        /// <summary>T：回到标题（暂停菜单 / 结算界面里）。</summary>
        public static bool TitlePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && kb.tKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.T);
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

        /// <summary>数字快捷键：按 1–5 返回 0–4，否则 -1（升级面板用 1–3，出战选择页用 1–5）。</summary>
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
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 4;
                if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 5;
                if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 6;
                if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 7;
                if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 8;
                if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) return 9;
                return -1;
#else
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
                if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
                if (Input.GetKeyDown(KeyCode.Alpha6)) return 5;
                if (Input.GetKeyDown(KeyCode.Alpha7)) return 6;
                if (Input.GetKeyDown(KeyCode.Alpha8)) return 7;
                if (Input.GetKeyDown(KeyCode.Alpha9)) return 8;
                if (Input.GetKeyDown(KeyCode.Alpha0)) return 9;
                return -1;
#endif
            }
        }
    }
}
