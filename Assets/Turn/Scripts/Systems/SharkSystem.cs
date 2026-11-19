using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 鲨鱼：海洋专属威胁。它的存在完全由当前激活地图决定，不跟随地图切换做空间传送：
    ///   海洋激活且没有鲨鱼 → 生成 SpawnCount 条；火山激活 → 全部直接销毁（实体丢弃，表现层 Destroy GameObject）。
    /// 没有生命值、不在 IMonsterSystem 里，所以武器不会索敌它、子弹 / 冲击波不会打中它，也不参与死亡与经验掉落。
    /// 接触攻击不分敌我：同一口咬下去，玩家（经 CombatSystem，检查无敌帧）和接触到的普通怪物都会受伤；鲨鱼之间互不伤害。
    /// </summary>
    public sealed class SharkSystem : ISharkSystem
    {
        private readonly ITurnContext _ctx;
        private readonly SharkConfig _config;
        private readonly List<SharkEntity> _active = new List<SharkEntity>();
        private readonly List<MonsterEntity> _bitten = new List<MonsterEntity>();
        private readonly List<Vec2> _candidates = new List<Vec2>();
        private int _nextId = 1;

        public IReadOnlyList<SharkEntity> Active { get { return _active; } }

        public SharkSystem(ITurnContext ctx, SharkConfig config)
        {
            _ctx = ctx;
            _config = config;
        }

        public void SyncWithMap()
        {
            if (_ctx.Map.Active == MapId.Ocean)
            {
                if (_active.Count == 0) SpawnAll();
            }
            else if (_active.Count > 0)
            {
                int count = _active.Count;
                for (int i = 0; i < _active.Count; i++) _active[i].Alive = false;
                _active.Clear();
                _ctx.Events.Publish(new SharksClearedEvent { Count = count });
            }
        }

        public void Tick(float dt)
        {
            SyncWithMap(); // 保险：任何时候都保证“只有海洋地图有鲨鱼”
            if (_active.Count == 0) return;

            PlayerState player = _ctx.Player.State;
            float playerRange = _config.Radius + _ctx.Player.Config.Radius;

            for (int i = 0; i < _active.Count; i++)
            {
                SharkEntity s = _active[i];

                // 持续追击玩家 + 统一边界
                Vec2 to = player.Position - s.Position;
                float dist = to.Magnitude;
                s.Velocity = dist > 0.05f ? to / dist * _config.Speed : Vec2.Zero;
                s.Position = _ctx.Bounds.Clamp(s.Position + s.Velocity * dt, _config.Radius);

                // 接触攻击：冷却好了且咬得到任何东西时，一口同时结算玩家与普通怪物
                if (s.AttackTimer > 0f) s.AttackTimer -= dt;
                if (s.AttackTimer > 0f) continue;

                bool hitPlayer = player.Alive && Vec2.SqrDistance(s.Position, player.Position) <= playerRange * playerRange;
                _ctx.Monsters.QueryCircle(s.Position, _config.Radius, _bitten);
                if (!hitPlayer && _bitten.Count == 0) continue;

                s.AttackTimer = _config.AttackInterval;
                if (hitPlayer) _ctx.Combat.DamagePlayer(_config.ContactDamage, DamageSource.Shark);
                for (int k = 0; k < _bitten.Count; k++)
                {
                    _ctx.Combat.DamageMonster(_bitten[k], _config.ContactDamage, DamageSource.Shark);
                }
            }
        }

        private void SpawnAll()
        {
            int count = Math.Max(0, _config.SpawnCount);
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                var s = new SharkEntity();
                s.Id = _nextId++;
                s.Position = PickSpawnPosition();
                s.AttackTimer = _config.AttackInterval; // 刚出现时给玩家一个反应时间
                s.Alive = true;
                _active.Add(s);
            }
            _ctx.Events.Publish(new SharksSpawnedEvent { Count = count });
        }

        /// <summary>优先用海洋地图的刷怪点里离玩家足够远的；没有就在边界内随机找一个足够远的点。</summary>
        private Vec2 PickSpawnPosition()
        {
            Vec2 player = _ctx.Player.State.Position;
            float minSq = _config.MinSpawnDistance * _config.MinSpawnDistance;

            List<Vec2> points = _ctx.Config.SpawnPoints[(int)MapId.Ocean];
            _candidates.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (Vec2.SqrDistance(points[i], player) >= minSq) _candidates.Add(points[i]);
            }
            if (_candidates.Count > 0)
            {
                return _ctx.Bounds.Clamp(_candidates[_ctx.Random.Next(_candidates.Count)], _config.Radius);
            }

            Vec2 best = _ctx.Bounds.RandomPoint(_config.Radius);
            float bestSq = Vec2.SqrDistance(best, player);
            for (int attempt = 0; attempt < 16 && bestSq < minSq; attempt++)
            {
                Vec2 p = _ctx.Bounds.RandomPoint(_config.Radius);
                float sq = Vec2.SqrDistance(p, player);
                if (sq > bestSq)
                {
                    best = p;
                    bestSq = sq;
                }
            }
            return best;
        }
    }
}
