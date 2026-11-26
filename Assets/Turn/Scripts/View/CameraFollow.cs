using System;
using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 相机参数（F1 面板「相机 / 2.5D 视角」，只影响画面，不影响任何玩法判定）。
    /// 倾斜角 = 0 时和原来的正俯视画面完全一样。
    /// </summary>
    [Serializable]
    public sealed class CameraRigSettings
    {
        [Tooltip("数据版本。F1 面板的旧存档里没有这一组时读出来是 0，会换成默认值。")]
        [HideInInspector] public int version = 1;

        [Tooltip("倾斜角（度）：0 = 正俯视（原来的画面），越大越像斜着看地面的 2.5D")]
        [UnityEngine.Range(0f, 70f)] public float pitch = 0f;
        [Tooltip("透视投影（近大远小）。关 = 正交：倾斜后地面只在纵向压扁，不会近大远小")]
        public bool perspective = false;
        [Tooltip("正交：画面半高（世界单位，原来是 7）。越大看得越多")]
        [Min(1f)] public float orthographicSize = 7f;
        [Tooltip("透视：竖直视角（度）")]
        [UnityEngine.Range(10f, 90f)] public float fieldOfView = 40f;
        [Tooltip("相机到注视点的距离（= 相机高度 / 镜头远近）。透视时决定画面大小；正交时只影响裁剪，不影响画面")]
        [Min(1f)] public float distance = 20f;
        [Tooltip("注视点相对玩家往地图上方（+Y）偏移：倾斜后让玩家落在画面偏下、多看一点前方")]
        public float lookAhead = 0f;
        [Tooltip("倾斜时玩家 / 怪物立起来面向镜头（关 = 和地面一样平躺，会被压扁）")]
        public bool standUp = true;
        [Tooltip("立起来的玩家 / 怪物离地高度（世界单位）。0 = 贴图中心就在真实位置；脚下阴影始终留在真实位置")]
        public float standLift = 0f;
        [Tooltip("倾斜时玩家 / 怪物按前后遮挡（画面靠下的挡住靠上的）。关 = 和原来一样玩家永远盖在怪物上面")]
        public bool depthSort = true;
        [Tooltip("跟随灵敏度（越大越紧）")]
        [Min(0.1f)] public float sharpness = 8f;

        public CameraRigSettings Clone()
        {
            return JsonUtility.FromJson<CameraRigSettings>(JsonUtility.ToJson(this));
        }
    }

    /// <summary>
    /// 当前相机姿态（CameraFollow 每帧写，表现层读）：立起来的物体用 <see cref="Upright"/> 作为世界旋转。
    /// </summary>
    public static class CameraRig
    {
        /// <summary>倾斜角（度）。</summary>
        public static float Pitch { get; private set; }
        /// <summary>立起来的物体（玩家 / 怪物 / 头顶文字）的世界旋转；不立起时为 identity。</summary>
        public static Quaternion Upright { get; private set; } = Quaternion.identity;
        /// <summary>当前是否让玩家 / 怪物立起来（倾斜角 &gt; 0.5° 且打开了「立起来」）。</summary>
        public static bool Standing { get; private set; }
        /// <summary>立起物体离地高度（世界单位，沿 −Z 抬起）。</summary>
        public static float Lift { get; private set; }
        /// <summary>立起时是否按前后遮挡排序。</summary>
        public static bool DepthSort { get; private set; }

        internal static void Set(CameraRigSettings s)
        {
            Pitch = s.pitch;
            Standing = s.standUp && s.pitch > 0.5f;
            Upright = Standing ? Quaternion.Euler(-s.pitch, 0f, 0f) : Quaternion.identity;
            Lift = Standing ? s.standLift : 0f;
            DepthSort = Standing && s.depthSort;
        }

        /// <summary>地面上一点「头顶上方」h 处的世界坐标（倾斜时沿立起方向往上，正俯视时就是 +Y）。</summary>
        public static Vector3 Above(Vector3 ground, float h)
        {
            if (!Standing) return ground + new Vector3(0f, h, 0f);
            return ground + Upright * new Vector3(0f, h, 0f) + new Vector3(0f, 0f, -Lift);
        }
    }

    /// <summary>
    /// 相机跟随玩家，并把视野限制在 WorldBounds 内（地图比屏幕大时不露出边界外）。
    /// 支持 2.5D：相机绕 X 轴倾斜 pitch 度斜看 Z = 0 的地面（正交 / 透视可选），
    /// 玩家 / 怪物由 <see cref="UprightView"/> 立起来面向镜头。玩法坐标全在模拟层，相机怎么转都不影响判定。
    /// 参数来源优先级：F1 面板打开时的实时预览 → F1 存档（开局 ApplySettings）→ Inspector 上这一份。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // 在 PlayerController.LateUpdate 同步完玩家位置之后再跟随
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private WorldBounds bounds;
        [SerializeField] private CameraRigSettings settings = new CameraRigSettings();

        private Camera _camera;
        private CameraRigSettings _applied;
        private Vector3 _focus;
        private bool _hasFocus;

        /// <summary>F1 面板打开时每帧写入当前编辑中的参数（实时预览）；关闭面板时置空。</summary>
        public static CameraRigSettings Preview { get; set; }

        /// <summary>Inspector 上这一份（F1 面板「从 Inspector 读取」用）。</summary>
        public CameraRigSettings Settings { get { return settings; } }

        private CameraRigSettings Current
        {
            get { return Valid(Preview) ?? _applied ?? Valid(settings) ?? (settings = new CameraRigSettings()); }
        }

        private static CameraRigSettings Valid(CameraRigSettings s)
        {
            return s != null && s.version > 0 ? s : null;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyProjection(Current);
        }

        /// <summary>开局时用 F1 存档里的相机参数；传 null = 用 Inspector 上的参数。</summary>
        public void ApplySettings(CameraRigSettings s)
        {
            _applied = Valid(s) != null ? s.Clone() : null;
        }

        public void SnapToTarget()
        {
            CameraRigSettings s = Current;
            ApplyProjection(s);
            if (target == null) return;
            _focus = Clamp(Goal(s), s);
            _hasFocus = true;
            Place(s);
        }

        private void LateUpdate()
        {
            CameraRigSettings s = Current;
            ApplyProjection(s);
            if (target != null)
            {
                Vector3 goal = Clamp(Goal(s), s);
                // 游戏暂停（F1 面板）时也要能预览：用现实时间
                float dt = Time.timeScale > 0.01f ? Time.deltaTime : Time.unscaledDeltaTime;
                _focus = _hasFocus ? Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-s.sharpness * dt)) : goal;
                _hasFocus = true;
            }
            Place(s);
        }

        private Vector3 Goal(CameraRigSettings s)
        {
            Vector3 p = target.position;
            return new Vector3(p.x, p.y + s.lookAhead, 0f);
        }

        private void ApplyProjection(CameraRigSettings s)
        {
            CameraRig.Set(s);
            if (_camera == null) return;
            _camera.orthographic = !s.perspective;
            _camera.orthographicSize = Mathf.Max(1f, s.orthographicSize);
            _camera.fieldOfView = Mathf.Clamp(s.fieldOfView, 10f, 90f);
            float far = Mathf.Max(1f, s.distance) * 6f + 100f;
            if (_camera.farClipPlane < far) _camera.farClipPlane = far;
            if (_camera.nearClipPlane > 0.1f) _camera.nearClipPlane = 0.1f;
        }

        /// <summary>相机放在注视点（地面）沿视线往后 distance 处。pitch = 0 时就是原来的 (x, y, −distance)。</summary>
        private void Place(CameraRigSettings s)
        {
            Quaternion rot = Quaternion.Euler(-Mathf.Clamp(s.pitch, 0f, 89f), 0f, 0f);
            transform.rotation = rot;
            transform.position = _focus - rot * Vector3.forward * Mathf.Max(1f, s.distance);
        }

        /// <summary>
        /// 屏幕在地面上的投影（相对注视点）：左右半宽、往下 / 往上能看到多远。
        /// 透视时画面上边沿接近水平（看不到地面）就不限制上边界。
        /// </summary>
        private void Footprint(CameraRigSettings s, out float halfW, out float below, out float above)
        {
            float aspect = _camera != null ? _camera.aspect : 16f / 9f;
            float pitch = Mathf.Clamp(s.pitch, 0f, 89f) * Mathf.Deg2Rad;
            if (!s.perspective)
            {
                float size = Mathf.Max(1f, s.orthographicSize);
                halfW = size * aspect;
                below = above = size / Mathf.Cos(pitch);
                return;
            }

            float d = Mathf.Max(1f, s.distance);
            float a = Mathf.Clamp(s.fieldOfView, 10f, 90f) * 0.5f * Mathf.Deg2Rad;
            float h = d * Mathf.Cos(pitch); // 相机离地高度
            below = h * (Mathf.Tan(pitch) - Mathf.Tan(pitch - a));
            float top = pitch + a;
            if (top < 85f * Mathf.Deg2Rad)
            {
                above = h * (Mathf.Tan(top) - Mathf.Tan(pitch));
                // 画面上边沿那条地面线离相机最远、最宽：按它算左右半宽（保守，不会露出左右边界）
                float depth = h / Mathf.Cos(top) * Mathf.Cos(a);
                halfW = depth * Mathf.Tan(a) * aspect;
            }
            else
            {
                above = 0f;
                halfW = d * Mathf.Tan(a) * aspect;
            }
        }

        private Vector3 Clamp(Vector3 p, CameraRigSettings s)
        {
            if (bounds == null) return p;
            float halfW, below, above;
            Footprint(s, out halfW, out below, out above);
            Rect r = bounds.Rect;
            float x = r.width <= halfW * 2f ? r.center.x : Mathf.Clamp(p.x, r.xMin + halfW, r.xMax - halfW);
            float y;
            if (r.height <= below + above) y = r.center.y - (above - below) * 0.5f;
            else y = Mathf.Clamp(p.y, r.yMin + below, r.yMax - above);
            return new Vector3(x, y, 0f);
        }
    }
}
