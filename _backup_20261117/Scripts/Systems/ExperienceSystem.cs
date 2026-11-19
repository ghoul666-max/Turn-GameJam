using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>经验球：怪物死亡时生成；进入吸附半径后飞向玩家，进入拾取半径后加经验。</summary>
    public sealed class ExperienceSystem : IExperienceSystem
    {
        private readonly ITurnContext _ctx;
        private readonly ExperienceConfig _config;
        private readonly List<OrbEntity> _active = new List<OrbEntity>();
        private int _nextId = 1;

        public IReadOnlyList<OrbEntity> Active { get { return _active; } }

        public ExperienceSystem(ITurnContext ctx, ExperienceConfig config)
        {
            _ctx = ctx;
            _config = config;
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

        public void Tick(float dt)
        {
            PlayerState player = _ctx.Player.State;
            if (player.Alive)
            {
                float magnetSq = _config.MagnetRadius * _config.MagnetRadius;
                float pickupSq = _config.PickupRadius * _config.PickupRadius;

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
                        float move = _config.MagnetSpeed * dt;
                        orb.Position = dist <= move ? player.Position : orb.Position + to / dist * move;
                        sq = Vec2.SqrDistance(player.Position, orb.Position);
                    }
                    if (sq <= pickupSq)
                    {
                        orb.Alive = false;
                        _ctx.Progression.AddXp(orb.Value);
                        _ctx.Events.Publish(new OrbCollectedEvent { Value = orb.Value });
                    }
                }
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Alive) continue;
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
        }
    }
}
