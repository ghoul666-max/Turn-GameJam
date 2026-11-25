namespace Turn.Core
{
    /// <summary>
    /// 局内强化词条（新策划案 11/24：三把武器各自一套定稿词条池，可重复获取叠加）。
    /// 编号 = 在 BuildConfig.Builds 数组里的下标；只能在末尾追加，不能改已有的值（Inspector / 面板存档按下标保存）。
    /// 每条的名字、说明、默认数值、最大层数在 Turn.Weapons.BuildCatalog。
    /// </summary>
    public enum BuildId
    {
        // ---------------- 水枪 ----------------
        WaterAmmo = 0, WaterRate, WaterSpread, WaterBulletSpeed, WaterPierce, WaterPuddleSize,
        WaterFountainDamage, WaterFountainArea, WaterFountainSlow, WaterFountainCrit, WaterOverlap,
        WaterFountainStun, WaterBurstRing, WaterPostHaste, WaterLifesteal, WaterHurtPuddle, WaterKillRefund,

        // ---------------- 投矛 ----------------
        SpearAmmo, SpearRate, SpearDouble, SpearFalloff, SpearChainMark, SpearRecallDamage,
        SpearStackBonus, SpearRecallWidth, SpearCrit, SpearStun, SpearPathRamp, SpearBurstRing,
        SpearPostHaste, SpearLifesteal, SpearHurtMarks,

        // ---------------- 陌刀 ----------------
        BladeAmmo, BladeRate, BladeAngle, BladeRange, BladePierce, BladeLength, BladeMarkBonus,
        BladeSlashWidth, BladeCrit, BladeStun, BladeExecute, BladeBurstRing, BladePostHaste, BladeHurtMarks,
    }

    public static class BuildIds
    {
        public const int Count = 46;

        public static int Index(BuildId id) { return (int)id; }

        public static bool IsValid(BuildId id) { return (int)id >= 0 && (int)id < Count; }

        /// <summary>词条属于哪把武器。</summary>
        public static WeaponKind WeaponOf(BuildId id)
        {
            int i = (int)id;
            if (i <= (int)BuildId.WaterKillRefund) return WeaponKind.WaterGun;
            if (i <= (int)BuildId.SpearHurtMarks) return WeaponKind.Spear;
            return WeaponKind.Blade;
        }
    }

    /// <summary>宝箱里的代价选项（策划：宝箱选项不全为正向强化）。每层效果见 BuildConfig。</summary>
    public enum ChestCurse
    {
        None = 0,
        /// <summary>最大生命降低。</summary>
        Frail = 1,
        /// <summary>怪物伤害提高。</summary>
        Fury = 2,
        /// <summary>怪物移速提高。</summary>
        Haste = 3,
    }

    public static class ChestCurses
    {
        public const int Count = 4;
    }
}
