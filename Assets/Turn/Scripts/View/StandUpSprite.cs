using UnityEngine;
using UnityEngine.Rendering;

namespace Turn.View
{
    /// <summary>
    /// 2.5D：让地图上的静态贴图（树、石头、桌子、路灯……）在倾斜相机时像立牌一样竖起来，和玩家 / 怪物用同一套姿态
    /// （竖直站立 + 竖向拉伸，见 <see cref="CameraRig"/>）。倾斜角 = 0 时恢复成原来的平铺状态。只改画面，不影响任何判定。
    ///
    /// 用法：挂在**贴图物体**上（美术摆好的树），锚点 = 树根在地面上的位置。
    ///   · 不要挂在带 <see cref="Obstacle"/> 的物体上：Obstacle 开局按物体位置 / 缩放算判定，被立起来会算错。
    ///     要「既是遮挡物又立起来」，把贴图放进 Obstacle 的子物体，把这个组件挂在子物体上。挂错了会在 Console 提示并停用。
    ///   · 贴图底边在锚点以下（圆心锚点）时自动抬起，让底边落在地面（可关）。手绘图锚点放在底部中心最省事。
    ///   · 打开「参与前后遮挡」时和玩家 / 怪物按相机远近排前后（画面靠下的挡住靠上的）。
    /// 不跟着纸片翻面，也不做剪影投影（静态物体的影子请美术直接画在地面上）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // 在 CameraFollow 更新完姿态之后
    public sealed class StandUpSprite : MonoBehaviour
    {
        [Tooltip("贴图底边在锚点以下时，立起来后整体抬起，让底边落在地面上")]
        [SerializeField] private bool autoFoot = true;
        [Tooltip("立起来后和玩家 / 怪物按相机远近排前后（会临时加一个 SortingGroup，sortingOrder = 怪物层）")]
        [SerializeField] private bool depthSort = true;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private float _bottom;
        private bool _captured;
        private bool _standing;
        private int _revision = -1;
        private SortingGroup _group;
        private bool _ownGroup;

        private void Awake()
        {
            if (GetComponent<Obstacle>() != null)
            {
                Debug.LogWarning("[Turn] StandUpSprite 不能挂在带 Obstacle 的物体上（会改变判定位置 / 缩放）：" + name +
                    "。请把贴图放进子物体，把组件挂在子物体上。已停用。", this);
                enabled = false;
                return;
            }
            Capture();
        }

        /// <summary>记下平铺时的姿态，并量出贴图底边在本地坐标里的高度。</summary>
        private void Capture()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
            _bottom = 0f;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            bool any = false;
            float minY = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null || r.sprite == null) continue;
                Bounds b = r.sprite.bounds;
                Matrix4x4 m = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int c = 0; c < 4; c++)
                {
                    Vector3 corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, 0f);
                    float y = m.MultiplyPoint3x4(corner).y;
                    if (!any || y < minY) minY = y;
                    any = true;
                }
            }
            if (any) _bottom = minY;
            _captured = true;
        }

        private void OnDisable()
        {
            if (_captured && _standing) Restore();
        }

        private void LateUpdate()
        {
            if (!_captured) return;
            bool stand = CameraRig.Standing;
            if (stand == _standing && (!stand || _revision == CameraRig.Revision)) return;
            _revision = CameraRig.Revision;

            if (!stand)
            {
                Restore();
                return;
            }

            _standing = true;
            transform.localPosition = _basePosition;
            transform.localScale = _baseScale;
            // 平铺时在地面里的旋转（绕 Z）保留在纸片平面内；整体立起来用和角色一样的姿态
            Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            transform.rotation = CameraRig.Stand * (parent * _baseRotation);
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * CameraRig.Stretch, _baseScale.z);
            if (autoFoot && CameraRig.AutoFoot && _bottom < 0f)
                transform.position += transform.TransformVector(new Vector3(0f, -_bottom, 0f));

            if (depthSort)
            {
                if (_group == null)
                {
                    _group = GetComponent<SortingGroup>();
                    if (_group == null)
                    {
                        _group = gameObject.AddComponent<SortingGroup>();
                        _ownGroup = true;
                    }
                }
                if (_ownGroup)
                {
                    _group.sortingOrder = ViewMath.MonsterOrder;
                    _group.enabled = true;
                }
            }
        }

        private void Restore()
        {
            _standing = false;
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            transform.localScale = _baseScale;
            if (_ownGroup && _group != null) _group.enabled = false;
        }
    }
}
