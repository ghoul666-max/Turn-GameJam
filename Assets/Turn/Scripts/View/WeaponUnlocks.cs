using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 武器解锁（程序A，11/27）。按「打开游戏的次数」解锁，记在 PlayerPrefs，跨局保留：
    ///   · 第 1 次打开：只有主武器的第一把（水枪），没有副武器；
    ///   · 之后每打开一次：按 <see cref="WeaponKinds.UnlockOrder"/> / <see cref="SubWeaponKinds.UnlockOrder"/> 各往下解锁 PerLaunch 把。
    /// 「打开一次」= 一次进程启动（编辑器里每次点 Play 也算一次）；同一次运行里重开多少局都不重复计数。
    /// 编辑器菜单「Turn → 武器解锁」可以重置为第一次打开 / 全部解锁。
    /// </summary>
    public static class WeaponUnlocks
    {
        /// <summary>每多打开一次，主武器 / 副武器各解锁几把。</summary>
        public const int PerLaunch = 3;

        private const string LaunchKey = "Turn.Unlock.LaunchCount";
        private const string AllKey = "Turn.Unlock.All";

        private static bool _registered;
        private static int _launches;

        // 编辑器关闭「进入 Play 模式时重载域」时静态字段不会自动清零，这里手动清，保证每次 Play 都算一次打开
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _registered = false;
            _launches = 0;
        }

        /// <summary>本次打开游戏计一次（只在第一次调用时生效）。GameManager 启动时调用。</summary>
        public static void RegisterLaunch()
        {
            if (_registered) return;
            _registered = true;
            _launches = Math.Max(0, PlayerPrefs.GetInt(LaunchKey, 0)) + 1;
            PlayerPrefs.SetInt(LaunchKey, _launches);
            PlayerPrefs.Save();
        }

        /// <summary>算上本次一共打开了几次（还没登记时按「本次 = 下一次」算）。</summary>
        public static int Launches
        {
            get { return _registered ? _launches : Math.Max(0, PlayerPrefs.GetInt(LaunchKey, 0)) + 1; }
        }

        private static bool AllUnlocked { get { return PlayerPrefs.GetInt(AllKey, 0) != 0; } }

        /// <summary>已解锁的主武器数（按解锁顺序从头数）。</summary>
        public static int MainCount
        {
            get
            {
                int total = WeaponKinds.UnlockOrder.Length;
                return AllUnlocked ? total : Math.Min(total, 1 + PerLaunch * Math.Max(0, Launches - 1));
            }
        }

        /// <summary>已解锁的副武器数（不含「不带副武器」）。</summary>
        public static int SubCount
        {
            get
            {
                int total = SubWeaponKinds.UnlockOrder.Length;
                return AllUnlocked ? total : Math.Min(total, PerLaunch * Math.Max(0, Launches - 1));
            }
        }

        public static bool IsUnlocked(WeaponKind kind)
        {
            int i = Array.IndexOf(WeaponKinds.UnlockOrder, kind);
            return i >= 0 && i < MainCount;
        }

        /// <summary>「不带副武器」永远可选。</summary>
        public static bool IsUnlocked(SubWeaponKind kind)
        {
            if (kind == SubWeaponKind.None) return true;
            int i = Array.IndexOf(SubWeaponKinds.UnlockOrder, kind);
            return i >= 0 && i < SubCount;
        }

        /// <summary>第几次打开游戏时解锁（0 = 一开始就有；-1 = 不在解锁列表里，例如已下架）。</summary>
        public static int UnlockLaunch(WeaponKind kind)
        {
            int i = Array.IndexOf(WeaponKinds.UnlockOrder, kind);
            return i < 0 ? -1 : (i == 0 ? 1 : 1 + (i - 1) / PerLaunch + 1);
        }

        public static int UnlockLaunch(SubWeaponKind kind)
        {
            if (kind == SubWeaponKind.None) return 1;
            int i = Array.IndexOf(SubWeaponKinds.UnlockOrder, kind);
            return i < 0 ? -1 : 2 + i / PerLaunch;
        }

        /// <summary>调试：回到「第一次打开」（下次打开算第 1 次）。</summary>
        public static void ResetToFirstLaunch()
        {
            PlayerPrefs.DeleteKey(LaunchKey);
            PlayerPrefs.DeleteKey(AllKey);
            PlayerPrefs.Save();
            _registered = false;
            _launches = 0;
        }

        /// <summary>调试：全部解锁 / 取消全部解锁（不影响打开次数）。</summary>
        public static void SetAllUnlocked(bool all)
        {
            PlayerPrefs.SetInt(AllKey, all ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
