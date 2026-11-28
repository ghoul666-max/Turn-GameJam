using System;
using Turn.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Turn.View
{
    /// <summary>View-only landscape touch overlay. Movement and roll requests are consumed by GameManager.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MobileControls : MonoBehaviour
    {
        [Tooltip("Enter Play Mode 前设置；运行中切换不会重建已生成的菜单布局。")]
        [SerializeField] private bool simulateInEditor;
        [Header("1920 × 1080 reference layout, inside device safe area")]
        [SerializeField, Min(120f)] private float joystickDiameter = 280f;
        [SerializeField, Min(80f)] private float rollDiameter = 210f;
        [SerializeField, Min(0f)] private float edgeMargin = 64f;
        [SerializeField, Range(0f, .95f)] private float deadzone = .15f;
        [SerializeField, Range(.2f, 1f)] private float opacity = .65f;

        private readonly MobilePointerState _input = new MobilePointerState();
        private Func<bool> _canPlay;
        private Action _pause;
        private ITurnContext _ctx;
        private GameObject _canvasObject, _content;
        private RectTransform _stick, _knob;
        private Button _rollButton;
        private Text _rollText;
        private bool _visible, _ready, _focused = true, _background;
        private string[] _cooldownLabels;
        private int _lastLabel = int.MinValue;
        private bool _ownsSimulation;
        private int _screenWidth, _screenHeight;
        private Rect _safeArea;

        private void Awake() { EnableEditorSimulation(); }

        private void EnableEditorSimulation()
        {
            if (!Application.isEditor || !simulateInEditor || MobilePlatform.SimulateTouch) return;
            MobilePlatform.SimulateTouch = true;
            _ownsSimulation = true;
        }

        public Vector2 Move { get { return Allowed ? new Vector2(_input.X, _input.Y) : Vector2.zero; } }
        private bool Allowed { get { return isActiveAndEnabled && _focused && !_background && MobilePlatform.IsTouchUI && _canPlay != null && _canPlay(); } }

        public void Setup(Func<bool> canPlay, Action pause)
        {
            _canPlay = canPlay;
            _pause = pause;
            EnableEditorSimulation();
            EnsureCanvas();
            ResetInput();
            Refresh();
        }

        public void Bind(ITurnContext ctx)
        {
            _ctx = ctx;
            ResetInput();
            _lastLabel = int.MinValue;
        }

        public bool ConsumeRoll()
        {
            if (!Allowed) { ResetInput(); return false; }
            return _input.ConsumeRoll();
        }

        public void ResetInput()
        {
            _input.Reset();
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }

        private void Update() { Refresh(); }
        private void LateUpdate() { if (!Allowed) { ResetInput(); SetVisible(false); } }
        private void OnDisable() { ResetInput(); SetVisible(false); }
        private void OnApplicationFocus(bool focus) { _focused = focus; if (!focus) { ResetInput(); SetVisible(false); } }
        private void OnApplicationPause(bool paused) { _background = paused; if (paused) { ResetInput(); SetVisible(false); } }
        private void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
            if (_ownsSimulation) MobilePlatform.SimulateTouch = false;
        }

        private void Refresh()
        {
            int width = Screen.width, height = Screen.height;
            Rect safe = Screen.safeArea;
            if (_screenWidth != width || _screenHeight != height || _safeArea != safe)
            {
                _screenWidth = width;
                _screenHeight = height;
                _safeArea = safe;
                ResetInput();
            }
            EnsureCanvas();
            bool allowed = Allowed;
            if (!allowed) ResetInput();
            SetVisible(allowed);
            if (!allowed || _rollButton == null) return;
            _ready = _ctx != null && _ctx.Roll.CanRoll;
            _rollButton.interactable = _ready;
            int label = _ctx == null ? -2 : _ctx.Player.State.InSubspace ? -1
                : _ready ? 0 : Mathf.Clamp(Mathf.CeilToInt(_ctx.Player.State.RollCooldownLeft), 1, _cooldownLabels.Length - 1);
            if (label == _lastLabel) return;
            _lastLabel = label;
            _rollText.text = label == -2 ? "翻滚" : label == -1 ? "翻滚中" : _cooldownLabels[label];
        }

        private void SetVisible(bool visible)
        {
            if (_content == null || _visible == visible) return;
            _visible = visible;
            _content.SetActive(visible);
        }

        private void EnsureCanvas()
        {
            if (_canvasObject != null || !MobilePlatform.IsTouchUI) return;
            MobilePlatform.EnsureEventSystem();
            _canvasObject = new GameObject("Mobile Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _content = new GameObject("Safe Controls", typeof(RectTransform));
            RectTransform safe = (RectTransform)_content.transform;
            safe.SetParent(_canvasObject.transform, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            MobileSafeArea.Attach(safe);

            _stick = Panel("Move Joystick", safe, new Vector2(0f, 0f), new Vector2(edgeMargin, edgeMargin), new Vector2(joystickDiameter, joystickDiameter), new Color(.10f, .14f, .23f, opacity));
            _stick.pivot = Vector2.zero;
            var stickPointer = _stick.gameObject.AddComponent<MobileControlPointer>();
            stickPointer.Owner = this;
            stickPointer.IsStick = true;
            _knob = Panel("Stick Handle", _stick, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * joystickDiameter * .36f, new Color(.75f, .87f, 1f, opacity));
            _knob.GetComponent<Image>().raycastTarget = false;
            AddLabel("移动", _stick, 28);

            RectTransform roll = Panel("Roll", safe, new Vector2(1f, 0f), new Vector2(-edgeMargin, edgeMargin), Vector2.one * rollDiameter, new Color(.15f, .35f, .58f, opacity));
            roll.pivot = new Vector2(1f, 0f);
            _rollButton = roll.gameObject.AddComponent<Button>();
            _rollButton.targetGraphic = roll.GetComponent<Image>();
            var rollPointer = roll.gameObject.AddComponent<MobileControlPointer>();
            rollPointer.Owner = this;
            _rollText = AddLabel("翻滚", roll, 38);
            _cooldownLabels = new string[601];
            _cooldownLabels[0] = "翻滚";
            for (int i = 1; i < _cooldownLabels.Length; i++) _cooldownLabels[i] = "翻滚\n" + i + "s";

            RectTransform pause = Panel("Pause", safe, new Vector2(1f, 1f), new Vector2(-edgeMargin, -edgeMargin), new Vector2(132f, 100f), new Color(.10f, .14f, .23f, opacity));
            pause.pivot = Vector2.one;
            Button pauseButton = pause.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pause.GetComponent<Image>();
            pauseButton.onClick.AddListener(PausePressed);
            AddLabel("暂停", pause, 32);
            _visible = true;
            SetVisible(false);
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text AddLabel(string value, RectTransform parent, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = go.GetComponent<Text>();
            text.font = UIFont.Chinese;
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        internal void PointerDown(PointerEventData data, bool stick)
        {
            if (!Allowed) { ResetInput(); return; }
            if (stick)
            {
                if (_input.BeginStick(data.pointerId)) Drag(data);
            }
            else _input.BeginRoll(data.pointerId, _ctx != null && _ctx.Roll.CanRoll);
        }

        internal void Drag(PointerEventData data)
        {
            if (!Allowed) { ResetInput(); return; }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_stick, data.position, data.pressEventCamera, out local)) return;
            Vector2 offset = local - _stick.rect.center;
            float radius = Mathf.Max(1f, _stick.rect.width * .5f);
            if (_input.DragStick(data.pointerId, offset.x / radius, offset.y / radius, deadzone))
                _knob.anchoredPosition = Vector2.ClampMagnitude(offset, radius);
        }

        internal void PointerUp(PointerEventData data, bool stick)
        {
            if (stick)
            {
                if (_input.EndStick(data.pointerId) && _knob != null) _knob.anchoredPosition = Vector2.zero;
            }
            else _input.EndRoll(data.pointerId);
        }

        private void PausePressed()
        {
            if (!Allowed) return;
            ResetInput();
            if (_pause != null) _pause();
            if (!Allowed) SetVisible(false);
        }
    }

    /// <summary>EventSystem routes drag/up to the initiating control, independently for each finger.</summary>
    public sealed class MobileControlPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        internal MobileControls Owner;
        internal bool IsStick;
        public void OnPointerDown(PointerEventData data) { if (Owner != null) Owner.PointerDown(data, IsStick); }
        public void OnPointerUp(PointerEventData data) { if (Owner != null) Owner.PointerUp(data, IsStick); }
        public void OnDrag(PointerEventData data) { if (IsStick && Owner != null) Owner.Drag(data); }
    }
}
