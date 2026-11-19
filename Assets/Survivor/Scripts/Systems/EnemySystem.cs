using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 敌人的生成、追踪、软碰撞（互相挤开）、接触伤害，以及基于网格的空间查询。
    /// 位置 / 击退 / 存活标记只有这里能写；生命由 CombatSystem 写。
    /// 跑得太远的普通敌人会被挪到玩家前方的屏幕外（不让玩家甩掉怪群）。
    /// </summary>
    public sealed class EnemySystem : IEnemySystem
    {
        public const float CellSize = 1.5f;
        public const float PlayerRadius = 0.35f;
        public const float ContactInterval = 0.6f;
        public const float KnockbackSpeed = 5f;
        public const float KnockbackDamping = 9f;
        public const float RelocateDistance = 22f;
        public const float RelocateRadius = 13f;
        public const float SeparationStrength = 0.6f;

        private readonly ISurvivorContext _ctx;
        private readonly int _maxEnemies;
        private readonly List<EnemyEntity> _active = new List<EnemyEntity>();
        private readonly Stack<EnemyEntity> _pool = new Stack<EnemyEntity>();
        private readonly Dictionary<long, List<EnemyEntity>> _grid = new Dictionary<long, List<EnemyEntity>>();
        private readonly Stack<List<EnemyEntity>> _cellPool = new Stack<List<EnemyEntity>>();
        private readonly List<EnemyEntity> _scratch = new List<EnemyEntity>();
        private readonly List<float> _scratchDist = new List<float>();
        private int _nextId = 1;
        private float _maxRadius = 0.5f;

        public IReadOnlyList<EnemyEntity> Active { get { return _active; } }
        public int AliveCount { get; private set; }

        public EnemySystem(ISurvivorContext ctx, int maxEnemies)
        {
            _ctx = ctx;
            _maxEnemies = maxEnemies > 0 ? maxEnemies : 350;
        }

        // ---------------- 生成 / 死亡 ----------------

        public EnemyEntity Spawn(EnemyData data, Vec2 position)
        {
            if (data == null) return null;
            if (AliveCount >= _maxEnemies && !data.Elite && !data.Boss) return null;

            EnemyEntity e = _pool.Count > 0 ? _pool.Pop() : new EnemyEntity();
            e.Id = _nextId++;
            e.Data = data;
            e.Position = position;
            e.Velocity = Vec2.Zero;
            e.Knockback = Vec2.Zero;
            e.MaxHp = data.MaxHp;
            e.Hp = data.MaxHp;
            e.AttackTimer = 0f;
            e.Alive = true;

            _active.Add(e);
            AliveCount++;
            if (data.Radius > _maxRadius) _maxRadius = data.Radius;
            AddToGrid(e); // 生成当帧就能被查询到
            return e;
        }

        public void Despawn(EnemyEntity enemy)
        {
            if (enemy == null || !enemy.Alive) return;
            enemy.Alive = false;
            AliveCount--;
        }

        public void ApplyKnockback(EnemyEntity enemy, Vec2 direction, float strength)
        {
            if (enemy == null || !enemy.Alive || strength <= 0f) return;
            float resist = enemy.Data.KnockbackResist;
            if (resist >= 1f) return;
            enemy.Knockback += direction.Normalized * (KnockbackSpeed * strength * (1f - resist));
        }

        // ---------------- 每步更新 ----------------

        public void Tick(float dt)
        {
            Compact();

            PlayerState player = _ctx.Player.State;
            Vec2 target = player.Position;
            float damping = MathF.Exp(-KnockbackDamping * dt);

            // 1) 追踪 + 击退
            for (int i = 0; i < _active.Count; i++)
            {
                EnemyEntity e = _active[i];
                if (!e.Alive) continue;

                Vec2 to = target - e.Position;
                float dist = to.Magnitude;
                Vec2 dir = dist > 0.001f ? to / dist : Vec2.Zero;
                e.Velocity = dir * e.Data.Speed;
                e.Position += (e.Velocity + e.Knockback) * dt;
                e.Knockback *= damping;

                if (!e.Data.Boss && dist > RelocateDistance) Relocate(e, player);
            }

            RebuildGrid();

            // 2) 软碰撞：与邻居重叠时按体型比例把自己推开（大个子推小个子）。
            for (int i = 0; i < _active.Count; i++)
            {
                EnemyEntity e = _active[i];
                if (!e.Alive) continue;
                Separate(e);
            }

            // 3) 接触伤害
            if (!player.Alive) return;
            for (int i = 0; i < _active.Count; i++)
            {
                EnemyEntity e = _active[i];
                if (!e.Alive) continue;
                if (e.AttackTimer > 0f) e.AttackTimer -= dt;

                float r = e.Data.Radius + PlayerRadius;
                if (Vec2.SqrDistance(e.Position, target) > r * r) continue;
                if (e.AttackTimer > 0f) continue;

                e.AttackTimer = ContactInterval;
                _ctx.Combat.DamagePlayer(e.Data.Damage);
                if (!player.Alive) return;
            }
        }

        private void Separate(EnemyEntity e)
        {
            float reach = e.Data.Radius + _maxRadius;
            int minX = Cell(e.Position.X - reach);
            int maxX = Cell(e.Position.X + reach);
            int minY = Cell(e.Position.Y - reach);
            int maxY = Cell(e.Position.Y + reach);
            Vec2 push = Vec2.Zero;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    List<EnemyEntity> cell;
                    if (!_grid.TryGetValue(Key(cx, cy), out cell)) continue;
                    for (int k = 0; k < cell.Count; k++)
                    {
                        EnemyEntity n = cell[k];
                        if (n == e || !n.Alive) continue;

                        float minDist = e.Data.Radius + n.Data.Radius;
                        Vec2 d = e.Position - n.Position;
                        float sq = d.SqrMagnitude;
                        if (sq >= minDist * minDist) continue;

                        float dist = MathF.Sqrt(sq);
                        Vec2 dir = dist > 0.0001f ? d / dist : Vec2.FromAngle(e.Id * 2.399f); // 完全重合时按 Id 给一个固定方向
                        float weight = n.Data.Radius / (e.Data.Radius + n.Data.Radius);
                        push += dir * ((minDist - dist) * weight);
                    }
                }
            }

            e.Position += Vec2.ClampMagnitude(push * SeparationStrength, 0.25f);
        }

        private void Relocate(EnemyEntity e, PlayerState player)
        {
            Vec2 ahead = player.MoveDir.SqrMagnitude > 0.01f ? player.MoveDir.Normalized : Vec2.FromAngle(NextAngle());
            float jitter = ((float)_ctx.Random.NextDouble() - 0.5f) * 2.1f; // 约 ±60°
            e.Position = player.Position + Vec2.Rotate(ahead, jitter) * RelocateRadius;
            e.Knockback = Vec2.Zero;
        }

        /// <summary>把死亡的实体移出列表放回对象池（交换删除，不保持顺序）。</summary>
        private void Compact()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                EnemyEntity e = _active[i];
                if (e.Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
                e.Data = null;
                _pool.Push(e);
            }
        }

        // ---------------- 空间查询 ----------------

        public void QueryCircle(Vec2 center, float radius, List<EnemyEntity> results)
        {
            results.Clear();
            float reach = radius + _maxRadius;
            int minX = Cell(center.X - reach);
            int maxX = Cell(center.X + reach);
            int minY = Cell(center.Y - reach);
            int maxY = Cell(center.Y + reach);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    List<EnemyEntity> cell;
                    if (!_grid.TryGetValue(Key(cx, cy), out cell)) continue;
                    for (int k = 0; k < cell.Count; k++)
                    {
                        EnemyEntity e = cell[k];
                        if (!e.Alive) continue;
                        float r = radius + e.Data.Radius;
                        if (Vec2.SqrDistance(center, e.Position) <= r * r) results.Add(e);
                    }
                }
            }
        }

        public void GetNearest(Vec2 from, float range, int count, List<EnemyEntity> results)
        {
            results.Clear();
            if (count <= 0) return;
            CollectInRange(from, range);

            // 选择排序前 count 个：count 很小，比整体排序便宜。
            int take = Math.Min(count, _scratch.Count);
            for (int i = 0; i < take; i++)
            {
                int best = i;
                for (int j = i + 1; j < _scratch.Count; j++)
                {
                    if (_scratchDist[j] < _scratchDist[best]) best = j;
                }
                Swap(i, best);
                results.Add(_scratch[i]);
            }
        }

        public void GetRandomInRange(Vec2 center, float range, int count, List<EnemyEntity> results)
        {
            results.Clear();
            if (count <= 0) return;
            CollectInRange(center, range);

            int take = Math.Min(count, _scratch.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = i + _ctx.Random.Next(_scratch.Count - i);
                Swap(i, pick);
                results.Add(_scratch[i]);
            }
        }

        private void CollectInRange(Vec2 center, float range)
        {
            _scratch.Clear();
            _scratchDist.Clear();
            float sqRange = range * range;
            for (int i = 0; i < _active.Count; i++)
            {
                EnemyEntity e = _active[i];
                if (!e.Alive) continue;
                float sq = Vec2.SqrDistance(center, e.Position);
                if (sq > sqRange) continue;
                _scratch.Add(e);
                _scratchDist.Add(sq);
            }
        }

        private void Swap(int a, int b)
        {
            if (a == b) return;
            EnemyEntity e = _scratch[a];
            _scratch[a] = _scratch[b];
            _scratch[b] = e;
            float d = _scratchDist[a];
            _scratchDist[a] = _scratchDist[b];
            _scratchDist[b] = d;
        }

        // ---------------- 网格 ----------------

        private void RebuildGrid()
        {
            foreach (List<EnemyEntity> list in _grid.Values) list.Clear();

            // 玩家一直往一个方向走时格子会越来越多，定期整体回收。
            if (_grid.Count > 4096)
            {
                foreach (List<EnemyEntity> list in _grid.Values) _cellPool.Push(list);
                _grid.Clear();
            }

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Alive) AddToGrid(_active[i]);
            }
        }

        private void AddToGrid(EnemyEntity e)
        {
            long key = Key(Cell(e.Position.X), Cell(e.Position.Y));
            List<EnemyEntity> list;
            if (!_grid.TryGetValue(key, out list))
            {
                list = _cellPool.Count > 0 ? _cellPool.Pop() : new List<EnemyEntity>(8);
                _grid[key] = list;
            }
            list.Add(e);
        }

        private static int Cell(float v)
        {
            return (int)MathF.Floor(v / CellSize);
        }

        private static long Key(int cx, int cy)
        {
            return ((long)cx << 32) ^ (uint)cy;
        }

        private float NextAngle()
        {
            return (float)(_ctx.Random.NextDouble() * Math.PI * 2.0);
        }
    }
}
