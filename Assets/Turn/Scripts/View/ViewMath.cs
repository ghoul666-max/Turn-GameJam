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
        public const int ObstacleOrder = -75;
        public const int BoundsOrder = -70;
        public const int OrbOrder = -20;
        public const int ShadowOrder = -10;
        public const int MonsterOrder = 0;
        public const int SharkOrder = 5;
        public const int PlayerOrder = 10;
        public const int ProjectileOrder = 20;
        public const int HpBarOrder = 25;
        public const int VfxOrder = 30;
        public const int ObstacleDebugOrder = 40;

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

        public static Color MapColor(PlaneId map)
        {
            switch (map)
            {
                case PlaneId.Volcano: return new Color(1f, 0.45f, 0.2f, 1f);
                case PlaneId.Ocean: return new Color(0.3f, 0.75f, 1f, 1f);
                case PlaneId.Office: return new Color(0.82f, 0.84f, 0.88f, 1f);
                case PlaneId.Dream: return new Color(0.95f, 0.5f, 0.9f, 1f);
                case PlaneId.Primal: return new Color(0.75f, 0.6f, 0.35f, 1f);
                case PlaneId.Future: return new Color(0.55f, 0.45f, 1f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>三把武器的主题色（HUD / 子弹占位 / 特效）。</summary>
        public static Color WeaponColor(WeaponKind weapon)
        {
            switch (weapon)
            {
                case WeaponKind.WaterGun: return new Color(0.3f, 0.75f, 1f, 1f);
                case WeaponKind.Spear: return new Color(1f, 0.78f, 0.3f, 1f);
                case WeaponKind.Blade: return new Color(0.95f, 0.35f, 0.4f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>五种怪物的占位颜色（白模阶段区分种类；美术素材到位后可以去掉）。</summary>
        public static Color MonsterColor(MonsterKind kind)
        {
            switch (kind)
            {
                case MonsterKind.Walker: return new Color(0.62f, 0.78f, 0.55f, 1f);
                case MonsterKind.Spore: return new Color(0.85f, 0.55f, 0.95f, 1f);
                case MonsterKind.Charger: return new Color(0.95f, 0.55f, 0.3f, 1f);
                case MonsterKind.Rotter: return new Color(0.45f, 0.85f, 0.3f, 1f);
                case MonsterKind.Sniper: return new Color(0.55f, 0.7f, 0.95f, 1f);
                default: return Color.white;
            }
        }
    }
}
