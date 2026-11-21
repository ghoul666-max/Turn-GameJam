using UnityEngine;
using UnityEngine.UI;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// HUD（节点 Canvas）：HealthUI / AmmoUI / ExperienceUI / MapUI，以及全屏闪色、暂停、失败面板。
    /// 单向：每帧读系统状态刷新；只订阅事件来触发一次性表现（受伤闪红、拒绝翻滚提示）。
    /// 文字默认用内置字体存进场景，运行时换成支持中文的系统字体。
    /// </summary>
    public sealed class HudUI : MonoBehaviour
    {
        [Header("HealthUI")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Text healthText;
        [Header("AmmoUI")]
        [SerializeField] private Text ammoText;
        [SerializeField] private Text ammoWarning;
        [Header("ExperienceUI")]
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private Text levelText;
        [Header("MapUI")]
        [SerializeField] private Text mapText;
        [SerializeField] private RectTransform rollFill;
        [SerializeField] private Image rollFillImage;
        [SerializeField] private Text rollText;
        [SerializeField] private Text terrainText;
        [SerializeField] private Text statsText;
        [Header("面板 / 特效")]
        [SerializeField] private Image screenFlash;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Text defeatText;

        private ITurnContext _ctx;
        private Color _flashColor;
        private float _flashTimer;
        private float _rejectTimer;
        private float _hurtTimer;

        public const float FlashTime = 0.35f;

        private void Awake()
        {
            UIFont.Apply(gameObject);
        }

        public void Bind(ITurnContext ctx)
        {
            Unbind();
            _ctx = ctx;
            ctx.Events.Subscribe<RollRejectedEvent>(OnRollRejected);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            ctx.Events.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            _flashTimer = 0f;
            _rejectTimer = 0f;
            _hurtTimer = 0f;
            SetPaused(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (screenFlash != null) screenFlash.color = new Color(0f, 0f, 0f, 0f);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<RollRejectedEvent>(OnRollRejected);
            _ctx.Events.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            _ctx.Events.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null) pausePanel.SetActive(paused);
        }

        /// <summary>地图切换特效：全屏闪一下新地图的主题色。</summary>
        public void FlashScreen(Color color)
        {
            _flashColor = color;
            _flashTimer = FlashTime;
        }

        private void OnRollRejected(RollRejectedEvent evt)
        {
            _rejectTimer = 0.4f;
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            _hurtTimer = 0.25f;
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (defeatPanel == null) return;
            defeatPanel.SetActive(evt.Phase == RunPhase.Defeat);
            if (evt.Phase == RunPhase.Defeat && defeatText != null)
            {
                defeatText.text = "倒下了……\n存活 " + FormatTime(_ctx.Session.Time) + "   等级 " + _ctx.Progression.Level +
                                  "   击杀 " + _ctx.Progression.Kills + "   翻滚 " + _ctx.Roll.RollCount + " 次\n\n按 R / Enter 重新开始";
            }
        }

        private void Update()
        {
            if (_ctx == null) return;
            float dt = Time.unscaledDeltaTime;
            PlayerState p = _ctx.Player.State;

            // HealthUI
            float hpRatio = p.MaxHp > 0f ? Mathf.Clamp01(p.Hp / p.MaxHp) : 0f;
            if (healthFill != null) healthFill.anchorMax = new Vector2(hpRatio, 1f);
            if (healthText != null)
            {
                string tag = p.Invincible ? "  [无敌]" : "";
                healthText.text = "生命 " + Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp) + tag;
            }

            // AmmoUI
            IWeaponSystem w = _ctx.Weapons;
            if (ammoText != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(w.CurrentForm.Name).Append("  ");
                for (int i = 0; i < w.Capacity; i++) sb.Append(i < w.Ammo ? "■" : "□");
                sb.Append("  ").Append(w.Ammo).Append('/').Append(w.Capacity);
                ammoText.text = sb.ToString();
                ammoText.color = ViewMath.MapColor(w.Form);
            }
            if (ammoWarning != null)
            {
                bool empty = w.Ammo < Mathf.Max(1, _ctx.Config.Weapon.AmmoPerShot);
                ammoWarning.enabled = empty && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.7f;
            }

            // ExperienceUI
            IProgressionSystem prog = _ctx.Progression;
            if (xpFill != null) xpFill.anchorMax = new Vector2(prog.XpToNext > 0 ? Mathf.Clamp01((float)prog.Xp / prog.XpToNext) : 0f, 1f);
            if (levelText != null) levelText.text = "Lv " + prog.Level + "   " + prog.Xp + " / " + prog.XpToNext;

            // MapUI
            PlaneId map = _ctx.Plane.Active;
            if (mapText != null)
            {
                mapText.text = "当前地图：" + PlaneIds.DisplayName(map) + (map == PlaneId.Ocean ? "   鲨鱼 ×" + _ctx.Sharks.Active.Count : "");
                mapText.color = ViewMath.MapColor(map);
            }
            float cd = _ctx.Roll.EffectiveCooldown;
            float ready = cd > 0f ? 1f - Mathf.Clamp01(p.RollCooldownLeft / cd) : 1f;
            if (rollFill != null) rollFill.anchorMax = new Vector2(ready, 1f);
            if (rollFillImage != null)
            {
                Color c = ready >= 1f ? new Color(0.5f, 1f, 0.6f, 1f) : new Color(0.6f, 0.6f, 0.7f, 1f);
                if (_rejectTimer > 0f) c = new Color(1f, 0.3f, 0.3f, 1f);
                rollFillImage.color = c;
            }
            if (rollText != null)
            {
                rollText.text = p.RollCooldownLeft > 0f
                    ? "翻滚冷却 " + p.RollCooldownLeft.ToString("0.0") + "s"
                    : "翻滚就绪 [Space]  → " + PlaneIds.DisplayName(PlaneIds.Other(map)) + "  +" + _ctx.Config.Penalty.Count + " 怪";
            }
            if (terrainText != null) terrainText.text = "脚下：" + TerrainName(p.Terrain);
            if (statsText != null)
            {
                statsText.text = FormatTime(_ctx.Session.Time) + "   击杀 " + prog.Kills + "   怪物 " + _ctx.Monsters.AliveCount +
                                 "   翻滚 " + _ctx.Roll.RollCount;
            }

            // 一次性表现
            if (_rejectTimer > 0f) _rejectTimer -= dt;
            if (_hurtTimer > 0f) _hurtTimer -= dt;
            if (screenFlash != null)
            {
                if (_flashTimer > 0f)
                {
                    _flashTimer -= dt;
                    Color c = _flashColor;
                    c.a = 0.45f * Mathf.Clamp01(_flashTimer / FlashTime);
                    screenFlash.color = c;
                }
                else if (_hurtTimer > 0f)
                {
                    screenFlash.color = new Color(0.9f, 0.05f, 0.05f, 0.25f * Mathf.Clamp01(_hurtTimer / 0.25f));
                }
                else
                {
                    screenFlash.color = new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        private static string TerrainName(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Lava: return "岩浆";
                case TerrainType.ShallowWater: return "浅水（缓慢回血）";
                default: return "普通地面";
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }

    /// <summary>uGUI 字体：场景 / Prefab 里存内置字体，运行时换成支持中文的系统字体。</summary>
    public static class UIFont
    {
        private static Font _font;

        public static Font Chinese
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 20);
                }
                return _font;
            }
        }

        public static void Apply(GameObject root)
        {
            if (root == null) return;
            Font font = Chinese;
            if (font == null) return;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = font;
        }
    }
}
