using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>两种武器形态共用的弹匣与射击参数（全局一份）。</summary>
    [Serializable]
    public sealed class WeaponSettings
    {
        [Tooltip("弹匣容量（火球与鱼叉共用，“扩容弹匣”词条在此基础上增加）")]
        [Min(1)] public int magazineSize = 6;
        [Tooltip("按 Volcano/Ocean/Office/Dream/Primal/Future 顺序的各武器基础弹匣容量")]
        public int[] planeCapacities = { 8, 8, 8, 8, 1, 1 };
        [Tooltip("每次发射消耗的弹药")]
        [Min(1)] public int ammoPerShot = 1;
        [Tooltip("装填参数：开局弹药，-1 = 满弹匣。之后唯一的装填方式是成功翻滚（立即装满），没有自动恢复、没有换弹键。")]
        public int initialAmmo = -1;
        [Tooltip("攻击间隔（秒）")]
        public float fireInterval = 0.6f;
        public float[] planeFireIntervals = { .7f, .9f, .45f, .6f, 2f, 2f };
        public float[] planeTargetRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        public float[] planeAttackRanges = { 6f, 8f, 5f, 6f, 10f, 12f };
        [Tooltip("自动索敌范围（世界单位）")]
        public float targetRange = 8f;
        [Tooltip("攻击范围：子弹最远飞行距离（世界单位）")]
        public float attackRange = 11f;
        [Tooltip("子弹最长存活秒数（切位面仅首次延长剩余时间；射程不变）")]
        [Min(0.1f)] public float projectileLifetime = 3f;
        [Tooltip("首次切离发射位面时剩余寿命倍率")]
        [Min(1f)] public float planeSwitchLifetimeMultiplier = 1.6f;

        public WeaponConfig ToConfig()
        {
            return new WeaponConfig
            {
                MagazineSize = magazineSize,
                PlaneCapacities = planeCapacities != null && planeCapacities.Length == PlaneIds.Count
                    ? (int[])planeCapacities.Clone() : new[] { 8, 8, 8, 8, 1, 1 },
                AmmoPerShot = ammoPerShot,
                InitialAmmo = initialAmmo,
                FireInterval = fireInterval,
                PlaneFireIntervals = planeFireIntervals != null && planeFireIntervals.Length == PlaneIds.Count
                    ? (float[])planeFireIntervals.Clone() : new[] { .7f, .9f, .45f, .6f, 2f, 2f },
                PlaneTargetRanges = planeTargetRanges != null && planeTargetRanges.Length == PlaneIds.Count
                    ? (float[])planeTargetRanges.Clone() : new[] { 6f, 8f, 5f, 6f, 10f, 12f },
                PlaneAttackRanges = planeAttackRanges != null && planeAttackRanges.Length == PlaneIds.Count
                    ? (float[])planeAttackRanges.Clone() : new[] { 6f, 8f, 5f, 6f, 10f, 12f },
                TargetRange = targetRange,
                AttackRange = attackRange,
                // 旧的数值面板存档里没有这两个字段时读出来是 0，回退到默认值
                ProjectileLifetime = projectileLifetime > 0f ? projectileLifetime : 3f,
                PlaneSwitchLifetimeMultiplier = planeSwitchLifetimeMultiplier >= 1f ? planeSwitchLifetimeMultiplier : 1.6f,
            };
        }
    }

    /// <summary>某张地图绑定的武器形态（子弹 Prefab + 子弹数值）。</summary>
    [Serializable]
    public sealed class WeaponFormSettings
    {
        public string displayName = "火球";
        public Projectile prefab;
        [Tooltip("子弹速度（世界单位 / 秒）")]
        public float projectileSpeed = 9f;
        [Tooltip("子弹伤害")]
        public float damage = 12f;
        [Tooltip("子弹判定半径")]
        public float radius = 0.25f;
        [Tooltip("可击中的怪物数")]
        [Min(1)] public int pierce = 1;
        [Min(0f)] public float splashRadius = 1.2f;
        [Min(0f)] public float burnDps = 6f, burnDuration = 2f;
        [Min(0f)] public float poisonDps = 5f, poisonDuration = 3f;
        [Min(1)] public int poisonMaxStacks = 3;
        [Range(0f, 1f)] public float harpoonFalloff = .30f;

        public WeaponFormConfig ToConfig(PlaneId map)
        {
            return new WeaponFormConfig
            {
                Map = map,
                Name = displayName,
                ProjectileSpeed = projectileSpeed,
                Damage = damage,
                Radius = radius,
                Pierce = pierce,
                SplashRadius = splashRadius, BurnDps = burnDps, BurnDuration = burnDuration,
                PoisonDps = poisonDps, PoisonDuration = poisonDuration,
                PoisonMaxStacks = poisonMaxStacks, HarpoonFalloff = harpoonFalloff,
            };
        }
    }

    /// <summary>
    /// 武器（Player/Weapon 子节点）。Unity 侧的职责：
    ///   · 暴露弹匣 / 攻击间隔 / 范围 / 两种形态的子弹参数与 Prefab；
    ///   · 运行时把当前弹药、容量、形态显示在 Inspector（只读），并按形态切换手上的小图标；
    /// 自动索敌、弹匣消耗、打空停火、翻滚装填、形态随地图切换的规则全部在 WeaponSystem 里。
    /// 扩展：新增地图时在这里再加一个 WeaponFormSettings，并在 GetForm 里返回它。
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("新策划案（11/24）· 三把武器：水枪 / 投矛 / 陌刀（1 单位 = 40 像素）")]
        [Tooltip("新系统只读这一组。下面的旧武器参数只保留子弹 Prefab：水弹借用火球 Prefab、长矛借用鱼叉 Prefab")]
        [SerializeField] private ArsenalConfig arsenal = new ArsenalConfig();

        [Header("旧方案（保留子弹 Prefab 引用）· 弹匣与射击")]
        [SerializeField] private WeaponSettings settings = new WeaponSettings();
        [Header("火山武器：火球")]
        [SerializeField] private WeaponFormSettings fireball = new WeaponFormSettings();
        [Header("海洋武器：鱼叉")]
        [SerializeField] private WeaponFormSettings harpoon = new WeaponFormSettings
        {
            displayName = "鱼叉", projectileSpeed = 12f, damage = 24f, radius = 0.2f, pierce = 2,
        };
        [Header("工位武器：键盘")]
        [SerializeField] private WeaponFormSettings keyboard = new WeaponFormSettings
        {
            displayName = "键盘", projectileSpeed = 11f, damage = 8f, pierce = 1,
        };
        [Header("梦境武器：画笔")]
        [SerializeField] private WeaponFormSettings paintbrush = new WeaponFormSettings
        {
            displayName = "画笔", projectileSpeed = 8f, damage = 12f, pierce = 1, poisonDps = 5f, poisonDuration = 3f,
        };
        [Header("原始武器：弩")]
        [SerializeField] private WeaponFormSettings crossbow = new WeaponFormSettings
        {
            displayName = "弩", projectileSpeed = 18f, damage = 45f, pierce = 3,
        };
        [Header("未来武器：激光")]
        [SerializeField] private WeaponFormSettings laser = new WeaponFormSettings
        {
            displayName = "激光枪", projectileSpeed = 12f, damage = 38f, pierce = 1, splashRadius = 1.2f,
        };

        [Header("显示部件")]
        [SerializeField] private SpriteRenderer formIcon;

        [Header("运行时只读")]
        [SerializeField] private WeaponKind currentWeapon;
        [SerializeField] private string currentFormName;
        [SerializeField] private int currentAmmo;
        [SerializeField] private int capacity;

        private ITurnContext _ctx;

        public WeaponSettings Settings { get { return settings; } }
        public ArsenalConfig Arsenal { get { return arsenal; } }

        /// <summary>新策划案：某把武器子弹的 Prefab（水弹 = 火球 Prefab，长矛 = 鱼叉 Prefab；陌刀没有子弹）。</summary>
        public Projectile GetPrefab(WeaponKind weapon)
        {
            if (weapon == WeaponKind.WaterGun) return fireball != null ? fireball.prefab : null;
            if (weapon == WeaponKind.Spear) return harpoon != null ? harpoon.prefab : null;
            return null;
        }

        /// <summary>该位面绑定的武器形态；还没做的位面返回 null（程序B 补齐其余 4 把）。</summary>
        public WeaponFormSettings GetForm(PlaneId map)
        {
            if (map == PlaneId.Volcano) return fireball;
            if (map == PlaneId.Ocean) return harpoon;
            if (map == PlaneId.Office) return keyboard;
            if (map == PlaneId.Dream) return paintbrush;
            if (map == PlaneId.Primal) return crossbow;
            if (map == PlaneId.Future) return laser;
            return null;
        }

        public Projectile GetPrefab(PlaneId map)
        {
            WeaponFormSettings form = GetForm(map);
            return form != null ? form.prefab : null;
        }

        public void Bind(ITurnContext ctx)
        {
            _ctx = ctx;
        }

        private void LateUpdate()
        {
            if (_ctx == null) return;
            IWeaponSystem w = _ctx.Weapons;
            currentWeapon = w.Current;
            currentFormName = WeaponKinds.DisplayName(currentWeapon);
            currentAmmo = w.Ammo;
            capacity = w.Capacity;

            if (formIcon != null)
            {
                Color c = ViewMath.WeaponColor(currentWeapon);
                if (currentAmmo <= 0) c.a = 0.3f; // 打空时变暗
                formIcon.color = c;
            }
        }
    }
}
