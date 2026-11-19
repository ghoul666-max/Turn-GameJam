using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 升级三选一面板（节点 Canvas/UpgradePanel）。LevelUp 阶段显示，点击按钮或按 1/2/3 选择。
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
            bool levelUp = evt.Phase == RunPhase.LevelUp;
            Show(levelUp);
            if (levelUp) Refresh();
        }

        private void Show(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void Refresh()
        {
            IReadOnlyList<UpgradeOption> list = _ctx.Progression.Options;
            if (header != null) header.text = "升级！ Lv " + _ctx.Progression.Level + "   选择一项（1 / 2 / 3）";
            for (int i = 0; i < options.Length; i++)
            {
                OptionView view = options[i];
                if (view == null || view.button == null) continue;
                bool has = i < list.Count;
                view.button.gameObject.SetActive(has);
                if (!has) continue;
                UpgradeOption o = list[i];
                if (view.title != null) view.title.text = (i + 1) + ". " + o.Name + "   Lv " + o.FromLevel + " → " + o.ToLevel;
                if (view.description != null) view.description.text = o.Description;
            }
        }
    }
}
