using System;
using UnityEngine;
using UnityEngine.UI;
using Survivor.Art;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>
    /// 升级面板里的一张选项卡：图标（按格摆动）、名称、“新！/ Lv x → y”、说明。
    /// 边框颜色按种类区分：新武器金、武器升级蓝、被动绿、回复红。
    /// </summary>
    public sealed class UpgradeCardView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(260f, 340f);

        [SerializeField] private Image _frame;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _tag;
        [SerializeField] private Text _description;
        [SerializeField] private Text _hotkey;
        [SerializeField] private StepAnimator _iconAnim;

        private Action _onClick;
        private bool _wired;

        public static GameObject BuildTemplate()
        {
            RectTransform root = UIKit.CreateRect(PrefabLibrary.UpgradeCard, null, Vector2.zero, Size);
            var view = root.gameObject.AddComponent<UpgradeCardView>();

            view._frame = root.gameObject.AddComponent<Image>();
            view._frame.sprite = SurvivorArt.Get(SurvivorArt.Panel);
            view._frame.color = Color.white;
            // 点击回调在 Show() 里按需挂上：运行时添加的监听器存不进 Prefab。
            view._button = UIKit.MakeButton(view._frame, null);

            Image iconBack = UIKit.CreateImage("IconBack", root, new Vector2(0f, 82f), new Vector2(120f, 120f),
                SurvivorArt.Get(SurvivorArt.Slot), new Color(1f, 1f, 1f, 0.9f));
            view._icon = UIKit.CreateImage("Icon", iconBack.rectTransform, Vector2.zero, new Vector2(84f, 84f),
                SurvivorArt.Get(SurvivorArt.IconWhip), Color.white);
            view._icon.preserveAspect = true;
            view._iconAnim = view._icon.gameObject.AddComponent<StepAnimator>();
            view._iconAnim.Setup(null, view._icon, 5f, 0f);

            view._name = UIKit.CreateText("Name", root, new Vector2(0f, -8f), new Vector2(230f, 40f), "武器",
                28, TextAnchor.MiddleCenter, new Color(0.88f, 0.88f, 0.88f, 1f));
            view._name.fontStyle = FontStyle.Bold;
            view._tag = UIKit.CreateText("Tag", root, new Vector2(0f, -44f), new Vector2(230f, 30f), "新！",
                20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f, 1f));
            view._description = UIKit.CreateText("Description", root, new Vector2(0f, -102f), new Vector2(220f, 80f), "",
                18, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.82f, 1f));
            view._hotkey = UIKit.CreateText("Hotkey", root, new Vector2(-104f, 146f), new Vector2(40f, 36f), "1",
                22, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.7f, 1f));
            return root.gameObject;
        }

        public void Show(UpgradeOption option, int index, Action onClick)
        {
            if (!_wired)
            {
                _button.onClick.AddListener(HandleClick);
                _wired = true;
            }
            _onClick = onClick;
            gameObject.SetActive(true);

            _icon.sprite = SurvivorArt.Get(option.ArtKey);
            _iconAnim.SetFrames(new Sprite[] { _icon.sprite });
            _name.text = option.Name;
            _description.text = option.Description;
            _hotkey.text = (index + 1).ToString();

            switch (option.Kind)
            {
                case UpgradeKind.NewWeapon:
                    _tag.text = "新武器！";
                    _frame.color = new Color(1f, 0.82f, 0.4f, 1f);
                    break;
                case UpgradeKind.WeaponLevel:
                    _tag.text = "Lv " + option.FromLevel + " → " + option.ToLevel;
                    _frame.color = new Color(0.55f, 0.7f, 1f, 1f);
                    break;
                case UpgradeKind.NewPassive:
                    _tag.text = "新道具！";
                    _frame.color = new Color(0.55f, 0.95f, 0.6f, 1f);
                    break;
                case UpgradeKind.PassiveLevel:
                    _tag.text = "Lv " + option.FromLevel + " → " + option.ToLevel;
                    _frame.color = new Color(0.55f, 0.95f, 0.6f, 1f);
                    break;
                default:
                    _tag.text = "回复";
                    _frame.color = new Color(1f, 0.5f, 0.5f, 1f);
                    break;
            }
        }

        public void Hide()
        {
            _onClick = null;
            gameObject.SetActive(false);
        }

        private void HandleClick()
        {
            Action click = _onClick;
            if (click != null) click();
        }
    }
}
