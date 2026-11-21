using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 位面环境机制（程序A）。进入位面时结算一次（OnPlaneEntered），持续效果在 Tick 里结算。
    ///
    /// 目前只搬入了白模的岩浆规则（原 LavaSystem），行为与之前完全一致：
    ///   · 火山被激活（开局 / 翻滚切过来）的那一刻：站在岩浆格里的普通怪物各受一次 LavaHitDamage；
    ///   · 结算完岩浆立刻消失：TerrainSystem 之后把岩浆格当作普通地面；
    ///   · 其他位面：什么都不做。玩家脚下的岩浆 / 浅水结算仍在 PlayerSystem 里。
    /// M1 / M2 待做：岩浆持续伤害（待策划确认）、工位困意、梦境减速层、老板来电。
    /// </summary>
    public sealed class EnvironmentSystem : IEnvironmentSystem
    {
        private readonly ITurnContext _ctx;
        private readonly EnvironmentConfig _env;
        private readonly MonsterConfig _monster;
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();

        public bool LavaPresent { get; private set; }

        public EnvironmentSystem(ITurnContext ctx, EnvironmentConfig env, MonsterConfig monster)
        {
            _ctx = ctx;
            _env = env;
            _monster = monster;
        }

        public void OnPlaneEntered(PlaneId plane)
        {
            LavaPresent = false;
            if (plane == PlaneId.Volcano) EruptLava();
            _ctx.Events.Publish(new PlaneEnteredEvent { Plane = plane });
        }

        public bool IsHazard(Vec2 position)
        {
            return _ctx.Terrain.Sample(position) == TerrainType.Lava;
        }

        public void Tick(float dt)
        {
            // M1 / M2：持续性环境效果在这里结算（_env 里的间隔、伤害等参数）。
            if (_env == null || dt <= 0f) return;
        }

        private void EruptLava()
        {
            LavaPresent = true;
            TerrainGridData grid = _ctx.Terrain.GetGrid(PlaneId.Volcano);

            // 先收集再结算：结算可能击杀怪物，不在遍历 Active 的同时改它的状态
            _hits.Clear();
            IReadOnlyList<MonsterEntity> monsters = _ctx.Monsters.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterEntity m = monsters[i];
                if (m.Alive && grid.Sample(m.Position) == TerrainType.Lava) _hits.Add(m);
            }

            float damage = _monster.LavaHitDamage;
            for (int i = 0; i < _hits.Count; i++) _ctx.Combat.DamageMonster(_hits[i], damage, DamageSource.Lava);

            LavaPresent = false; // 受过一次伤害后岩浆消失
            _ctx.Events.Publish(new LavaEruptedEvent { Hits = _hits.Count, Damage = damage });
            _hits.Clear();
        }
    }
}
