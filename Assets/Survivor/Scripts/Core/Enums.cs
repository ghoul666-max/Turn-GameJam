namespace Survivor.Core
{
    /// <summary>一局的阶段。只有 SessionSystem 能改。</summary>
    public enum RunPhase { Playing, LevelUp, Victory, Defeat }

    /// <summary>
    /// 角色属性。PlayerSystem.GetStat 的返回值含义：
    /// Cooldown = 冷却倍率（1 - 加成，最低 0.2）；Amount = 额外投射物数量（加法）；其余 = 倍率（1 + 加成）。
    /// </summary>
    public enum StatId { Might, Cooldown, Area, MoveSpeed, Magnet, MaxHp, Amount }

    /// <summary>武器种类，决定用哪个 IWeaponBehavior。</summary>
    public enum WeaponKind { Wand, Knife, Whip, Aura, Orbit, Lightning }

    /// <summary>投射物（武器产生的所有判定体）的运动方式。</summary>
    public enum ProjectileMotion
    {
        /// <summary>按速度直线飞行。</summary>
        Linear,
        /// <summary>跟随玩家，保持固定偏移（鞭子、大蒜）。</summary>
        Attached,
        /// <summary>绕玩家旋转（圣经）。</summary>
        Orbit,
        /// <summary>停在原地（闪电落点）。</summary>
        Static,
    }

    public enum HitShape { Circle, Box }

    public enum PickupKind { Gem, Heal }

    public enum UpgradeKind { NewWeapon, WeaponLevel, NewPassive, PassiveLevel, Heal }

    public enum SpawnPattern
    {
        /// <summary>在屏幕外的圆环上随机散布。</summary>
        Scatter,
        /// <summary>等间距围成一圈（包围）。</summary>
        Ring,
    }
}
