using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 副武器 · WASD（11/25 第二批策划案）。移动联动。
    /// 常态：按住移动键时，对应方向（离玩家 Distance）生成半径 Radius 的按键判定区；每只敌人 HitCooldown 秒最多吃 1 次常态伤害；
    ///       最多同时 2 个方向（斜向 = 两个键）；「斜键」词条再加一个对角线方向。
    /// 翻滚（爆发后）：W / A / S / D 四个方向同时齐发。
    /// 「防御键」：受到伤害时四个方向各触发一次常态按键伤害（不受冷却限制）。
    /// </summary>
    public sealed class WasdSubWeapon : SubWeaponBehavior
    {
        private static readonly Vec2[] Four = { new Vec2(0f, 1f), new Vec2(-1f, 0f), new Vec2(0f, -1f), new Vec2(1f, 0f) };
        private readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
        private readonly List<int> _expired = new List<int>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private float _time;
        private float _check;
        private float _cleanup;

        public WasdSubWeapon(WeaponKit kit) : base(kit)
        {
            kit.Ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private WasdConfig Cfg { get { return Kit.Arsenal.Wasd; } }

        public override SubWeaponKind Kind { get { return SubWeaponKind.Wasd; } }

        private float Area { get { return 1f + Kit.Value(BuildId.WasdArea); } }

        private float KeyDamage
        {
            get
            {
                float damage = Cfg.Damage * (1f + Kit.Value(BuildId.WasdDamage));
                float bonus = Kit.Value(BuildId.WasdSpeedBonus);
                if (bonus > 0f)
                {
                    // 连击：移动越快加成越高（按当前移速 / 基础移速）
                    float baseSpeed = MathF.Max(0.1f, Kit.Ctx.Player.Config.MoveSpeed);
                    damage *= 1f + bonus * Kit.Ctx.Player.MoveSpeed / baseSpeed;
                }
                return damage;
            }
        }

        public override void Tick(float dt)
        {
            _time += dt;
            _cleanup -= dt;
            if (_cleanup <= 0f)
            {
                _cleanup = 2f;
                foreach (KeyValuePair<int, float> kv in _lastHit)
                    if (_time - kv.Value > Cfg.HitCooldown) _expired.Add(kv.Key);
                for (int i = 0; i < _expired.Count; i++) _lastHit.Remove(_expired[i]);
                _expired.Clear();
            }

            _check -= dt;
            if (_check > 0f) return;
            _check += MathF.Max(0.02f, Cfg.CheckInterval);
            PlayerState s = Kit.Ctx.Player.State;
            if (!s.Alive) return;
            Vec2 move = s.MoveDir;
            Vec2 h = move.X > 0.3f ? new Vec2(1f, 0f) : move.X < -0.3f ? new Vec2(-1f, 0f) : Vec2.Zero;
            Vec2 v = move.Y > 0.3f ? new Vec2(0f, 1f) : move.Y < -0.3f ? new Vec2(0f, -1f) : Vec2.Zero;
            float damage = KeyDamage;
            if (h.SqrMagnitude > 0f) Zone(h, damage, true);
            if (v.SqrMagnitude > 0f) Zone(v, damage, true);
            if (h.SqrMagnitude > 0f && v.SqrMagnitude > 0f && Kit.Has(BuildId.WasdDiagonal)) Zone((h + v).Normalized, damage, true);
        }

        /// <summary>一个方向的常态按键判定。useCooldown = 受每只敌人 HitCooldown 秒一次的限制。</summary>
        private void Zone(Vec2 dir, float damage, bool useCooldown)
        {
            float area = Area;
            Vec2 center = Kit.PlayerPosition + dir * (Cfg.Distance * area);
            float radius = MathF.Max(0.1f, Cfg.Radius * area);
            Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
            int hits = 0;
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive) continue;
                float last;
                if (useCooldown && _lastHit.TryGetValue(m.Id, out last) && _time - last < Cfg.HitCooldown) continue;
                _lastHit[m.Id] = _time;
                Kit.Hit(m, damage, DamageSource.SubWeapon);
                hits++;
            }
            _query.Clear();
            if (hits > 0) Kit.Ctx.Events.Publish(new WasdStrikeEvent { Center = center, Radius = radius, Burst = false, Hits = hits });
        }

        public override void AfterBurst(Vec2 rollOrigin)
        {
            float area = 1f + Kit.Value(BuildId.WasdBurstArea);
            float radius = MathF.Max(0.1f, Cfg.BurstRadius * area);
            float damage = Cfg.BurstDamage * (1f + Kit.Value(BuildId.WasdBurstDamage));
            float stun = Kit.Value(BuildId.WasdBurstStun);
            for (int d = 0; d < Four.Length; d++)
            {
                Vec2 center = Kit.PlayerPosition + Four[d] * (Cfg.BurstDistance * area);
                Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
                int hits = 0;
                for (int i = 0; i < _query.Count; i++)
                {
                    MonsterEntity m = _query[i];
                    if (!m.Alive) continue;
                    bool crit;
                    float mult = Kit.Crit(BuildId.WasdBurstCrit, out crit);
                    Kit.Hit(m, damage * mult, DamageSource.SubWeapon, crit);
                    if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
                    hits++;
                }
                _query.Clear();
                Kit.Ctx.Events.Publish(new WasdStrikeEvent { Center = center, Radius = radius, Burst = true, Hits = hits });
            }
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            if (!Kit.Has(BuildId.WasdHurtStrike) || !Kit.Ctx.Player.State.Alive) return;
            float damage = KeyDamage;
            for (int d = 0; d < Four.Length; d++) Zone(Four[d], damage, false);
        }
    }
}
