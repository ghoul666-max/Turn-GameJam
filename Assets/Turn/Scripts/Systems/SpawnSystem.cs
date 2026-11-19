using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 刷怪：开局一批、定时刷（优先用当前地图的刷怪点）、翻滚惩罚。
    /// 所有新怪物都通过 IMonsterSystem.Spawn 加入同一个管理列表；这里永远不会动已有的怪物。
    /// 翻滚惩罚不受常规存活上限限制：每次成功翻滚固定生成 PenaltyConfig.Count 只。
    /// </summary>
    public sealed class SpawnSystem : ISpawnSystem
    {
        /// <summary>刷怪点附近的随机散布半径。</summary>
        public const float SpawnPointJitter = 1f;

        private readonly ITurnContext _ctx;
        private readonly SpawnConfig _spawn;
        private readonly PenaltyConfig _penalty;
        private readonly List<Vec2> _candidates = new List<Vec2>();
        private float _timer;

        public SpawnSystem(ITurnContext ctx, SpawnConfig spawn, PenaltyConfig penalty)
        {
            _ctx = ctx;
            _spawn = spawn;
            _penalty = penalty;
        }

        public void SpawnInitial()
        {
            for (int i = 0; i < _spawn.InitialCount; i++) SpawnRegular();
            _timer = _spawn.Interval;
        }

        public void Tick(float dt, float time)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer += MathF.Max(0.1f, _spawn.Interval);

            int ramp = _spawn.RampInterval > 0f ? (int)(time / _spawn.RampInterval) * _spawn.RampAmount : 0;
            int count = _spawn.CountPerSpawn + ramp;
            for (int i = 0; i < count; i++)
            {
                if (_ctx.Monsters.AliveCount >= _spawn.MaxAlive) break;
                SpawnRegular();
            }
        }

        public int SpawnPenalty()
        {
            int count = Math.Max(0, _penalty.Count);
            for (int i = 0; i < count; i++) _ctx.Monsters.Spawn(PenaltyPosition(), true);
            if (count > 0) _ctx.Events.Publish(new PenaltySpawnedEvent { Count = count });
            return count;
        }

        // ---------------- 位置 ----------------

        private void SpawnRegular()
        {
            _ctx.Monsters.Spawn(RegularPosition(), false);
        }

        /// <summary>当前地图的刷怪点里离玩家不小于 MinDistance 的随机一个；没有可用点时在玩家周围的圆环上找。</summary>
        private Vec2 RegularPosition()
        {
            Vec2 player = _ctx.Player.State.Position;
            List<Vec2> points = _ctx.Config.SpawnPoints[(int)_ctx.Map.Active];
            float minSq = _spawn.MinDistance * _spawn.MinDistance;

            _candidates.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (Vec2.SqrDistance(points[i], player) >= minSq) _candidates.Add(points[i]);
            }
            if (_candidates.Count > 0)
            {
                Vec2 p = _candidates[_ctx.Random.Next(_candidates.Count)];
                return p + Vec2.FromAngle(NextAngle()) * ((float)_ctx.Random.NextDouble() * SpawnPointJitter);
            }
            return RingPosition(player, _spawn.MinDistance, _spawn.MaxDistance);
        }

        private Vec2 PenaltyPosition()
        {
            Vec2 player = _ctx.Player.State.Position;
            if (_penalty.Area == PenaltyArea.AnywhereInBounds)
            {
                float minSq = _penalty.MinDistance * _penalty.MinDistance;
                Vec2 best = _ctx.Bounds.RandomPoint(0.5f);
                for (int attempt = 0; attempt < 12 && Vec2.SqrDistance(best, player) < minSq; attempt++)
                {
                    best = _ctx.Bounds.RandomPoint(0.5f);
                }
                return best;
            }
            return RingPosition(player, _penalty.MinDistance, _penalty.MaxDistance);
        }

        /// <summary>玩家周围 [minDist, maxDist] 圆环上的随机点，限制进边界；被边界压得太近就换个角度重试。</summary>
        private Vec2 RingPosition(Vec2 center, float minDist, float maxDist)
        {
            float lo = MathF.Max(0f, MathF.Min(minDist, maxDist));
            float hi = MathF.Max(minDist, maxDist);
            float wantSq = lo * 0.75f * (lo * 0.75f);

            Vec2 best = center;
            float bestSq = -1f;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float r = lo + (float)_ctx.Random.NextDouble() * (hi - lo);
                Vec2 p = _ctx.Bounds.Clamp(center + Vec2.FromAngle(NextAngle()) * r, 0.5f);
                float sq = Vec2.SqrDistance(p, center);
                if (sq >= wantSq) return p;
                if (sq > bestSq)
                {
                    best = p;
                    bestSq = sq;
                }
            }
            return best;
        }

        private float NextAngle()
        {
            return (float)(_ctx.Random.NextDouble() * Math.PI * 2.0);
        }
    }
}
