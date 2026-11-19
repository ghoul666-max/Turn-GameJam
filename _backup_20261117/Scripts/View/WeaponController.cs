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
        [Tooltip("每次发射消耗的弹药")]
        [Min(1)] public int ammoPerShot = 1;
        [Tooltip("装填参数：开局弹药，-1 = 满弹匣。之后唯一的装填方式是成功翻滚（立即装满），没有自动恢复、没有换弹键。")]
        public int initialAmmo = -1;
        [Tooltip("攻击间隔（秒）")]
        public float fireInterval = 0.6f;
        [Tooltip("自动索敌范围（世界单位）")]
        public float targetRange = 8f;
        [Tooltip("攻击范围：子弹最远飞行距离（世界单位）")]
        public float attackRange = 11f;

        public WeaponConfig ToConfig()
        {
            return new WeaponConfig
            {
                MagazineSize = magazineSize,
                AmmoPerShot = ammoPerShot,
                InitialAmmo = initialAmmo,
                FireInterval = fireInterval,
                TargetRange = targetRange,
                AttackRange = attackRange,
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

        public WeaponFormConfig ToConfig(MapId map)
        {
            return new WeaponFormConfig
            {
                Map = map,
                Name = displayName,
                ProjectileSpeed = projectileSpeed,
                Damage = damage,
                Radius = radius,
                Pierce = pierce,
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
        [Header("弹匣与射击")]
        [SerializeField] private WeaponSettings settings = new WeaponSettings();
        [Header("火山武器：火球")]
        [SerializeField] private WeaponFormSettings fireball = new WeaponFormSettings();
        [Header("海洋武器：鱼叉")]
        [SerializeField] private WeaponFormSettings harpoon = new WeaponFormSettings
        {
            displayName = "鱼叉", projectileSpeed = 13f, damage = 15f, radius = 0.2f, pierce = 2,
        };

        [Header("显示部件")]
        [SerializeField] private SpriteRenderer formIcon;

        [Header("运行时只读")]
        [SerializeField] private MapId currentForm;
        [SerializeField] private string currentFormName;
        [SerializeField] private int currentAmmo;
        [SerializeField] private int capacity;

        private ITurnContext _ctx;

        public WeaponSettings Settings { get { return settings; } }

        public WeaponFormSettings GetForm(MapId map)
        {
            return map == MapId.Volcano ? fireball : harpoon;
        }

        public Projectile GetPrefab(MapId map)
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
            currentForm = w.Form;
            currentFormName = w.CurrentForm.Name;
            currentAmmo = w.Ammo;
            capacity = w.Capacity;

            if (formIcon != null)
            {
                Color c = ViewMath.MapColor(currentForm);
                if (currentAmmo <= 0) c.a = 0.3f; // 打空时变暗
                formIcon.color = c;
            }
        }
    }
}
