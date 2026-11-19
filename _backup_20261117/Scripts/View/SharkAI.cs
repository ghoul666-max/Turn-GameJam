using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class SharkSettings
    {
        [Tooltip("移动速度（略快于玩家的 5）")]
        public float moveSpeed = 6f;
        [Tooltip("接触伤害：对玩家与普通怪物相同")]
        public float contactDamage = 15f;
        [Tooltip("攻击间隔（秒）")]
        public float attackInterval = 0.8f;
        [Tooltip("碰撞半径（世界单位）")]
        public float radius = 0.55f;
        [Tooltip("每次海洋地图激活时生成的数量")]
        [Min(0)] public int spawnCount = 2;
        [Tooltip("生成位置：优先使用 OceanRoot/OceanSpawnPoints 下离玩家不小于此距离的点")]
        public float minSpawnDistance = 7f;

        public SharkConfig ToConfig()
        {
            return new SharkConfig
            {
                Speed = moveSpeed,
                ContactDamage = contactDamage,
                AttackInterval = attackInterval,
                Radius = radius,
                SpawnCount = spawnCount,
                MinSpawnDistance = minSpawnDistance,
            };
        }
    }

    /// <summary>
    /// 鲨鱼（SharkPrefab 根节点）。Unity 侧的职责：暴露鲨鱼参数；按实体同步位置、朝向，咬人时抖一下。
    /// 追击、接触伤害（不分敌我）、生成 / 销毁（随海洋地图激活状态）都在 SharkSystem 里。
    /// 鲨鱼没有血条、不能被击杀；切到火山时模拟层丢弃实体，EntityViewSync 直接 Destroy 这个 GameObject。
    /// </summary>
    public sealed class SharkAI : MonoBehaviour
    {
        [Header("鲨鱼参数（读的是 Prefab 上这一份）")]
        [SerializeField] private SharkSettings settings = new SharkSettings();

        [Header("显示部件")]
        [SerializeField] private Transform body;

        [Header("运行时只读")]
        [SerializeField] private int entityId;
        [SerializeField] private float attackCooldown;

        private float _lastAttackTimer;
        private float _biteTimer;

        public SharkSettings Settings { get { return settings; } }

        public void Init(int id)
        {
            entityId = id;
        }

        public void Sync(SharkEntity e)
        {
            transform.position = ViewMath.ToWorld(e.Position);
            attackCooldown = e.AttackTimer;
            if (body != null && e.Velocity.SqrMagnitude > 0.01f)
            {
                float deg = Mathf.Atan2(e.Velocity.Y, e.Velocity.X) * Mathf.Rad2Deg;
                body.rotation = Quaternion.Euler(0f, 0f, deg);
            }

            // 攻击计时被重置（变大）= 刚咬了一口
            if (e.AttackTimer > _lastAttackTimer + 0.01f) _biteTimer = 0.15f;
            _lastAttackTimer = e.AttackTimer;
        }

        private void Update()
        {
            if (body == null) return;
            if (_biteTimer > 0f)
            {
                _biteTimer -= Time.deltaTime;
                body.localScale = new Vector3(1.25f, 0.85f, 1f);
            }
            else
            {
                body.localScale = Vector3.one;
            }
        }
    }
}
