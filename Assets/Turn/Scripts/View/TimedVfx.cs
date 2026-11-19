using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 一次性特效（PortalVFXPrefab / RollShockwaveVFXPrefab 根节点）：
    /// 在 lifetime 内从 startScale 缩放到 endScale、透明度淡出、可自转、可跟随目标，结束后 Destroy。
    /// 纯表现：传送门不带 Collider，不会、也不能触发地图切换。
    /// </summary>
    public sealed class TimedVfx : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.5f;
        [SerializeField] private float startScale = 0.6f;
        [SerializeField] private float endScale = 2.2f;
        [SerializeField] private float spinSpeed = 0f;
        [SerializeField] private SpriteRenderer[] renderers = new SpriteRenderer[0];

        private float _age;
        private float _scaleMultiplier = 1f;
        private Transform _follow;
        private Color[] _baseColors;

        /// <param name="scaleMultiplier">整体缩放倍率（冲击波用它匹配实际半径）。</param>
        /// <param name="follow">非空时每帧跟随这个物体（传送门跟着玩家）。</param>
        public void Play(Vector3 position, float scaleMultiplier, Transform follow)
        {
            transform.position = position;
            _scaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
            _follow = follow;
            _age = 0f;
            CacheColors();
            Apply(0f);
        }

        public void SetLifetime(float seconds)
        {
            lifetime = Mathf.Max(0.05f, seconds);
        }

        private void CacheColors()
        {
            if (_baseColors != null && _baseColors.Length == renderers.Length) return;
            _baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) _baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_follow != null) transform.position = _follow.position;
            if (spinSpeed != 0f) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float t = Mathf.Clamp01(_age / Mathf.Max(0.05f, lifetime));
            Apply(t);
            if (_age >= lifetime) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(startScale, endScale, eased) * _scaleMultiplier;
            transform.localScale = new Vector3(s, s, 1f);
            if (_baseColors == null) CacheColors();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = _baseColors[i];
                c.a *= 1f - t;
                renderers[i].color = c;
            }
        }
    }
}
