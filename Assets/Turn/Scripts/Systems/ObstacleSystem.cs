using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 遮挡物（程序A，M1 实现）。只读取当前激活位面的遮挡数据。
    /// 契约期：空实现 —— 什么都不挡，所以接入调用点后游戏行为与之前完全一致。
    /// 程序B 现在就可以在怪物移动 / 子弹飞行里调用它，等 A 填好数据后自动生效。
    /// </summary>
    public sealed class ObstacleSystem : IObstacleSystem
    {
        public ObstacleSystem(ITurnContext ctx)
        {
            // M1：从 ctx.Config 读各位面的遮挡数据；查询时按 ctx.Plane.Active 选用。
        }

        public Vec2 ResolveMove(Vec2 from, Vec2 to, float radius, ObstacleMask mask)
        {
            return to;
        }

        public bool SegmentBlocked(Vec2 a, Vec2 b, float radius, ObstacleMask mask)
        {
            return false;
        }

        public bool Overlaps(Vec2 position, float radius, ObstacleMask mask)
        {
            return false;
        }
    }
}
