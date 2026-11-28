using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 升级 / 宝箱三选一面板（节点 Canvas/UpgradePanel）。LevelUp / Chest 阶段显示（游戏全局暂停），点击按钮或按 1/2/3 选择。
    /// 单向：只把选择转成回调（GameManager 调 ISessionSystem.TryChooseUpgrade），不直接改升级数据。
    /// </summary>
    public sealed class UpgradeUI : MonoBehaviour
    {
        [Serializable]
        public sealed class OptionView
        {
            public Button button;
            public Text title;
            public Text description;
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private Text header;
        [SerializeField] private OptionView[] options = new OptionView[3];

        private ITurnContext _ctx;
        private Action<int> _onChoose;
        private bool _wired;
        private RectTransform _safeContent;
        private RectTransform _fittedContent;

        private void Awake()
        {
            UIFont.Apply(gameObject);
        }

        public void Bind(ITurnContext ctx, Action<int> onChoose)
        {
            Unbind();
            _ctx = ctx;
            _onChoose = onChoose;
            MobilePlatform.EnsureEventSystem();
            EnsureSafeLayout();
            Wire();
            ctx.Events.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            Show(false);
        }

        private void EnsureSafeLayout()
        {
            if (_safeContent != null || panel == null || !(panel.transform is RectTransform)) return;
            // The serialized Window is the full-screen raycast blocker: leave it untouched.
            var safe = new GameObject("UpgradeSafeContent", typeof(RectTransform));
            safe.transform.SetParent(panel.transform, false);
            _safeContent = (RectTransform)safe.transform;
            _safeContent.anchorMin = Vector2.zero;
            _safeContent.anchorMax = Vector2.one;
            _safeContent.offsetMin = _safeContent.offsetMax = Vector2.zero;
            MobileSafeArea.Attach(_safeContent);
            var fitted = new GameObject("FittedOptions", typeof(RectTransform));
            fitted.transform.SetParent(_safeContent, false);
            _fittedContent = (RectTransform)fitted.transform;
            _fittedContent.anchorMin = _fittedContent.anchorMax = _fittedContent.pivot = new Vector2(.5f, .5f);
            _fittedContent.sizeDelta = new Vector2(1000f, 600f);
            if (header != null) header.transform.SetParent(_fittedContent, false);
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null || options[i].button == null) continue;
                options[i].button.transform.SetParent(_fittedContent, false);
                if (MobilePlatform.IsTouchUI)
                {
                    RectTransform rt = options[i].button.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                    rt.anchoredPosition = new Vector2((i - 1) * 320f, 0f);
                    rt.sizeDelta = new Vector2(300f, 340f);
                    if (options[i].title != null)
                    {
                        Text title = options[i].title;
                        title.fontSize = 24;
                        title.resizeTextForBestFit = true;
                        title.resizeTextMinSize = 20;
                        title.resizeTextMaxSize = 24;
                        title.verticalOverflow = VerticalWrapMode.Truncate;
                        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1f);
                        title.rectTransform.pivot = new Vector2(.5f, 1f);
                        title.rectTransform.anchoredPosition = new Vector2(0f, -16f);
                        title.rectTransform.sizeDelta = new Vector2(270f, 78f);
                    }
                    if (options[i].description != null)
                    {
                        Text description = options[i].description;
                        description.fontSize = 22;
                        description.resizeTextForBestFit = true;
                        description.resizeTextMinSize = 18;
                        description.resizeTextMaxSize = 22;
                        description.verticalOverflow = VerticalWrapMode.Truncate;
                        description.rectTransform.anchorMin = description.rectTransform.anchorMax = new Vector2(.5f, 1f);
                        description.rectTransform.pivot = new Vector2(.5f, 1f);
                        description.rectTransform.anchoredPosition = new Vector2(0f, -104f);
                        description.rectTransform.sizeDelta = new Vector2(270f, 220f);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_fittedContent == null || _safeContent == null) return;
            // Desktop keeps the authored 280 × 300 cards and 300-unit spacing at its
            // 1280 × 720 reference size. Only shrink when the whole 1000 × 600 group
            // would clip; the mobile enlargement remains unchanged.
            float maxScale = MobilePlatform.IsTouchUI ? 1.25f : 1f;
            float scale = Mathf.Min(maxScale, Mathf.Min(_safeContent.rect.width / 1000f, _safeContent.rect.height / 600f));
            _fittedContent.localScale = Vector3.one * Mathf.Max(0f, scale);
        }

        public void Unbind()
        {
            if (_ctx == null) return;
            _ctx.Events.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            _ctx = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>按钮监听只挂一次（运行时添加的监听器存不进场景）。</summary>
        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null || options[i].button == null) continue;
                int index = i;
                options[i].button.onClick.AddListener(() => Choose(index));
            }
        }

        private void Choose(int index)
        {
            Action<int> cb = _onChoose;
            if (cb != null) cb(index);
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            bool open = evt.Phase == RunPhase.LevelUp || evt.Phase == RunPhase.Chest;
            Show(open);
            if (open) Refresh();
        }

        private void Show(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void Refresh()
        {
            IReadOnlyList<UpgradeOption> list = _ctx.Progression.Options;
            ILoadoutSystem l = _ctx.Loadout;
            string weapon = l.Scheme == LoadoutScheme.DualMain
                ? WeaponKinds.DisplayName(l.Equipped) + " / " + WeaponKinds.DisplayName(l.Standby)
                : WeaponKinds.DisplayName(l.Equipped) + (l.Sub != SubWeaponKind.None ? " + " + SubWeaponKinds.DisplayName(l.Sub) : "");
            if (header != null)
                header.text = _ctx.Progression.OptionsFromChest
                    ? "宝箱！ " + weapon + (MobilePlatform.IsTouchUI ? "   点击选择一项——有的选项带代价" : "   选择一项（1 / 2 / 3）—— 有的选项带代价")
                    : "升级！ Lv " + _ctx.Progression.Level + " · " + weapon + (MobilePlatform.IsTouchUI ? "   点击选择一项" : "   选择一项（1 / 2 / 3）");
            for (int i = 0; i < options.Length; i++)
            {
                OptionView view = options[i];
                if (view == null || view.button == null) continue;
                bool has = i < list.Count;
                view.button.gameObject.SetActive(has);
                if (!has) continue;
                UpgradeOption o = list[i];
                string levels = o.Curse != ChestCurse.None ? "   代价 " + o.FromLevel + " → " + o.ToLevel + " 层" : "   Lv " + o.FromLevel + " → " + o.ToLevel;
                if (view.title != null) view.title.text = (i + 1) + ". [" + o.Tag + "] " + o.Name + levels;
                if (view.description != null) view.description.text = o.Description;
            }
        }
    }
}
