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
