using System;
using System.Text;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 武器解锁（程序A，11/27）。按「完成了几局」解锁，记在 PlayerPrefs，跨局、跨启动保留：
    ///   · 一局都没完成：只有主武器的第一把（水枪），没有副武器；
    ///   · 每完成 1 局：按 <see cref="WeaponKinds.UnlockOrder"/> / <see cref="SubWeaponKinds.UnlockOrder"/> 各往下解锁 PerRun 把。
    /// 「完成 1 局」= 一局打到结算界面（倒下或最终通关）；中途重开、回标题不算。由 GameManager 在结算时调用 <see cref="RegisterRunFinished"/>。
    /// 编辑器菜单「Turn → 武器解锁」可以重置进度 / 全部解锁。
    /// </summary>
    public static class WeaponUnlocks
    {
        /// <summary>每完成 1 局，主武器 / 副武器各解锁几把。</summary>
        public const int PerRun = 3;

        private const string RunsKey = "Turn.Unlock.RunsFinished";
        private const string AllKey = "Turn.Unlock.All";

        /// <summary>已经完成的局数。</summary>
        public static int RunsFinished { get { return Math.Max(0, PlayerPrefs.GetInt(RunsKey, 0)); } }

        private static bool AllUnlocked { get { return PlayerPrefs.GetInt(AllKey, 0) != 0; } }

        /// <summary>已解锁的主武器数（按解锁顺序从头数）。</summary>
        public static int MainCount
        {
            get
            {
                int total = WeaponKinds.UnlockOrder.Length;
                return AllUnlocked ? total : Math.Min(total, 1 + PerRun * RunsFinished);
            }
        }

        /// <summary>已解锁的副武器数（不含「不带副武器」）。</summary>
        public static int SubCount
        {
            get
            {
                int total = SubWeaponKinds.UnlockOrder.Length;
                return AllUnlocked ? total : Math.Min(total, PerRun * RunsFinished);
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

        /// <summary>完成几局后解锁（0 = 一开始就有；-1 = 不在解锁列表里，例如已下架）。</summary>
        public static int UnlockAfterRuns(WeaponKind kind)
        {
            int i = Array.IndexOf(WeaponKinds.UnlockOrder, kind);
            return i < 0 ? -1 : (i == 0 ? 0 : (i - 1) / PerRun + 1);
        }

        public static int UnlockAfterRuns(SubWeaponKind kind)
        {
            if (kind == SubWeaponKind.None) return 0;
            int i = Array.IndexOf(SubWeaponKinds.UnlockOrder, kind);
            return i < 0 ? -1 : i / PerRun + 1;
        }

        /// <summary>
        /// 记一局完成。返回这一局新解锁的装备名（「投矛、陌刀、弩、浮游炮…」），没有新解锁时返回空字符串。
        /// GameManager 保证每局只调一次。
        /// </summary>
        public static string RegisterRunFinished()
        {
            int mainBefore = MainCount, subBefore = SubCount;
            PlayerPrefs.SetInt(RunsKey, RunsFinished + 1);
            PlayerPrefs.Save();

            var names = new StringBuilder();
            for (int i = mainBefore; i < MainCount; i++) Append(names, WeaponKinds.DisplayName(WeaponKinds.UnlockOrder[i]));
            for (int i = subBefore; i < SubCount; i++) Append(names, SubWeaponKinds.DisplayName(SubWeaponKinds.UnlockOrder[i]));
            return names.ToString();
        }

        private static void Append(StringBuilder sb, string name)
        {
            if (sb.Length > 0) sb.Append("、");
            sb.Append(name);
        }

        /// <summary>调试：清空进度（回到一局都没完成）。</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(RunsKey);
            PlayerPrefs.DeleteKey(AllKey);
            PlayerPrefs.DeleteKey("Turn.Unlock.LaunchCount"); // 旧版按打开次数计数时留下的键
            PlayerPrefs.Save();
        }

        /// <summary>调试：全部解锁 / 取消全部解锁（不影响局数）。</summary>
        public static void SetAllUnlocked(bool all)
        {
            PlayerPrefs.SetInt(AllKey, all ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
