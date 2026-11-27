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
        [UnityEngine.Range(0f, 70f)] public float pitch = 45f;
        [Tooltip("透视投影（近大远小）。关 = 正交：倾斜后地面只在纵向压扁，不会近大远小")]
        public bool perspective = true;
        [Tooltip("正交：画面半高（世界单位，原来是 7）。越大看得越多")]
        [Min(1f)] public float orthographicSize = 7f;
        [Tooltip("透视：竖直视角（度）")]
        [UnityEngine.Range(10f, 90f)] public float fieldOfView = 42f;
        [Tooltip("相机到注视点的距离（= 相机高度 / 镜头远近）。透视时决定画面大小；正交时只影响裁剪，不影响画面")]
        [Min(1f)] public float distance = 10f;
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
        [Tooltip("硬跟随：玩家脚点永远在画面正中、没有延迟（原作做法）。关 = 按「跟随灵敏度」平滑跟随")]
        public bool hardFollow = true;

        [Header("纸片角色（B 档，只在倾斜且立起来时生效）")]
        [Tooltip("竖直站立（原作做法）：玩家 / 怪物像立牌一样垂直于地面。关 = 整张图正对镜头（11/26 的做法）")]
        public bool vertical = true;
        [Tooltip("竖直站立时竖向拉长 1 / sin(倾斜角)，抵消俯视造成的压扁（45° 时 = √2）。倾斜角很小时最多拉长 3 倍")]
        public bool stretch = true;
        [Tooltip("自动脚底：贴图底边在锚点以下（白模圆心锚点的图）时整体抬起，让底边落在地面上；手绘图锚点放在脚底时不变")]
        public bool autoFoot = true;
        [Tooltip("纸片翻面：转身时绕竖轴转过去（正侧面变成一条线），代替左右镜像")]
        public bool paperTurn = true;
        [Tooltip("玩家转身速度（度 / 秒，原作 270）")]
        public float playerTurnSpeed = 270f;
        [Tooltip("怪物转身速度（度 / 秒，原作 180）")]
        public float monsterTurnSpeed = 180f;
        [Tooltip("斜向移动时多转的角度（度，原作 30）：往右上走时纸片往画面里侧转一点")]
        public float turnAngle = 30f;
        [Tooltip("玩家：移动方向的水平分量（单位向量的 |x|）落在 [下限, 上限] 之间算斜向，原作 0.5 ~ 0.8")]
        public float playerDiagonalMin = 0.5f;
        public float playerDiagonalMax = 0.8f;
        [Tooltip("怪物：同上，原作 0.2 ~ 0.7")]
        public float monsterDiagonalMin = 0.2f;
        public float monsterDiagonalMax = 0.7f;

        [Header("脚下阴影（倾斜时）")]
        [Tooltip("剪影投影：把角色当前帧染黑、按光照方向斜铺在地面上，翻面时跟着变窄")]
        public bool silhouetteShadow = true;
        [Tooltip("原来的圆形脚底阴影（倾斜角 = 0 时总是显示）")]
        public bool blobShadow = false;
        [Tooltip("剪影往哪个方向倒（度，地面上从 +X 逆时针量；90 = 往画面里侧）")]
        public float shadowDirection = 60f;
        [Tooltip("剪影长度 = 角色高度 × 这个系数（相当于太阳越低越长）")]
        public float shadowLength = 0.6f;
        [UnityEngine.Range(0f, 1f)] public float shadowAlpha = 0.35f;

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

        /// <summary>立起时是否竖直站立（垂直于地面）；否则正对镜头。</summary>
        public static bool Vertical { get; private set; }
        /// <summary>竖直站立时的竖向拉伸（1 / sin(倾斜角)，最多 3）；其他情况为 1。</summary>
        public static float Stretch { get; private set; } = 1f;
        /// <summary>身体（纸片）相对 <see cref="Upright"/> 的额外倾斜：竖直站立时把正对镜头的牌再立直。</summary>
        public static Quaternion BodyTilt { get; private set; } = Quaternion.identity;
        /// <summary>竖直站立 / 正对镜头时身体的世界旋转（静态立起物体直接用它）。</summary>
        public static Quaternion Stand { get; private set; } = Quaternion.identity;
        /// <summary>立起时是否自动把贴图底边放到脚底。</summary>
        public static bool AutoFoot { get; private set; } = true;
        /// <summary>立起姿态（Standing / Stand / Stretch）每变一次加 1，静态立起物体靠它判断要不要重算。</summary>
        public static int Revision { get; private set; }
        /// <summary>纸片翻面是否生效（倾斜、立起来且打开了翻面）。不生效时各控制器照旧用 flipX。</summary>
        public static bool PaperTurn { get; private set; }
        public static float PlayerTurnSpeed { get; private set; } = 270f;
        public static float MonsterTurnSpeed { get; private set; } = 180f;
        public static float TurnAngle { get; private set; } = 30f;
        public static Vector2 PlayerDiagonal { get; private set; } = new Vector2(0.5f, 0.8f);
        public static Vector2 MonsterDiagonal { get; private set; } = new Vector2(0.2f, 0.7f);
        /// <summary>是否显示剪影投影（只在立起时）。</summary>
        public static bool Silhouette { get; private set; }
        /// <summary>是否显示原来的圆形脚底阴影（不立起时总是显示）。</summary>
        public static bool Blob { get; private set; } = true;
        /// <summary>剪影倒向（地面上的单位向量）。</summary>
        public static Vector2 ShadowDir { get; private set; } = new Vector2(0.5f, 0.866f);
        public static float ShadowLength { get; private set; } = 0.6f;
        public static float ShadowAlpha { get; private set; } = 0.35f;

        internal static void Set(CameraRigSettings s)
        {
            bool wasStanding = Standing;
            float wasStretch = Stretch;
            bool wasAutoFoot = AutoFoot;
            Pitch = s.pitch;
            Standing = s.standUp && s.pitch > 0.5f;
            Upright = Standing ? Quaternion.Euler(-s.pitch, 0f, 0f) : Quaternion.identity;
            Lift = Standing ? s.standLift : 0f;
            DepthSort = Standing && s.depthSort;

            Vertical = Standing && s.vertical;
            float sin = Mathf.Sin(Mathf.Clamp(s.pitch, 0.5f, 89.5f) * Mathf.Deg2Rad);
            Stretch = Vertical && s.stretch ? Mathf.Min(3f, 1f / sin) : 1f;
            BodyTilt = Vertical ? Quaternion.Euler(-(90f - s.pitch), 0f, 0f) : Quaternion.identity;
            Quaternion stand = Upright * BodyTilt;
            if (stand != Stand || wasStanding != Standing || Mathf.Abs(wasStretch - Stretch) > 1e-5f || wasAutoFoot != (Standing && s.autoFoot)) Revision++;
            Stand = stand;
            AutoFoot = Standing && s.autoFoot;
            PaperTurn = Standing && s.paperTurn;
            PlayerTurnSpeed = Mathf.Max(1f, s.playerTurnSpeed);
            MonsterTurnSpeed = Mathf.Max(1f, s.monsterTurnSpeed);
            TurnAngle = s.turnAngle;
            PlayerDiagonal = new Vector2(s.playerDiagonalMin, s.playerDiagonalMax);
            MonsterDiagonal = new Vector2(s.monsterDiagonalMin, s.monsterDiagonalMax);
            Silhouette = Standing && s.silhouetteShadow;
            Blob = !Standing || s.blobShadow;
            float a = s.shadowDirection * Mathf.Deg2Rad;
            ShadowDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            ShadowLength = Mathf.Max(0f, s.shadowLength);
            ShadowAlpha = Mathf.Clamp01(s.shadowAlpha);
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
    /// 默认值（11/27 起）照搬《通神榜》：透视 42°、俯角 45°、距离 10、硬跟随，角色竖直站立并竖向拉长 √2。
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
                _focus = _hasFocus && !s.hardFollow ? Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-s.sharpness * dt)) : goal;
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
