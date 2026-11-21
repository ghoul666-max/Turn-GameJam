using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 地形检测。每个位面各有一份网格（开局从各自的 TerrainGrid 组件读出，没有的补空网格），
    /// 采样时只用 IPlaneSystem.Active 对应的那一份 —— 切换地图后下一次采样立即换数据源，
    /// 而且与 Unity 里哪个 Root 被隐藏无关，不可能读到另一张地图的数据。
    /// </summary>
    public sealed class TerrainSystem : ITerrainSystem
    {
        private readonly ITurnContext _ctx;
        private readonly TerrainGridData[] _grids = new TerrainGridData[PlaneIds.Count];

        public TerrainSystem(ITurnContext ctx, TerrainGridData[] grids)
        {
            _ctx = ctx;
            for (int i = 0; i < PlaneIds.Count; i++)
            {
                TerrainGridData grid = grids != null && i < grids.Length ? grids[i] : null;
                _grids[i] = grid ?? TerrainGridData.Empty(ctx.Bounds.Rect, 1f);
            }
        }

        public TerrainGridData ActiveGrid { get { return _grids[(int)_ctx.Plane.Active]; } }

        public TerrainGridData GetGrid(PlaneId map)
        {
            return _grids[(int)map];
        }

        public TerrainType Sample(Vec2 position)
        {
            TerrainType t = ActiveGrid.Sample(position);
            // 岩浆喷发一次就消失：消失后岩浆格就是普通地面
            if (t == TerrainType.Lava && _ctx.Environment != null && !_ctx.Environment.LavaPresent) return TerrainType.Normal;
            return t;
        }
    }
}
