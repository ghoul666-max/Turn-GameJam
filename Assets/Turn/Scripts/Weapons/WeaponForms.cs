using Turn.Core;

namespace Turn.Weapons
{
    // ------------------------------------------------------------------
    // 武器形态：<名词>Form。无状态，只通过 fire.World 上的系统接口生成子弹；数值都来自 fire.Form。
    // 扩展新地图 + 新武器：给 PlaneId 加一个值，写一个 IWeaponForm，在 Create 里加一行。
    // 弹匣、射击节奏、全局升级都在 WeaponSystem / ProjectileSystem 里统一处理，新形态自动共享。
    // ------------------------------------------------------------------

    public static class WeaponForms
    {
        public static IWeaponForm Create(PlaneId map)
        {
            switch (map)
            {
                case PlaneId.Volcano: return new FireballForm();
                case PlaneId.Ocean: return new HarpoonForm();
                case PlaneId.Office: return new KeyboardForm();
                case PlaneId.Dream: return new PaintbrushForm();
                case PlaneId.Primal: return new CrossbowForm();
                case PlaneId.Future: return new LaserForm();
                default: return null;
            }
        }

        /// <summary>朝目标发射一颗直线子弹（两种形态共用的发射方式）。</summary>
        public static void FireStraight(WeaponFireContext fire, PlaneId form)
        {
            Vec2 dir = (fire.Target.Position - fire.Origin).Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            FireStraight(fire, form, dir);
        }

        public static void FireStraight(WeaponFireContext fire, PlaneId form, Vec2 dir)
        {
            fire.World.Projectiles.Spawn(form, fire.Origin, dir * fire.Form.ProjectileSpeed,
                fire.Form.Radius, fire.Form.Damage, fire.Form.Pierce, fire.AttackRange);
        }
    }

    /// <summary>火山武器：火球。较慢、较大，默认只打一个目标。</summary>
    public sealed class FireballForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Volcano; } }

        public void Fire(WeaponFireContext fire)
        {
            if (fire.Target == null) return;
            WeaponForms.FireStraight(fire, PlaneId.Volcano);
        }
    }

    /// <summary>海洋武器：鱼叉。更快、更细，默认可穿透（穿透数在 Inspector 里调）。</summary>
    public sealed class HarpoonForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Ocean; } }

        public void Fire(WeaponFireContext fire)
        {
            if (fire.Target == null) return;
            WeaponForms.FireStraight(fire, PlaneId.Ocean);
        }
    }

    /// <summary>工位键盘：每次攻击三枚扇形字符弹。</summary>
    public sealed class KeyboardForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Office; } }
        public void Fire(WeaponFireContext fire)
        {
            if (fire.Target == null) return;
            Vec2 dir = (fire.Target.Position - fire.Origin).Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            float radians = 15f * (System.MathF.PI / 180f);
            for (int i = -1; i <= 1; i++)
            {
                float a = i * radians;
                WeaponForms.FireStraight(fire, Map, new Vec2(dir.X * System.MathF.Cos(a) - dir.Y * System.MathF.Sin(a),
                    dir.X * System.MathF.Sin(a) + dir.Y * System.MathF.Cos(a)));
            }
        }
    }

    public sealed class PaintbrushForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Dream; } }
        public void Fire(WeaponFireContext fire) { if (fire.Target != null) WeaponForms.FireStraight(fire, Map); }
    }

    public sealed class CrossbowForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Primal; } }
        public void Fire(WeaponFireContext fire) { if (fire.Target != null) WeaponForms.FireStraight(fire, Map); }
    }

    public sealed class LaserForm : IWeaponForm
    {
        public PlaneId Map { get { return PlaneId.Future; } }
        public void Fire(WeaponFireContext fire) { if (fire.Target != null) WeaponForms.FireStraight(fire, Map); }
    }
}
