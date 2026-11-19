using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class MonsterSettings
    {
        [Min(1f)] public float maxHp = 30f;
        public float moveSpeed = 2f;
        [Tooltip("接触伤害（每次）")]
        public float contactDamage = 8f;
        [Tooltip("接触伤害间隔（秒）")]
        public float contactInterval = 0.6f;
        [Tooltip("碰撞半径（世界单位）")]
        public float radius = 0.4f;
        [Tooltip("怪物踩岩浆每秒受到的伤害")]
        public float lavaDps = 10f;
        [Tooltip("死亡掉落的经验值")]
        [Min(0)] public int xp = 1;

        public MonsterConfig ToConfig()
        {
            return new MonsterConfig
            {
                MaxHp = maxHp,
                Speed = moveSpeed,
                ContactDamage = contactDamage,
                ContactInterval = contactInterval,
                Radius = radius,
                LavaDps = lavaDps,
                Xp = xp,
            };
        }
    }

    /// <summary>
    /// 普通怪物（MonsterPrefab 根节点）。Unity 侧的职责：
    ///   · 在 Inspector 暴露怪物参数（开局时 GameManager 从 Prefab 上读一次）；
    ///   · 按实体 Id 同步位置、朝向、血条、灼烧标记，受击闪白；
    ///   · 皮肤切换：只切换 VolcanoSkin / OceanSkin 两个子节点的显隐，不创建新实例；
    ///   · 死亡：模拟层已移除该实体，这里播放 0.2 秒淡出后 Destroy 自身。
    /// 追击、受伤、岩浆 DOT、灼烧、边界都在 MonsterSystem / CombatSystem 里。
    /// </summary>
    public sealed class MonsterController : MonoBehaviour
    {
        public const float FlashTime = 0.08f;
        public const float DeathTime = 0.2f;

        [Header("怪物参数（读的是 Prefab 上这一份）")]
        [SerializeField] private MonsterSettings settings = new MonsterSettings();

        [Header("两套外观（生成器自动拖好）")]
        [SerializeField] private GameObject volcanoSkin;
        [SerializeField] private GameObject oceanSkin;
        [SerializeField] private SpriteRenderer volcanoRenderer;
        [SerializeField] private SpriteRenderer oceanRenderer;
        [SerializeField] private Transform hpFill;
        [SerializeField] private GameObject hpBar;
        [SerializeField] private SpriteRenderer burnMark;

        [Header("运行时只读")]
        [SerializeField] private int entityId;
        [SerializeField] private float hp;
        [SerializeField] private bool burning;
        [SerializeField] private bool penaltySpawned;
        [SerializeField] private MapId skin;

        private Color _volcanoColor = Color.white;
        private Color _oceanColor = Color.white;
        private float _flashTimer;
        private float _deathTimer = -1f;

        public MonsterSettings Settings { get { return settings; } }
        public int EntityId { get { return entityId; } }

        public void Init(int id, MapId map)
        {
            entityId = id;
            if (volcanoRenderer != null) _volcanoColor = volcanoRenderer.color;
            if (oceanRenderer != null) _oceanColor = oceanRenderer.color;
            SetSkin(map);
        }

        /// <summary>只改外观：火山显示 VolcanoSkin，海洋显示 OceanSkin。</summary>
        public void SetSkin(MapId map)
        {
            skin = map;
            if (volcanoSkin != null) volcanoSkin.SetActive(map == MapId.Volcano);
            if (oceanSkin != null) oceanSkin.SetActive(map == MapId.Ocean);
        }

        public void Sync(MonsterEntity e)
        {
            transform.position = ViewMath.ToWorld(e.Position);
            hp = e.Hp;
            burning = e.Burning;
            penaltySpawned = e.Penalty;

            bool flip = e.Velocity.X < -0.01f;
            if (volcanoRenderer != null) volcanoRenderer.flipX = flip;
            if (oceanRenderer != null) oceanRenderer.flipX = flip;

            float ratio = e.MaxHp > 0f ? Mathf.Clamp01(e.Hp / e.MaxHp) : 0f;
            if (hpBar != null) hpBar.SetActive(ratio < 0.999f);
            if (hpFill != null)
            {
                hpFill.localScale = new Vector3(ratio, 1f, 1f);
                hpFill.localPosition = new Vector3((ratio - 1f) * 0.5f, 0f, 0f);
            }
            if (burnMark != null) burnMark.enabled = burning;
        }

        public void Flash()
        {
            _flashTimer = FlashTime;
        }

        public void BeginDeath()
        {
            if (_deathTimer >= 0f) return;
            _deathTimer = 0f;
            if (hpBar != null) hpBar.SetActive(false);
            if (burnMark != null) burnMark.enabled = false;
        }

        private void Update()
        {
            if (_deathTimer >= 0f)
            {
                _deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_deathTimer / DeathTime);
                transform.localScale = Vector3.one * (1f + t * 0.5f);
                SetTint(new Color(1f, 1f, 1f, 1f - t), new Color(1f, 1f, 1f, 1f - t));
                if (_deathTimer >= DeathTime) Destroy(gameObject);
                return;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                SetTint(Color.white, Color.white);
                if (_flashTimer <= 0f) SetTint(_volcanoColor, _oceanColor);
            }
        }

        private void SetTint(Color volcano, Color ocean)
        {
            if (volcanoRenderer != null) volcanoRenderer.color = volcano;
            if (oceanRenderer != null) oceanRenderer.color = ocean;
        }
    }
}
