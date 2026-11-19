using UnityEngine;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>模拟层 Vec2 与 Unity 向量的互相转换，以及表现层共用的排序层级。</summary>
    public static class ViewMath
    {
        // 排序层级（sortingOrder）。所有世界物体都在 Z = 0 平面上，前后关系靠它决定；
        // 树冠在更靠近相机的 Z 上，层级也最高。
        public const int GroundOrder = -3000;
        public const int PickupOrder = -1500;
        public const int AuraOrder = -1200;
        public const int ShadowOrder = -1100;
        /// <summary>角色按屏幕 y 排序：越靠下越靠前，范围 ±900。</summary>
        public const int ActorOrder = 0;
        public const int ProjectileOrder = 1500;
        public const int HpBarOrder = 1900;
        public const int FxOrder = 2000;
        public const int TextOrder = 2500;
        public const int CanopyOrder = 3000;

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

        /// <summary>相对相机的 y 排序：在相机下方的物体画在前面。</summary>
        public static int YSort(float y, float cameraY)
        {
            return ActorOrder + Mathf.Clamp(-Mathf.RoundToInt((y - cameraY) * 40f), -900, 900);
        }
    }
}
