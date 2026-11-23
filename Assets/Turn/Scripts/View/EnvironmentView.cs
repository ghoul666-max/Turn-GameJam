using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 位面机制的表现（程序A）。由 GameManager 自动挂上并绑定，场景里不需要摆任何东西：
    ///   · 工位困意：全屏变暗（最深 80%）+ 上下两片“眼皮”向中间合拢，进度读 IEnvironmentSystem.Drowsiness；
    ///   · 老板来电：HazardWarnedEvent → 落点红圈 + 电话从上方落下；HazardLandedEvent → 圈闪一下后消失；
    ///     HazardCancelledEvent / 换位面 → 直接清掉。
    /// 只读模拟层状态 + 订阅事件，不做任何玩法判断。
    /// </summary>
    public sealed class EnvironmentView : MonoBehaviour
    {
        /// <summary>困意最深时的变暗程度（策划书：渐暗至 80%）。</summary>
        public const float MaxDarkness = 0.8f;
        /// <summary>每片眼皮最多盖住屏幕高度的比例。</summary>
        public const float MaxEyelid = 0.38f;

        private const int RingSegments = 40;
        private const float RingWidth = 0.08f;
        private const float LandFlashTime = 0.3f;
        private const float PhoneDropHeight = 6f;

        private sealed class PhoneFx
        {
            public GameObject Root;
            public LineRenderer Ring;
            public SpriteRenderer Phone;
            public Vector3 Center;
            public float Radius;
            public float Delay;
            public float Elapsed;
            public float LandTimer = -1f;
            public bool Hit;
        }

        private ITurnContext _ctx;
        private Material _material;
        private Sprite _square;
        private Texture2D _squareTex;
        private Canvas _canvas;
        private Image _dark;
        private RectTransform _topLid;
        private RectTransform _bottomLid;
        private readonly Dictionary<int, PhoneFx> _phones = new Dictionary<int, PhoneFx>();
        private readonly List<int> _finished = new List<int>();

        public void Bind(ITurnContext ctx, Material material)
        {
            Unbind();
            _ctx = ctx;
            _material = material;
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) _material = new Material(shader);
            }
            EnsureOverlay();
            ctx.Events.Subscribe<HazardWarnedEvent>(OnWarned);
            ctx.Events.Subscribe<HazardLandedEvent>(OnLanded);
            ctx.Events.Subscribe<HazardCancelledEvent>(OnCancelled);
            ctx.Events.Subscribe<PlaneEnteredEvent>(OnPlaneEntered);
        }

        public void Unbind()
        {
            if (_ctx != null)
            {
                _ctx.Events.Unsubscribe<HazardWarnedEvent>(OnWarned);
                _ctx.Events.Unsubscribe<HazardLandedEvent>(OnLanded);
                _ctx.Events.Unsubscribe<HazardCancelledEvent>(OnCancelled);
                _ctx.Events.Unsubscribe<PlaneEnteredEvent>(OnPlaneEntered);
                _ctx = null;
            }
            ClearPhones();
            SetDrowsy(0f);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_squareTex != null) Destroy(_squareTex);
        }

        // ---------------- 事件 ----------------

        private void OnPlaneEntered(PlaneEnteredEvent evt)
        {
            ClearPhones();
        }

        private void OnWarned(HazardWarnedEvent evt)
        {
            RemovePhone(evt.Id);
            var fx = new PhoneFx
            {
                Center = ViewMath.ToWorld(evt.Position),
                Radius = evt.Radius,
                Delay = Mathf.Max(0.05f, evt.Delay),
            };
            fx.Root = new GameObject("PhoneHazard_" + evt.Id);
            fx.Root.transform.SetParent(transform, false);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(fx.Root.transform, false);
            fx.Ring = ringGo.AddComponent<LineRenderer>();
            fx.Ring.useWorldSpace = true;
            fx.Ring.loop = true;
            fx.Ring.positionCount = RingSegments;
            fx.Ring.startWidth = RingWidth;
            fx.Ring.endWidth = RingWidth;
            fx.Ring.sortingOrder = ViewMath.VfxOrder;
            if (_material != null) fx.Ring.sharedMaterial = _material;
            SetRing(fx.Ring, fx.Center, fx.Radius);

            var phoneGo = new GameObject("Phone");
            phoneGo.transform.SetParent(fx.Root.transform, false);
            fx.Phone = phoneGo.AddComponent<SpriteRenderer>();
            fx.Phone.sprite = SquareSprite();
            fx.Phone.color = new Color(0.12f, 0.12f, 0.16f, 1f);
            fx.Phone.sortingOrder = ViewMath.VfxOrder + 1;
            if (_material != null) fx.Phone.sharedMaterial = _material;
            phoneGo.transform.localScale = new Vector3(0.7f, 1.1f, 1f);
            phoneGo.transform.position = fx.Center + Vector3.up * PhoneDropHeight;

            _phones[evt.Id] = fx;
        }

        private void OnLanded(HazardLandedEvent evt)
        {
            PhoneFx fx;
            if (!_phones.TryGetValue(evt.Id, out fx)) return;
            fx.LandTimer = 0f;
            fx.Hit = evt.HitPlayer;
            if (fx.Phone != null) fx.Phone.transform.position = fx.Center;
        }

        private void OnCancelled(HazardCancelledEvent evt)
        {
            RemovePhone(evt.Id);
        }

        // ---------------- 每帧 ----------------

        private void LateUpdate()
        {
            if (_ctx == null) return;
            SetDrowsy(_ctx.Plane.Active == PlaneId.Office ? _ctx.Environment.Drowsiness : 0f);

            float dt = Time.deltaTime;
            _finished.Clear();
            foreach (KeyValuePair<int, PhoneFx> pair in _phones)
            {
                PhoneFx fx = pair.Value;
                if (fx.LandTimer >= 0f)
                {
                    fx.LandTimer += dt;
                    float k = Mathf.Clamp01(fx.LandTimer / LandFlashTime);
                    Color flash = fx.Hit ? new Color(1f, 0.2f, 0.2f, 1f - k) : new Color(1f, 1f, 1f, 1f - k);
                    fx.Ring.startColor = flash;
                    fx.Ring.endColor = flash;
                    SetRing(fx.Ring, fx.Center, fx.Radius * (1f + 0.25f * k));
                    if (fx.Phone != null) fx.Phone.color = new Color(0.12f, 0.12f, 0.16f, 1f - k);
                    if (fx.LandTimer >= LandFlashTime) _finished.Add(pair.Key);
                    continue;
                }

                fx.Elapsed += dt;
                float t = Mathf.Clamp01(fx.Elapsed / fx.Delay);
                float pulse = 0.45f + 0.35f * Mathf.Abs(Mathf.Sin(fx.Elapsed * 14f));
                Color warn = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.35f, 1f, t) * pulse + 0.2f);
                fx.Ring.startColor = warn;
                fx.Ring.endColor = warn;
                if (fx.Phone != null) fx.Phone.transform.position = fx.Center + Vector3.up * (PhoneDropHeight * (1f - t * t));
            }
            for (int i = 0; i < _finished.Count; i++) RemovePhone(_finished[i]);
        }

        // ---------------- 困意遮罩 ----------------

        private void EnsureOverlay()
        {
            if (_canvas != null) return;
            var go = new GameObject("DrowsyOverlay");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -5; // 在 HUD 下面：HUD 不被遮住
            _canvas.enabled = false;

            _dark = MakeImage("Dark", go.transform, new Color(0f, 0f, 0f, 0f));
            Stretch(_dark.rectTransform, 0f, 1f);
            _topLid = MakeImage("TopLid", go.transform, new Color(0.03f, 0.02f, 0.03f, 1f)).rectTransform;
            _bottomLid = MakeImage("BottomLid", go.transform, new Color(0.03f, 0.02f, 0.03f, 1f)).rectTransform;
        }

        private void SetDrowsy(float v)
        {
            if (_canvas == null) return;
            v = Mathf.Clamp01(v);
            bool show = v > 0.001f;
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            _dark.color = new Color(0f, 0f, 0f, MaxDarkness * v);
            float lid = MaxEyelid * v * v; // 先慢后快地合上
            Stretch(_topLid, 1f - lid, 1f);
            Stretch(_bottomLid, 0f, lid);
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>横向铺满，纵向占屏幕高度的 [yMin, yMax]。</summary>
        private static void Stretch(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------------- 小工具 ----------------

        private static void SetRing(LineRenderer ring, Vector3 center, float radius)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }

        private Sprite SquareSprite()
        {
            if (_square != null) return _square;
            _squareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            _squareTex.SetPixels32(pixels);
            _squareTex.Apply();
            _squareTex.hideFlags = HideFlags.DontSave;
            _square = Sprite.Create(_squareTex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _square;
        }

        private void RemovePhone(int id)
        {
            PhoneFx fx;
            if (!_phones.TryGetValue(id, out fx)) return;
            if (fx.Root != null) Destroy(fx.Root);
            _phones.Remove(id);
        }

        private void ClearPhones()
        {
            foreach (KeyValuePair<int, PhoneFx> pair in _phones)
            {
                if (pair.Value.Root != null) Destroy(pair.Value.Root);
            }
            _phones.Clear();
        }
    }
}
