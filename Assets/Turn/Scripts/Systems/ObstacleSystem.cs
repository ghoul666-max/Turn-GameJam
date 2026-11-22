using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 遮挡物（程序A）。每个位面一份遮挡数据（开局由 TurnConfigReader 从各位面 Root 下的 Obstacle 组件读出），
    /// 查询时只用当前激活位面的那一份 —— 翻滚切位面后立刻换成新位面的遮挡，与 Unity 里哪个 Root 被隐藏无关。
    ///
    /// · ResolveMove：先让单位走到想去的位置，再把它从所有重叠的遮挡物里推出去（沿最短方向），
    ///   所以贴着遮挡物走会自然地“贴边滑动”。站在遮挡物里面（例如翻滚切位面后新位面这里正好有一块石头）
    ///   也会在下一次调用时被推到最近的外侧。
    /// · SegmentBlocked：线段扫过（带半径）是否碰到遮挡物，给子弹 / 弹幕用。矩形按“外扩半径的矩形”近似，拐角处略保守。
    /// · Overlaps：一个圆是否与遮挡物重叠，给刷怪选点用。
    /// 只处理 mask 与遮挡物的 Mask 有交集的遮挡物：实体挡两者，软遮挡只挡人或只挡弹。
    /// </summary>
    public sealed class ObstacleSystem : IObstacleSystem
    {
        /// <summary>推出迭代次数：同时贴着几个遮挡物时多推几轮，保证最终不重叠。</summary>
        public const int ResolveIterations = 4;
        /// <summary>推出后额外留出的间隙，避免浮点误差导致下一帧又判成重叠。</summary>
        private const float Skin = 0.001f;

        private static readonly ObstacleData[] None = new ObstacleData[0];

        private readonly ITurnContext _ctx;
        private readonly ObstacleData[][] _planes = new ObstacleData[PlaneIds.Count][];

        public ObstacleSystem(ITurnContext ctx)
        {
            _ctx = ctx;
            List<ObstacleData>[] source = ctx.Config != null ? ctx.Config.Obstacles : null;
            for (int i = 0; i < PlaneIds.Count; i++)
            {
                List<ObstacleData> list = source != null && i < source.Length ? source[i] : null;
                _planes[i] = list != null && list.Count > 0 ? list.ToArray() : None;
            }
        }

        public IReadOnlyList<ObstacleData> Active { get { return Current; } }

        private ObstacleData[] Current
        {
            get
            {
                IPlaneSystem plane = _ctx.Plane;
                int index = plane != null ? (int)plane.Active : 0;
                return index >= 0 && index < _planes.Length ? _planes[index] : None;
            }
        }

        // ---------------- 查询 ----------------

        public Vec2 ResolveMove(Vec2 from, Vec2 to, float radius, ObstacleMask mask)
        {
            ObstacleData[] obstacles = Current;
            if (obstacles.Length == 0 || mask == ObstacleMask.None) return to;

            Vec2 p = to;
            radius = MathF.Max(0f, radius);
            for (int iter = 0; iter < ResolveIterations; iter++)
            {
                bool pushed = false;
                for (int i = 0; i < obstacles.Length; i++)
                {
                    if (!obstacles[i].Blocks(mask)) continue;
                    Vec2 result;
                    if (PushOut(obstacles[i], p, radius, from, out result))
                    {
                        p = result;
                        pushed = true;
                    }
                }
                if (!pushed) break;
            }
            return p;
        }

        public bool SegmentBlocked(Vec2 a, Vec2 b, float radius, ObstacleMask mask)
        {
            ObstacleData[] obstacles = Current;
            if (obstacles.Length == 0 || mask == ObstacleMask.None) return false;

            radius = MathF.Max(0f, radius);
            for (int i = 0; i < obstacles.Length; i++)
            {
                ObstacleData o = obstacles[i];
                if (!o.Blocks(mask)) continue;
                if (o.Shape == ObstacleShape.Circle)
                {
                    float reach = o.Radius + radius;
                    if (SegmentSqrDistance(o.Center, a, b) < reach * reach) return true;
                }
                else
                {
                    Vec2 grow = new Vec2(o.HalfSize.X + radius, o.HalfSize.Y + radius);
                    if (SegmentHitsBox(a, b, o.Center - grow, o.Center + grow)) return true;
                }
            }
            return false;
        }

        public bool Overlaps(Vec2 position, float radius, ObstacleMask mask)
        {
            ObstacleData[] obstacles = Current;
            if (obstacles.Length == 0 || mask == ObstacleMask.None) return false;

            radius = MathF.Max(0f, radius);
            for (int i = 0; i < obstacles.Length; i++)
            {
                if (obstacles[i].Blocks(mask) && Intersects(obstacles[i], position, radius)) return true;
            }
            return false;
        }

        // ---------------- 几何 ----------------

        private static bool Intersects(ObstacleData o, Vec2 p, float r)
        {
            if (o.Shape == ObstacleShape.Circle)
            {
                float reach = o.Radius + r;
                return Vec2.SqrDistance(p, o.Center) < reach * reach;
            }
            Vec2 min = o.Center - o.HalfSize;
            Vec2 max = o.Center + o.HalfSize;
            if (p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y) return true;
            Vec2 q = new Vec2(Clamp(p.X, min.X, max.X), Clamp(p.Y, min.Y, max.Y));
            return Vec2.SqrDistance(p, q) < r * r;
        }

        /// <summary>半径 r 的圆在 p 与遮挡物重叠时，算出沿最短方向推出去后的位置。</summary>
        private static bool PushOut(ObstacleData o, Vec2 p, float r, Vec2 from, out Vec2 result)
        {
            result = p;
            if (o.Shape == ObstacleShape.Circle)
            {
                float reach = o.Radius + r;
                Vec2 d = p - o.Center;
                float sq = d.SqrMagnitude;
                if (sq >= reach * reach) return false;

                Vec2 n;
                if (sq > 1e-8f) n = d / MathF.Sqrt(sq);
                else
                {
                    // 正好在圆心：往来的方向退回去
                    n = (from - o.Center).Normalized;
                    if (n.SqrMagnitude < 0.5f) n = Vec2.Right;
                }
                result = o.Center + n * (reach + Skin);
                return true;
            }

            Vec2 min = o.Center - o.HalfSize;
            Vec2 max = o.Center + o.HalfSize;
            bool inside = p.X > min.X && p.X < max.X && p.Y > min.Y && p.Y < max.Y;
            if (!inside)
            {
                Vec2 q = new Vec2(Clamp(p.X, min.X, max.X), Clamp(p.Y, min.Y, max.Y));
                Vec2 d = p - q;
                float sq = d.SqrMagnitude;
                if (sq >= r * r) return false;
                if (sq > 1e-8f)
                {
                    result = q + d / MathF.Sqrt(sq) * (r + Skin);
                    return true;
                }
                // 正好压在边上：按下面“在内部”的规则推出
            }

            // 圆心在矩形内部：从最近的一条边推出去
            float left = p.X - min.X;
            float right = max.X - p.X;
            float down = p.Y - min.Y;
            float up = max.Y - p.Y;
            float best = MathF.Min(MathF.Min(left, right), MathF.Min(down, up));
            if (best == left) result = new Vec2(min.X - r - Skin, p.Y);
            else if (best == right) result = new Vec2(max.X + r + Skin, p.Y);
            else if (best == down) result = new Vec2(p.X, min.Y - r - Skin);
            else result = new Vec2(p.X, max.Y + r + Skin);
            return true;
        }

        private static float SegmentSqrDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len = ab.SqrMagnitude;
            float t = 0f;
            if (len > 1e-8f)
            {
                Vec2 ap = p - a;
                t = Clamp((ap.X * ab.X + ap.Y * ab.Y) / len, 0f, 1f);
            }
            return Vec2.SqrDistance(p, a + ab * t);
        }

        /// <summary>线段 a → b 是否与轴对齐矩形相交（slab 法）。</summary>
        private static bool SegmentHitsBox(Vec2 a, Vec2 b, Vec2 min, Vec2 max)
        {
            float tMin = 0f;
            float tMax = 1f;
            Vec2 d = b - a;
            if (!Slab(a.X, d.X, min.X, max.X, ref tMin, ref tMax)) return false;
            if (!Slab(a.Y, d.Y, min.Y, max.Y, ref tMin, ref tMax)) return false;
            return true;
        }

        private static bool Slab(float start, float dir, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (MathF.Abs(dir) < 1e-6f) return start >= lo && start <= hi;
            float t1 = (lo - start) / dir;
            float t2 = (hi - start) / dir;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMin <= tMax;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
