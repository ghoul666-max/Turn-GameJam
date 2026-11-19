using System;

namespace Survivor.Core
{
    /// <summary>
    /// 纯 C# 的二维向量。模拟层（Core / Systems / Weapons / Content）不引用 UnityEngine，
    /// 所以不能用 Vector2；表现层用 ViewMath 互相转换。
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

        public static float Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

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

        public static Vec2 Rotate(Vec2 v, float radians)
        {
            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>垂直方向（逆时针 90°）。</summary>
        public Vec2 Perpendicular { get { return new Vec2(-Y, X); } }

        public override string ToString() { return "(" + X.ToString("0.00") + ", " + Y.ToString("0.00") + ")"; }
    }
}
