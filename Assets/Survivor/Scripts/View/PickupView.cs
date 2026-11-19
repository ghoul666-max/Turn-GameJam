using UnityEngine;
using Survivor.Art;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>掉落物：按种类 / 经验值挑图（蓝 &lt; 2 ≤ 绿 &lt; 10 ≤ 红），按格上下跳动。</summary>
    public sealed class PickupView : MonoBehaviour
    {
        public const float Bob = 0.08f;

        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private StepAnimator _anim;

        private string _key;

        public static GameObject BuildTemplate()
        {
            var root = new GameObject(PrefabLibrary.Pickup);
            var view = root.AddComponent<PickupView>();
            view._sprite = SpriteKit.Create("Sprite", root.transform, SurvivorArt.Get(SurvivorArt.GemBlue));
            view._sprite.sortingOrder = ViewMath.PickupOrder;
            view._anim = view._sprite.gameObject.AddComponent<StepAnimator>();
            view._anim.Setup(view._sprite, null, 0f, Bob);
            return root;
        }

        public static string KeyFor(PickupEntity p)
        {
            if (p.Kind == PickupKind.Heal) return SurvivorArt.Heal;
            if (p.Value >= 10) return SurvivorArt.GemRed;
            if (p.Value >= 2) return SurvivorArt.GemGreen;
            return SurvivorArt.GemBlue;
        }

        public void Sync(PickupEntity p)
        {
            string key = KeyFor(p);
            if (key != _key)
            {
                _key = key;
                _anim.SetFrames(SurvivorArt.GetFrames(key));
                _sprite.transform.localScale = p.Kind == PickupKind.Heal ? new Vector3(1.2f, 1.2f, 1f) : Vector3.one;
            }
            transform.position = ViewMath.ToWorld(p.Position);
        }

        public void ResetKey()
        {
            _key = null;
        }
    }
}
