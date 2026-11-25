using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 一把副武器的行为（11/25 双武器策划案，方案 B）。常驻生效、不参与武器切换，核心效果与翻滚强绑定。
    /// 翻滚钩子由 SubWeaponSystem 转发，顺序固定：BeforeBurst（主武器爆发前）→ AfterBurst（爆发后）→ OnSubspaceExited（回到常态后）。
    ///   · 浮游炮（DronesSubWeapon）：Tick 里环绕玩家自动攻击；OnSubspaceExited 盲盒分裂 / 合体；
    ///   · 引力黑洞（GravityWellSubWeapon）：BeforeBurst 以翻滚前的站位为中心把怪物拉拢；
    ///   · 空间锚（SpaceAnchorSubWeapon）：AfterBurst 回溯到上一个锚点并沿连线斩击，然后记录新锚点；
    ///   · 时间沙漏（HourglassSubWeapon）：Tick 里全场敌人移速渐进衰减；BeforeBurst 重置回基础值；
    ///   · WASD（WasdSubWeapon）：Tick 里按住的移动键方向生成判定区；AfterBurst 四向齐发。
    /// 与主武器共用 WeaponKit（读词条、暴击、伤害结算等工具）。副武器造成的伤害一律用 DamageSource.SubWeapon。
    /// </summary>
    public abstract class SubWeaponBehavior
    {
        protected static readonly List<Vec2> Empty = new List<Vec2>();
        protected readonly WeaponKit Kit;

        protected SubWeaponBehavior(WeaponKit kit) { Kit = kit; }

        public abstract SubWeaponKind Kind { get; }

        public virtual void BeforeBurst(Vec2 rollOrigin) { }
        public virtual void AfterBurst(Vec2 rollOrigin) { }
        public virtual void OnSubspaceExited() { }
        public virtual void Tick(float dt) { }

        public virtual int UnitCount { get { return 0; } }
        public virtual IReadOnlyList<Vec2> Satellites { get { return Empty; } }
        public virtual IReadOnlyList<Vec2> Anchors { get { return Empty; } }
        public virtual IReadOnlyList<Vec2> Trail { get { return Empty; } }
        public virtual bool OnProjectileHit(ProjectileEntity p, MonsterEntity m) { return false; }
        public virtual void OnProjectileEnded(ProjectileEntity p) { }
        /// <summary>全场敌人移速倍率（时间沙漏）。</summary>
        public virtual float MonsterSpeedMultiplier { get { return 1f; } }
        /// <summary>某只怪受到伤害的倍率（时间沙漏「迟滞」）。</summary>
        public virtual float DamageTakenMultiplier(MonsterEntity m) { return 1f; }
    }
}
