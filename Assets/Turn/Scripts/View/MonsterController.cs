using System;
using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>单种怪物的手绘外观参数。</summary>
    [Serializable]
    public sealed class MonsterArtEntry
    {
        [Tooltip("贴图画布最长边在世界里的长度（普通体型；精英 / Boss 跟着体型一起放大）。碰撞直径是 0.8")]
        [Min(0.1f)] public float size = 1.4f;
        [Tooltip("贴图离地高度：阴影留在真实碰撞位置，贴图浮在它上面")]
        public float hover = 0.2f;
        [Tooltip("序列帧播放速度（帧 / 秒）")]
        [Min(0f)] public float fps = 10f;
        [Tooltip("上下浮动幅度（世界单位）")]
        [Min(0f)] public float bob = 0.05f;
        [Tooltip("左右摇摆角度（度）")]
        [Min(0f)] public float tilt = 5f;
        [Tooltip("浮动 / 摇摆的速度（弧度 / 秒）")]
        [Min(0f)] public float swaySpeed = 4f;

        public MonsterArtEntry() { }

        public MonsterArtEntry(float size, float hover, float fps, float bob, float tilt, float swaySpeed)
        {
            this.size = size; this.hover = hover; this.fps = fps; this.bob = bob; this.tilt = tilt; this.swaySpeed = swaySpeed;
        }
    }

    /// <summary>
    /// 手绘怪物外观。序列帧放在 Resources/Monsters/&lt;种类&gt;/，按文件名排序播放；某种怪没有贴图时继续用白模占位。
    /// 阴影：Resources/Monsters/monster_shadow，圆心 = 模拟层位置，直径 = 碰撞直径。
    /// </summary>
    [Serializable]
    public sealed class MonsterArtSettings
    {
        public MonsterArtEntry walker = new MonsterArtEntry(1.3f, 0.15f, 8f, 0.06f, 6f, 5f);
        public MonsterArtEntry spore = new MonsterArtEntry(1.8f, 0.3f, 10f, 0.08f, 3f, 3f);
        public MonsterArtEntry charger = new MonsterArtEntry(1.9f, 0.28f, 12f, 0.05f, 3f, 4f);
        [Tooltip("圆形阴影的不透明度")]
        [Range(0f, 1f)] public float shadowAlpha = 0.5f;
        [Tooltip("受击白闪的不透明度")]
        [Range(0f, 1f)] public float flashAlpha = 0.85f;

        public MonsterArtEntry For(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Walker: return walker ?? new MonsterArtEntry();
                case MonsterKind.Spore: return spore ?? new MonsterArtEntry();
                case MonsterKind.Charger: return charger ?? new MonsterArtEntry();
                default: return null;
            }
        }
    }

    [Serializable]
    public sealed class MonsterSettings
    {
        [Tooltip("火山 / 海洋之外、还没单独配置的位面的怪物最大生命")]
        [Min(1f)] public float maxHp = 30f;
        [Tooltip("火山形态（石头人）的最大生命。切位面时血量按比例换算。")]
        [Min(1f)] public float volcanoMaxHp = 60f;
        [Tooltip("海洋形态（安康鱼）的最大生命。切位面时血量按比例换算。")]
        [Min(1f)] public float oceanMaxHp = 45f;
        public float moveSpeed = 2f;
        [Tooltip("接触伤害（每次）")]
        public float contactDamage = 8f;
        [Tooltip("接触伤害间隔（秒）")]
        public float contactInterval = 0.6f;
        [Tooltip("碰撞半径（世界单位）")]
        public float radius = 0.4f;
        [Tooltip("切到火山地图时，站在岩浆范围内受到的一次性伤害（之后岩浆消失）")]
        public float lavaHitDamage = 20f;
        [Tooltip("死亡掉落的经验值")]
        [Min(0)] public int xp = 1;
        [Tooltip("按 MonsterKind 顺序配置全部怪物原型，长度应为 13；缺项使用策划默认值")]
        public MonsterArchetype[] archetypes = Turn.Monsters.MonsterCatalog.CreateDefaults();
        [Header("怪物攻击/特殊行为")]
        public float splitInterval = 3f;
        public int bugletLimitPerParent = 6;
        public float bubbleSpeed = 3f;
        public float enemyProjectileLifetime = 3f;
        public float enemyProjectileRadius = .22f;
        public float stoneRecovery = .5f;
        public float chargeSpeed = 5.5f;
        public float chargeDuration = .8f;
        public float robotChargeSpeed = 4.8f;
        public float robotKnockbackDistance = 1.5f;
        public float boarChargeSpeed = 5f;
        public float boarStunDuration = 2f;
        public float dataWormExplosionRadius = 1f;
        public float alarmAuraRadius = 2f;
        public float laserDuration = 1f;
        public float laserWarning = .4f;
        public float laserWidth = .18f;
        public int codeBurstShots = 3;
        public float codeShotInterval = .18f;
        public float rockWarning = .3f;
        public float rockImpactRadius = .7f;

        public MonsterConfig ToConfig()
        {
            return new MonsterConfig
            {
                MaxHp = maxHp,
                // 旧的数值面板存档里没有这两个字段时读出来是 0，回退到默认值
                VolcanoMaxHp = volcanoMaxHp > 0f ? volcanoMaxHp : 60f,
                OceanMaxHp = oceanMaxHp > 0f ? oceanMaxHp : 45f,
                Speed = moveSpeed,
                ContactDamage = contactDamage,
                ContactInterval = contactInterval,
                Radius = radius,
                LavaHitDamage = lavaHitDamage,
                Xp = xp,
                Archetypes = NormalizeArchetypes(archetypes),
                SplitInterval = splitInterval,
                BugletLimitPerParent = bugletLimitPerParent,
                BubbleSpeed = bubbleSpeed,
                EnemyProjectileLifetime = enemyProjectileLifetime,
                EnemyProjectileRadius = enemyProjectileRadius,
                StoneRecovery = stoneRecovery,
                ChargeSpeed = chargeSpeed,
                ChargeDuration = chargeDuration,
                RobotChargeSpeed = robotChargeSpeed,
                RobotKnockbackDistance = robotKnockbackDistance,
                BoarChargeSpeed = boarChargeSpeed,
                BoarStunDuration = boarStunDuration,
                DataWormExplosionRadius = dataWormExplosionRadius,
                AlarmAuraRadius = alarmAuraRadius,
                LaserDuration = laserDuration,
                LaserWarning = laserWarning,
                LaserWidth = laserWidth,
                CodeBurstShots = codeBurstShots,
                CodeShotInterval = codeShotInterval,
                RockWarning = rockWarning,
                RockImpactRadius = rockImpactRadius,
            };
        }
        private static MonsterArchetype[] NormalizeArchetypes(MonsterArchetype[] configured)
        {
            MonsterArchetype[] defaults = Turn.Monsters.MonsterCatalog.CreateDefaults();
            if (configured == null) return defaults;
            var result = new MonsterArchetype[defaults.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = i < configured.Length && configured[i] != null && configured[i].MaxHp > 0f
                    ? configured[i] : defaults[i];
            return result;
        }
    }

    /// <summary>
    /// 普通怪物（MonsterPrefab 根节点）。Unity 侧的职责：
    ///   · 在 Inspector 暴露怪物参数（开局时 GameManager 从 Prefab 上读一次）；
    ///   · 按实体 Id 同步位置、朝向、血条、灼烧标记，受击闪白；
    ///   · 皮肤切换：只切换 VolcanoSkin / OceanSkin 两个子节点的显隐，不创建新实例；
    ///   · 死亡：模拟层已移除该实体，这里播放 0.2 秒淡出后 Destroy 自身。
    /// 追击、受伤、灼烧、边界在 MonsterSystem / CombatSystem 里；岩浆一次性伤害在 EnvironmentSystem 里。
    /// </summary>
    public sealed class MonsterController : MonoBehaviour
    {
        public const float FlashTime = 0.08f;
        public const float DeathTime = 0.2f;

        [Header("怪物参数（读的是 Prefab 上这一份）")]
        [SerializeField] private MonsterSettings settings = new MonsterSettings();

        [Header("手绘外观（游荡尸 / 弹幕孢 / 疾冲兽；其余种类还是白模）")]
        [SerializeField] private MonsterArtSettings art = new MonsterArtSettings();

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
        [SerializeField] private PlaneId skin;
        [SerializeField] private MonsterKind kind;
        [SerializeField] private int attackPhase;
        [SerializeField] private float slowLeft;
        [SerializeField] private float laserWarningLeft;

        private Color _volcanoColor = Color.white;
        private Color _oceanColor = Color.white;
        private float _flashTimer;
        private float _deathTimer = -1f;
        private bool _lookApplied;
        private float _baseScale = 1f;
        private bool _squashed;
        private TextMesh _markLabel;
        private string _markText = "";
        private Color _stateTint = Color.white;

        // 手绘外观（有贴图的种类才会创建）
        private static readonly Color EliteTint = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color BossTint = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Dictionary<MonsterKind, Sprite[]> FrameCache = new Dictionary<MonsterKind, Sprite[]>();
        private static Sprite _shadowSprite;
        private static bool _shadowTried;
        private static Material _flashMaterial;
        private static bool _flashTried;

        private Transform _artRoot;
        private SpriteRenderer _artRenderer;
        private SpriteRenderer _flashRenderer;
        private SpriteRenderer _footShadow;
        private Sprite[] _frames;
        private MonsterArtEntry _artEntry;
        private MonsterKind _artKind;
        private float _artScale = 1f;
        private float _framePos;
        private int _frameIndex = -1;
        private float _swayTime;
        private bool _faceLeft;
        private bool _frozen;
        private float _punch;

        public MonsterSettings Settings { get { return settings; } }
        public int EntityId { get { return entityId; } }

        public void Init(int id, PlaneId map)
        {
            entityId = id;
            if (volcanoRenderer != null) _volcanoColor = volcanoRenderer.color;
            if (oceanRenderer != null) _oceanColor = oceanRenderer.color;
            SetSkin(map);
            UprightView.Ensure(gameObject); // 2.5D：倾斜相机时贴图 / 血条 / 印记立起来面向镜头（脚下阴影留在地面）
        }

        /// <summary>只改外观：火山显示 VolcanoSkin，海洋显示 OceanSkin。</summary>
        public void SetSkin(PlaneId map)
        {
            skin = map;
            // 其他位面还没有专属皮肤：组内第一面借用火山皮、第二面借用海洋皮，避免怪物隐形（程序A 补，白模占位）
            bool firstOfPair = ((int)map & 1) == 0;
            bool hand = _artRoot != null; // 有手绘贴图时两套白模皮肤都不显示
            if (volcanoSkin != null) volcanoSkin.SetActive(firstOfPair && !hand);
            if (oceanSkin != null) oceanSkin.SetActive(!firstOfPair && !hand);
        }

        public void Sync(MonsterEntity e)
        {
            transform.position = ViewMath.ToWorld(e.Position);
            if (_artRoot != null)
            {
                // 同一排序层里靠 Z 决定前后：越靠下（Y 越小）越靠前，避免重叠的怪物互相闪烁
                Vector3 p = transform.position;
                transform.position = new Vector3(p.x, p.y, p.y * 0.001f);
                float fx = e.Velocity.X;
                if (e.Kind == MonsterKind.Charger && e.AiPhase >= 1) fx = e.AttackDirection.X; // 蓄力 / 冲锋时朝冲锋方向
                if (fx < -0.05f) _faceLeft = true;
                else if (fx > 0.05f) _faceLeft = false;
                _artRenderer.flipX = _faceLeft;
                if (_flashRenderer != null) _flashRenderer.flipX = _faceLeft;
                _frozen = e.StunLeft > 0f || e.PinnedLeft > 0f;
            }
            hp = e.Hp;
            burning = e.Burning;
            penaltySpawned = e.Penalty;
            kind = e.Kind;
            attackPhase = e.AiPhase;
            slowLeft = e.SlowLeft;
            laserWarningLeft = e.Kind == MonsterKind.FutureSoldier && e.AiPhase == 1 ? e.AiTimer : 0f;

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
            ApplyLook(e);
        }

        /// <summary>
        /// 新策划案的占位外观（白模）：按种类染色、按体型缩放（精英 / Boss 更大），
        /// 蓄力时发红、眩晕时发灰，头顶显示印记层数（矛 / 剑）。
        /// </summary>
        private void ApplyLook(MonsterEntity e)
        {
            if (MonsterKinds.HordeIndex(e.Kind) < 0) return;
            if (!_lookApplied)
            {
                _lookApplied = true;
                Color c;
                if (SetupArt(e))
                {
                    // 手绘：保留原画颜色，精英偏金、Boss 偏红
                    c = e.Rank == 1 ? Color.Lerp(Color.white, EliteTint, 0.45f)
                        : e.Rank >= 2 ? Color.Lerp(Color.white, BossTint, 0.5f) : Color.white;
                }
                else
                {
                    c = ViewMath.MonsterColor(e.Kind);
                    if (e.Rank == 1) c = Color.Lerp(c, new Color(1f, 0.85f, 0.2f, 1f), 0.45f);
                    else if (e.Rank >= 2) c = Color.Lerp(c, new Color(0.75f, 0.1f, 0.15f, 1f), 0.55f);
                }
                _volcanoColor = c;
                _oceanColor = c;
                _baseScale = Mathf.Max(0.25f, e.Radius / 0.4f);
                transform.localScale = Vector3.one * _baseScale;
                _stateTint = Color.white;
                SetTint(_volcanoColor, _oceanColor);
            }

            bool windup = e.AiPhase == 1 && (e.Kind == MonsterKind.Charger || e.Kind == MonsterKind.Sniper);
            Color tint = e.StunLeft > 0f ? new Color(0.55f, 0.55f, 0.65f, 1f)
                : windup ? (Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? new Color(1f, 0.25f, 0.2f, 1f) : Color.white)
                : Color.white;
            if (_flashTimer <= 0f && _deathTimer < 0f && tint != _stateTint)
            {
                _stateTint = tint;
                SetTint(_volcanoColor * tint, _oceanColor * tint);
            }

            // 光标「降维打击」定身：压扁贴地
            bool squash = e.PinnedLeft > 0f;
            if (squash != _squashed)
            {
                _squashed = squash;
                transform.localScale = squash ? new Vector3(_baseScale * 1.35f, _baseScale * 0.3f, 1f) : Vector3.one * _baseScale;
            }

            string text = "";
            if (e.SpearMarks > 0) text += "矛" + e.SpearMarks;
            if (e.BladeMarks > 0) text += (text.Length > 0 ? " " : "") + "剑" + e.BladeMarks;
            if (e.PalmMarks > 0) text += (text.Length > 0 ? " " : "") + "掌" + e.PalmMarks;
            if (e.StapleMarks > 0) text += (text.Length > 0 ? " " : "") + (e.StapleGroup != 0 ? "钉▣" : "钉") + e.StapleMarks;
            if (e.LightningMarks > 0) text += (text.Length > 0 ? " " : "") + "雷" + e.LightningMarks;
            if (text != _markText)
            {
                _markText = text;
                EnsureMarkLabel();
                if (_markLabel != null)
                {
                    _markLabel.text = text;
                    _markLabel.color = e.SpearMarks > 0 ? ViewMath.WeaponColor(WeaponKind.Spear)
                        : e.PalmMarks > 0 ? ViewMath.WeaponColor(WeaponKind.Palm)
                        : e.StapleMarks > 0 ? ViewMath.WeaponColor(WeaponKind.Stapler) : ViewMath.WeaponColor(WeaponKind.Blade);
                }
            }
        }

        private void EnsureMarkLabel()
        {
            if (_markLabel != null) return;
            Font font = UIFont.Chinese;
            if (font == null) return;
            var go = new GameObject("Marks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _artRoot != null ? ArtTop() + 0.3f : 0.75f, 0f);
            _markLabel = go.AddComponent<TextMesh>();
            _markLabel.font = font;
            _markLabel.fontSize = 48;
            _markLabel.characterSize = 0.05f;
            _markLabel.anchor = TextAnchor.MiddleCenter;
            _markLabel.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = ViewMath.HpBarOrder + 1;
        }

        public void Flash()
        {
            _flashTimer = FlashTime;
            _punch = 1f;
        }

        public void BeginDeath()
        {
            if (_deathTimer >= 0f) return;
            _deathTimer = 0f;
            if (hpBar != null) hpBar.SetActive(false);
            if (burnMark != null) burnMark.enabled = false;
            if (_markLabel != null) _markLabel.text = "";
        }

        private void Update()
        {
            if (_deathTimer >= 0f)
            {
                _deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_deathTimer / DeathTime);
                transform.localScale = Vector3.one * (_baseScale * (1f + t * 0.5f));
                Color fade = _artRoot != null ? new Color(_volcanoColor.r, _volcanoColor.g, _volcanoColor.b, 1f - t) : new Color(1f, 1f, 1f, 1f - t);
                SetTint(fade, fade);
                if (_flashRenderer != null) _flashRenderer.enabled = false;
                if (_deathTimer >= DeathTime) Destroy(gameObject);
                return;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_artRoot != null)
                {
                    // 手绘：叠一层白色剪影闪一下（白色乘原画颜色看不出来）
                    if (_flashRenderer != null)
                    {
                        _flashRenderer.enabled = _flashTimer > 0f;
                        _flashRenderer.color = new Color(1f, 1f, 1f, art.flashAlpha * Mathf.Clamp01(_flashTimer / FlashTime + 0.3f));
                    }
                    else SetTint(_flashTimer > 0f ? new Color(1f, 0.55f, 0.5f, 1f) : _volcanoColor * _stateTint, _oceanColor * _stateTint);
                }
                else
                {
                    SetTint(Color.white, Color.white);
                    if (_flashTimer <= 0f) SetTint(_volcanoColor * _stateTint, _oceanColor * _stateTint);
                }
            }

            AnimateArt(Time.deltaTime);
        }

        private void SetTint(Color volcano, Color ocean)
        {
            if (volcanoRenderer != null) volcanoRenderer.color = volcano;
            if (oceanRenderer != null) oceanRenderer.color = ocean;
            if (_artRenderer != null) _artRenderer.color = volcano;
            if (_footShadow != null) _footShadow.color = new Color(1f, 1f, 1f, art.shadowAlpha * volcano.a);
        }

        // ---------------- 手绘外观 ----------------

        /// <summary>该种类的序列帧（Resources/Monsters/&lt;种类&gt;/，按文件名排序）；没有贴图返回 null。</summary>
        public static Sprite[] LoadFrames(MonsterKind kind)
        {
            Sprite[] frames;
            if (FrameCache.TryGetValue(kind, out frames) && (frames == null || frames[0] != null)) return frames;
            frames = null;
            string folder = ArtFolder(kind);
            if (folder != null)
            {
                frames = Resources.LoadAll<Sprite>("Monsters/" + folder);
                if (frames == null || frames.Length == 0)
                {
                    // 贴图没按 Sprite 导入时兜底：直接用纹理现做 Sprite
                    Texture2D[] textures = Resources.LoadAll<Texture2D>("Monsters/" + folder);
                    frames = new Sprite[textures != null ? textures.Length : 0];
                    for (int i = 0; i < frames.Length; i++)
                    {
                        Texture2D tex = textures[i];
                        frames[i] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 256f);
                        frames[i].name = tex.name;
                    }
                }
                if (frames.Length > 0) Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                else frames = null;
            }
            FrameCache[kind] = frames;
            return frames;
        }

        private static string ArtFolder(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Walker: return "Walker";
                case MonsterKind.Spore: return "Spore";
                case MonsterKind.Charger: return "Charger";
                default: return null;
            }
        }

        private static Sprite ShadowSprite()
        {
            if (_shadowSprite != null || _shadowTried) return _shadowSprite;
            _shadowTried = true;
            _shadowSprite = Resources.Load<Sprite>("Monsters/monster_shadow");
            if (_shadowSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Monsters/monster_shadow");
                if (tex != null) _shadowSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
            }
            return _shadowSprite;
        }

        /// <summary>白色剪影材质（GUI/Text Shader 只取贴图 alpha，颜色来自顶点色）。找不到就退回染色闪烁。</summary>
        private static Material FlashMaterial()
        {
            if (_flashMaterial != null || _flashTried) return _flashMaterial;
            _flashTried = true;
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader != null) _flashMaterial = new Material(shader) { name = "MonsterFlash" };
            return _flashMaterial;
        }

        /// <summary>有手绘贴图就建「圆形阴影 + 贴图 + 受击剪影」三个子物体，隐藏白模皮肤；没有贴图返回 false。</summary>
        private bool SetupArt(MonsterEntity e)
        {
            MonsterArtEntry entry = art != null ? art.For(e.Kind) : null;
            Sprite[] frames = entry != null ? LoadFrames(e.Kind) : null;
            if (frames == null) return false;

            _frames = frames;
            _artEntry = entry;
            _artKind = e.Kind;
            _framePos = UnityEngine.Random.value * frames.Length; // 每只怪错开，不要整齐划一
            _swayTime = UnityEngine.Random.value * 10f;

            // 圆形阴影：圆心 = 真实位置，直径 = 碰撞直径（根节点已按 Radius / 0.4 缩放，这里按基础半径 0.4 算）
            Sprite shadowSprite = ShadowSprite();
            if (shadowSprite != null)
            {
                var sg = new GameObject("FootShadow");
                sg.transform.SetParent(transform, false);
                _footShadow = sg.AddComponent<SpriteRenderer>();
                _footShadow.sprite = shadowSprite;
                _footShadow.sortingOrder = ViewMath.ShadowOrder;
                float d = Mathf.Max(0.01f, shadowSprite.bounds.size.x);
                sg.transform.localScale = Vector3.one * (0.8f / d);
            }
            Transform oldShadow = transform.Find("Shadow");
            if (oldShadow != null && shadowSprite != null) oldShadow.gameObject.SetActive(false);

            var go = new GameObject("Art");
            go.transform.SetParent(transform, false);
            _artRoot = go.transform;
            _artRenderer = go.AddComponent<SpriteRenderer>();
            _artRenderer.sprite = frames[0];
            _artRenderer.sortingOrder = ViewMath.MonsterOrder;
            Vector3 b = frames[0].bounds.size;
            _artScale = entry.size / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));

            Material flash = FlashMaterial();
            if (flash != null)
            {
                var fg = new GameObject("HitFlash");
                fg.transform.SetParent(_artRoot, false);
                _flashRenderer = fg.AddComponent<SpriteRenderer>();
                _flashRenderer.sharedMaterial = flash;
                _flashRenderer.sprite = frames[0];
                _flashRenderer.sortingOrder = ViewMath.MonsterOrder + 1;
                _flashRenderer.enabled = false;
            }

            if (volcanoSkin != null) volcanoSkin.SetActive(false);
            if (oceanSkin != null) oceanSkin.SetActive(false);
            // 血条挪到贴图头顶
            if (hpBar != null) hpBar.transform.localPosition = new Vector3(0f, ArtTop() + 0.12f, 0f);
            AnimateArt(0f);
            return true;
        }

        /// <summary>贴图大致的顶边（根节点局部坐标；画布四周留了约 6% 空白）。</summary>
        private float ArtTop()
        {
            if (_artEntry == null) return 0.62f;
            return _artEntry.hover + _artEntry.size * 0.44f;
        }

        /// <summary>
        /// 晃动动画：序列帧（翅膀 / 尾巴摆动 + 线条抖动）+ 浮动、摇摆、挤压拉伸。
        /// 用 Time.deltaTime：亚空间里跟着慢放，暂停时停住；眩晕 / 定身时停帧。
        /// 疾冲兽蓄力时原地发抖、冲锋时拉长前倾。
        /// </summary>
        private void AnimateArt(float dt)
        {
            if (_artRoot == null || _frames == null || _artEntry == null) return;
            MonsterArtEntry a = _artEntry;
            bool windup = _artKind == MonsterKind.Charger && attackPhase == 1 && !_frozen;
            bool charging = _artKind == MonsterKind.Charger && attackPhase == 2 && !_frozen;

            float speedMul = _frozen ? 0f : charging ? 2.2f : windup ? 1.6f : 1f;
            _framePos += dt * a.fps * speedMul;
            if (_framePos >= _frames.Length) _framePos -= _frames.Length * Mathf.Floor(_framePos / _frames.Length);
            int frame = Mathf.Clamp((int)_framePos, 0, _frames.Length - 1);
            if (frame != _frameIndex)
            {
                _frameIndex = frame;
                _artRenderer.sprite = _frames[frame];
                if (_flashRenderer != null) _flashRenderer.sprite = _frames[frame];
            }

            _swayTime += dt * a.swaySpeed * (_frozen ? 0f : 1f);
            float s = Mathf.Sin(_swayTime);
            float bob, tilt;
            if (_artKind == MonsterKind.Walker)
            {
                bob = Mathf.Abs(s) * a.bob * 1.6f - a.bob * 0.5f;                  // 一颠一颠
                tilt = s * a.tilt;                                                  // 左右摇头晃脑
            }
            else
            {
                bob = s * a.bob;                                                    // 飞行：上下漂浮
                tilt = Mathf.Sin(_swayTime * 0.5f + 1f) * a.tilt;
            }
            float squash = Mathf.Sin(_swayTime * 2f) * 0.035f;
            Vector3 scale = new Vector3(1f + squash, 1f - squash, 1f);
            Vector3 jitter = Vector3.zero;

            if (windup)
            {
                // 蓄力：压低身子原地发抖
                jitter = new Vector3(UnityEngine.Random.Range(-0.04f, 0.04f), UnityEngine.Random.Range(-0.03f, 0.03f), 0f);
                scale = new Vector3(1.1f, 0.9f, 1f);
                bob = -a.hover * 0.3f;
                tilt = 0f;
            }
            else if (charging)
            {
                // 冲锋：拉长、前倾
                scale = new Vector3(1.18f, 0.88f, 1f);
                bob = 0f;
                tilt = -8f;
            }
            if (_faceLeft) tilt = -tilt;

            _punch = Mathf.Max(0f, _punch - dt * 8f);
            float punch = 1f + 0.12f * _punch;

            _artRoot.localPosition = new Vector3(jitter.x, a.hover + bob + jitter.y, 0f);
            _artRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _artRoot.localScale = scale * (_artScale * punch);
        }
    }
}
