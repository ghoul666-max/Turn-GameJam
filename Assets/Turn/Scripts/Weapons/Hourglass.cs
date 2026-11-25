using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · 时间沙漏（11/25 第二批策划案）。节奏控场。
    /// 常态：全场敌人移速随时间渐进衰减：倍率 = 下限 + (1 − 下限) × e^(−衰减速率 × t)，t = 距上次翻滚的常态时间；下限默认 1/2。
    /// 翻滚（爆发前）：所有敌人移速立即重置为基础值，回到常态后重新开始衰减。
    /// 「迟滞」：敌人越慢受到的伤害越高（降到一半移速时 +词条数值）；亚空间里按重置前的减速程度算，让本次爆发也吃到加成。
    /// </summary>
    public sealed class HourglassSubWeapon : SubWeaponBehavior
    {
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private float _time;
        private float _postSlowLeft;
        private float _burstSlowness;

        public HourglassSubWeapon(WeaponKit kit) : base(kit) { }

        private HourglassConfig Cfg { get { return Kit.Arsenal.Hourglass; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.Hourglass; } }

        private float Floor { get { return MathF.Max(0.05f, MathF.Min(1f, Cfg.FloorSpeed - Kit.Value(BuildId.HourglassFloor))); } }

        /// <summary>只算渐进衰减的部分（不含「余韵」）。</summary>
        private float Decay
        {
            get
            {
                float rate = MathF.Max(0f, Cfg.DecayRate * (1f + Kit.Value(BuildId.HourglassDecay)));
                float floor = Floor;
                return floor + (1f - floor) * MathF.Exp(-rate * _time);
            }
        }

        public override float MonsterSpeedMultiplier
        {
            get
            {
                float m = Decay;
                if (_postSlowLeft > 0f) m *= MathF.Max(0f, 1f - Kit.Value(BuildId.HourglassPostSlow));
                return m;
            }
        }

        public override void Tick(float dt)
        {
            _time += dt;
            if (_postSlowLeft > 0f) _postSlowLeft = MathF.Max(0f, _postSlowLeft - dt);
        }

        public override void BeforeBurst(Vec2 rollOrigin)
        {
            _burstSlowness = 1f - MonsterSpeedMultiplier;
            _time = 0f;
            _postSlowLeft = 0f;
            int hits = 0;
            float radius = 0f;
            float ratio = Kit.Value(BuildId.HourglassResetBlast);
            if (ratio > 0f)
            {
                // 时之刃：重置瞬间对周身敌人造成最大生命百分比伤害
                radius = MathF.Max(0.3f, Kit.Value2(BuildId.HourglassResetBlast));
                Kit.Ctx.Monsters.QueryCircle(Kit.PlayerPosition, radius, _query);
                for (int i = 0; i < _query.Count; i++)
                {
                    MonsterEntity m = _query[i];
                    if (!m.Alive) continue;
                    Kit.Hit(m, m.MaxHp * ratio, DamageSource.SubWeapon);
                    hits++;
                }
                _query.Clear();
            }
            Kit.Ctx.Events.Publish(new HourglassResetEvent { Position = Kit.PlayerPosition, BlastRadius = radius, Hits = hits });
        }

        public override void OnSubspaceExited()
        {
            if (Kit.Has(BuildId.HourglassPostSlow)) _postSlowLeft = MathF.Max(0.1f, Kit.Value2(BuildId.HourglassPostSlow));
        }

        public override float DamageTakenMultiplier(MonsterEntity m)
        {
            float v = Kit.Value(BuildId.HourglassVulnerable);
            if (v <= 0f || m == null) return 1f;
            float slowness = Kit.Ctx.Player.State.InSubspace ? _burstSlowness : 1f - MonsterSpeedMultiplier * m.SlowFactor;
            return 1f + v * MathF.Max(0f, slowness) / 0.5f;
        }
    }
}
