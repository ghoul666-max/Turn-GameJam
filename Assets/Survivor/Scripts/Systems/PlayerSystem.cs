using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>玩家移动、朝向、被动道具与属性计算。生命由 CombatSystem 负责（初始化除外）。</summary>
    public sealed class PlayerSystem : IPlayerSystem
    {
        public const int MaxPassiveSlots = 6;

        private readonly ISurvivorContext _ctx;
        private readonly List<PassiveInstance> _passives = new List<PassiveInstance>();
        private Vec2 _input;

        public PlayerState State { get; private set; }
        public CharacterData Character { get; private set; }
        public IReadOnlyList<PassiveInstance> Passives { get { return _passives; } }
        public bool CanAddPassive { get { return _passives.Count < MaxPassiveSlots; } }

        public float MaxHp
        {
            get { return (Character != null ? Character.MaxHp : 100f) * GetStat(StatId.MaxHp); }
        }

        public float MoveSpeed
        {
            get { return (Character != null ? Character.MoveSpeed : 3f) * GetStat(StatId.MoveSpeed); }
        }

        public float PickupRadius
        {
            get { return (Character != null ? Character.PickupRadius : 1.5f) * GetStat(StatId.Magnet); }
        }

        public PlayerSystem(ISurvivorContext ctx)
        {
            _ctx = ctx;
            State = new PlayerState();
        }

        public void Setup(CharacterData character)
        {
            Character = character;
            _passives.Clear();
            State = new PlayerState();
            State.Hp = MaxHp;
            State.Alive = true;
        }

        public void SetMoveInput(Vec2 input)
        {
            _input = Vec2.ClampMagnitude(input, 1f);
        }

        public void Tick(float dt)
        {
            if (!State.Alive)
            {
                State.MoveDir = Vec2.Zero;
                return;
            }

            State.MoveDir = _input;
            if (_input.SqrMagnitude < 0.0001f) return;

            State.Position += _input * (MoveSpeed * dt);
            State.Facing = _input.Normalized;
            if (MathF.Abs(_input.X) > 0.1f) State.FacingX = _input.X > 0f ? 1 : -1;
        }

        public float GetStat(StatId id)
        {
            float bonus = 0f;
            for (int i = 0; i < _passives.Count; i++)
            {
                PassiveInstance p = _passives[i];
                if (p.Data.Stat == id) bonus += p.Data.AmountPerLevel * p.Level;
            }

            switch (id)
            {
                case StatId.Cooldown: return MathF.Max(0.2f, 1f - bonus);
                case StatId.Amount: return bonus;
                default: return 1f + bonus;
            }
        }

        public PassiveInstance GetPassive(string id)
        {
            for (int i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].Data.Id == id) return _passives[i];
            }
            return null;
        }

        public bool TryAddOrLevelPassive(PassiveData data)
        {
            if (data == null) return false;
            float oldMax = MaxHp;

            PassiveInstance existing = GetPassive(data.Id);
            if (existing != null)
            {
                if (existing.IsMaxLevel) return false;
                existing.Level++;
            }
            else
            {
                if (!CanAddPassive) return false;
                _passives.Add(new PassiveInstance { Data = data, Level = 1 });
            }

            // 最大生命提高时，把多出来的部分补上（经由 CombatSystem，保持生命的单一写入）。
            float gained = MaxHp - oldMax;
            if (gained > 0f) _ctx.Combat.HealPlayer(gained);
            return true;
        }
    }
}
