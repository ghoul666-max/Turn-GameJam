using System;
using UnityEngine;

namespace Turn.View
{
    /// <summary>HUD 布局（11/28）：A 四角分散 / B 状态左上 · 操作右下 / C 跟随角色极简 / 原版白模。只能在末尾追加（存档按编号）。</summary>
    public enum HudLayout { CornerA = 0, SplitB = 1, FollowC = 2, Classic = 3 }

    /// <summary>HUD 图标风格（11/28）：墨线符号 / 绘本道具 / 印章剪纸。只能在末尾追加。</summary>
    public enum HudIconSet { Ink = 0, Picture = 1, Stamp = 2 }

    /// <summary>
    /// 界面设置（标题页 / 暂停菜单的「界面设置（H）」）：HUD 布局 + 图标风格，存在 PlayerPrefs，改了立刻生效（PaperHud 监听 Changed）。
    /// </summary>
    public static class HudPrefs
    {
        public const int LayoutCount = 4;
        public const int IconSetCount = 3;

        private const string LayoutKey = "Turn.HudLayout";
        private const string IconKey = "Turn.HudIcons";

        /// <summary>布局或图标风格变化时触发。</summary>
        public static event Action Changed;

        public static HudLayout Layout
        {
            get
            {
                int v = PlayerPrefs.GetInt(LayoutKey, (int)HudLayout.CornerA);
                return v >= 0 && v < LayoutCount ? (HudLayout)v : HudLayout.CornerA;
            }
            set
            {
                if (value == Layout) return;
                PlayerPrefs.SetInt(LayoutKey, (int)value);
                PlayerPrefs.Save();
                if (Changed != null) Changed();
            }
        }

        public static HudIconSet Icons
        {
            get
            {
                int v = PlayerPrefs.GetInt(IconKey, (int)HudIconSet.Ink);
                return v >= 0 && v < IconSetCount ? (HudIconSet)v : HudIconSet.Ink;
            }
            set
            {
                if (value == Icons) return;
                PlayerPrefs.SetInt(IconKey, (int)value);
                PlayerPrefs.Save();
                if (Changed != null) Changed();
            }
        }

        public static string DisplayName(HudLayout layout)
        {
            switch (layout)
            {
                case HudLayout.CornerA: return "A · 四角分散";
                case HudLayout.SplitB: return "B · 左上状态 / 右下操作";
                case HudLayout.FollowC: return "C · 跟随角色";
                case HudLayout.Classic: return "原版白模";
                default: return layout.ToString();
            }
        }

        public static string Describe(HudLayout layout)
        {
            switch (layout)
            {
                case HudLayout.CornerA: return "生命左上、计时顶部、武器左下、翻滚下方正中";
                case HudLayout.SplitB: return "状态一张卡放左上，武器和翻滚收在右下";
                case HudLayout.FollowC: return "头顶生命、脚下翻滚环，画面最干净";
                case HudLayout.Classic: return "开发用的文字版 HUD，信息最全";
                default: return "";
            }
        }

        public static string DisplayName(HudIconSet set)
        {
            switch (set)
            {
                case HudIconSet.Ink: return "墨线符号";
                case HudIconSet.Picture: return "绘本道具";
                case HudIconSet.Stamp: return "印章剪纸";
                default: return set.ToString();
            }
        }

        /// <summary>Resources/HUD/Icons/ 下的文件夹名。</summary>
        public static string Folder(HudIconSet set)
        {
            switch (set)
            {
                case HudIconSet.Picture: return "Picture";
                case HudIconSet.Stamp: return "Stamp";
                default: return "Ink";
            }
        }

        /// <summary>Resources/HUD/Preview/ 下的预览图名。</summary>
        public static string PreviewName(HudLayout layout)
        {
            switch (layout)
            {
                case HudLayout.SplitB: return "layout_B";
                case HudLayout.FollowC: return "layout_C";
                case HudLayout.Classic: return "layout_Classic";
                default: return "layout_A";
            }
        }
    }
}
