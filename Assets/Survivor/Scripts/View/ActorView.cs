using UnityEngine;
using Survivor.Art;

namespace Survivor.View
{
    /// <summary>
    /// 角色（玩家 / 敌人）的显示：影子 + 两帧抽帧待机 + 受击闪白 + 死亡的三格淡出。
    /// 闪白用的是同形状的纯白剪影（越过 Bloom 阈值，所以挨打时会“亮一下”）。
    /// 根节点只负责位置；Body 子节点负责图像与动画，Flash 是 Body 的子节点。
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        public const float FlashTime = 0.1f;
        public const float ShadowOffset = -0.45f;

        [SerializeField] private SpriteRenderer _body;
        [SerializeField] private SpriteRenderer _flash;
        [SerializeField] private SpriteRenderer _shadow;
        [SerializeField] private StepAnimator _anim;

        private Sprite[] _flashFrames;
        private float _flashTimer;
        private bool _dying;
        private float _deathTimer;
        private float _scale = 1f;
        private int _order;

        public bool DeathFinished { get; private set; }

        public static GameObject BuildTemplate()
        {
            var root = new GameObject(PrefabLibrary.Actor);
            var view = root.AddComponent<ActorView>();

            view._shadow = SpriteKit.Create("Shadow", root.transform, SurvivorArt.Get(SurvivorArt.Shadow));
            view._shadow.transform.localPosition = new Vector3(0f, ShadowOffset, 0f);
            view._shadow.sortingOrder = ViewMath.ShadowOrder;

            view._body = SpriteKit.Create("Body", root.transform, SurvivorArt.Get(SurvivorArt.Hunter));
            view._anim = view._body.gameObject.AddComponent<StepAnimator>();
            view._anim.Setup(view._body, null, 0f, 0f);

            view._flash = SpriteKit.Create("Flash", view._body.transform, SurvivorArt.Get(SurvivorArt.Hunter + SurvivorArt.FlashSuffix));
            view._flash.enabled = false;
            return root;
        }

        public void Setup(string spriteKey, float scale)
        {
            _scale = scale > 0f ? scale : 1f;
            _anim.enabled = true;
            _anim.SetFrames(SurvivorArt.GetFrames(spriteKey));
            _flashFrames = SurvivorArt.GetFlashFrames(spriteKey);

            _body.transform.localScale = new Vector3(_scale, _scale, 1f);
            _body.transform.localRotation = Quaternion.identity;
            _body.color = Color.white;
            _shadow.transform.localScale = new Vector3(_scale, _scale, 1f);
            _shadow.transform.localPosition = new Vector3(0f, ShadowOffset * _scale, 0f);
            _shadow.enabled = true;

            _flash.enabled = false;
            _flashTimer = 0f;
            _order = int.MinValue; // 下一次 SetState 强制刷新排序
            _dying = false;
            _deathTimer = 0f;
            DeathFinished = false;
        }

        public void SetState(Vector3 position, int facing, int order)
        {
            transform.position = position;
            bool flip = facing < 0;
            _body.flipX = flip;
            _flash.flipX = flip;
            if (order != _order)
            {
                _order = order;
                _body.sortingOrder = order;
                _flash.sortingOrder = order + 1;
            }
        }

        public void Flash()
        {
            if (_dying) return;
            _flashTimer = FlashTime;
            _flash.enabled = true;
            SyncFlashFrame();
        }

        /// <summary>开始死亡表现。模拟层里它已经没了；这里只是把最后的样子播完（表现滞后于结算）。</summary>
        public void BeginDeath()
        {
            _dying = true;
            _deathTimer = 0f;
            _anim.enabled = false;
            _flash.enabled = true;
            SyncFlashFrame();
        }

        private void Update()
        {
            if (_dying)
            {
                UpdateDeath();
                return;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) _flash.enabled = false;
                else SyncFlashFrame();
            }
        }

        /// <summary>按格跳变：第 0 格全白 → 第 1 格半透明放大 → 第 2 格更淡并压扁 → 结束。</summary>
        private void UpdateDeath()
        {
            _deathTimer += Time.deltaTime;
            int step = Mathf.FloorToInt(_deathTimer / StepAnimator.Step);
            if (step == 0) return;

            _flash.enabled = false;
            _shadow.enabled = false;
            if (step == 1)
            {
                _body.color = new Color(1f, 1f, 1f, 0.55f);
                _body.transform.localScale = new Vector3(_scale * 1.15f, _scale * 1.15f, 1f);
            }
            else if (step == 2)
            {
                _body.color = new Color(1f, 1f, 1f, 0.25f);
                _body.transform.localScale = new Vector3(_scale * 1.3f, _scale * 0.7f, 1f);
            }
            else
            {
                DeathFinished = true;
            }
        }

        private void SyncFlashFrame()
        {
            if (_flashFrames == null || _flashFrames.Length == 0) return;
            _flash.sprite = _flashFrames[_anim.FrameIndex % _flashFrames.Length];
        }
    }
}
