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
            Arsenal = (arsenal ?? new ArsenalConfig()).Normalize();
        }

        /// <summary>能不能被击退 / 拉扯：冲锋中的疾冲兽免疫（策划：冲锋型怪物免疫击退类效果）。</summary>
        public static bool CanDisplace(MonsterEntity m)
        {
            return m != null && m.Alive && !(m.Kind == MonsterKind.Charger && m.AiPhase == 2);
        }

        /// <summary>击退 distance；返回是否撞上了障碍物 / 地图边缘（实际位移明显短于 distance）。免疫击退的返回 false。</summary>
        public bool Knockback(MonsterEntity m, Vec2 direction, float distance)
        {
            if (!CanDisplace(m) || distance <= 0f) return false;
            float moved = Ctx.Monsters.Knockback(m, direction, distance);
            return moved < distance * 0.85f - 0.02f;
        }

        public Vec2 PlayerPosition { get { return Ctx.Player.State.Position; } }

        public int Level(BuildId id) { return Ctx.Progression.GetLevel(id); }
        public bool Has(BuildId id) { return Ctx.Progression.GetLevel(id) > 0; }
        /// <summary>总效果 = 层数 × 每层数值。</summary>
        public float Value(BuildId id) { return Ctx.Progression.Value(id); }
        /// <summary>可空版本：没有这个词条（null）时为 0。</summary>
        public float Value(BuildId? id) { return id.HasValue ? Ctx.Progression.Value(id.Value) : 0f; }
        public float Value2(BuildId? id) { return id.HasValue ? Ctx.Progression.Value2(id.Value) : 0f; }
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
        public float Hit(MonsterEntity m, float damage, DamageSource source, bool critical = false, bool percentageDamage = false)
        {
            if (m == null || !m.Alive || damage <= 0f) return 0f;
            float before = m.Hp;
            if (!percentageDamage) damage *= MathF.Max(0f, Arsenal.FixedDamageMultiplier);
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
        public void BurstRing(BuildId? build)
        {
            if (build == null) return;
            BuildId id = build.Value;
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
                Hit(m, m.MaxHp * ratio, DamageSource.Burst, percentageDamage: true);
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
        /// <summary>「弹药上限 +15%」词条；没有的武器返回 null。</summary>
        public virtual BuildId? AmmoBuild { get { return null; } }
        /// <summary>「攻速 +10%」词条；没有的武器返回 null。</summary>
        public virtual BuildId? RateBuild { get { return null; } }
        /// <summary>「翻滚结束后攻速提升」词条；没有的武器（弩）返回 null。</summary>
        public virtual BuildId? PostHasteBuild { get { return null; } }
        /// <summary>「翻滚爆发时对周身一圈百分比伤害」词条；没有的武器（弩）返回 null。</summary>
        public virtual BuildId? BurstRingBuild { get { return null; } }
        /// <summary>基础攻击间隔（秒）。</summary>
        public abstract float BaseInterval { get; }
        /// <summary>自动索敌范围。</summary>
        public abstract float TargetRange { get; }

        /// <summary>false = 不需要目标也会「攻击」（扑克牌：没有敌人也照样抽牌；Fire 的 target 可能为 null）。</summary>
        public virtual bool NeedsTarget { get { return true; } }

        /// <summary>有目标进入索敌范围时，这一次要不要攻击（掌：面向的扇形里没有敌人就不打，不浪费弹药）。</summary>
        public virtual bool WantsToFire(Vec2 origin, MonsterEntity nearest) { return true; }

        /// <summary>朝 target 攻击一次（弹药由 WeaponSystem 扣）。</summary>
        public abstract void Fire(Vec2 origin, MonsterEntity target);
        /// <summary>玩家子弹命中怪物：返回 true = 继续飞。</summary>
        public virtual bool OnHit(ProjectileEntity p, MonsterEntity m) { return false; }
        public virtual void OnEnded(ProjectileEntity p) { }
        /// <summary>进入亚空间的瞬间：全图铺垫同步爆发。</summary>
        public abstract void Burst();
        /// <summary>受到伤害时的词条。</summary>
        public virtual void OnHurt() { }
        /// <summary>成为当前生效的主武器（开局不调用）。</summary>
        public virtual void OnEquipped(bool byRoll) { }
        /// <summary>不再是当前生效的主武器（例：弩的杀气在这里清零）。</summary>
        public virtual void OnUnequipped(bool byRoll) { }
        /// <summary>常态空间每步调用（不管是不是当前武器）：留在场上的东西（手里剑、裂隙）在这里结算。</summary>
        public virtual void TickAlways(float dt) { }
        /// <summary>表现层：把留在场上的东西写进 results（不清空）。</summary>
        public virtual void CollectFields(List<WeaponField> results) { }
        /// <summary>挡下一次玩家受到的碰撞 / 弹幕伤害（方片护盾）。</summary>
        public virtual bool AbsorbPlayerHit() { return false; }
    }
}
