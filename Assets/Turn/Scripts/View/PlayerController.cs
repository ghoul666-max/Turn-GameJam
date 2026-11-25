using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class PlayerSettings
    {
        [Tooltip("每秒移动的世界单位（策划 200 像素/秒，按 1 单位 = 40 像素 = 5）")]
        public float moveSpeed = 5f;
        [Min(1f)] public float maxHp = 100f;
        [Tooltip("碰撞半径（世界单位）")]
        public float radius = 0.35f;
        [Tooltip("受伤后的无敌帧（秒，本轮调整为 0.5）")]
        [Min(0f)] public float hurtInvincibleTime = 0.5f;
        [Min(0f)] public float gravityExitProtectionTime = 0.5f;

        public PlayerConfig ToConfig()
        {
            return new PlayerConfig { MoveSpeed = moveSpeed, MaxHp = maxHp, Radius = radius, HurtInvincibleTime = hurtInvincibleTime, GravityExitProtectionTime = gravityExitProtectionTime };
        }
    }

    /// <summary>翻滚 / 亚空间参数（新策划案 11/24）。默认值 = 策划书。</summary>
    [Serializable]
    public sealed class RollSettings
    {
        [Tooltip("数据版本。F1 数值面板的旧存档（亚空间之前）读出来不是 2，会换成默认值。")]
        [HideInInspector] public int version = 4;

        [Header("翻滚冷却（每把主武器独立，从回到常态空间开始计；双主武器按切换后的那把）")]
        [Min(0f)] public float waterGunCooldown = 2f;
        [Min(0f)] public float spearCooldown = 2.5f;
        [Min(0f)] public float bladeCooldown = 3f;
        [Tooltip("掌（策划书没给，暂定 2.5）")]
        [Min(0f)] public float palmCooldown = 2.5f;
        [Tooltip("弩：基础翻滚冷却显著短于其他主武器（暂定 1.2）")]
        [Min(0f)] public float crossbowCooldown = 1.2f;
        [Tooltip("扑克牌 / 手里剑 / 订书机 / 剑（策划书没给，暂定 2.5）")]
        [Min(0f)] public float pokerCooldown = 2.5f;
        [Min(0f)] public float shurikenCooldown = 2.5f;
        [Min(0f)] public float staplerCooldown = 2.5f;
        [Min(0f)] public float swordCooldown = 2.5f;

        [Header("亚空间")]
        [Tooltip("亚空间持续时长（现实秒，策划 1）。全程无敌，结束后补满弹药、清空印记")]
        [Min(0.05f)] public float subspaceDuration = 1f;
        [Tooltip("亚空间里的游戏时间倍率（策划 0.3）")]
        [UnityEngine.Range(0f, 1f)] public float subspaceTimeScale = 0.3f;

        [Header("翻滚位移")]
        [Tooltip("翻滚位移时长（游戏秒；亚空间里按慢放推进）")]
        public float duration = 0.2f;
        [Tooltip("翻滚移动距离（世界单位）")]
        public float distance = 0.8f;

        public RollConfig ToConfig()
        {
            return new RollConfig
            {
                WeaponCooldowns = new[]
                {
                    waterGunCooldown, spearCooldown, bladeCooldown, palmCooldown, crossbowCooldown,
                    pokerCooldown, shurikenCooldown, staplerCooldown, swordCooldown,
                },
                Cooldown = waterGunCooldown,
                SubspaceDuration = subspaceDuration,
                SubspaceTimeScale = subspaceTimeScale,
                Duration = duration,
                Distance = distance,
            };
        }
    }

    /// <summary>
    /// 玩家（PlayerPrefab 根节点）。Unity 侧的职责：
    ///   · 在 Inspector 暴露玩家与翻滚参数（开局由 TurnConfigReader 读入模拟层）；
    ///   · 每帧把模拟层 PlayerState 同步到 transform（位置由 PlayerSystem 计算，所以不挂 Rigidbody2D）；
    ///   · 驱动动画状态 Idle / Move / Roll / Hurt / Death：有 Animator 时写整数参数 “State”，
    ///     同时提供不依赖美术的占位表现（翻滚旋转、无敌闪烁 + 护盾圈、受伤变红、死亡压扁变灰）。
    /// 移动、生命、翻滚、无敌帧的规则分别在 PlayerSystem / CombatSystem / RollSystem 里。
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public const float HurtDisplayTime = 0.2f;
        private static readonly int StateParam = Animator.StringToHash("State");

        [Header("玩家参数")]
        [SerializeField] private PlayerSettings settings = new PlayerSettings();
        [Header("翻滚参数")]
        [SerializeField] private RollSettings rollSettings = new RollSettings();

        [Header("显示部件（生成器自动拖好）")]
        [SerializeField] private Transform body;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer shieldRing;
        [SerializeField] private Transform facingArrow;
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.95f, 1f, 1f);
        [SerializeField] private Color hurtColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("运行时只读")]
        [SerializeField] private PlayerAnimState state;
        [SerializeField] private float hp;
        [SerializeField] private float rollCooldownLeft;
        [SerializeField] private float invincibleLeft;

        private ITurnContext _ctx;
        private float _hurtTimer;
        private float _rollAngle;

        public PlayerSettings Settings { get { return settings; } }
        public RollSettings RollSettings { get { return rollSettings; } }
        public PlayerAnimState State { get { return state; } }

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnDamaged);
            _hurtTimer = 0f;
            _rollAngle = 0f;
            if (body != null)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
            }
            if (bodyRenderer != null) bodyRenderer.color = bodyColor;
            transform.position = ViewMath.ToWorld(ctx.Player.State.Position);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnDamaged(PlayerDamagedEvent evt)
        {
            _hurtTimer = HurtDisplayTime;
        }

        private void LateUpdate()
        {
            if (_ctx == null) return;
            PlayerState s = _ctx.Player.State;

            transform.position = ViewMath.ToWorld(s.Position);
            hp = s.Hp;
            rollCooldownLeft = s.RollCooldownLeft;
            invincibleLeft = s.InvincibleLeft;
            if (_hurtTimer > 0f) _hurtTimer -= Time.deltaTime;

            state = ResolveState(s);
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetInteger(StateParam, (int)state);
            ApplyPlaceholder(s);
        }

        private PlayerAnimState ResolveState(PlayerState s)
        {
            if (!s.Alive) return PlayerAnimState.Death;
            if (s.IsRolling || s.InSubspace) return PlayerAnimState.Roll;
            if (_hurtTimer > 0f) return PlayerAnimState.Hurt;
            if (s.MoveDir.SqrMagnitude > 0.01f) return PlayerAnimState.Move;
            return PlayerAnimState.Idle;
        }

        /// <summary>没有美术资源时的占位动画，只改 Body 子节点，不影响逻辑。</summary>
        private void ApplyPlaceholder(PlayerState s)
        {
            if (facingArrow != null)
            {
                float deg = Mathf.Atan2(s.Facing.Y, s.Facing.X) * Mathf.Rad2Deg;
                facingArrow.localRotation = Quaternion.Euler(0f, 0f, deg);
                facingArrow.gameObject.SetActive(s.Alive);
            }
            if (shieldRing != null)
            {
                shieldRing.enabled = s.Alive && s.Invincible;
                if (shieldRing.enabled)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.06f;
                    shieldRing.transform.localScale = new Vector3(pulse, pulse, 1f);
                }
            }
            if (body == null || bodyRenderer == null) return;

            switch (state)
            {
                case PlayerAnimState.Death:
                    body.localRotation = Quaternion.identity;
                    body.localScale = new Vector3(1.3f, 0.45f, 1f);
                    bodyRenderer.color = new Color(0.45f, 0.45f, 0.5f, 1f);
                    return;
                case PlayerAnimState.Roll:
                    // 一次翻滚转一整圈（整个亚空间期间，按现实时间），方向跟随翻滚的水平方向
                    float total = Mathf.Max(0.05f, _ctx.Config.Roll.SubspaceDuration);
                    float t = s.InSubspace ? 1f - Mathf.Clamp01(s.SubspaceLeft / total) : 1f;
                    _rollAngle = (s.RollDirection.X >= 0f ? -360f : 360f) * t;
                    body.localRotation = Quaternion.Euler(0f, 0f, _rollAngle);
                    body.localScale = new Vector3(0.85f, 0.85f, 1f);
                    break;
                case PlayerAnimState.Move:
                    body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * 6f);
                    body.localScale = Vector3.one;
                    break;
                default:
                    body.localRotation = Quaternion.identity;
                    float breathe = 1f + Mathf.Sin(Time.time * 4f) * 0.03f;
                    body.localScale = new Vector3(1f, breathe, 1f);
                    break;
            }

            Color c = state == PlayerAnimState.Hurt ? hurtColor : bodyColor;
            if (s.Invincible) c.a = Mathf.Repeat(Time.time * 16f, 1f) < 0.5f ? 0.35f : 1f; // 无敌闪烁
            bodyRenderer.color = c;
        }
    }
}
