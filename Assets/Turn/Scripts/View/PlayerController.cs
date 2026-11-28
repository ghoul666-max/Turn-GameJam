using System;
using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class PlayerSettings
    {
        [Tooltip("每秒移动的世界单位（策划 200 像素/秒，按 1 单位 = 40 像素 = 5）")]
        public float moveSpeed = 5f;
        [Tooltip("1 级生命（11/27：20；之后每级 +4，封顶 100，见 UpgradeManager 的等级属性成长）")]
        [Min(1f)] public float maxHp = 20f;
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
        [Tooltip("闪电（11/27，策划书没给，暂定 2.5）")]
        [Min(0f)] public float lightningCooldown = 2.5f;

        [Header("亚空间")]
        [Tooltip("亚空间持续时长（现实秒，策划 1）。全程无敌，结束后补满弹药、清空印记")]
        [Min(0.05f)] public float subspaceDuration = 1f;
        [Tooltip("亚空间里的游戏时间倍率（策划 0.3）")]
        [UnityEngine.Range(0f, 1f)] public float subspaceTimeScale = 0.3f;

        [Header("翻滚位移")]
        [Tooltip("翻滚位移时长（游戏秒；亚空间里按慢放推进）")]
        public float duration = 0.2f;
        [Tooltip("扑克牌翻滚动作耗时倍率；不影响无敌或亚空间时长")]
        [Min(0.01f)] public float pokerDurationMultiplier = 0.65f;
        [Tooltip("弩翻滚动作耗时倍率；不影响无敌或亚空间时长")]
        [Min(0.01f)] public float crossbowDurationMultiplier = 0.65f;
        [Tooltip("翻滚移动距离（世界单位）")]
        public float distance = 0.8f;

        public RollConfig ToConfig()
        {
            return new RollConfig
            {
                WeaponCooldowns = new[]
                {
                    waterGunCooldown, spearCooldown, bladeCooldown, palmCooldown, crossbowCooldown,
                    pokerCooldown, shurikenCooldown, staplerCooldown, swordCooldown, lightningCooldown,
                },
                Cooldown = waterGunCooldown,
                SubspaceDuration = subspaceDuration,
                SubspaceTimeScale = subspaceTimeScale,
                Duration = duration,
                PokerDurationMultiplier = pokerDurationMultiplier,
                CrossbowDurationMultiplier = crossbowDurationMultiplier,
                Distance = distance,
            };
        }
    }

    /// <summary>
    /// 主角手绘外观（F1「主角外观」，只影响画面）。序列帧在 Assets/Turn/Art/Resources/Player/&lt;片段&gt;/，按文件名排序播放：
    /// Side 侧走、Front34 3/4 正面走、Back 背走、Back34 3/4 背面走、Roll 翻滚（除背走外都朝右画，朝左靠纸片翻面 / flipX）。
    /// 每帧画布同样大小、已按身体对齐，脚底在画布底边中点（运行时按底部中心锚点建 Sprite）。没有 Side 帧时继续用白模圆形。
    /// </summary>
    [Serializable]
    public sealed class PlayerArtSettings
    {
        [Tooltip("数据版本。F1 旧存档里没有这一组时读出来是 0，会换成默认值。")]
        [HideInInspector] public int version = 1;
        [Tooltip("使用手绘主角（关 = 白模圆形）")]
        public bool useArt = true;
        [Tooltip("帧画布高度在世界里的长度（角色几乎占满画布高度；碰撞直径 0.7，怪物贴图 1.3 ~ 1.9）")]
        [Min(0.1f)] public float height = 1.5f;
        [Tooltip("侧走帧率（10 帧）")]
        [Min(0f)] public float sideFps = 14f;
        [Tooltip("3/4 正面走帧率（5 帧）")]
        [Min(0f)] public float front34Fps = 8f;
        [Tooltip("背走帧率（7 帧）")]
        [Min(0f)] public float backFps = 11f;
        [Tooltip("3/4 背面走帧率（6 帧）")]
        [Min(0f)] public float back34Fps = 9f;
        [Tooltip("移动方向（单位向量）的 y 分量 ≥ 这个值用背走（往画面里侧走），约 cos 22.5°")]
        public float backY = 0.92f;
        [Tooltip("y ≥ 这个值用 3/4 背面走，约 sin 22.5°")]
        public float back34Y = 0.38f;
        [Tooltip("y ≤ 这个值用 3/4 正面走（往镜头走；没有正面图，正下方也用 3/4 正面）")]
        public float front34Y = -0.38f;
        [Tooltip("待机呼吸幅度（竖向缩放）")]
        [Min(0f)] public float breathe = 0.015f;
        [Tooltip("左右转向（A / D）时播放纸片绕竖轴转身。关 = 直接翻面（默认）")]
        public bool paperTurnOnFlip = false;
        [Tooltip("走路动画播放倍率（乘在各片段帧率上；1 = 原样）")]
        [Min(0f)] public float walkSpeed = 1.5f;

        public PlayerArtSettings Clone()
        {
            return JsonUtility.FromJson<PlayerArtSettings>(JsonUtility.ToJson(this));
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

        [Header("手绘外观（F1「主角外观」；帧在 Art/Resources/Player/）")]
        [SerializeField] private PlayerArtSettings art = new PlayerArtSettings();

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
        private float _rollDuration;
        private UprightView _upright;

        // 手绘外观
        public const string ArtFolder = "Player/";
        private static readonly Dictionary<string, Sprite[]> ArtCache = new Dictionary<string, Sprite[]>();
        private PlayerArtSettings _appliedArt;
        private Sprite[] _side, _front34, _back, _back34, _roll;
        private bool _artLoaded;
        private bool _artActive;
        private SpriteRenderer _artRenderer;
        private int _dir;          // 0 侧走 1 3/4 正面 2 背走 3 3/4 背面
        private bool _faceLeft;
        private float _walkTime;

        /// <summary>F1 面板打开时每帧写入正在编辑的主角外观（实时预览）；关闭面板时置空。</summary>
        public static PlayerArtSettings ArtPreview { get; set; }
        /// <summary>Inspector 上这一份（F1「从 Inspector 读取」用）。</summary>
        public PlayerArtSettings Art { get { return art; } }

        /// <summary>开局时用 F1 存档里的主角外观；传 null = 用 Inspector 上的参数。</summary>
        public void ApplyArt(PlayerArtSettings s)
        {
            _appliedArt = s != null && s.version > 0 ? s.Clone() : null;
        }

        private PlayerArtSettings CurrentArt
        {
            get
            {
                if (ArtPreview != null && ArtPreview.version > 0) return ArtPreview;
                if (_appliedArt != null) return _appliedArt;
                if (art == null || art.version <= 0) art = new PlayerArtSettings();
                return art;
            }
        }

        public PlayerSettings Settings { get { return settings; } }
        public RollSettings RollSettings { get { return rollSettings; } }
        public PlayerAnimState State { get { return state; } }

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnDamaged);
            ctx.Events.Subscribe<RollPerformedEvent>(OnRolled);
            _rollDuration = 0f;
            _hurtTimer = 0f;
            _rollAngle = 0f;
            if (body != null)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
            }
            if (bodyRenderer != null) bodyRenderer.color = bodyColor;
            transform.position = ViewMath.ToWorld(ctx.Player.State.Position);
            _upright = UprightView.Ensure(gameObject, UprightRole.Player); // 2.5D：倾斜相机时身体立起来（纸片），阴影 / 箭头 / 护盾圈留在地面
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
            _ctx.Events.Unsubscribe<RollPerformedEvent>(OnRolled);
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

        private void OnRolled(RollPerformedEvent evt)
        {
            _rollDuration = _ctx != null ? Mathf.Max(0.001f, _ctx.Player.State.RollTimer) : 0.001f;
        }

        private void LateUpdate()
        {
            if (_ctx == null) return;
            PlayerState s = _ctx.Player.State;

            transform.position = ViewMath.ToWorld(s.Position);
            // 纸片翻面：按移动输入转身（没有水平输入时沿用上次朝向）；翻面关闭时不起作用。手绘外观自己处理朝向（见 ApplyArtFrame）
            if (_upright != null && !_artActive && s.MoveDir.SqrMagnitude > 0.01f) _upright.SetFacing(new Vector2(s.MoveDir.X, s.MoveDir.Y));
            hp = s.Hp;
            rollCooldownLeft = s.RollCooldownLeft;
            invincibleLeft = s.InvincibleLeft;
            if (_hurtTimer > 0f) _hurtTimer -= Time.deltaTime;

            state = ResolveState(s);
            if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
            {
                animator.SetInteger(StateParam, (int)state);
                animator.speed = state == PlayerAnimState.Roll
                    ? Mathf.Max(0.05f, _ctx.Config.Roll.Duration) / Mathf.Max(0.001f, _rollDuration) : 1f;
            }
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
            // 手绘外观：有帧且打开时隐藏白模身体
            bool artOn = CurrentArt.useArt && EnsureArt();
            if (_artRenderer != null && _artRenderer.gameObject.activeSelf != artOn) _artRenderer.gameObject.SetActive(artOn);
            if (body != null && body.gameObject.activeSelf == artOn) body.gameObject.SetActive(!artOn);
            _artActive = artOn;
            if (artOn)
            {
                ApplyArtFrame(s);
                return;
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
                    // 一次位移转一圈，动作结束不再因亚空间无敌继续翻滚。
                    float total = Mathf.Max(0.001f, _rollDuration);
                    float t = 1f - Mathf.Clamp01(s.RollTimer / total);
                    _rollAngle = (s.RollDirection.X >= 0f ? -360f : 360f) * t;
                    if (_upright != null && _upright.ShowsBack) _rollAngle = -_rollAngle; // 纸片翻到背面时局部旋转是镜像的，保持屏幕上的滚动方向
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

        // ---------------- 手绘外观 ----------------

        /// <summary>某个片段的序列帧（Resources/Player/&lt;片段&gt;/，按文件名排序，底部中心锚点、1 单位高）；没有返回 null。</summary>
        public static Sprite[] LoadArt(string clip)
        {
            Sprite[] frames;
            if (ArtCache.TryGetValue(clip, out frames) && (frames == null || frames[0] != null)) return frames;
            frames = null;
            Texture2D[] textures = Resources.LoadAll<Texture2D>(ArtFolder + clip);
            if (textures != null && textures.Length > 0)
            {
                Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
                frames = new Sprite[textures.Length];
                for (int i = 0; i < textures.Length; i++)
                {
                    Texture2D t = textures[i];
                    // 每帧画布同样大小，脚底在底边中点：锚点放底部中心；PPU = 画布高度 → Sprite 正好 1 单位高
                    frames[i] = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0f), t.height);
                    frames[i].name = t.name;
                }
            }
            ArtCache[clip] = frames;
            return frames;
        }

        /// <summary>读帧并建「Art」子物体（UprightView 会把它收进 Paper 跟着立起 / 翻面）；没有侧走帧返回 false。</summary>
        private bool EnsureArt()
        {
            if (!_artLoaded)
            {
                _artLoaded = true;
                _side = LoadArt("Side");
                _front34 = LoadArt("Front34");
                _back = LoadArt("Back");
                _back34 = LoadArt("Back34");
                _roll = LoadArt("Roll");
            }
            if (_side == null) return false;
            if (_artRenderer == null)
            {
                var go = new GameObject("Art");
                go.transform.SetParent(transform, false);
                _artRenderer = go.AddComponent<SpriteRenderer>();
                _artRenderer.sprite = _side[0];
                _artRenderer.sortingOrder = ViewMath.PlayerOrder;
            }
            return true;
        }

        private Sprite[] WalkClip(int dir)
        {
            Sprite[] clip = dir == 1 ? _front34 : dir == 2 ? _back : dir == 3 ? _back34 : _side;
            return clip ?? _side;
        }

        private static float WalkFps(PlayerArtSettings a, int dir)
        {
            return dir == 1 ? a.front34Fps : dir == 2 ? a.backFps : dir == 3 ? a.back34Fps : a.sideFps;
        }

        /// <summary>
        /// 按状态选帧：走路按移动方向分四个片段（左右靠翻面），待机停在当前方向第 1 帧并轻微呼吸，
        /// 翻滚按本次翻滚时长把 Roll 片段播完一遍，受伤偏红、无敌闪烁、死亡变灰停帧。
        /// 有 3/4 图，所以斜向不再额外多转纸片（只告诉 UprightView 朝左还是朝右）。
        /// </summary>
        private void ApplyArtFrame(PlayerState s)
        {
            PlayerArtSettings a = CurrentArt;
            float dt = Time.deltaTime;
            Vector2 move = new Vector2(s.MoveDir.X, s.MoveDir.Y);
            bool moving = move.sqrMagnitude > 0.01f && state != PlayerAnimState.Death;
            bool snap = false;

            if (state == PlayerAnimState.Roll)
            {
                // 翻滚很快：按翻滚方向立刻转到位，不播纸片转身
                float rx = s.RollDirection.X;
                if (Mathf.Abs(rx) > 0.05f)
                {
                    bool left = rx < 0f;
                    snap = left != _faceLeft;
                    _faceLeft = left;
                }
            }
            else if (moving)
            {
                if (Mathf.Abs(move.x) > 0.05f) _faceLeft = move.x < 0f;
                float ny = move.normalized.y;
                _dir = ny >= a.backY ? 2 : ny >= a.back34Y ? 3 : ny <= a.front34Y ? 1 : 0;
            }
            // 默认直接翻面：不播纸片转身，立刻转到位（看起来就是左右镜像）
            if (!a.paperTurnOnFlip) snap = true;
            if (_upright != null) _upright.SetFacing(new Vector2(_faceLeft ? -1f : 1f, 0f), snap);

            Sprite[] clip = WalkClip(_dir);
            Sprite frame;
            float sy = 1f;
            if (state == PlayerAnimState.Roll && _roll != null)
            {
                float total = Mathf.Max(0.001f, _rollDuration);
                float t = 1f - Mathf.Clamp01(s.RollTimer / total);
                frame = _roll[Mathf.Min(_roll.Length - 1, (int)(t * _roll.Length))];
            }
            else if (state == PlayerAnimState.Death)
            {
                frame = _artRenderer.sprite != null ? _artRenderer.sprite : clip[0];
            }
            else if (moving && (state == PlayerAnimState.Move || state == PlayerAnimState.Hurt))
            {
                _walkTime += dt;
                float fps = WalkFps(a, _dir) * (a.walkSpeed > 0f ? a.walkSpeed : 1f);
                frame = clip[fps > 0f ? (int)(_walkTime * fps) % clip.Length : 0];
            }
            else
            {
                _walkTime = 0f;
                frame = clip[0];
                sy = 1f + Mathf.Sin(Time.time * 4f) * a.breathe;
            }

            _artRenderer.sprite = frame;
            _artRenderer.flipX = _faceLeft && !CameraRig.PaperTurn;
            Transform t0 = _artRenderer.transform;
            t0.localPosition = Vector3.zero;
            t0.localRotation = Quaternion.identity;
            float h = Mathf.Max(0.1f, a.height);
            t0.localScale = new Vector3(h, h * sy, 1f);

            Color c = Color.white;
            if (state == PlayerAnimState.Death) c = new Color(0.5f, 0.5f, 0.55f, 1f);
            else if (state == PlayerAnimState.Hurt) c = Color.Lerp(Color.white, hurtColor, 0.7f);
            if (s.Invincible && state != PlayerAnimState.Death) c.a = Mathf.Repeat(Time.time * 16f, 1f) < 0.5f ? 0.35f : 1f; // 无敌闪烁
            _artRenderer.color = c;
        }
    }
}