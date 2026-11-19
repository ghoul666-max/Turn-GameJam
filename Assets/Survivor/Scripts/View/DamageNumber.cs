using System;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.View
{
    /// <summary>世界空间的伤害数字：弹出 → 上浮 → 淡出 → 回调归还对象池。接近纯白，会被 Bloom 提亮。</summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        public const float Life = 0.6f;
        /// <summary>画布单位（100 = 1 世界单位）。</summary>
        public const float Rise = 45f;

        [SerializeField] private Text _text;

        private RectTransform _rt;
        private Vector2 _start;
        private float _age;
        private float _size = 1f;
        private Color _color = Color.white;
        private Action<DamageNumber> _onDone;

        public static GameObject BuildTemplate()
        {
            RectTransform rt = UIKit.CreateRect(PrefabLibrary.DamageNumber, null, Vector2.zero, new Vector2(160f, 50f));
            var number = rt.gameObject.AddComponent<DamageNumber>();
            number._text = UIKit.CreateText("Text", rt, Vector2.zero, new Vector2(160f, 50f), "0",
                30, TextAnchor.MiddleCenter, Color.white);
            number._text.fontStyle = FontStyle.Bold;
            UIKit.AddOutline(number._text, 2f);
            return rt.gameObject;
        }

        public void Play(Vector2 canvasPosition, string content, Color color, float size, Action<DamageNumber> onDone)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _start = canvasPosition;
            _age = 0f;
            _size = size;
            _color = color;
            _onDone = onDone;
            _text.text = content;
            _text.color = color;
            _rt.anchoredPosition = canvasPosition;
            _rt.localScale = new Vector3(size * 1.6f, size * 1.6f, 1f);
        }

        private void Update()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Life);

            float eased = 1f - (1f - t) * (1f - t);
            _rt.anchoredPosition = _start + new Vector2(0f, Rise * eased);
            float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
            _rt.localScale = new Vector3(pop * _size, pop * _size, 1f);

            Color c = _color;
            c.a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            _text.color = c;

            if (_age >= Life)
            {
                Action<DamageNumber> done = _onDone;
                _onDone = null;
                if (done != null) done(this);
                else gameObject.SetActive(false);
            }
        }
    }
}
