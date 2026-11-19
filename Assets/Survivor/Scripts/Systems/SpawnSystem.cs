using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>按关卡时间轴刷怪：持续波次 + 定时事件（精英 / 首领 / 包围圈）。只通过 IEnemySystem.Spawn 生成。</summary>
    public sealed class SpawnSystem : ISpawnSystem
    {
        /// <summary>刷怪圈半径：略大于屏幕对角线的一半，保证在画面外出现。</summary>
        public const float SpawnRadius = 13f;
        public const float SpawnRadiusJitter = 2f;

        private readonly ISurvivorContext _ctx;
        private readonly StageData _stage;
        private readonly float[] _waveTimers;
        private readonly List<SpawnEventData> _events = new List<SpawnEventData>();
        private int _nextEvent;

        public SpawnSystem(ISurvivorContext ctx, StageData stage)
        {
            _ctx = ctx;
            _stage = stage;
            _waveTimers = new float[stage.Waves.Count];
            _events.AddRange(stage.Events);
            _events.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public void Tick(float dt, float time)
        {
            for (int i = 0; i < _stage.Waves.Count; i++)
            {
                SpawnWave wave = _stage.Waves[i];
                if (time < wave.Start || time >= wave.End || wave.Pool.Count == 0) continue;

                _waveTimers[i] -= dt;
                float interval = MathF.Max(0.05f, wave.Interval);
                while (_waveTimers[i] <= 0f)
                {
                    _waveTimers[i] += interval;
                    for (int c = 0; c < wave.Count; c++)
                    {
                        EnemyData data = wave.Pool[_ctx.Random.Next(wave.Pool.Count)];
                        SpawnScatter(data);
                    }
                }
            }

            while (_nextEvent < _events.Count && _events[_nextEvent].Time <= time)
            {
                Fire(_events[_nextEvent]);
                _nextEvent++;
            }
        }

        private void Fire(SpawnEventData ev)
        {
            if (!string.IsNullOrEmpty(ev.Announce))
            {
                bool boss = ev.Enemy != null && ev.Enemy.Boss;
                _ctx.Events.Publish(new WaveAnnouncedEvent { Message = ev.Announce, Boss = boss });
            }
            if (ev.Enemy == null) return;

            int count = Math.Max(1, ev.Count);
            if (ev.Pattern == SpawnPattern.Ring)
            {
                Vec2 center = _ctx.Player.State.Position;
                float offset = NextAngle();
                for (int i = 0; i < count; i++)
                {
                    float angle = offset + i * (float)(Math.PI * 2.0) / count;
                    _ctx.Enemies.Spawn(ev.Enemy, center + Vec2.FromAngle(angle) * (SpawnRadius * 0.85f));
                }
            }
            else
            {
                for (int i = 0; i < count; i++) SpawnScatter(ev.Enemy);
            }
        }

        private void SpawnScatter(EnemyData data)
        {
            float radius = SpawnRadius + (float)_ctx.Random.NextDouble() * SpawnRadiusJitter;
            Vec2 position = _ctx.Player.State.Position + Vec2.FromAngle(NextAngle()) * radius;
            _ctx.Enemies.Spawn(data, position);
        }

        private float NextAngle()
        {
            return (float)(_ctx.Random.NextDouble() * Math.PI * 2.0);
        }
    }
}
