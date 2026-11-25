using System.Collections.Generic;
using Turn.Core;
using Turn.Weapons;

namespace Turn.Systems
{
    /// <summary>
    /// 副武器（11/25 双武器策划案，方案 B）：按 ILoadoutSystem.Sub 创建对应的 SubWeaponBehavior，把翻滚钩子、Tick、子弹回调转发给它。
    /// 方案 A、不带副武器时什么都不做。翻滚钩子只由 RollSystem 调用，Tick 由 SessionSystem 在常态空间调用。
    /// 新增一把副武器 = 写一个 SubWeaponBehavior + 在 Create 和 Supports 里各加一行。
    /// </summary>
    public sealed class SubWeaponSystem : ISubWeaponSystem
    {
        private static readonly List<Vec2> Empty = new List<Vec2>();
        private readonly SubWeaponBehavior _behavior;

        public SubWeaponKind Kind { get; private set; }

        public SubWeaponSystem(ITurnContext ctx, ArsenalConfig arsenal)
        {
            Kind = ctx.Loadout.Sub;
            _behavior = Create(Kind, new WeaponKit(ctx, arsenal));
            if (_behavior == null) Kind = SubWeaponKind.None;
        }

        /// <summary>副武器行为的唯一创建点。</summary>
        private static SubWeaponBehavior Create(SubWeaponKind kind, WeaponKit kit)
        {
            switch (kind)
            {
                case SubWeaponKind.Drones: return new DronesSubWeapon(kit);
                case SubWeaponKind.GravityWell: return new GravityWellSubWeapon(kit);
                case SubWeaponKind.SpaceAnchor: return new SpaceAnchorSubWeapon(kit);
                case SubWeaponKind.Hourglass: return new HourglassSubWeapon(kit);
                case SubWeaponKind.Wasd: return new WasdSubWeapon(kit);
                case SubWeaponKind.Magnet: return new MagnetSubWeapon(kit);
                case SubWeaponKind.Cursor: return new CursorSubWeapon(kit);
                default: return null;
            }
        }

        public bool Supports(SubWeaponKind kind)
        {
            switch (kind)
            {
                case SubWeaponKind.None:
                case SubWeaponKind.Drones:
                case SubWeaponKind.GravityWell:
                case SubWeaponKind.SpaceAnchor:
                case SubWeaponKind.Hourglass:
                case SubWeaponKind.Wasd:
                case SubWeaponKind.Magnet:
                case SubWeaponKind.Cursor:
                    return true;
                default:
                    return false;
            }
        }

        public void BeforeBurst(Vec2 rollOrigin) { if (_behavior != null) _behavior.BeforeBurst(rollOrigin); }
        public void AfterBurst(Vec2 rollOrigin) { if (_behavior != null) _behavior.AfterBurst(rollOrigin); }
        public void OnSubspaceExited() { if (_behavior != null) _behavior.OnSubspaceExited(); }
        public void Tick(float dt) { if (_behavior != null) _behavior.Tick(dt); }

        public int UnitCount { get { return _behavior != null ? _behavior.UnitCount : 0; } }
        public IReadOnlyList<Vec2> Satellites { get { return _behavior != null ? _behavior.Satellites : Empty; } }
        public IReadOnlyList<Vec2> Anchors { get { return _behavior != null ? _behavior.Anchors : Empty; } }
        public IReadOnlyList<Vec2> Trail { get { return _behavior != null ? _behavior.Trail : Empty; } }
        public float MonsterSpeedMultiplier { get { return _behavior != null ? _behavior.MonsterSpeedMultiplier : 1f; } }
        public float DamageTakenMultiplier(MonsterEntity monster) { return _behavior != null ? _behavior.DamageTakenMultiplier(monster) : 1f; }

        public bool OnProjectileHit(ProjectileEntity projectile, MonsterEntity monster)
        {
            return _behavior != null && _behavior.OnProjectileHit(projectile, monster);
        }

        public void OnProjectileEnded(ProjectileEntity projectile)
        {
            if (_behavior != null) _behavior.OnProjectileEnded(projectile);
        }
    }
}
