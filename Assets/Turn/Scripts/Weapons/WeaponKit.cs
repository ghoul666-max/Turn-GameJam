using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 三把武器共用的工具箱（WeaponSystem 创建，一局一份）：读词条、暴击、旋转、造成伤害并测量实际扣血、水渍存储、退弹。
    /// 武器类（WaterGunWeapon / SpearWeapon / BladeWeapon）只通过它和 ITurnContext 上的系统接口工作，不直接改其它系统的数据，
    /// 唯一的例外是怪物身上的印记字段（MonsterEntity.SpearMarks / BladeMarks 的写入方是武器）。
    /// </summary>
    public sealed class WeaponKit
    {
        public readonly ITurnContext Ctx;
        public readonly ArsenalConfig Arsenal;
        public readonly List<WaterMark> Puddles = new List<WaterMark>();
        /// <summary>临时查询列表（用完即弃，不跨调用保存）。</summary>
        public readonly List<MonsterEntity> Hits = new List<MonsterEntity>();
        public readonly List<MonsterEntity> Hits2 = new List<MonsterEntity>();

        /// <summary>待返还的子弹（击杀回流词条），WeaponSystem 下一步加到当前弹匣。</summary>
        public int PendingRefund;

        public WeaponKit(ITurnContext ctx, ArsenalConfig arsenal)
        {
            Ctx = ctx;
            Arsenal = arsenal ?? new ArsenalConfig();
        }

        public Vec2 PlayerPosition { get { return Ctx.Player.State.Position; } }

        public int Level(BuildId id) { return Ctx.Progression.GetLevel(id); }
        public bool Has(BuildId id) { return Ctx.Progression.GetLevel(id) > 0; }
        /// <summary>总效果 = 层数 × 每层数值。</summary>
        public float Value(BuildId id) { return Ctx.Progression.Value(id); }
        public float Value2(BuildId id) { return Ctx.Progression.Value2(id); }

        public bool Chance(float p)
        {
            return p > 0f && Ctx.Random.NextDouble() < p;
        }

        /// <summary>按暴击词条的概率掷一次：返回伤害倍率，critical 输出是否暴击。</summary>
        public float Crit(BuildId id, out bool critical)
        {
            critical = Chance(MathF.Min(1f, Value(id)));
            return critical ? MathF.Max(1f, Arsenal.CritMultiplier) : 1f;
        }

        /// <summary>造成伤害，返回实际扣掉的生命（吸血按它算）。</summary>
        public float Hit(MonsterEntity m, float damage, DamageSource source, bool critical = false)
        {
            if (m == null || !m.Alive || damage <= 0f) return 0f;
            float before = m.Hp;
            Ctx.Combat.DamageMonster(m, damage, source, critical);
            return MathF.Max(0f, before - MathF.Max(0f, m.Hp));
        }

        /// <summary>按吸血比例回血。</summary>
        public void Lifesteal(float dealt, BuildId id)
        {
            float ratio = Value(id);
            if (ratio > 0f && dealt > 0f) Ctx.Combat.HealPlayer(dealt * ratio);
        }

        public static Vec2 Rotate(Vec2 v, float degrees)
        {
            float a = degrees * (MathF.PI / 180f);
            float c = MathF.Cos(a), s = MathF.Sin(a);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        public static float Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        public static Vec2 Direction(Vec2 from, Vec2 to)
        {
            Vec2 d = to - from;
            return d.SqrMagnitude > 0.0001f ? d.Normalized : Vec2.Right;
        }

        /// <summary>水渍半径 = 基础 × (1 + 水渍扩散)。</summary>
        public float PuddleRadius
        {
            get { return MathF.Max(0.05f, Arsenal.WaterGun.PuddleRadius * (1f + Value(BuildId.WaterPuddleSize))); }
        }

        public void AddPuddle(Vec2 position)
        {
            int max = Math.Max(1, Arsenal.WaterGun.MaxPuddles);
            if (Puddles.Count >= max) Puddles.RemoveAt(0);
            Puddles.Add(new WaterMark { Position = Ctx.Bounds.Clamp(position, 0f), Radius = PuddleRadius });
        }

        /// <summary>离 from 最近的 count 只存活怪物（range 内，按距离从近到远），结果写进 results。</summary>
        public void Nearest(Vec2 from, float range, int count, List<MonsterEntity> results, MonsterEntity exclude = null)
        {
            results.Clear();
            IReadOnlyList<MonsterEntity> all = Ctx.Monsters.Active;
            float rangeSq = range * range;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (!m.Alive || m == exclude || Vec2.SqrDistance(from, m.Position) > rangeSq) continue;
                results.Add(m);
            }
            results.Sort((a, b) => Vec2.SqrDistance(from, a.Position).CompareTo(Vec2.SqrDistance(from, b.Position)));
            if (results.Count > count) results.RemoveRange(count, results.Count - count);
        }

        /// <summary>爆发词条：以玩家为中心一圈，按最大生命百分比造成伤害。</summary>
        public void BurstRing(BuildId id)
        {
            float ratio = Value(id);
            if (ratio <= 0f) return;
            float radius = Value2(id) > 0f ? Value2(id) : Arsenal.BurstRingRadius;
            Vec2 center = PlayerPosition;
            Ctx.Monsters.QueryCircle(center, radius, Hits2);
            int hits = 0;
            for (int i = 0; i < Hits2.Count; i++)
            {
                MonsterEntity m = Hits2[i];
                if (!m.Alive) continue;
                Hit(m, m.MaxHp * ratio, DamageSource.Burst);
                hits++;
            }
            Hits2.Clear();
            Ctx.Events.Publish(new BurstRingEvent { Position = center, Radius = radius, Hits = hits });
        }
    }

    /// <summary>一把武器的行为。无状态（印记存在怪物身上 / WeaponKit.Puddles 里），数值都现读配置与词条。</summary>
    public abstract class WeaponBehavior
    {
        protected readonly WeaponKit Kit;

        protected WeaponBehavior(WeaponKit kit) { Kit = kit; }

        public abstract WeaponKind Kind { get; }
        /// <summary>基础弹药上限（不含词条）。</summary>
        public abstract int BaseAmmo { get; }
        public abstract BuildId AmmoBuild { get; }
        public abstract BuildId RateBuild { get; }
        public abstract BuildId PostHasteBuild { get; }
        public abstract BuildId BurstRingBuild { get; }
        /// <summary>基础攻击间隔（秒）。</summary>
        public abstract float BaseInterval { get; }
        /// <summary>自动索敌范围。</summary>
        public abstract float TargetRange { get; }

        /// <summary>朝 target 攻击一次（弹药由 WeaponSystem 扣）。</summary>
        public abstract void Fire(Vec2 origin, MonsterEntity target);
        /// <summary>玩家子弹命中怪物：返回 true = 继续飞。</summary>
        public virtual bool OnHit(ProjectileEntity p, MonsterEntity m) { return false; }
        public virtual void OnEnded(ProjectileEntity p) { }
        /// <summary>进入亚空间的瞬间：全图铺垫同步爆发。</summary>
        public abstract void Burst();
        /// <summary>受到伤害时的词条。</summary>
        public virtual void OnHurt() { }
    }
}
