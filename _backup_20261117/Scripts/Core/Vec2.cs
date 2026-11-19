using System;

namespace Turn.Core
{
    /// <summary>
    /// 纯 C# 二维向量。模拟层（Core / Systems / Weapons）不引用 UnityEngine，
    /// 所以不能用 Vector2；表现层通过 Turn.Unity.ViewMath 互相转换。
    /// </summary>
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 Right = new Vec2(1f, 0f);

        public float SqrMagnitude { get { return X * X + Y * Y; } }
        public float Magnitude { get { return MathF.Sqrt(X * X + Y * Y); } }
        /// <summary>弧度，atan2(y, x)。</summary>
        public float Angle { get { return MathF.Atan2(Y, X); } }

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-5f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.X, -a.Y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator *(float s, Vec2 a) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator /(Vec2 a, float s) { return new Vec2(a.X / s, a.Y / s); }

        public static float SqrDistance(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static float Distance(Vec2 a, Vec2 b) { return MathF.Sqrt(SqrDistance(a, b)); }

        public static Vec2 FromAngle(float radians) { return new Vec2(MathF.Cos(radians), MathF.Sin(radians)); }

        public static Vec2 ClampMagnitude(Vec2 v, float max)
        {
            float sq = v.SqrMagnitude;
            if (sq <= max * max) return v;
            float m = MathF.Sqrt(sq);
            return new Vec2(v.X / m * max, v.Y / m * max);
        }

        public override string ToString() { return "(" + X.ToString("0.00") + ", " + Y.ToString("0.00") + ")"; }
    }

    /// <summary>轴对齐矩形（世界坐标）。两张地图共用的移动边界就是一个 BoundsRect。</summary>
    public struct BoundsRect
    {
        public Vec2 Min;
        public Vec2 Max;

        public BoundsRect(Vec2 center, Vec2 size)
        {
            Min = new Vec2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
            Max = new Vec2(center.X + size.X * 0.5f, center.Y + size.Y * 0.5f);
        }

        public Vec2 Center { get { return new Vec2((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f); } }
        public Vec2 Size { get { return new Vec2(Max.X - Min.X, Max.Y - Min.Y); } }

        /// <summary>把半径为 radius 的圆限制在矩形内部。</summary>
        public Vec2 Clamp(Vec2 p, float radius)
        {
            float minX = Min.X + radius;
            float maxX = Max.X - radius;
            float minY = Min.Y + radius;
            float maxY = Max.Y - radius;
            if (minX > maxX) { minX = maxX = (Min.X + Max.X) * 0.5f; }
            if (minY > maxY) { minY = maxY = (Min.Y + Max.Y) * 0.5f; }
            return new Vec2(MathF.Max(minX, MathF.Min(maxX, p.X)), MathF.Max(minY, MathF.Min(maxY, p.Y)));
        }

        public bool Contains(Vec2 p, float margin)
        {
            return p.X >= Min.X - margin && p.X <= Max.X + margin && p.Y >= Min.Y - margin && p.Y <= Max.Y + margin;
        }
    }
}
