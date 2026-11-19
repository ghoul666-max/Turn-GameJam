using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 统一移动边界。整局只有这一个矩形：地图切换不会改变它，两张地图也没有各自的边界。
    /// 玩家 / 怪物 / 鲨鱼在各自的系统里移动之后，都调用这里的 Clamp。
    /// </summary>
    public sealed class BoundsSystem : IBoundsSystem
    {
        private readonly ITurnContext _ctx;

        public BoundsRect Rect { get; private set; }

        public BoundsSystem(ITurnContext ctx, BoundsRect rect)
        {
            _ctx = ctx;
            Rect = rect;
        }

        public Vec2 Clamp(Vec2 position, float radius)
        {
            return Rect.Clamp(position, radius);
        }

        public bool Contains(Vec2 position, float margin)
        {
            return Rect.Contains(position, margin);
        }

        public Vec2 RandomPoint(float margin)
        {
            BoundsRect r = Rect;
            float x = r.Min.X + margin + (float)_ctx.Random.NextDouble() * (r.Size.X - margin * 2f);
            float y = r.Min.Y + margin + (float)_ctx.Random.NextDouble() * (r.Size.Y - margin * 2f);
            return Clamp(new Vec2(x, y), margin);
        }
    }
}
