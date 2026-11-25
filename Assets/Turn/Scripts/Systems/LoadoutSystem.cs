using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 出战配置（程序A，11/25 双武器策划案）：出战前在选择页选定（LoadoutConfig），局内不按波次解锁。
    ///   · 方案 A 双主武器：Equipped = 当前生效，Standby = 待切换；翻滚爆发后 RollSystem 调 SwapMains 互换（发布 WeaponEquippedEvent，ByRoll = true）。
    ///     两把必须不同：配置里相同时，第二把自动换成下一把不同的。
    ///   · 方案 B 主 + 副：Equipped = 唯一主武器，Standby = Equipped，Sub = 副武器（None = 不带）。
    /// 本系统只管「带了什么、现在用哪把」，弹药 / 攻击 / 印记都在 WeaponSystem，副武器行为在 SubWeaponSystem。
    /// </summary>
    public sealed class LoadoutSystem : ILoadoutSystem
    {
        private readonly ITurnContext _ctx;

        public LoadoutScheme Scheme { get; private set; }
        public WeaponKind Equipped { get; private set; }
        public WeaponKind Standby { get; private set; }
        public SubWeaponKind Sub { get; private set; }

        public LoadoutSystem(ITurnContext ctx, LoadoutConfig config)
        {
            _ctx = ctx;
            config = config ?? new LoadoutConfig();
            Scheme = config.Scheme;
            Equipped = WeaponKinds.IsValid(config.Main) ? config.Main : WeaponKind.WaterGun;
            if (Scheme == LoadoutScheme.DualMain)
            {
                WeaponKind second = WeaponKinds.IsValid(config.SecondMain) ? config.SecondMain : WeaponKind.Spear;
                if (second == Equipped) second = (WeaponKind)(((int)Equipped + 1) % WeaponKinds.Count);
                Standby = second;
                Sub = SubWeaponKind.None;
            }
            else
            {
                Standby = Equipped;
                Sub = SubWeaponKinds.IsValid(config.Sub) ? config.Sub : SubWeaponKind.None;
            }
        }

        public bool Carries(WeaponKind weapon)
        {
            return weapon == Equipped || (Scheme == LoadoutScheme.DualMain && weapon == Standby);
        }

        public void SwapMains()
        {
            if (Scheme != LoadoutScheme.DualMain || Standby == Equipped) return;
            WeaponKind from = Equipped;
            Equipped = Standby;
            Standby = from;
            _ctx.Events.Publish(new WeaponEquippedEvent { From = from, To = Equipped, ByRoll = true });
        }

        public bool Equip(WeaponKind weapon)
        {
            if (!WeaponKinds.IsValid(weapon) || weapon == Equipped) return false;
            if (_ctx.Player.State.InSubspace) return false;
            WeaponKind from = Equipped;
            if (Scheme == LoadoutScheme.DualMain && weapon == Standby) Standby = from;
            else if (Scheme != LoadoutScheme.DualMain) Standby = weapon;
            Equipped = weapon;
            _ctx.Events.Publish(new WeaponEquippedEvent { From = from, To = weapon, ByRoll = false });
            return true;
        }
    }
}
