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
        }

        public PlaneId Form { get { return _ctx.Plane.Active; } }
        public WeaponFormConfig CurrentForm { get { return _forms[(int)Form]; } }

        /// <summary>容量 = 基础 + 扩容词条等级 × 每级增加（两种形态共用）。</summary>
        public int Capacity
        {
            get { return Math.Max(1, _config.MagazineSize + _ctx.Progression.GetLevel(UpgradeId.Magazine) * _upgrades.MagazinePerLevel); }
        }

        public void Reload()
        {
            Ammo = Capacity;
            _emptyAnnounced = false;
            _ctx.Events.Publish(new MagazineReloadedEvent { Ammo = Ammo, Capacity = Capacity });
        }

        public void Tick(float dt)
        {
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

            MonsterEntity target = _ctx.Monsters.GetNearest(player.Position, _config.TargetRange);
            if (target == null)
            {
                FireTimer = 0f; // 有目标进入范围时立刻开火
                return;
            }

            IWeaponForm behavior = _behaviors[(int)Form];
            if (behavior == null) return;

            behavior.Fire(new WeaponFireContext
            {
                World = _ctx,
                Form = CurrentForm,
                Origin = player.Position,
                Target = target,
                AttackRange = _config.AttackRange,
            });

            Ammo -= cost;
            FireTimer = MathF.Max(0.05f, _config.FireInterval);
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
