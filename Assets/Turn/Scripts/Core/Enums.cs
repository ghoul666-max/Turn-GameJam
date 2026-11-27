namespace Turn.Core
{
    /// <summary>
    /// 位面。成对出现：同一组的两个位面共用同一坐标系、同一边界，翻滚只在组内互换。
    /// 规则：(int)PlaneId / 2 = 所属组；同组两面的值只差最低位（偶数 = 组内第一面）。
    /// 数值会被 Unity 序列化（Inspector 里的枚举字段），只能在末尾追加，不能改已有的值。
    /// </summary>
    public enum PlaneId { Volcano = 0, Ocean = 1, Office = 2, Dream = 3, Primal = 4, Future = 5 }

    /// <summary>位面组（一组 = 一对可以互相翻滚切换的位面）。</summary>
    public enum PlanePairId { VolcanoOcean = 0, OfficeDream = 1, PrimalFuture = 2 }

    public static class PlaneIds
    {
        /// <summary>位面数量（数组按 (int)PlaneId 下标存放每个位面的数据）。</summary>
        public const int Count = 6;

        /// <summary>位面组数量。</summary>
        public const int PairCount = 3;

        /// <summary>翻滚切换到的另一面（同组的另一个位面）。</summary>
        public static PlaneId Other(PlaneId plane) { return (PlaneId)((int)plane ^ 1); }

        /// <summary>位面所属的组。</summary>
        public static PlanePairId PairOf(PlaneId plane) { return (PlanePairId)((int)plane >> 1); }

        /// <summary>组内第一面（开局默认进入的那一面）。</summary>
        public static PlaneId First(PlanePairId pair) { return (PlaneId)((int)pair * 2); }

        /// <summary>组内第二面。</summary>
        public static PlaneId Second(PlanePairId pair) { return (PlaneId)((int)pair * 2 + 1); }

        public static bool IsValid(PlaneId plane) { return (int)plane >= 0 && (int)plane < Count; }

        public static string DisplayName(PlaneId plane)
        {
            switch (plane)
            {
                case PlaneId.Volcano: return "火山";
                case PlaneId.Ocean: return "海洋";
                case PlaneId.Office: return "工位";
                case PlaneId.Dream: return "梦境";
                case PlaneId.Primal: return "原始";
                case PlaneId.Future: return "未来";
                default: return plane.ToString();
            }
        }

        public static string DisplayName(PlanePairId pair)
        {
            return DisplayName(First(pair)) + "↔" + DisplayName(Second(pair));
        }
    }

    /// <summary>
    /// 遮挡物会挡住什么。实体 = 两者都挡；软遮挡只挡其中一种（策划书：文件堆只挡人、云朵 / 全息挡板只挡弹）。
    /// </summary>
    [System.Flags]
    public enum ObstacleMask : byte
    {
        None = 0,
        /// <summary>挡玩家、怪物、鲨鱼的移动。</summary>
        Units = 1,
        /// <summary>挡子弹 / 敌方弹幕。</summary>
        Projectiles = 2,
        /// <summary>实体遮挡：两者都挡。</summary>
        Solid = Units | Projectiles,
    }

    /// <summary>遮挡物的判定形状。矩形是轴对齐的（不随物体旋转）。</summary>
    public enum ObstacleShape : byte { Circle = 0, Box = 1 }

    /// <summary>
    /// 玩家移速修正的来源。每个来源一个倍率（1 = 不影响），多个来源相乘。
    /// 只能在末尾追加。
    /// </summary>
    public enum PlayerSpeedSource { DreamSlow = 0, AnchorBoost = 1, CursorHaste = 2 }

    public static class PlayerSpeedSources
    {
        public const int Count = 3;
    }

    /// <summary>地形类型。存进网格时按 byte 存。</summary>
    public enum TerrainType : byte { Normal = 0, Lava = 1, ShallowWater = 2 }

    /// <summary>
    /// 一局（= 一关 = 一组位面）的阶段。只有 SessionSystem 能改。只能在末尾追加。
    /// LevelCleared：本关存活够时间，等表现层翻页后进入下一关；Victory：最后一关也过了。
    /// Chest：打开了宝箱，三选一弹窗（和 LevelUp 一样全局暂停）。
    /// </summary>
    public enum RunPhase { Playing, LevelUp, Defeat, LevelCleared, Victory, Chest }

    /// <summary>五种升级词条。</summary>
    public enum UpgradeId { Magazine = 0, Burn = 1, RollCooldown = 2, RollInvincible = 3, Shockwave = 4 }

    public static class UpgradeIds
    {
        public const int Count = 5;
    }

    /// <summary>伤害来源：用来区分表现，以及在测试时确认无敌帧挡住了哪一种伤害。</summary>
    /// Weapon = 常态空间的武器攻击（铺垫）；Burst = 翻滚爆发；Poison = 毒腐体的毒潭；Laser = 狙击械激光。
    /// SubWeapon = 副武器（浮游炮 / 引力黑洞 / 空间锚）。
    public enum DamageSource { Weapon, Burn, Lava, Monster, Shark, Shockwave, Drowsy, Phone, Burst, Poison, Laser, SubWeapon }

    /// <summary>玩家动画状态（表现层据此驱动 Animator 的 State 参数）。</summary>
    public enum PlayerAnimState { Idle = 0, Move = 1, Roll = 2, Hurt = 3, Death = 4 }

    /// <summary>翻滚惩罚怪的生成区域。</summary>
    public enum PenaltyArea
    {
        /// <summary>玩家周围 [最小距离, 最大距离] 的圆环内（再限制进边界）。</summary>
        AroundPlayer,
        /// <summary>整个边界内随机，但离玩家不小于最小距离。</summary>
        AnywhereInBounds,
    }

    /// <summary>
    /// 主武器种类。数值会被 Unity 序列化，只能在末尾追加。
    /// 水枪 / 投矛 / 陌刀（11/24 策划案），掌 / 弩（11/25 双武器策划案）。
    /// </summary>
    public enum WeaponKind { WaterGun = 0, Spear = 1, Blade = 2, Palm = 3, Crossbow = 4, Poker = 5, Shuriken = 6, Stapler = 7, Sword = 8, Lightning = 9 }

    public static class WeaponKinds
    {
        public const int Count = 10;

        public static bool IsValid(WeaponKind kind) { return (int)kind >= 0 && (int)kind < Count; }

        /// <summary>
        /// 已下架的主武器（11/27：掌、订书机）。代码与存档编号保留，但选择页、解锁、F6 调试、F1 面板都不再出现。
        /// 想加回来：从这里去掉，再把它放进 <see cref="UnlockOrder"/>。
        /// </summary>
        public static bool IsRetired(WeaponKind kind) { return kind == WeaponKind.Palm || kind == WeaponKind.Stapler; }

        /// <summary>主武器的解锁顺序（11/27）：第 1 次打开游戏只有第一把，之后每打开一次往下解锁 3 把。也是选择页的排列顺序。</summary>
        public static readonly WeaponKind[] UnlockOrder =
        {
            WeaponKind.WaterGun, WeaponKind.Spear, WeaponKind.Blade, WeaponKind.Crossbow,
            WeaponKind.Poker, WeaponKind.Shuriken, WeaponKind.Sword, WeaponKind.Lightning,
        };

        public static string DisplayName(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.WaterGun: return "水枪";
                case WeaponKind.Spear: return "投矛";
                case WeaponKind.Blade: return "陌刀";
                case WeaponKind.Palm: return "掌";
                case WeaponKind.Crossbow: return "弩";
                case WeaponKind.Poker: return "扑克牌";
                case WeaponKind.Shuriken: return "手里剑";
                case WeaponKind.Stapler: return "订书机";
                case WeaponKind.Sword: return "剑";
                case WeaponKind.Lightning: return "闪电";
                default: return kind.ToString();
            }
        }
    }

    /// <summary>副武器种类（方案 B 携带）。None = 不带副武器。只能在末尾追加。</summary>
    public enum SubWeaponKind { None = 0, Drones = 1, GravityWell = 2, SpaceAnchor = 3, Hourglass = 4, Wasd = 5, Magnet = 6, Cursor = 7 }

    public static class SubWeaponKinds
    {
        /// <summary>含 None 的总数（数组按 (int)SubWeaponKind 下标）。</summary>
        public const int Count = 8;

        public static bool IsValid(SubWeaponKind kind) { return (int)kind >= 0 && (int)kind < Count; }

        /// <summary>
        /// 已下架的副武器（11/27：WASD、强磁铁）。代码与存档编号保留，但选择页、解锁都不再出现。
        /// WASD 的 F1 参数分组 / 词条表 / F7 分组也隐藏；强磁铁的配置与词条已并入引力黑洞（黑洞读 MagnetConfig、共用词条池），面板里保留。
        /// 想加回来：从这里去掉，再把它放进 <see cref="UnlockOrder"/>。
        /// </summary>
        public static bool IsRetired(SubWeaponKind kind) { return kind == SubWeaponKind.Wasd || kind == SubWeaponKind.Magnet; }

        /// <summary>已下架、且面板里也要隐藏的副武器（强磁铁的配置被引力黑洞用着，不隐藏）。</summary>
        public static bool IsHiddenInPanels(SubWeaponKind kind) { return IsRetired(kind) && kind != SubWeaponKind.Magnet; }

        /// <summary>副武器的解锁顺序（11/27，不含 None；已下架的不在里面）：一局都没完成时一把都没有，之后每完成 1 局往下解锁 3 把。也是选择页的排列顺序。</summary>
        public static readonly SubWeaponKind[] UnlockOrder =
        {
            SubWeaponKind.Drones, SubWeaponKind.GravityWell, SubWeaponKind.SpaceAnchor, SubWeaponKind.Hourglass,
            SubWeaponKind.Cursor,
        };

        public static string DisplayName(SubWeaponKind kind)
        {
            switch (kind)
            {
                case SubWeaponKind.None: return "无";
                case SubWeaponKind.Drones: return "浮游炮";
                case SubWeaponKind.GravityWell: return "引力黑洞";
                case SubWeaponKind.SpaceAnchor: return "空间锚";
                case SubWeaponKind.Hourglass: return "时间沙漏";
                case SubWeaponKind.Wasd: return "WASD";
                case SubWeaponKind.Magnet: return "强磁铁";
                case SubWeaponKind.Cursor: return "光标";
                default: return kind.ToString();
            }
        }
    }

    /// <summary>
    /// 出战方案（11/25 双武器策划案，二选一、互斥）。
    /// DualMain：两把主武器，翻滚爆发后自动切到另一把；MainAndSub：一把主武器 + 一把常驻副武器（不切换）。
    /// 11/27：双主武器方案已删除，出战固定为 MainAndSub；DualMain 只为兼容旧存档 / 旧代码分支保留。
    /// </summary>
    public enum LoadoutScheme { DualMain = 0, MainAndSub = 1 }

    public static class LoadoutSchemes
    {
        public static string DisplayName(LoadoutScheme scheme)
        {
            return scheme == LoadoutScheme.DualMain ? "双主武器" : "主武器 + 副武器";
        }
    }
}