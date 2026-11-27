using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Turn.View
{
    /// <summary>谁在用 UprightView（决定转身速度、斜向阈值用玩家还是怪物那一套）。</summary>
    public enum UprightRole
    {
        Monster = 0,
        Player = 1,
    }

    /// <summary>
    /// 2.5D：倾斜相机时让玩家 / 怪物立起来（纸片角色）。PlayerController / MonsterController 自动挂上。
    ///
    /// 层级（运行时自动建，局部坐标不变）：
    ///   根节点（地面上的真实位置，阴影 / 箭头 / 护盾圈 / 武器 / 剪影投影留在这一层，平铺在地面）
    ///   └ Upright（正对镜头：血条、头顶印记文字这类「看板」留在这一层，不翻面、不拉伸）
    ///       └ Paper（身体 / 贴图：竖直站立 + 竖向拉伸 √2 + 纸片翻面，参数见 <see cref="CameraRig"/>）
    ///
    /// 纸片翻面：控制器每帧 <see cref="SetFacing"/> 告诉它往哪走；只有水平分量决定朝左 / 朝右（没有水平移动时沿用上次），
    /// 斜向（单位方向的 |x| 落在斜向阈值区间）多转 <see cref="CameraRig.TurnAngle"/> 度；按转身速度匀速转过去，
    /// 转到 90° 时纸片正好侧对镜头（变成一条线）。关掉翻面或倾斜角 = 0 时 Paper 不转，控制器照旧用 flipX。
    ///
    /// 剪影投影：取 Paper 里面积最大的那张图，每帧复制当前帧，染黑后按「贴图 → 地面」的投影铺在地面上
    /// （横向跟着翻面变窄、纵向沿光照方向倒下）。投影是任意 2×2 线性变换，用两层 Transform（旋转·缩放·旋转）表示。
    ///
    /// 自动脚底：贴图底边在脚底以下（白模圆心锚点的图）时，整体抬起到底边落在地面；锚点本来就在脚底的手绘图不变。
    /// 前后遮挡：倾斜时给 Upright 打开 SortingGroup（统一 sortingOrder = MonsterOrder），由相机距离决定前后。
    /// 倾斜角 = 0 时所有这些都关闭，和原来的正俯视完全一样。
    /// </summary>
    [DefaultExecutionOrder(200)] // 在控制器写完子物体动画、CameraFollow 更新完姿态之后
    public sealed class UprightView : MonoBehaviour
    {
        public const string PivotName = "Upright";
        public const string BodyName = "Paper";
        public const string SilhouetteName = "SilhouetteShadow";

        /// <summary>留在地面上、不跟着立起来的子物体名字。新加的贴地子物体要加进来。</summary>
        private static readonly HashSet<string> GroundChildren = new HashSet<string>
        {
            "Shadow", "FootShadow", "TerrainMarker", "FacingArrow", "ShieldRing", "Weapon", SilhouetteName,
        };

        /// <summary>原来的圆形脚底阴影（打开剪影投影时可以隐藏）。</summary>
        private static readonly HashSet<string> BlobShadowNames = new HashSet<string> { "Shadow", "FootShadow" };

        /// <summary>立起来但只正对镜头、不翻面不拉伸的子物体（血条、头顶文字）。</summary>
        private static readonly HashSet<string> BillboardChildren = new HashSet<string> { "HpBar", "Marks" };

        /// <summary>挑剪影来源时跳过的部件（受击闪白、眼睛、燃烧标记这类叠加小图）。</summary>
        private static readonly HashSet<string> ShadowSkip = new HashSet<string> { "HitFlash", "Eye", "BurnMark" };

        private Transform _pivot;
        private Transform _body;
        private SortingGroup _group;
        private int _childCount = -1;
        private int _pivotChildCount = -1;
        private UprightRole _role;

        // 纸片翻面
        private float _yaw;
        private float _targetYaw;
        private int _side = 1;
        private bool _hasFacing;

        // 圆形阴影
        private readonly List<SpriteRenderer> _blobs = new List<SpriteRenderer>();
        private int _blobShown = -1;

        // 剪影投影
        private Transform _shadowRoot;
        private Transform _shadowSpriteNode;
        private SpriteRenderer _shadowRenderer;
        private SpriteRenderer _source;
        private Sprite _sourceSprite;
        private float _sourceTimer;
        private float _footOffset;
        private bool _measuredAutoFoot;
        private readonly List<SpriteRenderer> _tmp = new List<SpriteRenderer>();

        /// <summary>给一个根节点挂上（已经有就直接返回）。默认按怪物处理。</summary>
        public static UprightView Ensure(GameObject go)
        {
            return Ensure(go, UprightRole.Monster);
        }

        /// <summary>给一个根节点挂上（已经有就直接返回），并重置朝向（对象池复用时也调这个）。</summary>
        public static UprightView Ensure(GameObject go, UprightRole role)
        {
            if (go == null) return null;
            UprightView u = go.GetComponent<UprightView>();
            if (u == null) u = go.AddComponent<UprightView>();
            u._role = role;
            u.EnsurePivot();
            u.Collect();
            u.ResetFacing();
            return u;
        }

        /// <summary>纸片翻面后正在显示背面（转过了 90°）：身体的局部旋转在屏幕上是镜像的。</summary>
        public bool ShowsBack
        {
            get { return CameraRig.PaperTurn && Mathf.Abs(Mathf.DeltaAngle(_yaw, 0f)) > 90f; }
        }

        /// <summary>忘掉上次朝向：下一次 <see cref="SetFacing"/> 直接转到位，不播转身。</summary>
        public void ResetFacing()
        {
            _hasFacing = false;
            _side = 1;
            _yaw = 0f;
            _targetYaw = 0f;
        }

        /// <summary>
        /// 告诉纸片往哪个方向走（地面坐标，不必是单位向量）。水平分量 |x| &gt; 0.05 时决定朝右 / 朝左，否则沿用上次；
        /// 方向接近 0 时什么都不改。斜向时多转 TurnAngle：往右上走 → 0 − 30°，往左上走 → 180 + 30°。
        /// </summary>
        public void SetFacing(Vector2 dir)
        {
            float m = dir.magnitude;
            if (m < 0.05f) return;
            if (Mathf.Abs(dir.x) > 0.05f) _side = dir.x < 0f ? -1 : 1;

            float target = _side > 0 ? 0f : 180f;
            float nx = Mathf.Abs(dir.x) / m;
            float ny = dir.y / m;
            Vector2 band = _role == UprightRole.Player ? CameraRig.PlayerDiagonal : CameraRig.MonsterDiagonal;
            if (nx >= band.x && nx <= band.y && Mathf.Abs(ny) > 0.01f)
                target -= _side * Mathf.Sign(ny) * CameraRig.TurnAngle;

            _targetYaw = target;
            if (!_hasFacing)
            {
                _yaw = target;
                _hasFacing = true;
            }
        }

        private void EnsurePivot()
        {
            if (_pivot == null)
            {
                Transform existing = transform.Find(PivotName);
                if (existing != null) _pivot = existing;
                else
                {
                    var go = new GameObject(PivotName);
                    _pivot = go.transform;
                    _pivot.SetParent(transform, false);
                }
                _group = _pivot.GetComponent<SortingGroup>();
                if (_group == null) _group = _pivot.gameObject.AddComponent<SortingGroup>();
                _group.sortingOrder = ViewMath.MonsterOrder;
                _group.enabled = false;
            }
            if (_body == null)
            {
                Transform existing = _pivot.Find(BodyName);
                if (existing != null) _body = existing;
                else
                {
                    var go = new GameObject(BodyName);
                    _body = go.transform;
                    _body.SetParent(_pivot, false);
                }
            }
        }

        /// <summary>把根节点下新出现的子物体分到地面 / Upright / Paper（局部坐标原样保留）。</summary>
        private void Collect()
        {
            if (_pivot == null || _body == null) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c == _pivot || GroundChildren.Contains(c.name)) continue;
                c.SetParent(BillboardChildren.Contains(c.name) ? _pivot : _body, false);
            }
            // 11/26 版本已经收进 Upright 的身体部件，挪进 Paper
            for (int i = _pivot.childCount - 1; i >= 0; i--)
            {
                Transform c = _pivot.GetChild(i);
                if (c == _body || BillboardChildren.Contains(c.name)) continue;
                c.SetParent(_body, false);
            }
            _childCount = transform.childCount;
            _pivotChildCount = _pivot.childCount;
            CacheBlobs();
            _sourceTimer = 0f; // 部件变了：重新挑剪影来源
        }

        private void CacheBlobs()
        {
            _blobs.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (!BlobShadowNames.Contains(c.name)) continue;
                var r = c.GetComponent<SpriteRenderer>();
                if (r != null) _blobs.Add(r);
            }
            _blobShown = -1;
        }

        private void LateUpdate()
        {
            EnsurePivot();
            if (transform.childCount != _childCount || _pivot.childCount != _pivotChildCount) Collect();

            float dt = Time.deltaTime;
            bool paper = CameraRig.PaperTurn;
            if (paper)
            {
                float speed = _role == UprightRole.Player ? CameraRig.PlayerTurnSpeed : CameraRig.MonsterTurnSpeed;
                _yaw = Mathf.MoveTowardsAngle(_yaw, _targetYaw, speed * dt);
            }
            else _yaw = _targetYaw; // 关着时不播转身；再打开时直接在目标朝向

            if (_sourceTimer > 0f) _sourceTimer -= dt;
            SpriteRenderer source = CameraRig.Standing || CameraRig.Silhouette ? PickSource() : null;

            if (CameraRig.Standing)
            {
                _pivot.rotation = CameraRig.Upright;
                _body.localRotation = CameraRig.BodyTilt * (paper ? Quaternion.Euler(0f, _yaw, 0f) : Quaternion.identity);
                _body.localScale = new Vector3(1f, CameraRig.Stretch, 1f);
                _body.localPosition = Vector3.zero;

                // 离地高度（沿 −Z）+ 自动脚底（沿正对镜头的上方，屏幕上抬起的高度 = 贴图底边到锚点的距离）
                float k = CameraRig.Vertical ? CameraRig.Stretch * Mathf.Sin(CameraRig.Pitch * Mathf.Deg2Rad) : 1f;
                Vector3 lift = transform.InverseTransformVector(new Vector3(0f, 0f, -CameraRig.Lift));
                _pivot.localPosition = lift + CameraRig.Upright * new Vector3(0f, _footOffset * k, 0f);
            }
            else
            {
                _pivot.localRotation = Quaternion.identity;
                _pivot.localPosition = Vector3.zero;
                _body.localRotation = Quaternion.identity;
                _body.localScale = Vector3.one;
                _body.localPosition = Vector3.zero;
            }

            bool group = CameraRig.DepthSort;
            if (_group.enabled != group) _group.enabled = group;

            int blob = CameraRig.Blob ? 1 : 0;
            if (blob != _blobShown)
            {
                _blobShown = blob;
                for (int i = 0; i < _blobs.Count; i++)
                    if (_blobs[i] != null) _blobs[i].enabled = blob == 1;
            }

            UpdateSilhouette(source);
        }

        // ───────────────────────── 剪影投影 ─────────────────────────

        /// <summary>Paper 里面积最大、正在显示的那张图（每 0.5 秒或部件变化时重挑）。</summary>
        private SpriteRenderer PickSource()
        {
            bool valid = _source != null && _source.enabled && _source.gameObject.activeInHierarchy && _source.sprite != null;
            if (valid && _sourceTimer > 0f)
            {
                if (_source.sprite != _sourceSprite || _measuredAutoFoot != CameraRig.AutoFoot) MeasureFoot();
                return _source;
            }
            _sourceTimer = 0.5f;

            SpriteRenderer best = null;
            float bestArea = 0f;
            _body.GetComponentsInChildren(false, _tmp);
            for (int i = 0; i < _tmp.Count; i++)
            {
                SpriteRenderer r = _tmp[i];
                if (r == null || !r.enabled || r.sprite == null || r == _shadowRenderer || ShadowSkip.Contains(r.name)) continue;
                Vector3 size = r.sprite.bounds.size;
                Vector3 ls = r.transform.lossyScale;
                float area = Mathf.Abs(size.x * ls.x * size.y * ls.y);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = r;
                }
            }
            _tmp.Clear();
            if (best != _source || (best != null && best.sprite != _sourceSprite) || _measuredAutoFoot != CameraRig.AutoFoot)
            {
                _source = best;
                MeasureFoot();
            }
            return _source;
        }

        /// <summary>
        /// 来源图的底边在 Paper 局部坐标里的高度（只算平移和缩放，不受翻滚旋转影响）；在脚底以下时记为要抬起的量。
        /// 只在换图 / 换来源时量一次，避免呼吸、上下漂浮让脚底跟着抖。
        /// </summary>
        private void MeasureFoot()
        {
            _sourceSprite = _source != null ? _source.sprite : null;
            _measuredAutoFoot = CameraRig.AutoFoot;
            _footOffset = 0f;
            if (_source == null || _sourceSprite == null || !CameraRig.AutoFoot) return;
            Matrix4x4 m = _body.worldToLocalMatrix * _source.transform.localToWorldMatrix;
            float sy = new Vector2(m.m01, m.m11).magnitude;
            float bottom = m.m13 + _sourceSprite.bounds.min.y * sy;
            _footOffset = Mathf.Max(0f, -bottom);
        }

        private void EnsureShadow()
        {
            if (_shadowRoot != null) return;
            Transform existing = transform.Find(SilhouetteName);
            if (existing == null)
            {
                var go = new GameObject(SilhouetteName);
                existing = go.transform;
                existing.SetParent(transform, false);
                _childCount = transform.childCount; // 自己建的，不触发重新分组
            }
            _shadowRoot = existing;
            if (_shadowRoot.childCount == 0)
            {
                var sg = new GameObject("Sprite");
                sg.transform.SetParent(_shadowRoot, false);
            }
            _shadowSpriteNode = _shadowRoot.GetChild(0);
            _shadowRenderer = _shadowSpriteNode.GetComponent<SpriteRenderer>();
            if (_shadowRenderer == null) _shadowRenderer = _shadowSpriteNode.gameObject.AddComponent<SpriteRenderer>();
            _shadowRenderer.sortingOrder = ViewMath.ShadowOrder;
        }

        private void UpdateSilhouette(SpriteRenderer src)
        {
            bool show = CameraRig.Silhouette && src != null && src.sprite != null && _body != null;
            if (!show)
            {
                if (_shadowRoot != null && _shadowRoot.gameObject.activeSelf) _shadowRoot.gameObject.SetActive(false);
                return;
            }
            EnsureShadow();
            if (!_shadowRoot.gameObject.activeSelf) _shadowRoot.gameObject.SetActive(true);

            _shadowRenderer.sprite = src.sprite;
            _shadowRenderer.flipX = src.flipX;
            _shadowRenderer.flipY = src.flipY;
            _shadowRenderer.color = new Color(0f, 0f, 0f, CameraRig.ShadowAlpha * src.color.a);

            // 来源图在纸片（Paper 局部）坐标里的 2D 仿射：A·p + t；脚底对齐后 y 再加 footOffset（纸片坐标 y = 0 就是地面）
            Matrix4x4 card = _body.worldToLocalMatrix * src.transform.localToWorldMatrix;
            float a00 = card.m00, a01 = card.m01, a10 = card.m10, a11 = card.m11;
            float tx = card.m03, ty = card.m13 + _footOffset;

            // 纸片 → 地面（根节点局部）：x 轴 = 纸片横向在地面上的投影（翻面时变短、反向），y 轴 = 沿光照方向倒下
            Vector3 right = transform.InverseTransformVector(_body.TransformVector(Vector3.right));
            float height = _body.TransformVector(Vector3.up).magnitude;
            Vector2 dir = CameraRig.ShadowDir * CameraRig.ShadowLength;
            Vector3 fall = transform.InverseTransformVector(new Vector3(dir.x * height, dir.y * height, 0f));
            float m00 = right.x, m10 = right.y, m01 = fall.x, m11 = fall.y;

            // N = M · A
            float n00 = m00 * a00 + m01 * a10;
            float n01 = m00 * a01 + m01 * a11;
            float n10 = m10 * a00 + m11 * a10;
            float n11 = m10 * a01 + m11 * a11;
            // 平移 = M · t + 离地高度造成的偏移
            Vector3 liftShift = transform.InverseTransformVector(new Vector3(dir.x * CameraRig.Lift, dir.y * CameraRig.Lift, 0f));
            float px = m00 * tx + m01 * ty + liftShift.x;
            float py = m10 * tx + m11 * ty + liftShift.y;

            // 2×2 分解：N = R(phi) · diag(sx, sy) · R(theta)（sy 可为负 = 镜像）
            float e = (n00 + n11) * 0.5f, f = (n00 - n11) * 0.5f;
            float g = (n10 + n01) * 0.5f, h = (n10 - n01) * 0.5f;
            float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
            float sx = q + r, sy = q - r;
            float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
            float theta = (a2 - a1) * 0.5f, phi = (a2 + a1) * 0.5f;

            _shadowRoot.localPosition = new Vector3(px, py, 0f);
            _shadowRoot.localRotation = Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
            _shadowRoot.localScale = new Vector3(sx, sy, 1f);
            _shadowSpriteNode.localPosition = Vector3.zero;
            _shadowSpriteNode.localRotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
            _shadowSpriteNode.localScale = Vector3.one;
        }
    }
}
