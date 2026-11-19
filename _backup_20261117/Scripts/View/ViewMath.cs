using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>模拟层 Vec2 与 Unity 向量的互相转换，以及表现层共用的排序层级。</summary>
    public static class ViewMath
    {
        // 排序层级（SpriteRenderer.sortingOrder）。全部物体都在 Z = 0 平面上，前后关系靠它决定。
        public const int GroundOrder = -100;
        public const int TerrainOrder = -90;
        public const int DecorOrder = -80;
        public const int BoundsOrder = -70;
        public const int OrbOrder = -20;
        public const int ShadowOrder = -10;
        public const int MonsterOrder = 0;
        public const int SharkOrder = 5;
        public const int PlayerOrder = 10;
        public const int ProjectileOrder = 20;
        public const int HpBarOrder = 25;
        public const int VfxOrder = 30;

        public static Vector3 ToWorld(Vec2 v)
        {
            return new Vector3(v.X, v.Y, 0f);
        }

        public static Vector2 ToVector2(Vec2 v)
        {
            return new Vector2(v.X, v.Y);
        }

        public static Vec2 ToVec2(Vector2 v)
        {
            return new Vec2(v.x, v.y);
        }

        public static Color MapColor(MapId map)
        {
            return map == MapId.Volcano ? new Color(1f, 0.45f, 0.2f, 1f) : new Color(0.3f, 0.75f, 1f, 1f);
        }
    }
}
