using System;
using Turn.Core;
using Turn.Monsters;

namespace Turn.Systems
{
    /// <summary>
    /// 刷怪（新策划案 11/24）：
    ///   · 常规：每 SpawnInterval 秒从地图边缘随机位置刷一批，每批数量 = BaseCount + CountPerWave × (波次 − 1)（线性，小数部分累积），
    ///     存活上限 MaxAlive；种类从已解锁的里面按权重随机（游荡尸第 1 波、弹幕孢第 3 波、疾冲兽第 5 波、毒腐体第 7 波、狙击械第 10 波）；
    ///   · 每波开始：第 EliteFromWave 波起刷 1 只精英；每 BossEveryWaves 波刷 1 只 Boss（精英 / Boss 死亡掉宝箱）。
    /// 只通过 IMonsterSystem 生成。亚空间里 SessionSystem 不调用 Tick。
    /// </summary>
    public sealed class SpawnSystem : ISpawnSystem
    {
        private readonly ITurnContext _ctx;
        private readonly HordeConfig _horde;
        private float _timer;
        private float _fraction;
        private int _handledWave = 1;

        public SpawnSystem(ITurnContext ctx, HordeConfig horde)
        {
            _ctx = ctx;
            _horde = horde ?? new HordeConfig();
        }

        public void SpawnInitial()
        {
            int count = Math.Max(0, (int)MathF.Floor(_horde.BaseCount));
            for (int i = 0; i < count; i++) _ctx.Monsters.Spawn(EdgePoint(), false);
            _timer = MathF.Max(0.1f, _horde.SpawnInterval);
        }

        public void Tick(float dt, float time)
        {
            int wave = Math.Max(1, _ctx.Session.Wave);
            while (_handledWave < wave)
            {
                _handledWave++;
                OnWaveStarted(_handledWave);
            }

            _timer -= dt;
            if (_timer > 0f) return;
            _timer += MathF.Max(0.1f, _horde.SpawnInterval);

            float exact = MathF.Max(0f, _horde.BaseCount + _horde.CountPerWave * (wave - 1)) + _fraction;
            int count = (int)MathF.Floor(exact);
            _fraction = exact - count;
            for (int i = 0; i < count; i++)
            {
                if (_ctx.Monsters.AliveCount >= Math.Max(1, _horde.MaxAlive)) break;
                _ctx.Monsters.Spawn(EdgePoint(), false);
            }
        }

        private void OnWaveStarted(int wave)
        {
            if (_horde.EliteFromWave > 0 && wave >= _horde.EliteFromWave)
                _ctx.Monsters.SpawnKind(EdgePoint(), MonsterSpawnPool.PickUnlocked(_horde, wave, _ctx.Random), 1);
            if (_horde.BossEveryWaves > 0 && wave % _horde.BossEveryWaves == 0)
                _ctx.Monsters.SpawnKind(EdgePoint(), MonsterSpawnPool.PickUnlocked(_horde, wave, _ctx.Random), 2);
        }

        public int SpawnPenalty(int count)
        {
            count = Math.Max(0, count);
            for (int i = 0; i < count; i++) _ctx.Monsters.Spawn(EdgePoint(), true);
            if (count > 0) _ctx.Events.Publish(new PenaltySpawnedEvent { Count = count });
            return count;
        }

        /// <summary>地图边缘的随机点：按边长加权选一条边，离玩家太近或压在遮挡物上就重新随机（最多 12 次）。</summary>
        public Vec2 EdgePoint()
        {
            BoundsRect rect = _ctx.Bounds.Rect;
            float inset = MathF.Max(0f, _horde.EdgeInset);
            float minX = rect.Min.X + inset, maxX = rect.Max.X - inset;
            float minY = rect.Min.Y + inset, maxY = rect.Max.Y - inset;
            if (maxX < minX) maxX = minX;
            if (maxY < minY) maxY = minY;
            float w = maxX - minX, h = maxY - minY;
            Vec2 player = _ctx.Player.State.Position;
            float minSq = _horde.MinPlayerDistance * _horde.MinPlayerDistance;
            Vec2 best = new Vec2(minX, minY);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float roll = (float)_ctx.Random.NextDouble() * (2f * (w + h));
                Vec2 p;
                if (roll < w) p = new Vec2(minX + roll, minY);
                else if (roll < 2f * w) p = new Vec2(minX + roll - w, maxY);
                else if (roll < 2f * w + h) p = new Vec2(minX, minY + roll - 2f * w);
                else p = new Vec2(maxX, minY + roll - 2f * w - h);
                best = p;
                if (Vec2.SqrDistance(p, player) < minSq) continue;
                if (_ctx.Obstacles.Overlaps(p, _horde.Radius, ObstacleMask.Units)) continue;
                return p;
            }
            return best;
        }
    }
}
