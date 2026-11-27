using UnityEditor;
using UnityEngine;
using Turn.View;

namespace Turn.EditorTools
{
    /// <summary>武器解锁的调试菜单（程序A，11/27）。解锁进度存在 PlayerPrefs，跨局、跨 Play 保留。</summary>
    public static class TurnUnlockMenu
    {
        [MenuItem("Turn/武器解锁：重置为第一次打开", false, 40)]
        public static void ResetToFirst()
        {
            WeaponUnlocks.ResetToFirstLaunch();
            Debug.Log("[Turn] 武器解锁已重置：下次点 Play 算第 1 次打开（只有水枪、没有副武器）。");
        }

        [MenuItem("Turn/武器解锁：全部解锁", false, 41)]
        public static void UnlockAll()
        {
            WeaponUnlocks.SetAllUnlocked(true);
            Debug.Log("[Turn] 已全部解锁（打开次数不变；用「重置为第一次打开」恢复）。");
        }

        [MenuItem("Turn/武器解锁：查看当前进度", false, 42)]
        public static void Show()
        {
            Debug.Log("[Turn] 下次 Play 算第 " + WeaponUnlocks.Launches + " 次打开：主武器 " + WeaponUnlocks.MainCount +
                      " 把、副武器 " + WeaponUnlocks.SubCount + " 把已解锁。");
        }
    }
}
