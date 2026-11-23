using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 生命增减。玩家与怪物的生命只有这里能改。
    /// 玩家无敌（PlayerState.InvincibleLeft &gt; 0）时，怪物 / 鲨鱼 / 岩浆的伤害全部在这一个入口被拒绝。
    /// 怪物死亡顺序：标记死亡（MonsterSystem）→ 掉经验球（ExperienceSystem）→ 计击杀（ProgressionSystem）→ 发事件。
    /// </summary>
    public sealed class CombatSystem : ICombatSystem
    {
        private readonly ITurnContext _ctx;

        public CombatSystem(ITurnContext ctx)
        {
            _ctx = ctx;
        }

        public bool DamagePlayer(float amount, DamageSource source)
        {
            PlayerState player = _ctx.Player.State;
            if (!player.Alive || amount <= 0f) return false;

            if (player.Invincible)
            {
                _ctx.Events.Publish(new DamageBlockedEvent { Source = source });
                return false;
            }

            player.Hp -= amount;
            if (player.Hp <= 0f)
            {
                player.Hp = 0f;
                player.Alive = false;
            }
            _ctx.Events.Publish(new PlayerDamagedEvent { Amount = amount, Hp = player.Hp, Source = source });
            if (!player.Alive) _ctx.Events.Publish(new PlayerDiedEvent());
            return true;
        }

        public void HealPlayer(float amount)
        {
            PlayerState player = _ctx.Player.State;
            if (!player.Alive || amount <= 0f) return;

            float before = player.Hp;
            player.Hp = MathF.Min(player.MaxHp, player.Hp + amount);
            float healed = player.Hp - before;
            if (healed > 0f) _ctx.Events.Publish(new PlayerHealedEvent { Amount = healed, Hp = player.Hp });
        }

        public void DamageMonster(MonsterEntity monster, float amount, DamageSource source)
        {
            if (monster == null || !monster.Alive || amount <= 0f) return;
            if (source == DamageSource.Weapon)
            {
                IProgressionSystem builds = _ctx.Progression;
                int damageLevel = builds.GetLevel(BuildIds.Query(BuildId.B01));
                amount *= 1f + damageLevel * _ctx.Config.Upgrades.DamagePerLevel;
                int hazardLevel = builds.GetLevel(BuildIds.Query(BuildId.B10));
                if (hazardLevel > 0 && _ctx.Environment.IsHazard(monster.Position))
                    amount *= 1f + hazardLevel * _ctx.Config.Upgrades.HazardDamagePerLevel;
            }
            float applied = MathF.Min(monster.Hp, amount);
            monster.Hp -= amount;
            if (source == DamageSource.Weapon && applied > 0f)
            {
                int level = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B07));
                if (level > 0) HealPlayer(applied * level * _ctx.Config.Upgrades.LifestealPerLevel);
            }
            _ctx.Events.Publish(new MonsterDamagedEvent
            {
                MonsterId = monster.Id,
                Position = monster.Position,
                Amount = amount,
                Source = source,
            });
            if (monster.Hp <= 0f) Kill(monster, source);
        }

        private void Kill(MonsterEntity monster, DamageSource source)
        {
            monster.Hp = 0f;
            _ctx.Monsters.Despawn(monster);
            _ctx.Experience.SpawnOrb(monster.Position, monster.XpValue);
            _ctx.Progression.AddKill();
            if (source == DamageSource.Weapon)
            {
                int refund = _ctx.Progression.GetLevel(BuildIds.Query(BuildId.B11));
                if (refund > 0)
                    _ctx.Events.Publish(new WeaponKillConfirmedEvent { MonsterId = monster.Id, Form = monster.Form });
            }
            _ctx.Events.Publish(new MonsterDiedEvent { MonsterId = monster.Id, Position = monster.Position });
        }
    }
}
