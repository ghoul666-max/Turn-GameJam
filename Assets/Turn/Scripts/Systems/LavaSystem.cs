using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 岩浆：一次性的喷发，而不是持续地形。
    ///   · 火山地图被激活（开局起始地图 / 翻滚切过来）的那一刻：站在岩浆格里的普通怪物各受一次 LavaHitDamage；
    ///   · 结算完岩浆立刻消失：TerrainSystem 之后把岩浆格当作普通地面，没有任何持续伤害；
    ///   · 切到海洋：什么都不做，浅水一直存在；再切回火山时岩浆在同样的位置再喷发一次。
    /// 玩家和鲨鱼不受岩浆影响。伤害经 CombatSystem 结算（击杀照常掉经验）。
    /// </summary>
    public sealed class LavaSystem : ILavaSystem
    {
        private readonly ITurnContext _ctx;
        private readonly MonsterConfig _config;
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();
        private bool _synced;
        private MapId _lastMap;

        public bool Present { get; private set; }

        public LavaSystem(ITurnContext ctx, MonsterConfig config)
        {
            _ctx = ctx;
            _config = config;
        }

        public void SyncWithMap()
        {
            MapId active = _ctx.Map.Active;
            if (_synced && active == _lastMap) return; // 同一张图只在“刚激活”时结算一次
            _synced = true;
            _lastMap = active;

            if (active == MapId.Volcano) Erupt();
            else Present = false;
        }

        private void Erupt()
        {
            Present = true;
            TerrainGridData grid = _ctx.Terrain.GetGrid(MapId.Volcano);

            // 先收集再结算：结算可能击杀怪物，不在遍历 Active 的同时改它的状态
            _hits.Clear();
            IReadOnlyList<MonsterEntity> monsters = _ctx.Monsters.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterEntity m = monsters[i];
                if (m.Alive && grid.Sample(m.Position) == TerrainType.Lava) _hits.Add(m);
            }

            float damage = _config.LavaHitDamage;
            for (int i = 0; i < _hits.Count; i++) _ctx.Combat.DamageMonster(_hits[i], damage, DamageSource.Lava);

            Present = false; // 受过一次伤害后岩浆消失
            _ctx.Events.Publish(new LavaEruptedEvent { Hits = _hits.Count, Damage = damage });
            _hits.Clear();
        }
    }
}
