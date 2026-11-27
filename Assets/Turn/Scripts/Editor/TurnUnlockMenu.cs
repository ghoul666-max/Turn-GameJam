using UnityEditor;
using UnityEngine;
using Turn.View;

namespace Turn.EditorTools
{
    /// <summary>武器解锁的调试菜单（程序A，11/27）。解锁进度（完成的局数）存在 PlayerPrefs，跨局、跨 Play 保留。</summary>
    public static class TurnUnlockMenu
    {
        [MenuItem("Turn/武器解锁：重置进度（一局都没完成）", false, 40)]
        public static void ResetProgress()
        {
            WeaponUnlocks.ResetProgress();
            Debug.Log("[Turn] 武器解锁已重置：只有水枪、没有副武器；每完成 1 局各解锁 3 把。");
        }

        [MenuItem("Turn/武器解锁：完成局数 +1", false, 41)]
        public static void AddRun()
        {
            string unlocked = WeaponUnlocks.RegisterRunFinished();
            Debug.Log("[Turn] 完成局数 +1（现在 " + WeaponUnlocks.RunsFinished + " 局）" +
                      (unlocked.Length > 0 ? "，新解锁：" + unlocked : "，没有新解锁（已全部解锁）") + "。");
        }

        [MenuItem("Turn/武器解锁：全部解锁", false, 42)]
        public static void UnlockAll()
        {
            WeaponUnlocks.SetAllUnlocked(true);
            Debug.Log("[Turn] 已全部解锁（局数不变；用「重置进度」恢复）。");
        }

        [MenuItem("Turn/武器解锁：查看当前进度", false, 43)]
        public static void Show()
        {
            Debug.Log("[Turn] 已完成 " + WeaponUnlocks.RunsFinished + " 局：主武器 " + WeaponUnlocks.MainCount +
                      " 把、副武器 " + WeaponUnlocks.SubCount + " 把已解锁。");
        }
    }
}
