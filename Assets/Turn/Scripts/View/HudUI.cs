using System.Collections.Generic;
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
        /// <summary>双主武器切换提示的剩余显示时间（现实秒）。</summary>
        private float _switchTimer;

        public const float FlashTime = 0.35f;

        /// <summary>false = 暂停 / 结算由 FlowUI 显示，HUD 自带的暂停面板和结算面板不再打开。</summary>
        public bool UseBuiltInPanels { get; set; } = true;

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
            ctx.Events.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
            _flashTimer = 0f;
            _rejectTimer = 0f;
            _switchTimer = 0f;
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
            _ctx.Events.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>覆盖结算面板上的文字（GameManager 在胜利时写入全程统计）。</summary>
        public void SetResultText(string text)
        {
            if (defeatText != null) defeatText.text = text;
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null) pausePanel.SetActive(paused && UseBuiltInPanels);
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

        private void OnWeaponEquipped(WeaponEquippedEvent evt)
        {
            _switchTimer = 1.5f;
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (defeatPanel == null) return;
            defeatPanel.SetActive(UseBuiltInPanels && (evt.Phase == RunPhase.Defeat || evt.Phase == RunPhase.Victory));
            if (evt.Phase == RunPhase.Victory && defeatText != null)
            {
                defeatText.text = "胜利！三组位面全部通过\n\n按 R / Enter 从第一关重新开始";
            }
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
                sb.Append(WeaponKinds.DisplayName(w.Current)).Append("  ");
                // 弹药条：最多画 20 格，按比例
                int cells = Mathf.Min(20, Mathf.Max(1, w.Capacity));
                int filled = w.Capacity > 0 ? Mathf.CeilToInt((float)w.Ammo / w.Capacity * cells) : 0;
                for (int i = 0; i < cells; i++) sb.Append(i < filled ? "■" : "□");
                sb.Append("  ").Append(w.Ammo).Append('/').Append(w.Capacity);
                // 铺垫进度：当前武器的痕迹 / 印记
                switch (w.Current)
                {
                    case WeaponKind.WaterGun: sb.Append("   水渍 ").Append(w.Puddles.Count); break;
                    case WeaponKind.Spear: sb.Append("   矛印记 ").Append(w.TotalSpearMarks); break;
                    case WeaponKind.Blade:
                        sb.Append("   剑气印记 ").Append(w.TotalBladeMarks).Append("（斩击 +")
                          .Append(Mathf.RoundToInt(w.TotalBladeMarks * _ctx.Config.Arsenal.Blade.MarkBonus * 100f)).Append("%）");
                        break;
                    case WeaponKind.Palm: sb.Append("   掌印 ").Append(w.TotalPalmMarks); break;
                    case WeaponKind.Crossbow: sb.Append("   杀气 ").Append(w.KillingIntent); break;
                    case WeaponKind.Poker:
                        sb.Append("   手牌");
                        for (int i = 0; i < w.Hand.Count; i++) sb.Append(' ').Append(w.Hand[i].Label);
                        if (w.Hand.Count == 0) sb.Append(" —");
                        break;
                    case WeaponKind.Shuriken:
                    {
                        int stars = 0, orbit = 0;
                        IReadOnlyList<WeaponField> fields = w.Fields;
                        for (int i = 0; i < fields.Count; i++)
                        {
                            if (fields[i].Kind == WeaponFieldKind.Shuriken) stars++;
                            else if (fields[i].Kind == WeaponFieldKind.OrbitShuriken) orbit++;
                        }
                        sb.Append("   场上手里剑 ").Append(stars);
                        if (orbit > 0) sb.Append("  环绕 ").Append(orbit);
                        break;
                    }
                    case WeaponKind.Stapler: sb.Append("   钉痕 ").Append(w.TotalStapleMarks); break;
                    case WeaponKind.Sword:
                    {
                        int trails = 0;
                        IReadOnlyList<WeaponField> fields = w.Fields;
                        for (int i = 0; i < fields.Count; i++) if (fields[i].Kind == WeaponFieldKind.SwordTrail) trails++;
                        sb.Append("   剑痕 ").Append(trails);
                        if (w.SwordBonusCharges > 0) sb.Append("  突进上限 +").Append(w.SwordBonusCharges);
                        break;
                    }
                }
                // 扑克牌的护盾 / 大小王加成：不管当前是哪把都显示（方案 A 切走后护盾还在）
                if (w.ShieldCharges > 0) sb.Append("   ♦护盾 ").Append(w.ShieldCharges);
                if (w.JokerBonus > 0f) sb.Append("   王牌 +").Append(Mathf.RoundToInt(w.JokerBonus * 100f)).Append('%');
                // 副武器状态
                ISubWeaponSystem sub = _ctx.SubWeapons;
                if (sub.Kind == SubWeaponKind.Drones) sb.Append("   浮游炮 ×").Append(sub.UnitCount);
                else if (sub.Kind == SubWeaponKind.SpaceAnchor) sb.Append("   锚点 ").Append(sub.Anchors.Count);
                else if (sub.Kind == SubWeaponKind.Hourglass) sb.Append("   敌人移速 ").Append(Mathf.RoundToInt(sub.MonsterSpeedMultiplier * 100f)).Append('%');
                else if (sub.Kind == SubWeaponKind.Magnet) sb.Append(sub.Anchors.Count > 0 ? "   磁极：正极已放下（下次翻滚对撞）" : "   磁极：下次翻滚放正极");
                else if (sub.Kind == SubWeaponKind.Cursor) sb.Append(sub.Trail.Count > 0 ? "   光标绘制中（下次翻滚打击）" : "   光标：下次翻滚开始绘制");
                ammoText.text = sb.ToString();
                ammoText.color = ViewMath.WeaponColor(w.Current);
            }
            if (ammoWarning != null)
            {
                bool empty = w.Ammo <= 0;
                ammoWarning.enabled = empty && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.7f;
            }

            // ExperienceUI
            IProgressionSystem prog = _ctx.Progression;
            if (xpFill != null) xpFill.anchorMax = new Vector2(prog.XpToNext > 0 ? Mathf.Clamp01((float)prog.Xp / prog.XpToNext) : 0f, 1f);
            if (levelText != null) levelText.text = "Lv " + prog.Level + "   " + prog.Xp + " / " + prog.XpToNext;

            // MapUI（新策划案：波次 + 当前武器 + 翻滚 / 亚空间状态）
            PlaneId map = _ctx.Plane.Active;
            bool mechanics = _ctx.Config.Environment.MechanicsEnabled;
            ILoadoutSystem loadout = _ctx.Loadout;
            if (mapText != null)
            {
                string wave = "第 " + _ctx.Session.Wave + " 波" + (_ctx.Session.WaveTimeLeft > 0f ? "  下一波 " + FormatTime(_ctx.Session.WaveTimeLeft) : "");
                string weapon;
                if (loadout.Scheme == LoadoutScheme.DualMain)
                    weapon = "   主武器：" + WeaponKinds.DisplayName(loadout.Equipped) + "  ▶ 翻滚后切到 " + WeaponKinds.DisplayName(loadout.Standby);
                else
                    weapon = "   主武器：" + WeaponKinds.DisplayName(loadout.Equipped) + "   副武器：" +
                             (loadout.Sub == SubWeaponKind.None ? "无" : SubWeaponKinds.DisplayName(loadout.Sub));
                string switched = _switchTimer > 0f ? "   已切换！" : "";
                mapText.text = wave + weapon + switched + (mechanics ? "   " + PlaneIds.DisplayName(map) + PlaneStatus(map) : "");
                mapText.color = _switchTimer > 0f ? ViewMath.WeaponColor(loadout.Equipped) : (mechanics ? ViewMath.MapColor(map) : Color.white);
            }
            float ready;
            if (p.InSubspace)
            {
                float total = Mathf.Max(0.05f, _ctx.Config.Roll.SubspaceDuration);
                ready = Mathf.Clamp01(p.SubspaceLeft / total);
            }
            else
            {
                float cd = _ctx.Roll.EffectiveCooldown;
                ready = cd > 0f ? 1f - Mathf.Clamp01(p.RollCooldownLeft / cd) : 1f;
            }
            if (rollFill != null) rollFill.anchorMax = new Vector2(ready, 1f);
            if (rollFillImage != null)
            {
                Color c = p.InSubspace ? new Color(0.85f, 0.85f, 0.85f, 1f)
                    : (ready >= 1f ? new Color(0.5f, 1f, 0.6f, 1f) : new Color(0.6f, 0.6f, 0.7f, 1f));
                if (_rejectTimer > 0f) c = new Color(1f, 0.3f, 0.3f, 1f);
                rollFillImage.color = c;
            }
            if (rollText != null)
            {
                if (p.InSubspace) rollText.text = "亚空间 " + p.SubspaceLeft.ToString("0.0") + "s";
                else if (p.RollCooldownLeft > 0f) rollText.text = "翻滚冷却 " + p.RollCooldownLeft.ToString("0.0") + "s";
                else rollText.text = "翻滚就绪 [Space]  → 亚空间爆发 + 补满弹药";
            }
            if (terrainText != null) terrainText.text = mechanics ? "脚下：" + TerrainName(p.Terrain) : "";
            if (_switchTimer > 0f) _switchTimer -= dt;
            if (statsText != null)
            {
                float left = _ctx.Session.LevelTimeLeft;
                string level = _ctx.Config.Level.Endless
                    ? "无尽"
                    : "第 " + ((int)_ctx.Plane.ActivePair + 1) + " 关" + (left > 0f ? "  过关还剩 " + FormatTime(left) : "");
                statsText.text = level + "   " + FormatTime(_ctx.Session.Time) + "   击杀 " + prog.Kills + "   怪物 " + _ctx.Monsters.AliveCount +
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

        /// <summary>位面机制状态（鲨鱼数量 / 困意 / 梦境减速），接在“当前地图”后面。</summary>
        private string PlaneStatus(PlaneId map)
        {
            IEnvironmentSystem env = _ctx.Environment;
            switch (map)
            {
                case PlaneId.Ocean:
                    return "   鲨鱼 ×" + _ctx.Sharks.Active.Count;
                case PlaneId.Office:
                    return "   困意 " + Mathf.RoundToInt(env.Drowsiness * 100f) + "%" + (env.DrowsyDamaging ? "  掉血中" : "");
                case PlaneId.Dream:
                    return "   减速 " + env.DreamSlowStacks + " 层 -" + Mathf.RoundToInt(env.DreamSlow * 100f) + "%";
                default:
                    return "";
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
