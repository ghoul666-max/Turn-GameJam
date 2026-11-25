namespace Turn.Core
{
    /// <summary>
    /// 局内强化词条：九把主武器 + 七把副武器各自一套定稿词条池，可重复获取叠加。
    /// 编号 = 在 BuildConfig.Builds 数组里的下标；只能在末尾追加，不能改已有的值（Inspector / 面板存档按下标保存）。
    /// 每条的名字、说明、默认数值、最大层数在 Turn.Weapons.BuildCatalog。
    /// </summary>
    public enum BuildId
    {
        // ---------------- 水枪 ----------------
        WaterAmmo = 0, WaterRate, WaterSpread, WaterBulletSpeed, WaterPierce, WaterPuddleSize, WaterFountainDamage,
        WaterFountainArea, WaterFountainSlow, WaterFountainCrit, WaterOverlap, WaterFountainStun, WaterBurstRing,
        WaterPostHaste, WaterLifesteal, WaterHurtPuddle, WaterKillRefund,

        // ---------------- 投矛 ----------------
        SpearAmmo, SpearRate, SpearDouble, SpearFalloff, SpearChainMark, SpearRecallDamage, SpearStackBonus,
        SpearRecallWidth, SpearCrit, SpearStun, SpearPathRamp, SpearBurstRing, SpearPostHaste, SpearLifesteal,
        SpearHurtMarks,

        // ---------------- 陌刀 ----------------
        BladeAmmo, BladeRate, BladeAngle, BladeRange, BladePierce, BladeLength, BladeMarkBonus, BladeSlashWidth,
        BladeCrit, BladeStun, BladeExecute, BladeBurstRing, BladePostHaste, BladeHurtMarks,

        // ---------------- 掌 ----------------
        PalmAmmo, PalmRate, PalmAngle, PalmRange, PalmExtraMark, PalmKnockback, PalmPierce, PalmSkyDamage,
        PalmMarkBonus, PalmSkyArea, PalmCrit, PalmStun, PalmDoubleDrop, PalmBurstRing, PalmPostHaste, PalmLifesteal,
        PalmHurtStrike, PalmKillRefund,

        // ---------------- 弩 ----------------
        BowDamage, BowKnockback, BowPierce, BowFalloff, BowStunTime, BowExtraIntent, BowIntentBonus, BowCrit,
        BowSplash, BowWallHeal, BowHurtShot, BowKillReload,

        // ---------------- 副武器 · 浮游炮 ----------------
        DroneDamage, DroneRate, DronePierce, DroneSplitChance, DronePostHaste, DroneSwarmBonus, DroneMinTwo,
        DroneKillSplit, DroneSplitBlast,

        // ---------------- 副武器 · 引力黑洞 ----------------
        GravityRadius, GravityPull, GravityPullCharging, GravityPullDamage, GravityBlast, GravityVulnerable,
        GravitySecondary,

        // ---------------- 副武器 · 空间锚 ----------------
        AnchorDamage, AnchorWidth, AnchorThroughWalls, AnchorSpeedBoost, AnchorStun, AnchorDoubleAnchor,
        AnchorCooldownCut,

        // ======== 11/25 第二批（只能追加在末尾，所以主 / 副武器的编号段不再连续，归属看 WeaponOf / SubOf） ========

        // ---------------- 扑克牌 ----------------
        PokerTrimDeck, PokerJokerChance, PokerNumberBoost, PokerHeartDamage, PokerHeartCount, PokerHeartPierce,
        PokerSpadeDamage, PokerSpadeArea, PokerSpadeStun, PokerShieldBurst, PokerClubDamage, PokerClubPull,
        PokerClubDuration, PokerJokerBoost, PokerLifesteal,

        // ---------------- 手里剑 ----------------
        ShurikenRate, ShurikenDouble, ShurikenRange, ShurikenSpinDamage, ShurikenSpinArea, ShurikenRecallDamage,
        ShurikenRecallWidth, ShurikenCrit, ShurikenBurstRing, ShurikenPostHaste, ShurikenOrbitBonus, ShurikenLifesteal,

        // ---------------- 副武器 · 时间沙漏 ----------------
        HourglassDecay, HourglassFloor, HourglassResetBlast, HourglassPostSlow, HourglassVulnerable,

        // ---------------- 副武器 · WASD ----------------
        WasdDamage, WasdArea, WasdSpeedBonus, WasdDiagonal, WasdBurstDamage, WasdBurstArea, WasdBurstStun,
        WasdBurstCrit, WasdHurtStrike,

        // ======== 11/25 第三批 ========

        // ---------------- 副武器 · 强磁铁 ----------------
        MagnetPathDamage, MagnetPathWidth, MagnetBlastDamage, MagnetBlastArea, MagnetStun, MagnetHitRamp,
        MagnetPathPull, MagnetVulnerable, MagnetSpawnPull, MagnetThroughWalls,

        // ---------------- 副武器 · 光标 ----------------
        CursorDamage, CursorArea, CursorPinTime, CursorThickness, CursorCrowdBonus, CursorCrit, CursorAfterSlow,
        CursorDrawHaste, CursorPinVulnerable,

        // ---------------- 订书机 ----------------
        StaplerAmmo, StaplerRate, StaplerDouble, StaplerKnockback, StaplerPierce, StaplerSlow, StaplerGroupSize,
        StaplerScatterDamage, StaplerMarkBonus, StaplerScatterCount, StaplerCrit, StaplerStun, StaplerBurstRing,
        StaplerPostHaste, StaplerLifesteal,

        // ---------------- 剑 ----------------
        SwordAmmo, SwordRate, SwordRange, SwordWidth, SwordHitRamp, SwordShadowDamage, SwordTrailBonus, SwordCrit,
        SwordStun, SwordKillThreshold, SwordBurstRing, SwordPostHaste, SwordLifesteal, SwordHurtDash, SwordKillRefund,
    }

    public static class BuildIds
    {
        public const int Count = 189;

        /// <summary>第一批主武器词条的最后一条（之后是第一批副武器词条）。</summary>
        public const BuildId LastMain = BuildId.BowKillReload;

        public static int Index(BuildId id) { return (int)id; }

        public static bool IsValid(BuildId id) { return (int)id >= 0 && (int)id < Count; }

        /// <summary>是不是副武器的词条。</summary>
        public static bool IsSub(BuildId id)
        {
            int i = (int)id;
            return (i > (int)LastMain && i <= (int)BuildId.AnchorCooldownCut)
                || (i >= (int)BuildId.HourglassDecay && i <= (int)BuildId.CursorPinVulnerable);
        }

        /// <summary>主武器词条属于哪把主武器（副武器词条返回 WaterGun，先用 IsSub 判断）。</summary>
        public static WeaponKind WeaponOf(BuildId id)
        {
            int i = (int)id;
            if (i <= (int)BuildId.WaterKillRefund) return WeaponKind.WaterGun;
            if (i <= (int)BuildId.SpearHurtMarks) return WeaponKind.Spear;
            if (i <= (int)BuildId.BladeHurtMarks) return WeaponKind.Blade;
            if (i <= (int)BuildId.PalmKillRefund) return WeaponKind.Palm;
            if (i <= (int)BuildId.BowKillReload) return WeaponKind.Crossbow;
            if (i < (int)BuildId.PokerTrimDeck) return WeaponKind.WaterGun; // 第一批副武器词条
            if (i <= (int)BuildId.PokerLifesteal) return WeaponKind.Poker;
            if (i <= (int)BuildId.ShurikenLifesteal) return WeaponKind.Shuriken;
            if (i < (int)BuildId.StaplerAmmo) return WeaponKind.WaterGun; // 第二 / 三批副武器词条
            if (i <= (int)BuildId.StaplerLifesteal) return WeaponKind.Stapler;
            return WeaponKind.Sword;
        }

        /// <summary>副武器词条属于哪把副武器（主武器词条返回 None）。</summary>
        public static SubWeaponKind SubOf(BuildId id)
        {
            int i = (int)id;
            if (!IsSub(id)) return SubWeaponKind.None;
            if (i <= (int)BuildId.DroneSplitBlast) return SubWeaponKind.Drones;
            if (i <= (int)BuildId.GravitySecondary) return SubWeaponKind.GravityWell;
            if (i <= (int)BuildId.AnchorCooldownCut) return SubWeaponKind.SpaceAnchor;
            if (i <= (int)BuildId.HourglassVulnerable) return SubWeaponKind.Hourglass;
            if (i <= (int)BuildId.WasdHurtStrike) return SubWeaponKind.Wasd;
            if (i <= (int)BuildId.MagnetThroughWalls) return SubWeaponKind.Magnet;
            return SubWeaponKind.Cursor;
        }

        /// <summary>词条所属装备的显示名（「水枪」「浮游炮」…）。</summary>
        public static string OwnerName(BuildId id)
        {
            return IsSub(id) ? SubWeaponKinds.DisplayName(SubOf(id)) : WeaponKinds.DisplayName(WeaponOf(id));
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
