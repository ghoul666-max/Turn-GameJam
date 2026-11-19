using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Weapons
{
    // ------------------------------------------------------------------
    // 武器行为：<武器>Behavior。无状态（只有临时缓冲区），
    // 只通过 fire.World 上的系统接口产生投射物；数值都来自 fire.Stats。
    // 投射物的“基础尺寸 / 速度”常量写在各类里，等级表里的 Area / Speed 是倍率。
    // ------------------------------------------------------------------

    public static class WeaponBehaviors
    {
        public static IWeaponBehavior Create(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Wand: return new WandBehavior();
                case WeaponKind.Knife: return new KnifeBehavior();
                case WeaponKind.Whip: return new WhipBehavior();
                case WeaponKind.Aura: return new AuraBehavior();
                case WeaponKind.Orbit: return new OrbitBehavior();
                case WeaponKind.Lightning: return new LightningBehavior();
                default: return null;
            }
        }
    }

    /// <summary>魔杖：向最近的敌人发射魔弹。范围内没有敌人时不发射。</summary>
    public sealed class WandBehavior : IWeaponBehavior
    {
        public const float Range = 12f;
        public const float BaseSpeed = 8f;
        public const float BaseRadius = 0.22f;
        public const float Life = 2f;

        private readonly List<EnemyEntity> _targets = new List<EnemyEntity>();

        public void Fire(WeaponFireContext fire)
        {
            ISurvivorContext world = fire.World;
            Vec2 origin = world.Player.State.Position;
            world.Enemies.GetNearest(origin, Range, fire.Stats.Amount, _targets);
            if (_targets.Count == 0) return;

            for (int i = 0; i < fire.Stats.Amount; i++)
            {
                EnemyEntity target = _targets[i % _targets.Count];
                Vec2 dir = (target.Position - origin).Normalized;
                if (dir.SqrMagnitude < 0.5f) dir = world.Player.State.Facing;
                if (i >= _targets.Count) dir = Vec2.Rotate(dir, (i - _targets.Count + 1) * 0.18f); // 目标不够时稍微散开

                ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
                spec.Motion = ProjectileMotion.Linear;
                spec.Position = origin;
                spec.Velocity = dir * (BaseSpeed * fire.Stats.Speed);
                spec.Radius = BaseRadius * fire.Stats.Area;
                spec.Life = Life;
                spec.Pierce = fire.Stats.Pierce;
                spec.Damage = fire.Stats.Damage;
                spec.Knockback = fire.Stats.Knockback;
                spec.Rotation = dir.Angle;
                spec.Scale = fire.Stats.Area;
                world.Projectiles.Spawn(spec);
            }
        }
    }

    /// <summary>飞刀：朝最近一次移动的方向连续掷出，多把飞刀左右错开。</summary>
    public sealed class KnifeBehavior : IWeaponBehavior
    {
        public const float BaseSpeed = 14f;
        public const float BaseRadius = 0.18f;
        public const float Spacing = 0.22f;
        public const float Life = 1.1f;

        public void Fire(WeaponFireContext fire)
        {
            ISurvivorContext world = fire.World;
            PlayerState player = world.Player.State;
            Vec2 dir = player.Facing.SqrMagnitude > 0.01f ? player.Facing.Normalized : Vec2.Right;
            Vec2 side = dir.Perpendicular;
            int amount = fire.Stats.Amount;

            for (int i = 0; i < amount; i++)
            {
                float offset = (i - (amount - 1) * 0.5f) * Spacing;
                ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
                spec.Motion = ProjectileMotion.Linear;
                spec.Position = player.Position + side * offset - dir * (MathF.Abs(offset) * 0.6f);
                spec.Velocity = dir * (BaseSpeed * fire.Stats.Speed);
                spec.Radius = BaseRadius * fire.Stats.Area;
                spec.Life = Life;
                spec.Pierce = fire.Stats.Pierce;
                spec.Damage = fire.Stats.Damage;
                spec.Knockback = fire.Stats.Knockback * 0.5f;
                spec.Rotation = dir.Angle;
                spec.Scale = fire.Stats.Area;
                world.Projectiles.Spawn(spec);
            }
        }
    }

    /// <summary>鞭子：在面朝的一侧横扫一个矩形；数量 2 时身后也来一鞭，再多就上下错开。</summary>
    public sealed class WhipBehavior : IWeaponBehavior
    {
        public const float BaseHalfWidth = 1.35f;
        public const float BaseHalfHeight = 0.38f;
        public const float Reach = 1.45f;
        public const float RowSpacing = 0.45f;

        public void Fire(WeaponFireContext fire)
        {
            ISurvivorContext world = fire.World;
            int facing = world.Player.State.FacingX;
            float area = fire.Stats.Area;

            for (int i = 0; i < fire.Stats.Amount; i++)
            {
                int side = i % 2 == 0 ? facing : -facing;
                float row = (i / 2) * RowSpacing;

                ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
                spec.Motion = ProjectileMotion.Attached;
                spec.Shape = HitShape.Box;
                spec.Offset = new Vec2(side * Reach * area, 0.15f + row);
                spec.HalfExtents = new Vec2(BaseHalfWidth * area, BaseHalfHeight * area);
                spec.Life = fire.Stats.Duration;
                spec.Pierce = 0;
                spec.Damage = fire.Stats.Damage;
                spec.Knockback = fire.Stats.Knockback;
                spec.FitVisual = true;
                spec.Facing = side;
                world.Projectiles.Spawn(spec);
            }
        }
    }

    /// <summary>
    /// 大蒜：贴身的圆形光环。每次冷却生成一个寿命 = 冷却的光环，它对每个敌人只打一次，
    /// 于是“站在光环里的敌人每隔一个冷却挨一下”。前后两个光环首尾相接，看起来是常驻的。
    /// </summary>
    public sealed class AuraBehavior : IWeaponBehavior
    {
        public const float BaseRadius = 1.3f;

        public void Fire(WeaponFireContext fire)
        {
            ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
            spec.Motion = ProjectileMotion.Attached;
            spec.Shape = HitShape.Circle;
            spec.Radius = BaseRadius * fire.Stats.Area;
            spec.Life = fire.Stats.Cooldown + 0.02f;
            spec.Pierce = 0;
            spec.Damage = fire.Stats.Damage;
            spec.Knockback = fire.Stats.Knockback;
            spec.FitVisual = true;
            fire.World.Projectiles.Spawn(spec);
        }
    }

    /// <summary>圣经：若干本书绕玩家旋转 Duration 秒，同一敌人每 RehitInterval 秒可再被同一本书打中。</summary>
    public sealed class OrbitBehavior : IWeaponBehavior
    {
        public const float BaseOrbitRadius = 1.7f;
        public const float BaseAngularSpeed = 3.4f;
        public const float BaseRadius = 0.32f;
        public const float RehitInterval = 0.5f;

        public void Fire(WeaponFireContext fire)
        {
            int amount = fire.Stats.Amount;
            float start = (float)(fire.World.Random.NextDouble() * Math.PI * 2.0);

            for (int i = 0; i < amount; i++)
            {
                ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
                spec.Motion = ProjectileMotion.Orbit;
                spec.OrbitAngle = start + i * (float)(Math.PI * 2.0) / amount;
                spec.OrbitRadius = BaseOrbitRadius * fire.Stats.Area;
                spec.OrbitSpeed = BaseAngularSpeed * fire.Stats.Speed;
                spec.Radius = BaseRadius * fire.Stats.Area;
                spec.Life = fire.Stats.Duration;
                spec.Pierce = 0;
                spec.Damage = fire.Stats.Damage;
                spec.Knockback = fire.Stats.Knockback;
                spec.RehitInterval = RehitInterval;
                spec.Scale = fire.Stats.Area;
                fire.World.Projectiles.Spawn(spec);
            }
        }
    }

    /// <summary>闪电：随机劈中附近的若干敌人，落点有一个小范围伤害。</summary>
    public sealed class LightningBehavior : IWeaponBehavior
    {
        public const float Range = 9f;
        public const float BaseRadius = 0.7f;
        public const float Life = 0.3f;

        private readonly List<EnemyEntity> _targets = new List<EnemyEntity>();

        public void Fire(WeaponFireContext fire)
        {
            ISurvivorContext world = fire.World;
            world.Enemies.GetRandomInRange(world.Player.State.Position, Range, fire.Stats.Amount, _targets);

            for (int i = 0; i < _targets.Count; i++)
            {
                ProjectileSpec spec = ProjectileSpec.Create(fire.Weapon);
                spec.Motion = ProjectileMotion.Static;
                spec.Position = _targets[i].Position;
                spec.Radius = BaseRadius * fire.Stats.Area;
                spec.Life = Life;
                spec.Pierce = 0;
                spec.Damage = fire.Stats.Damage;
                spec.Knockback = 0f;
                spec.Scale = fire.Stats.Area;
                world.Projectiles.Spawn(spec);
            }
        }
    }
}
