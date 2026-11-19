using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Survivor.Art;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>
    /// HUD（Overlay 画布，不受后期影响）：经验条、等级、计时、击杀、武器 / 被动栏、播报、受伤红闪，
    /// 以及升级三选一、暂停、结算三个面板（对应卡牌白模的 RunView）。
    /// 单向：订阅事件 → 标脏 → 按当前状态整体刷新；点击只转成回调（由 SurvivorRoot 调系统接口）。
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public const int SlotCount = 6;
        private const float AnnounceTime = 2.4f;
        private const float HurtFlashTime = 0.3f;

        private ISurvivorContext _ctx;
        private Action<int> _onChoose;
        private Action _onRestart;

        private RectTransform _xpFill;
        private Text _levelText;
        private Text _timerText;
        private Text _killText;
        private Text _hpText;
        private Text _announceText;
        private Image _hurtOverlay;
        private readonly List<Slot> _weaponSlots = new List<Slot>();
        private readonly List<Slot> _passiveSlots = new List<Slot>();

        private GameObject _levelUpPanel;
        private Text _levelUpTitle;
        private readonly List<UpgradeCardView> _cards = new List<UpgradeCardView>();
        private GameObject _pausePanel;
        private GameObject _endPanel;
        private Text _endTitle;
        private Text _endStats;

        private bool _slotsDirty = true;
        private int _shownSecond = -1;
        private int _shownKills = -1;
        private float _announceTimer;
        private float _hurtTimer;

        private sealed class Slot
        {
            public Image Frame;
            public Image Icon;
            public Text Level;
        }

        // ---------------- 创建 ----------------

        public static HudView Create()
        {
            var go = new GameObject("[Survivor] HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = go.AddComponent<HudView>();
            hud.Build((RectTransform)go.transform);
            UIKit.ApplyFont(go);
            return hud;
        }

        private void Build(RectTransform root)
        {
            // 受伤红闪（最底层，铺满）
            _hurtOverlay = UIKit.CreateImage("Hurt", root, Vector2.zero, Vector2.zero, null, new Color(0.8f, 0.05f, 0.08f, 0f));
            UIKit.Stretch(_hurtOverlay.rectTransform);

            // 经验条
            Image xpBack = UIKit.CreateImage("XpBar", root, Vector2.zero, Vector2.zero, null, new Color(0.06f, 0.06f, 0.1f, 0.9f));
            RectTransform xpRt = xpBack.rectTransform;
            xpRt.anchorMin = new Vector2(0f, 1f);
            xpRt.anchorMax = new Vector2(1f, 1f);
            xpRt.pivot = new Vector2(0.5f, 1f);
            xpRt.offsetMin = new Vector2(0f, -18f);
            xpRt.offsetMax = Vector2.zero;
            Image xpFill = UIKit.CreateImage("Fill", xpRt, Vector2.zero, Vector2.zero, null, new Color(0.3f, 0.7f, 1f, 1f));
            _xpFill = xpFill.rectTransform;
            _xpFill.anchorMin = Vector2.zero;
            _xpFill.anchorMax = new Vector2(0f, 1f);
            _xpFill.offsetMin = Vector2.zero;
            _xpFill.offsetMax = Vector2.zero;
            _levelText = UIKit.CreateText("Level", xpRt, Vector2.zero, new Vector2(120f, 18f), "Lv 1",
                14, TextAnchor.MiddleRight, Color.white);
            UIKit.Pin(_levelText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f));

            _timerText = UIKit.CreateText("Timer", root, Vector2.zero, new Vector2(200f, 44f), "00:00",
                34, TextAnchor.MiddleCenter, Color.white);
            _timerText.fontStyle = FontStyle.Bold;
            UIKit.AddOutline(_timerText, 2f);
            UIKit.Pin(_timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f));

            _killText = UIKit.CreateText("Kills", root, Vector2.zero, new Vector2(220f, 30f), "击杀 0",
                20, TextAnchor.MiddleRight, new Color(0.85f, 0.85f, 0.85f, 1f));
            UIKit.AddOutline(_killText, 1.5f);
            UIKit.Pin(_killText.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -26f));

            _hpText = UIKit.CreateText("Hp", root, Vector2.zero, new Vector2(240f, 30f), "",
                18, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.55f, 1f));
            UIKit.AddOutline(_hpText, 1.5f);
            UIKit.Pin(_hpText.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 14f));

            for (int i = 0; i < SlotCount; i++)
            {
                _weaponSlots.Add(CreateSlot(root, new Vector2(14f + i * 42f, -26f)));
                _passiveSlots.Add(CreateSlot(root, new Vector2(14f + i * 42f, -70f)));
            }

            _announceText = UIKit.CreateText("Announce", root, new Vector2(0f, 170f), new Vector2(900f, 60f), "",
                36, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f, 1f));
            _announceText.fontStyle = FontStyle.Bold;
            UIKit.AddOutline(_announceText, 2.5f);

            BuildLevelUpPanel(root);
            BuildPausePanel(root);
            BuildEndPanel(root);
        }

        private Slot CreateSlot(RectTransform root, Vector2 topLeft)
        {
            var slot = new Slot();
            slot.Frame = UIKit.CreateImage("Slot", root, Vector2.zero, new Vector2(38f, 38f), SurvivorArt.Get(SurvivorArt.Slot), Color.white);
            UIKit.Pin(slot.Frame.rectTransform, new Vector2(0f, 1f), topLeft);
            slot.Icon = UIKit.CreateImage("Icon", slot.Frame.rectTransform, Vector2.zero, new Vector2(28f, 28f), null, Color.white);
            slot.Icon.preserveAspect = true;
            slot.Level = UIKit.CreateText("Lv", slot.Frame.rectTransform, new Vector2(10f, -12f), new Vector2(24f, 18f), "",
                13, TextAnchor.MiddleRight, new Color(1f, 0.9f, 0.5f, 1f));
            UIKit.AddOutline(slot.Level, 1f);
            return slot;
        }

        private RectTransform CreateDim(RectTransform root, string name, float alpha)
        {
            Image dim = UIKit.CreateImage(name, root, Vector2.zero, Vector2.zero, null, new Color(0f, 0f, 0f, alpha));
            dim.raycastTarget = true; // 挡住下面的点击
            UIKit.Stretch(dim.rectTransform);
            return dim.rectTransform;
        }

        private void BuildLevelUpPanel(RectTransform root)
        {
            RectTransform panel = CreateDim(root, "LevelUp", 0.62f);
            _levelUpPanel = panel.gameObject;

            _levelUpTitle = UIKit.CreateText("Title", panel, new Vector2(0f, 230f), new Vector2(600f, 60f), "升级！",
                44, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f, 1f));
            _levelUpTitle.fontStyle = FontStyle.Bold;
            UIKit.AddOutline(_levelUpTitle, 3f);

            for (int i = 0; i < 3; i++)
            {
                GameObject go = PrefabLibrary.Spawn(PrefabLibrary.UpgradeCard, panel, UpgradeCardView.BuildTemplate);
                ((RectTransform)go.transform).anchoredPosition = new Vector2((i - 1) * 290f, 0f);
                var card = go.GetComponent<UpgradeCardView>();
                card.Hide();
                _cards.Add(card);
            }

            UIKit.CreateText("Hint", panel, new Vector2(0f, -232f), new Vector2(600f, 30f), "点击或按 1 / 2 / 3 选择",
                18, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f, 1f));
            _levelUpPanel.SetActive(false);
        }

        private void BuildPausePanel(RectTransform root)
        {
            RectTransform panel = CreateDim(root, "Pause", 0.5f);
            _pausePanel = panel.gameObject;
            Text title = UIKit.CreateText("Title", panel, new Vector2(0f, 20f), new Vector2(600f, 60f), "暂停",
                48, TextAnchor.MiddleCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            UIKit.CreateText("Hint", panel, new Vector2(0f, -40f), new Vector2(600f, 30f), "按 Esc 继续",
                20, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.8f, 1f));
            _pausePanel.SetActive(false);
        }

        private void BuildEndPanel(RectTransform root)
        {
            RectTransform panel = CreateDim(root, "End", 0.72f);
            _endPanel = panel.gameObject;
            _endTitle = UIKit.CreateText("Title", panel, new Vector2(0f, 110f), new Vector2(700f, 80f), "",
                56, TextAnchor.MiddleCenter, Color.white);
            _endTitle.fontStyle = FontStyle.Bold;
            UIKit.AddOutline(_endTitle, 3f);
            _endStats = UIKit.CreateText("Stats", panel, new Vector2(0f, 10f), new Vector2(600f, 120f), "",
                24, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.88f, 1f));

            Image button = UIKit.CreateImage("Restart", panel, new Vector2(0f, -110f), new Vector2(280f, 64f),
                SurvivorArt.Get(SurvivorArt.ButtonFrame), Color.white);
            UIKit.MakeButton(button, () => { if (_onRestart != null) _onRestart(); });
            UIKit.CreateText("Label", button.rectTransform, Vector2.zero, new Vector2(280f, 64f), "重新开始（空格）",
                24, TextAnchor.MiddleCenter, Color.white);
            _endPanel.SetActive(false);
        }

        // ---------------- 绑定 ----------------

        public void Bind(ISurvivorContext ctx, Action<int> onChoose, Action onRestart)
        {
            Unbind();
            _ctx = ctx;
            _onChoose = onChoose;
            _onRestart = onRestart;

            ctx.Events.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            ctx.Events.Subscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
            ctx.Events.Subscribe<WaveAnnouncedEvent>(OnWaveAnnounced);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);

            _slotsDirty = true;
            _shownSecond = -1;
            _shownKills = -1;
            _announceTimer = 0f;
            _hurtTimer = 0f;
            _announceText.text = "";
            _levelUpPanel.SetActive(false);
            _pausePanel.SetActive(false);
            _endPanel.SetActive(false);
            UIKit.ApplyFont(gameObject);
        }

        private void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            _ctx.Events.Unsubscribe<UpgradeAppliedEvent>(OnUpgradeApplied);
            _ctx.Events.Unsubscribe<WaveAnnouncedEvent>(OnWaveAnnounced);
            _ctx.Events.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            _ctx = null;
        }

        public void SetPaused(bool paused)
        {
            _pausePanel.SetActive(paused);
        }

        // ---------------- 事件 ----------------

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            _levelUpPanel.SetActive(evt.Phase == RunPhase.LevelUp);
            if (evt.Phase == RunPhase.LevelUp) ShowLevelUp();
            if (evt.Phase == RunPhase.Victory || evt.Phase == RunPhase.Defeat) ShowEnd(evt.Phase == RunPhase.Victory);
        }

        private void OnUpgradeApplied(UpgradeAppliedEvent evt)
        {
            _slotsDirty = true;
        }

        private void OnWaveAnnounced(WaveAnnouncedEvent evt)
        {
            _announceText.text = evt.Message;
            _announceText.color = evt.Boss ? new Color(1f, 0.45f, 0.45f, 1f) : new Color(1f, 0.85f, 0.4f, 1f);
            _announceTimer = AnnounceTime;
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            _hurtTimer = HurtFlashTime;
        }

        private void ShowLevelUp()
        {
            IReadOnlyList<UpgradeOption> options = _ctx.Progression.Options;
            _levelUpTitle.text = "升级！  Lv " + _ctx.Progression.Level;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i >= options.Count)
                {
                    _cards[i].Hide();
                    continue;
                }
                int index = i;
                _cards[i].Show(options[i], i, () => { if (_onChoose != null) _onChoose(index); });
            }
            // 选项数少于 3 时居中排列
            int shown = Mathf.Min(options.Count, _cards.Count);
            for (int i = 0; i < shown; i++)
            {
                ((RectTransform)_cards[i].transform).anchoredPosition = new Vector2((i - (shown - 1) * 0.5f) * 290f, 0f);
            }
        }

        private void ShowEnd(bool victory)
        {
            _levelUpPanel.SetActive(false);
            _pausePanel.SetActive(false);
            _endPanel.SetActive(true);
            _endTitle.text = victory ? "存活成功！" : "倒下了……";
            _endTitle.color = victory ? new Color(1f, 0.88f, 0.45f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
            _endStats.text = "存活 " + FormatTime(_ctx.Session.Time) + "\n等级 " + _ctx.Progression.Level +
                             "    击杀 " + _ctx.Progression.Kills;
        }

        // ---------------- 每帧 ----------------

        private void Update()
        {
            if (_ctx == null) return;

            float xpRatio = _ctx.Progression.XpToNext > 0 ? (float)_ctx.Progression.Xp / _ctx.Progression.XpToNext : 0f;
            _xpFill.anchorMax = new Vector2(Mathf.Clamp01(xpRatio), 1f);
            _levelText.text = "Lv " + _ctx.Progression.Level;

            int second = Mathf.FloorToInt(_ctx.Session.Time);
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _timerText.text = FormatTime(_ctx.Session.Time);
            }
            if (_ctx.Progression.Kills != _shownKills)
            {
                _shownKills = _ctx.Progression.Kills;
                _killText.text = "击杀 " + _shownKills;
            }
            _hpText.text = "生命 " + Mathf.CeilToInt(_ctx.Player.State.Hp) + " / " + Mathf.CeilToInt(_ctx.Player.MaxHp);

            if (_slotsDirty)
            {
                _slotsDirty = false;
                RefreshSlots();
            }

            if (_announceTimer > 0f)
            {
                _announceTimer -= Time.unscaledDeltaTime;
                Color c = _announceText.color;
                // 按格闪两下再常亮，最后淡出
                bool blink = _announceTimer > AnnounceTime - 0.6f && Mathf.FloorToInt(_announceTimer / StepAnimator.Step) % 2 == 0;
                c.a = blink ? 0.35f : Mathf.Clamp01(_announceTimer / 0.5f);
                _announceText.color = c;
                if (_announceTimer <= 0f) _announceText.text = "";
            }

            if (_hurtTimer > 0f) _hurtTimer -= Time.unscaledDeltaTime;
            float stepped = Mathf.Ceil(Mathf.Max(0f, _hurtTimer) / StepAnimator.Step) * StepAnimator.Step;
            Color hurt = _hurtOverlay.color;
            hurt.a = 0.22f * Mathf.Clamp01(stepped / HurtFlashTime);
            _hurtOverlay.color = hurt;
        }

        private void RefreshSlots()
        {
            IReadOnlyList<WeaponInstance> weapons = _ctx.Weapons.Owned;
            for (int i = 0; i < _weaponSlots.Count; i++)
            {
                Slot slot = _weaponSlots[i];
                bool has = i < weapons.Count;
                slot.Icon.enabled = has;
                slot.Icon.sprite = has ? SurvivorArt.Get(weapons[i].Data.ArtKey) : null;
                slot.Level.text = has ? (weapons[i].IsMaxLevel ? "MAX" : weapons[i].Level.ToString()) : "";
                slot.Frame.color = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            IReadOnlyList<PassiveInstance> passives = _ctx.Player.Passives;
            for (int i = 0; i < _passiveSlots.Count; i++)
            {
                Slot slot = _passiveSlots[i];
                bool has = i < passives.Count;
                slot.Icon.enabled = has;
                slot.Icon.sprite = has ? SurvivorArt.Get(passives[i].Data.ArtKey) : null;
                slot.Level.text = has ? (passives[i].IsMaxLevel ? "MAX" : passives[i].Level.ToString()) : "";
                slot.Frame.color = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
