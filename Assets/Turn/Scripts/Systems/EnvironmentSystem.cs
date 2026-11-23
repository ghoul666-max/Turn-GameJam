using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 位面环境机制（程序A）。进入位面时结算一次（OnPlaneEntered），持续效果在 Tick 里结算。
    /// 对玩家 / 怪物的伤害一律经 ICombatSystem（无敌帧照常生效），玩家移速经 IPlayerSystem 的移速修正。
    ///
    ///   · 火山：岩浆规则保持白模做法（11/21 确认）—— 进入时站在岩浆格里的怪物各受一次伤害，随后岩浆消失。
    ///   · 工位：困意。进入后 PlaneTime 开始计时，画面变暗 / 眼皮合拢的进度 = PlaneTime / DrowsyDarkenTime；
    ///           超过 DrowsyDelay 秒后每秒掉 DrowsyDps（按 DotTickInterval 结算）。进入工位时清除玩家身上所有减速。
    ///   · 梦境：每 DreamSlowInterval 秒叠一层 DreamSlowPerStack 减速，上限 DreamSlowMax，离开梦境清零；
    ///           老板来电：每隔 [PhoneIntervalMin, PhoneIntervalMax] 秒在玩家当前位置预警 PhoneWarning 秒，
    ///           随后砸下（半径 PhoneRadius，伤害 PhoneDamage，只伤玩家）。还没砸下就离开梦境的来电直接取消。
    /// 每次进入位面（开局 / 翻滚）都会把计时、困意、减速层、待砸的电话全部清零。
    /// </summary>
    public sealed class EnvironmentSystem : IEnvironmentSystem
    {
        private struct PendingPhone
        {
            public int Id;
            public Vec2 Position;
            public float Timer;
        }

        private readonly ITurnContext _ctx;
        private readonly EnvironmentConfig _env;
        private readonly MonsterConfig _monster;
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private readonly List<PendingPhone> _phones = new List<PendingPhone>();
        private PlaneId _plane;
        private float _drowsyTimer;
        private float _dreamTimer;
        private float _phoneTimer;
        private int _nextHazardId = 1;

        public bool LavaPresent { get; private set; }
        public float PlaneTime { get; private set; }
        public float Drowsiness { get; private set; }
        public bool DrowsyDamaging { get; private set; }
        public int DreamSlowStacks { get; private set; }
        public float DreamSlow { get { return MathF.Min(MaxSlow, DreamSlowStacks * MathF.Max(0f, _env.DreamSlowPerStack)); } }

        private float MaxSlow { get { return MathF.Max(0f, MathF.Min(0.95f, _env.DreamSlowMax)); } }

        public EnvironmentSystem(ITurnContext ctx, EnvironmentConfig env, MonsterConfig monster)
        {
            _ctx = ctx;
            _env = env ?? new EnvironmentConfig();
            _monster = monster;
        }

        // ---------------- 进入位面 ----------------

        public void OnPlaneEntered(PlaneId plane)
        {
            _plane = plane;
            PlaneTime = 0f;
            Drowsiness = 0f;
            DrowsyDamaging = false;
            _drowsyTimer = 0f;
            DreamSlowStacks = 0;
            _dreamTimer = 0f;
            CancelPhones();

            // 移速：进入工位清除所有减速；其他位面只清掉梦境减速（离开梦境清零 / 进入梦境从 0 开始叠）
            if (plane == PlaneId.Office) _ctx.Player.ClearSpeedModifiers();
            else _ctx.Player.SetSpeedModifier(PlayerSpeedSource.DreamSlow, 1f);

            if (plane == PlaneId.Dream) _phoneTimer = NextPhoneInterval();

            LavaPresent = false;
            if (plane == PlaneId.Volcano) EruptLava();

            _ctx.Events.Publish(new PlaneEnteredEvent { Plane = plane });
        }

        public bool IsHazard(Vec2 position)
        {
            if (_ctx.Terrain.Sample(position) == TerrainType.Lava) return true;
            float r = _env.PhoneRadius;
            for (int i = 0; i < _phones.Count; i++)
            {
                if (Vec2.SqrDistance(position, _phones[i].Position) <= r * r) return true;
            }
            return false;
        }

        // ---------------- 每步 ----------------

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            PlaneTime += dt;
            if (_plane == PlaneId.Office) TickDrowsy(dt);
            else if (_plane == PlaneId.Dream)
            {
                TickDreamSlow(dt);
                TickPhones(dt);
            }
        }

        private void TickDrowsy(float dt)
        {
            Drowsiness = Clamp01(PlaneTime / MathF.Max(0.01f, _env.DrowsyDarkenTime));
            if (PlaneTime <= _env.DrowsyDelay)
            {
                DrowsyDamaging = false;
                return;
            }
            DrowsyDamaging = true;

            float interval = MathF.Max(0.02f, _env.DotTickInterval);
            _drowsyTimer += dt;
            while (_drowsyTimer >= interval)
            {
                _drowsyTimer -= interval;
                if (!_ctx.Player.State.Alive) break;
                _ctx.Combat.DamagePlayer(_env.DrowsyDps * interval, DamageSource.Drowsy);
            }
        }

        private void TickDreamSlow(float dt)
        {
            float perStack = MathF.Max(0f, _env.DreamSlowPerStack);
            int maxStacks = perStack > 0f ? (int)MathF.Floor(MaxSlow / perStack + 0.0001f) : 0;
            float interval = MathF.Max(0.05f, _env.DreamSlowInterval);

            _dreamTimer += dt;
            while (_dreamTimer >= interval)
            {
                _dreamTimer -= interval;
                if (DreamSlowStacks < maxStacks) DreamSlowStacks++;
            }
            _ctx.Player.SetSpeedModifier(PlayerSpeedSource.DreamSlow, 1f - DreamSlow);
        }

        // ---------------- 老板来电 ----------------

        private void TickPhones(float dt)
        {
            for (int i = _phones.Count - 1; i >= 0; i--)
            {
                PendingPhone phone = _phones[i];
                phone.Timer -= dt;
                if (phone.Timer > 0f)
                {
                    _phones[i] = phone;
                    continue;
                }
                _phones.RemoveAt(i);
                LandPhone(phone);
            }

            _phoneTimer -= dt;
            if (_phoneTimer > 0f) return;
            _phoneTimer = NextPhoneInterval();
            PlayerState player = _ctx.Player.State;
            if (player.Alive) WarnPhone(player.Position);
        }

        private void WarnPhone(Vec2 position)
        {
            var phone = new PendingPhone
            {
                Id = _nextHazardId++,
                Position = position,
                Timer = MathF.Max(0.05f, _env.PhoneWarning),
            };
            _phones.Add(phone);
            _ctx.Events.Publish(new HazardWarnedEvent { Id = phone.Id, Position = position, Radius = _env.PhoneRadius, Delay = phone.Timer });
        }

        private void LandPhone(PendingPhone phone)
        {
            PlayerState player = _ctx.Player.State;
            float reach = _env.PhoneRadius + _ctx.Player.Config.Radius;
            bool inRange = player.Alive && Vec2.SqrDistance(player.Position, phone.Position) <= reach * reach;
            bool hit = inRange && _ctx.Combat.DamagePlayer(_env.PhoneDamage, DamageSource.Phone);
            _ctx.Events.Publish(new HazardLandedEvent { Id = phone.Id, Position = phone.Position, Radius = _env.PhoneRadius, HitPlayer = hit });
        }

        private void CancelPhones()
        {
            for (int i = 0; i < _phones.Count; i++) _ctx.Events.Publish(new HazardCancelledEvent { Id = _phones[i].Id });
            _phones.Clear();
        }

        private float NextPhoneInterval()
        {
            float lo = MathF.Max(0.5f, _env.PhoneIntervalMin);
            float hi = MathF.Max(lo, _env.PhoneIntervalMax);
            return lo + (float)_ctx.Random.NextDouble() * (hi - lo);
        }

        // ---------------- 火山岩浆（白模规则） ----------------

        private void EruptLava()
        {
            LavaPresent = true;
            TerrainGridData grid = _ctx.Terrain.GetGrid(PlaneId.Volcano);

            // 先收集再结算：结算可能击杀怪物，不在遍历 Active 的同时改它的状态
            _hits.Clear();
            IReadOnlyList<MonsterEntity> monsters = _ctx.Monsters.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterEntity m = monsters[i];
                if (m.Alive && grid.Sample(m.Position) == TerrainType.Lava) _hits.Add(m);
            }

            float damage = _monster.LavaHitDamage;
            for (int i = 0; i < _hits.Count; i++) _ctx.Combat.DamageMonster(_hits[i], damage, DamageSource.Lava);

            LavaPresent = false; // 受过一次伤害后岩浆消失
            _ctx.Events.Publish(new LavaEruptedEvent { Hits = _hits.Count, Damage = damage });
            _hits.Clear();
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
