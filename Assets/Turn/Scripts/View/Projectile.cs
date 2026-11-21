using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 子弹显示（FireballProjectilePrefab / HarpoonProjectilePrefab 根节点）。
    /// 只同步位置与朝向；飞行、命中、穿透、灼烧附加都在 ProjectileSystem 里。
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [Tooltip("火球可以在飞行时自转；鱼叉保持朝向")]
        [SerializeField] private float spinSpeed;
        [SerializeField] private Transform visual;

        [Header("运行时只读")]
        [SerializeField] private int entityId;
        [SerializeField] private PlaneId form;

        public void Init(ProjectileEntity p)
        {
            entityId = p.Id;
            form = p.Form;
            Sync(p);
        }

        public void Sync(ProjectileEntity p)
        {
            transform.position = ViewMath.ToWorld(p.Position);
            float deg = Mathf.Atan2(p.Velocity.Y, p.Velocity.X) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, deg);
        }

        private void Update()
        {
            if (visual != null && spinSpeed != 0f) visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }
    }
}
