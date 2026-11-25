using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 生命增减。玩家与怪物的生命只有这里能改。
    /// 玩家无敌（PlayerState.Invincible：亚空间 / 受伤无敌帧 / 调试无敌）时，所有伤害都在这一个入口被拒绝。
    /// 怪物死亡顺序：标记死亡（MonsterSystem，毒腐体在这里留毒潭）→ 掉经验球 → 精英 / Boss 掉宝箱 → 计击杀 → 发事件。
    /// 吸血、暴击等武器词条在武器里结算，这里只管扣血与死亡。
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

        public void DamageMonster(MonsterEntity monster, float amount, DamageSource source, bool critical = false)
        {
            if (monster == null || !monster.Alive || amount <= 0f) return;
            monster.Hp -= amount;
            _ctx.Events.Publish(new MonsterDamagedEvent
            {
                MonsterId = monster.Id,
                Position = monster.Position,
                Amount = amount,
                Source = source,
                Critical = critical,
            });
            if (monster.Hp <= 0f) Kill(monster, source);
        }

        private void Kill(MonsterEntity monster, DamageSource source)
        {
            monster.Hp = 0f;
            _ctx.Monsters.Despawn(monster);
            _ctx.Experience.SpawnOrb(monster.Position, monster.XpValue);
            if (monster.Rank > 0) _ctx.Experience.SpawnChest(monster.Position, monster.Rank);
            _ctx.Progression.AddKill();
            if (source == DamageSource.Weapon || source == DamageSource.Burst)
                _ctx.Events.Publish(new WeaponKillConfirmedEvent
                {
                    MonsterId = monster.Id, Weapon = _ctx.Loadout.Equipped, Source = source, Position = monster.Position,
                });
            _ctx.Events.Publish(new MonsterDiedEvent { MonsterId = monster.Id, Position = monster.Position, Rank = monster.Rank });
        }
    }
}
