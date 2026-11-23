using System;
using Turn.Core;
using Turn.Weapons;

namespace Turn.Systems
{
    /// <summary>
    /// 武器：全局只有一个弹匣（火球和鱼叉共用），形态由当前激活地图决定，自动索敌、自动射击。
    /// 硬性规则：
    ///   · 弹药 &lt; 每发消耗时停止攻击；
    ///   · 没有任何随时间恢复弹药的逻辑，也没有换弹按键；
    ///   · Reload() 是唯一的装填入口，唯一调用方是 RollSystem.TryRoll（成功翻滚）。
    /// 扩容弹匣词条只增加容量、不补弹（补弹 = 非翻滚装填，违反规则）。
    /// </summary>
    public sealed class WeaponSystem : IWeaponSystem
    {
        private readonly ITurnContext _ctx;
        private readonly WeaponConfig _config;
        private readonly WeaponFormConfig[] _forms = new WeaponFormConfig[PlaneIds.Count];
        private readonly IWeaponForm[] _behaviors = new IWeaponForm[PlaneIds.Count];
        private readonly UpgradeConfig _upgrades;
        private bool _emptyAnnounced;
        private int _pendingKillRefund;

        public int Ammo { get; private set; }
        public float FireTimer { get; private set; }

        public WeaponSystem(ITurnContext ctx, WeaponConfig config, WeaponFormConfig[] forms, UpgradeConfig upgrades)
        {
            _ctx = ctx;
            _config = config;
            _upgrades = upgrades;
            for (int i = 0; i < PlaneIds.Count; i++)
            {
                PlaneId map = (PlaneId)i;
                WeaponFormConfig form = forms != null && i < forms.Length ? forms[i] : null;
                _forms[i] = form ?? new WeaponFormConfig { Map = map, Name = PlaneIds.DisplayName(map) + "武器" };
                _behaviors[i] = WeaponForms.Create(map);
            }

            Ammo = config.InitialAmmo < 0 ? Capacity : Math.Min(config.InitialAmmo, Capacity);
            FireTimer = config.FireInterval;
            _ctx.Events.Subscribe<WeaponKillConfirmedEvent>(OnWeaponKillConfirmed);
        }

        private float PlaneValue(float[] values, float fallback)
        {
            int index = (int)Form;
            return values != null && index >= 0 && index < values.Length && values[index] > 0f
                ? values[index] : fallback;
        }

        public PlaneId Form { get { return _ctx.Plane.Active; } }
        private int BuildLevel(BuildId id) { return _ctx.Progression.GetLevel(BuildIds.Query(id)); }
        public WeaponFormConfig CurrentForm { get { return _forms[(int)Form]; } }

        /// <summary>两侧共用基础弹匣；装填只允许成功翻滚调用。</summary>
        public int Capacity
        {
            get
            {
                int index = (int)Form;
                return index >= 0 && index < _config.PlaneCapacities.Length
                    ? Math.Max(1, _config.PlaneCapacities[index]) : Math.Max(1, _config.MagazineSize);
            }
        }

        public void Reload()
        {
            Ammo = Capacity;
            _emptyAnnounced = false;
            _ctx.Events.Publish(new MagazineReloadedEvent { Ammo = Ammo, Capacity = Capacity });
        }

        private void OnWeaponKillConfirmed(WeaponKillConfirmedEvent evt)
        {
            if (evt.Form == PlaneId.Primal || evt.Form == PlaneId.Future) return;
            _pendingKillRefund += BuildLevel(BuildId.B11) * Math.Max(0, _upgrades.KillAmmoRefundPerLevel);
        }

        public void Tick(float dt)
        {
            if (_pendingKillRefund > 0)
            {
                Ammo = Math.Min(Capacity, Ammo + _pendingKillRefund);
                _pendingKillRefund = 0;
                _emptyAnnounced = false;
            }
            PlayerState player = _ctx.Player.State;
            if (!player.Alive) return;

            if (FireTimer > 0f) FireTimer -= dt;
            if (FireTimer > 0f) return;

            int cost = Math.Max(1, _config.AmmoPerShot);
            if (Ammo < cost)
            {
                // 打空：停火，等待翻滚装填（不会自动恢复）。
                if (!_emptyAnnounced)
                {
                    _emptyAnnounced = true;
                    _ctx.Events.Publish(new AmmoEmptiedEvent());
                }
                FireTimer = 0f;
                return;
            }

            float baseInterval = PlaneValue(_config.PlaneFireIntervals, _config.FireInterval);
            float targetRange = PlaneValue(_config.PlaneTargetRanges, _config.TargetRange) *
                (1f + BuildLevel(BuildId.B05) * _upgrades.TargetRangePerLevel);
            MonsterEntity target = _ctx.Monsters.GetNearest(player.Position, targetRange);
            if (target == null)
            {
                FireTimer = 0f; // 有目标进入范围时立刻开火
                return;
            }

            IWeaponForm behavior = _behaviors[(int)Form];
            if (behavior == null) return;

            int extra = BuildLevel(BuildId.B03) * Math.Max(0, _upgrades.ExtraProjectilePerLevel);
            float speedScale = 1f + BuildLevel(BuildId.B04) * _upgrades.ProjectileSpeedPerLevel;
            var boosted = new WeaponFormConfig
            {
                Map = CurrentForm.Map, Name = CurrentForm.Name,
                ProjectileSpeed = CurrentForm.ProjectileSpeed * speedScale,
                Damage = CurrentForm.Damage,
                Radius = CurrentForm.Radius, Pierce = CurrentForm.Pierce,
                SplashRadius = CurrentForm.SplashRadius, BurnDps = CurrentForm.BurnDps,
                BurnDuration = CurrentForm.BurnDuration, PoisonDps = CurrentForm.PoisonDps,
                PoisonDuration = CurrentForm.PoisonDuration, PoisonMaxStacks = CurrentForm.PoisonMaxStacks,
                HarpoonFalloff = CurrentForm.HarpoonFalloff,
            };
            Vec2 direction = (target.Position - player.Position).Normalized;
            if (direction.SqrMagnitude < 0.5f) direction = Vec2.Right;
            var shotContext = new WeaponFireContext
            {
                World = _ctx, Form = boosted, Origin = player.Position, Target = target,
                AttackRange = PlaneValue(_config.PlaneAttackRanges, _config.AttackRange),
            };
            behavior.Fire(shotContext);
            for (int shot = 0; shot < extra; shot++)
            {
                float offset = (shot - (extra - 1) * .5f) * _upgrades.MultiShotSpreadDegrees;
                // 单枚额外投射也有散布，避免与主弹完全重合。
                if (extra == 1) offset = _upgrades.MultiShotSpreadDegrees;
                float angle = offset * (MathF.PI / 180f);
                Vec2 aimed = new Vec2(direction.X * MathF.Cos(angle) - direction.Y * MathF.Sin(angle),
                    direction.X * MathF.Sin(angle) + direction.Y * MathF.Cos(angle));
                WeaponForms.FireStraight(shotContext, Form, aimed);
            }

            Ammo -= cost;
            float intervalScale = 1f - BuildLevel(BuildId.B02) * _upgrades.FireIntervalReductionPerLevel;
            FireTimer = MathF.Max(_upgrades.MinFireInterval, baseInterval * MathF.Max(0f, intervalScale));
            _ctx.Events.Publish(new ShotFiredEvent
            {
                Form = Form,
                Origin = player.Position,
                Direction = (target.Position - player.Position).Normalized,
                AmmoLeft = Ammo,
            });
        }
    }
}
