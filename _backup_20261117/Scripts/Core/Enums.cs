namespace Turn.Core
{
    /// <summary>地图。两张地图是同一世界空间里的两套表现与地形数据，不是两个世界。</summary>
    public enum MapId { Volcano = 0, Ocean = 1 }

    public static class MapIds
    {
        /// <summary>地图数量（数组按 (int)MapId 下标存放每张地图的数据）。</summary>
        public const int Count = 2;

        /// <summary>翻滚切换到的另一张地图。</summary>
        public static MapId Other(MapId map) { return map == MapId.Volcano ? MapId.Ocean : MapId.Volcano; }

        public static string DisplayName(MapId map) { return map == MapId.Volcano ? "火山" : "海洋"; }
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
    public enum DamageSource { Weapon, Burn, Lava, Monster, Shark, Shockwave }

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
