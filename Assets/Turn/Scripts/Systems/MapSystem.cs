using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 当前激活的地图。切换 = 改一个枚举值 + 发布 MapSwitchedEvent。
    /// 这里不碰任何单位：玩家 / 怪物的世界坐标、血量、Buff 在切换前后完全不变。
    /// 表现层的 MapManager 收到事件后用 SetActive 互斥显隐 VolcanoRoot / OceanRoot。
    /// </summary>
    public sealed class MapSystem : IMapSystem
    {
        private readonly ITurnContext _ctx;

        public MapId Active { get; private set; }
        public int SwitchCount { get; private set; }

        public MapSystem(ITurnContext ctx, MapId startMap)
        {
            _ctx = ctx;
            Active = startMap;
        }

        public void Switch()
        {
            MapId from = Active;
            Active = MapIds.Other(from);
            SwitchCount++;
            _ctx.Events.Publish(new MapSwitchedEvent { From = from, To = Active });
        }
    }
}
