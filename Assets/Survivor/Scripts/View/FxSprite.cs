using System;
using UnityEngine;
using Survivor.Art;

namespace Survivor.View
{
    /// <summary>
    /// 一次性的小特效（火花、升级圆环、回复加号）。缩放与透明度按 0.15s 的格跳变，播完回调归还对象池。
    /// </summary>
    public sealed class FxSprite : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        private float _life;
        private float _age;
        private float _startScale;
        private float _endScale;
        private Color _color;
        private Vector3 _velocity;
        private Action<FxSprite> _onDone;

        public static GameObject BuildTemplate()
        {
            var root = new GameObject(PrefabLibrary.Fx);
            var fx = root.AddComponent<FxSprite>();
            fx._sprite = SpriteKit.Create("Sprite", root.transform, SurvivorArt.Get(SurvivorArt.Spark));
            fx._sprite.sortingOrder = ViewMath.FxOrder;
            return root;
        }

        public void Play(string key, Vector3 position, Color color, float startScale, float endScale, float life,
            float rotationDegrees, Vector3 velocity, Action<FxSprite> onDone)
        {
            _sprite.sprite = SurvivorArt.Get(key);
            _color = color;
            _startScale = startScale;
            _endScale = endScale;
            _life = Mathf.Max(0.01f, life);
            _age = 0f;
            _velocity = velocity;
            _onDone = onDone;

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life)
            {
                Action<FxSprite> done = _onDone;
                _onDone = null;
                if (done != null) done(this);
                else gameObject.SetActive(false);
                return;
            }

            // 位置连续移动，缩放 / 透明度按格跳变。
            transform.position += _velocity * Time.deltaTime;
            float stepped = Mathf.Floor(_age / StepAnimator.Step) * StepAnimator.Step;
            Apply(Mathf.Clamp01(stepped / _life));
        }

        private void Apply(float t)
        {
            float s = Mathf.Lerp(_startScale, _endScale, t);
            transform.localScale = new Vector3(s, s, 1f);
            Color c = _color;
            c.a *= 1f - t * t;
            _sprite.color = c;
        }
    }
}
