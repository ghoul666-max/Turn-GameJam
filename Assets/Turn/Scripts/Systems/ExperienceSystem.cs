using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 经验球与宝箱（新策划案）。
    ///   · 经验球：怪物死亡时生成；玩家进入拾取范围（PickupRange，策划 100 像素 = 2.5）后自动飞向玩家并捡起。
    ///   · 宝箱：精英 / Boss 死亡时生成；玩家靠近到 ChestOpenRadius 自动打开 → Progression.AddChest()，Session 下一步弹三选一。
    /// </summary>
    public sealed class ExperienceSystem : IExperienceSystem
    {
        /// <summary>经验球飞到玩家身边多近算捡到。</summary>
        public const float CollectRadius = 0.35f;

        private readonly ITurnContext _ctx;
        private readonly ExperienceConfig _config;
        private readonly List<OrbEntity> _active = new List<OrbEntity>();
        private readonly List<ChestEntity> _chests = new List<ChestEntity>();
        private int _nextId = 1;
        private int _nextChestId = 1;

        public IReadOnlyList<OrbEntity> Active { get { return _active; } }
        public IReadOnlyList<ChestEntity> Chests { get { return _chests; } }

        public ExperienceSystem(ITurnContext ctx, ExperienceConfig config)
        {
            _ctx = ctx;
            _config = config ?? new ExperienceConfig();
        }

        public void SpawnOrb(Vec2 position, int value)
        {
            if (value <= 0) return;
            var orb = new OrbEntity();
            orb.Id = _nextId++;
            orb.Position = _ctx.Bounds.Clamp(position, 0.2f);
            orb.Value = value;
            orb.Alive = true;
            _active.Add(orb);
        }

        public void SpawnChest(Vec2 position, int rank)
        {
            var chest = new ChestEntity
            {
                Id = _nextChestId++,
                Position = _ctx.Bounds.Clamp(position, 0.3f),
                Rank = rank,
                Alive = true,
            };
            _chests.Add(chest);
            _ctx.Events.Publish(new ChestSpawnedEvent { ChestId = chest.Id, Position = chest.Position, Rank = rank });
        }

        public void Tick(float dt)
        {
            PlayerState player = _ctx.Player.State;
            if (player.Alive)
            {
                float magnet = MathF.Max(CollectRadius, _config.PickupRange);
                float magnetSq = magnet * magnet;
                float collectSq = CollectRadius * CollectRadius;
                float speed = MathF.Max(1f, _config.MagnetSpeed);

                for (int i = 0; i < _active.Count; i++)
                {
                    OrbEntity orb = _active[i];
                    if (!orb.Alive) continue;

                    Vec2 to = player.Position - orb.Position;
                    float sq = to.SqrMagnitude;
                    if (!orb.Magnetized && sq <= magnetSq) orb.Magnetized = true;
                    if (orb.Magnetized)
                    {
                        float dist = to.Magnitude;
                        float move = speed * dt;
                        orb.Position = dist <= move ? player.Position : orb.Position + to / dist * move;
                        sq = Vec2.SqrDistance(player.Position, orb.Position);
                    }
                    if (sq <= collectSq)
                    {
                        orb.Alive = false;
                        _ctx.Progression.AddXp(orb.Value);
                        _ctx.Events.Publish(new OrbCollectedEvent { Value = orb.Value });
                    }
                }

                float open = MathF.Max(0.1f, _config.ChestOpenRadius) + _ctx.Player.Config.Radius;
                for (int i = 0; i < _chests.Count; i++)
                {
                    ChestEntity chest = _chests[i];
                    if (!chest.Alive || Vec2.SqrDistance(chest.Position, player.Position) > open * open) continue;
                    chest.Alive = false;
                    _ctx.Progression.AddChest();
                    _ctx.Events.Publish(new ChestOpenedEvent { ChestId = chest.Id, Position = chest.Position });
                }
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
            for (int i = _chests.Count - 1; i >= 0; i--)
                if (!_chests[i].Alive) _chests.RemoveAt(i);
        }
    }
}
