using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 掉落物：敌人死亡掉经验宝石（偶尔掉回复道具），进入拾取半径的宝石会加速飞向玩家。
    /// 宝石数量到上限后，新掉落的经验并进已有的宝石里（不丢经验，也不让画面爆炸）。
    /// </summary>
    public sealed class PickupSystem : IPickupSystem
    {
        public const int MaxGems = 400;
        public const float CollectRadius = 0.45f;
        public const float HealCollectRadius = 0.6f;
        public const float MagnetStartSpeed = 3f;
        public const float MagnetAcceleration = 28f;
        public const float MagnetMaxSpeed = 18f;
        public const int HealValue = 30;

        private readonly ISurvivorContext _ctx;
        private readonly List<PickupEntity> _active = new List<PickupEntity>();
        private readonly Stack<PickupEntity> _pool = new Stack<PickupEntity>();
        private int _nextId = 1;
        private int _gemCount;

        public IReadOnlyList<PickupEntity> Active { get { return _active; } }

        public PickupSystem(ISurvivorContext ctx)
        {
            _ctx = ctx;
        }

        public void SpawnDrop(EnemyEntity enemy)
        {
            if (enemy == null || enemy.Data == null) return;
            SpawnGem(enemy.Position, enemy.Data.Xp);

            if (enemy.Data.HealDropChance > 0f && _ctx.Random.NextDouble() < enemy.Data.HealDropChance)
            {
                Spawn(PickupKind.Heal, enemy.Position + new Vec2(0.35f, 0.2f), HealValue);
            }
        }

        public void SpawnGem(Vec2 position, int value)
        {
            if (value <= 0) return;

            if (_gemCount >= MaxGems)
            {
                PickupEntity target = FindAnyGem();
                if (target != null)
                {
                    target.Value += value;
                    return;
                }
            }
            Spawn(PickupKind.Gem, position, value);
        }

        public void Tick(float dt)
        {
            Compact();

            PlayerState player = _ctx.Player.State;
            if (!player.Alive) return;

            float magnet = _ctx.Player.PickupRadius;
            float sqMagnet = magnet * magnet;

            for (int i = 0; i < _active.Count; i++)
            {
                PickupEntity p = _active[i];
                if (!p.Alive) continue;

                Vec2 to = player.Position - p.Position;
                float sq = to.SqrMagnitude;

                if (p.Kind == PickupKind.Gem)
                {
                    if (!p.Magnetized && sq <= sqMagnet)
                    {
                        p.Magnetized = true;
                        p.Speed = MagnetStartSpeed;
                    }
                    if (p.Magnetized)
                    {
                        p.Speed = MathF.Min(MagnetMaxSpeed, p.Speed + MagnetAcceleration * dt);
                        float dist = MathF.Sqrt(sq);
                        if (dist > 0.0001f) p.Position += to / dist * MathF.Min(dist, p.Speed * dt);
                        sq = Vec2.SqrDistance(player.Position, p.Position);
                    }
                    if (sq <= CollectRadius * CollectRadius) Collect(p);
                }
                else if (sq <= HealCollectRadius * HealCollectRadius)
                {
                    Collect(p);
                }
            }
        }

        private void Collect(PickupEntity p)
        {
            p.Alive = false;
            if (p.Kind == PickupKind.Gem)
            {
                _gemCount--;
                _ctx.Progression.AddXp(p.Value);
            }
            else
            {
                _ctx.Combat.HealPlayer(p.Value);
            }
            _ctx.Events.Publish(new PickupCollectedEvent { Kind = p.Kind, Value = p.Value, Position = p.Position });
        }

        private PickupEntity Spawn(PickupKind kind, Vec2 position, int value)
        {
            PickupEntity p = _pool.Count > 0 ? _pool.Pop() : new PickupEntity();
            p.Id = _nextId++;
            p.Kind = kind;
            p.Position = position;
            p.Value = value;
            p.Magnetized = false;
            p.Speed = 0f;
            p.Alive = true;
            _active.Add(p);
            if (kind == PickupKind.Gem) _gemCount++;
            return p;
        }

        private PickupEntity FindAnyGem()
        {
            if (_active.Count == 0) return null;
            int start = _ctx.Random.Next(_active.Count);
            for (int k = 0; k < _active.Count; k++)
            {
                PickupEntity p = _active[(start + k) % _active.Count];
                if (p.Alive && p.Kind == PickupKind.Gem && !p.Magnetized) return p;
            }
            return null;
        }

        private void Compact()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                PickupEntity p = _active[i];
                if (p.Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
                _pool.Push(p);
            }
        }
    }
}
