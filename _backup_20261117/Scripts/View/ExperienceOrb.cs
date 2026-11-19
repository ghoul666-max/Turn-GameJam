using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>经验球显示（ExperienceOrbPrefab 根节点）：同步位置，原地轻微上下浮动。吸附与拾取在 ExperienceSystem 里。</summary>
    public sealed class ExperienceOrb : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 5f;

        [Header("运行时只读")]
        [SerializeField] private int entityId;
        [SerializeField] private int value;

        private float _phase;

        public void Init(OrbEntity orb)
        {
            entityId = orb.Id;
            value = orb.Value;
            _phase = orb.Id * 0.7f;
            Sync(orb);
        }

        public void Sync(OrbEntity orb)
        {
            transform.position = ViewMath.ToWorld(orb.Position);
        }

        private void Update()
        {
            if (visual == null) return;
            visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight, 0f);
            visual.Rotate(0f, 0f, 90f * Time.deltaTime);
        }
    }
}
