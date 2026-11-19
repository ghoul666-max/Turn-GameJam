using UnityEngine;
using UnityEngine.UI;

namespace Survivor.View
{
    /// <summary>
    /// 低帧率“手绘感”动画：每 0.15 秒（约 6.7fps）跳一格 —— 切换序列帧，可附带随机小角度摆动、上下跳动。
    /// 画面本身按满帧率渲染，只有这些图形在“抽帧”（与卡牌白模的 SteppedAnimator 同一节奏）。
    /// 目标可以是 SpriteRenderer（世界里的物体）或 Image（界面）。
    /// 摆动 / 跳动会改本物体的 localRotation / localPosition.y，所以要挂在“只负责显示”的子节点上。
    /// </summary>
    public sealed class StepAnimator : MonoBehaviour
    {
        /// <summary>全局统一的步长，其他需要“跳格”的效果也引用它。</summary>
        public const float Step = 0.15f;

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Image _image;
        [Tooltip("每一格随机摆动的最大角度；0 = 不摆动")]
        [SerializeField] private float _wiggleDegrees;
        [Tooltip("偶数格与奇数格之间的高度差（本地单位）；0 = 不跳动")]
        [SerializeField] private float _bob;

        private Sprite[] _frames;
        private float _timer;
        private int _tick;

        public int FrameIndex { get; private set; }

        public void Setup(SpriteRenderer renderer, Image image, float wiggleDegrees, float bob)
        {
            _renderer = renderer;
            _image = image;
            _wiggleDegrees = wiggleDegrees;
            _bob = bob;
        }

        public void SetFrames(Sprite[] frames)
        {
            _frames = frames;
            FrameIndex = 0;
            ApplyFrame();
        }

        public void SetWiggle(float degrees)
        {
            _wiggleDegrees = degrees;
            if (degrees <= 0f) transform.localRotation = Quaternion.identity;
        }

        /// <summary>从第 0 格开始计时（一次性的特效要从头播）。</summary>
        public void Restart()
        {
            _timer = 0f;
            _tick = 0;
            FrameIndex = 0;
            ApplyFrame();
            ApplyBob();
        }

        private void OnEnable()
        {
            // 错开相位，避免所有物体同一帧一起跳。
            _timer = Random.Range(0f, Step);
            _tick = Random.Range(0, 8);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < Step) return;
            _timer -= Step;
            if (_timer > Step) _timer = 0f; // 卡顿后不连跳
            _tick++;

            if (_frames != null && _frames.Length > 1)
            {
                FrameIndex = _tick % _frames.Length;
                ApplyFrame();
            }

            if (_wiggleDegrees > 0f)
            {
                transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-_wiggleDegrees, _wiggleDegrees));
            }
            ApplyBob();
        }

        private void ApplyFrame()
        {
            if (_frames == null || _frames.Length == 0) return;
            Sprite sprite = _frames[Mathf.Clamp(FrameIndex, 0, _frames.Length - 1)];
            if (_renderer != null) _renderer.sprite = sprite;
            if (_image != null) _image.sprite = sprite;
        }

        private void ApplyBob()
        {
            if (_bob == 0f) return;
            Vector3 p = transform.localPosition;
            p.y = (_tick & 1) == 0 ? 0f : _bob;
            transform.localPosition = p;
        }
    }
}
