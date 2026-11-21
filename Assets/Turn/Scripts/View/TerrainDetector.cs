using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 地形检测辅助脚本（挂在 Player 上）。每帧通过 ITerrainSystem 读取“当前激活地图”的地形数据，
    /// 判断玩家脚下是 Normal / Lava / ShallowWater，并显示在 Inspector 与脚下的标记上，方便测试。
    /// 规则结算（岩浆扣血、浅水回血、怪物岩浆 DOT）在模拟层，和这里读的是同一个数据源（TerrainSystem）。
    /// 也可以用 Probe() 查询任意世界坐标的地形（例如调试鼠标位置）。
    /// </summary>
    public sealed class TerrainDetector : MonoBehaviour
    {
        [Tooltip("脚下的地形标记（可选）")]
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private Color lavaColor = new Color(1f, 0.35f, 0.1f, 0.9f);
        [SerializeField] private Color shallowColor = new Color(0.4f, 0.9f, 1f, 0.9f);

        [Header("运行时只读")]
        [SerializeField] private PlaneId activeMap;
        [SerializeField] private TerrainType currentTerrain;

        private ITurnContext _ctx;

        public TerrainType CurrentTerrain { get { return currentTerrain; } }
        public PlaneId ActiveMap { get { return activeMap; } }

        public void Bind(ITurnContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>查询任意世界坐标在当前激活地图上的地形。</summary>
        public TerrainType Probe(Vector2 worldPosition)
        {
            return _ctx != null ? _ctx.Terrain.Sample(ViewMath.ToVec2(worldPosition)) : TerrainType.Normal;
        }

        private void LateUpdate()
        {
            if (_ctx == null) return;
            activeMap = _ctx.Plane.Active;
            currentTerrain = _ctx.Terrain.Sample(_ctx.Player.State.Position);

            if (marker == null) return;
            marker.enabled = currentTerrain != TerrainType.Normal;
            if (marker.enabled) marker.color = currentTerrain == TerrainType.Lava ? lavaColor : shallowColor;
        }
    }
}
