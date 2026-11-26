using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>旧磁铁枚举的兼容别名；与引力黑洞共享同一套合并、牵引和伤害规则。</summary>
    public sealed class MagnetSubWeapon : GravityWellSubWeapon
    {
        public MagnetSubWeapon(WeaponKit kit) : base(kit) { }
        public override SubWeaponKind Kind { get { return SubWeaponKind.Magnet; } }
    }
}
