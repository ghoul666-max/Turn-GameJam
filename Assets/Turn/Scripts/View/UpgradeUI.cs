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

        private void Awake()
        {
            UIFont.Apply(gameObject);
        }

        public void Bind(ITurnContext ctx, Action<int> onChoose)
        {
            Unbind();
            _ctx = ctx;
            _onChoose = onChoose;
            Wire();
            ctx.Events.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            Show(false);
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
                    ? "宝箱！ " + weapon + "   选择一项（1 / 2 / 3）—— 有的选项带代价"
                    : "升级！ Lv " + _ctx.Progression.Level + " · " + weapon + "   选择一项（1 / 2 / 3）";
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
