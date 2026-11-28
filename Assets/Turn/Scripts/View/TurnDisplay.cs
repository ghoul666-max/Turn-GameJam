using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 玩家的窗口设置（「窗口设置」界面写）：窗口化 / 全屏 / 无边框窗口。存在 PlayerPrefs（Turn.Display.Mode），下次打开游戏沿用。
    ///   · 窗口化：16:9 窗口，取能放进桌面 85% 的最大一档（1920 / 1600 / 1280 / 960 宽），玩家可用 Alt+Enter 切回全屏；
    ///   · 全屏：独占全屏（ExclusiveFullScreen），桌面原生分辨率；
    ///   · 无边框窗口：FullScreenWindow，桌面原生分辨率（= Player Settings 的默认模式）。
    /// 从没选过时不做任何事，沿用 Player Settings / Unity 自己记住的模式。编辑器里 Screen 模式切换不起作用，只记录选择。
    /// 摄像机不需要配合：CameraFollow 每帧按当前 camera.aspect 重算可视范围。
    /// </summary>
    public static class TurnDisplay
    {
        public enum Mode { Windowed = 0, Fullscreen = 1, Borderless = 2 }

        public const int ModeCount = 3;
        private const string KeyMode = "Turn.Display.Mode";

        private static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1280, 720), new Vector2Int(960, 540),
        };

        public static string DisplayName(Mode mode)
        {
            switch (mode)
            {
                case Mode.Windowed: return "窗口化";
                case Mode.Fullscreen: return "全屏";
                default: return "无边框窗口";
            }
        }

        public static string Describe(Mode mode)
        {
            switch (mode)
            {
                case Mode.Windowed: return "带标题栏的窗口，可以和其他程序并排";
                case Mode.Fullscreen: return "独占屏幕，切出游戏时会最小化";
                default: return "铺满屏幕、没有边框，切换程序更快";
            }
        }

        /// <summary>当前实际的模式（读 Screen，Alt+Enter 切换后也准确）。编辑器里返回上次的选择。</summary>
        public static Mode Current
        {
            get
            {
                if (Application.isEditor) return Saved;
                switch (Screen.fullScreenMode)
                {
                    case FullScreenMode.ExclusiveFullScreen: return Mode.Fullscreen;
                    case FullScreenMode.FullScreenWindow: return Mode.Borderless;
                    default: return Mode.Windowed; // Windowed / MaximizedWindow
                }
            }
        }

        /// <summary>存档里的选择；没选过时为无边框窗口（Player Settings 默认）。</summary>
        public static Mode Saved
        {
            get
            {
                int v = PlayerPrefs.GetInt(KeyMode, (int)Mode.Borderless);
                return v >= 0 && v < ModeCount ? (Mode)v : Mode.Borderless;
            }
        }

        /// <summary>切换并保存。</summary>
        public static void Set(Mode mode)
        {
            PlayerPrefs.SetInt(KeyMode, (int)mode);
            PlayerPrefs.Save();
            Apply(mode);
        }

        private static void Apply(Mode mode)
        {
            if (Application.isEditor || Application.isMobilePlatform) return;
            int nativeW = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
            int nativeH = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
            switch (mode)
            {
                case Mode.Fullscreen:
                    Screen.SetResolution(nativeW, nativeH, FullScreenMode.ExclusiveFullScreen);
                    break;
                case Mode.Borderless:
                    Screen.SetResolution(nativeW, nativeH, FullScreenMode.FullScreenWindow);
                    break;
                default:
                    Vector2Int size = FitWindow(nativeW, nativeH);
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    break;
            }
        }

        private static Vector2Int FitWindow(int nativeW, int nativeH)
        {
            for (int i = 0; i < WindowSizes.Length; i++)
            {
                Vector2Int s = WindowSizes[i];
                if (s.x <= nativeW * 0.85f && s.y <= nativeH * 0.85f) return s;
            }
            return WindowSizes[WindowSizes.Length - 1];
        }

        /// <summary>打包后开局：有存档就按存档切一次（盖过 Unity 自己记住的模式）；退出时记下实际模式（Alt+Enter 切过的也算）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Application.isEditor || Application.isMobilePlatform) return;
            if (PlayerPrefs.HasKey(KeyMode) && Current != Saved) Apply(Saved);
            Application.quitting += () =>
            {
                if (!PlayerPrefs.HasKey(KeyMode)) return;
                PlayerPrefs.SetInt(KeyMode, (int)Current);
                PlayerPrefs.Save();
            };
        }
    }
}
