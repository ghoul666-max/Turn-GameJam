using Turn.Core;

namespace Turn.Weapons
{
    // ------------------------------------------------------------------
    // 武器形态：<名词>Form。无状态，只通过 fire.World 上的系统接口生成子弹；数值都来自 fire.Form。
    // 扩展新地图 + 新武器：给 MapId 加一个值，写一个 IWeaponForm，在 Create 里加一行。
    // 弹匣、射击节奏、全局升级都在 WeaponSystem / ProjectileSystem 里统一处理，新形态自动共享。
    // ------------------------------------------------------------------

    public static class WeaponForms
    {
        public static IWeaponForm Create(MapId map)
        {
            switch (map)
            {
                case MapId.Volcano: return new FireballForm();
                case MapId.Ocean: return new HarpoonForm();
                default: return null;
            }
        }

        /// <summary>朝目标发射一颗直线子弹（两种形态共用的发射方式）。</summary>
        public static void FireStraight(WeaponFireContext fire, MapId form)
        {
            Vec2 dir = (fire.Target.Position - fire.Origin).Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            fire.World.Projectiles.Spawn(form, fire.Origin, dir * fire.Form.ProjectileSpeed,
                fire.Form.Radius, fire.Form.Damage, fire.Form.Pierce, fire.AttackRange);
        }
    }

    /// <summary>火山武器：火球。较慢、较大，默认只打一个目标。</summary>
    public sealed class FireballForm : IWeaponForm
    {
        public MapId Map { get { return MapId.Volcano; } }

        public void Fire(WeaponFireContext fire)
        {
            if (fire.Target == null) return;
            WeaponForms.FireStraight(fire, MapId.Volcano);
        }
    }

    /// <summary>海洋武器：鱼叉。更快、更细，默认可穿透（穿透数在 Inspector 里调）。</summary>
    public sealed class HarpoonForm : IWeaponForm
    {
        public MapId Map { get { return MapId.Ocean; } }

        public void Fire(WeaponFireContext fire)
        {
            if (fire.Target == null) return;
            WeaponForms.FireStraight(fire, MapId.Ocean);
        }
    }
}
