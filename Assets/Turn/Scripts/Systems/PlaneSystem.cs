using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 当前激活的位面。切换 = 改一个枚举值 + 发布 PlaneSwitchedEvent。
    /// 这里不碰任何单位：怪物偏移 / 换皮、弹幕保留、装填、环境结算都由 RollSystem 按时序调用各系统完成。
    /// 表现层的 MapManager 收到事件后用 SetActive 互斥显隐各位面的 Root。
    /// </summary>
    public sealed class PlaneSystem : IPlaneSystem
    {
        private readonly ITurnContext _ctx;

        public PlaneId Active { get; private set; }
        public PlanePairId ActivePair { get { return PlaneIds.PairOf(Active); } }
        public int SwitchCount { get; private set; }

        public PlaneSystem(ITurnContext ctx, PlaneId startPlane)
        {
            _ctx = ctx;
            Active = PlaneIds.IsValid(startPlane) ? startPlane : PlaneId.Volcano;
        }

        public void Switch()
        {
            PlaneId from = Active;
            Active = PlaneIds.Other(from);
            SwitchCount++;
            _ctx.Events.Publish(new PlaneSwitchedEvent { From = from, To = Active });
        }

        public void EnterPair(PlanePairId pair)
        {
            PlaneId from = Active;
            PlaneId to = PlaneIds.First(pair);
            if (from == to) return;
            Active = to;
            _ctx.Events.Publish(new PlaneSwitchedEvent { From = from, To = to });
        }
    }
}
