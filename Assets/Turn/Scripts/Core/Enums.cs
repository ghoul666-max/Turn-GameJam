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
    public enum PlayerSpeedSource { DreamSlow = 0 }

    public static class PlayerSpeedSources
    {
        public const int Count = 1;
    }

    /// <summary>地形类型。存进网格时按 byte 存。</summary>
    public enum TerrainType : byte { Normal = 0, Lava = 1, ShallowWater = 2 }

    /// <summary>一局的阶段。只有 SessionSystem 能改。</summary>
    public enum RunPhase { Playing, LevelUp, Defeat }

    /// <summary>五种升级词条。</summary>
    public enum UpgradeId { Magazine = 0, Burn = 1, RollCooldown = 2, RollInvincible = 3, Shockwave = 4 }

    public static class UpgradeIds
    {
        public const int Count = 5;
    }

    /// <summary>伤害来源：用来区分表现，以及在测试时确认无敌帧挡住了哪一种伤害。</summary>
    public enum DamageSource { Weapon, Burn, Lava, Monster, Shark, Shockwave, Drowsy, Phone }

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
}
