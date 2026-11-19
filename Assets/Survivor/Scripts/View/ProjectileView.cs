using UnityEngine;
using Survivor.Art;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>
    /// 投射物的显示。FitVisual 的投射物（鞭痕、光环）把图拉伸到判定体大小；其余按 Scale 缩放。
    /// 闪电的图以底部为落点向上延伸；光环画在角色下面，其余画在角色上面。
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private StepAnimator _anim;

        private string _visualKey;

        public static GameObject BuildTemplate()
        {
            var root = new GameObject(PrefabLibrary.Projectile);
            var view = root.AddComponent<ProjectileView>();
            view._sprite = SpriteKit.Create("Sprite", root.transform, SurvivorArt.Get(SurvivorArt.Orb));
            view._anim = view._sprite.gameObject.AddComponent<StepAnimator>();
            view._anim.Setup(view._sprite, null, 0f, 0f);
            return root;
        }

        public void Setup(ProjectileEntity p)
        {
            _visualKey = p.VisualKey;
            Sprite[] frames = SurvivorArt.GetFrames(p.VisualKey);
            _anim.SetFrames(frames);
            _anim.SetWiggle(p.VisualKey == SurvivorArt.Book ? 8f : 0f);
            _anim.Restart();

            Vector2 size = frames[0] != null ? (Vector2)frames[0].bounds.size : Vector2.one;
            if (size.x <= 0f) size.x = 1f;
            if (size.y <= 0f) size.y = 1f;

            float sx;
            float sy;
            if (p.FitVisual)
            {
                if (p.Shape == HitShape.Box)
                {
                    sx = p.HalfExtents.X * 2f / size.x;
                    sy = p.HalfExtents.Y * 2f / size.y;
                }
                else
                {
                    sx = p.Radius * 2f / size.x;
                    sy = p.Radius * 2f / size.y;
                }
            }
            else
            {
                sx = p.Scale;
                sy = p.Scale;
            }
            if (p.Facing < 0) sx = -sx;

            Transform t = _sprite.transform;
            t.localScale = new Vector3(sx, sy, 1f);
            t.localRotation = Quaternion.identity;
            bool anchorBottom = p.VisualKey == SurvivorArt.Bolt;
            t.localPosition = anchorBottom ? new Vector3(0f, size.y * sy * 0.5f - 0.2f, 0f) : Vector3.zero;

            _sprite.sortingOrder = p.VisualKey == SurvivorArt.Aura ? ViewMath.AuraOrder : ViewMath.ProjectileOrder;
            _sprite.color = Color.white;
        }

        public void Sync(ProjectileEntity p)
        {
            transform.position = ViewMath.ToWorld(p.Position);
            transform.rotation = p.Motion == ProjectileMotion.Linear
                ? Quaternion.Euler(0f, 0f, p.Rotation * Mathf.Rad2Deg)
                : Quaternion.identity;

            // 光环在寿命最后一格变淡，衔接下一个光环时有一下“脉冲”。
            if (_visualKey == SurvivorArt.Aura)
            {
                _sprite.color = p.Life < StepAnimator.Step ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }
        }
    }
}
