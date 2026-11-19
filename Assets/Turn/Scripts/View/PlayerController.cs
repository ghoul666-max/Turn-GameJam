using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class PlayerSettings
    {
        [Tooltip("每秒移动的世界单位")]
        public float moveSpeed = 5f;
        [Min(1f)] public float maxHp = 100f;
        [Tooltip("碰撞半径（世界单位）")]
        public float radius = 0.35f;

        public PlayerConfig ToConfig()
        {
            return new PlayerConfig { MoveSpeed = moveSpeed, MaxHp = maxHp, Radius = radius };
        }
    }

    [Serializable]
    public sealed class RollSettings
    {
        [Tooltip("翻滚全局冷却（秒）")]
        public float cooldown = 3f;
        [Tooltip("“翻滚 CD 缩短”词条能压到的最低冷却（秒）")]
        public float minCooldown = 0.8f;
        [Tooltip("翻滚无敌时长（秒）")]
        public float invincibleTime = 0.5f;
        [Tooltip("翻滚动画 / 位移时长（秒）")]
        public float duration = 0.35f;
        [Tooltip("翻滚移动距离（世界单位）")]
        public float distance = 2.5f;

        public RollConfig ToConfig()
        {
            return new RollConfig
            {
                Cooldown = cooldown,
                MinCooldown = minCooldown,
                InvincibleTime = invincibleTime,
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
            if (s.IsRolling) return PlayerAnimState.Roll;
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
                    // 一次翻滚转一整圈，方向跟随翻滚的水平方向
                    float total = Mathf.Max(0.05f, rollSettings.duration);
                    float t = 1f - Mathf.Clamp01(s.RollTimer / total);
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
