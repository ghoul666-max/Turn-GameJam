using System;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>
    /// 伤害公式与生命增减。玩家与敌人的生命只有这里能改。
    /// 敌人死亡时按顺序：标记死亡（EnemySystem）→ 掉落（PickupSystem）→ 计击杀（ProgressionSystem）→ 发事件。
    /// </summary>
    public sealed class CombatSystem : ICombatSystem
    {
        private readonly ISurvivorContext _ctx;

        public CombatSystem(ISurvivorContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>伤害 = 基础 × 力量（菠菜），至少 1。</summary>
        public float CalculateDamage(float baseDamage)
        {
            return MathF.Max(1f, baseDamage * _ctx.Player.GetStat(StatId.Might));
        }

        public void DamageEnemy(EnemyEntity enemy, float baseDamage, Vec2 knockDir, float knockback)
        {
            if (enemy == null || !enemy.Alive) return;

            float amount = CalculateDamage(baseDamage);
            enemy.Hp -= amount;
            _ctx.Events.Publish(new EnemyDamagedEvent { EnemyId = enemy.Id, Position = enemy.Position, Amount = amount });

            if (enemy.Hp <= 0f)
            {
                Kill(enemy);
                return;
            }
            if (knockback > 0f) _ctx.Enemies.ApplyKnockback(enemy, knockDir, knockback);
        }

        public void DamagePlayer(float amount)
        {
            PlayerState player = _ctx.Player.State;
            if (!player.Alive || amount <= 0f) return;

            player.Hp -= amount;
            if (player.Hp <= 0f)
            {
                player.Hp = 0f;
                player.Alive = false;
            }
            _ctx.Events.Publish(new PlayerDamagedEvent { Amount = amount, Hp = player.Hp });
        }

        public void HealPlayer(float amount)
        {
            PlayerState player = _ctx.Player.State;
            if (!player.Alive || amount <= 0f) return;

            float before = player.Hp;
            player.Hp = MathF.Min(_ctx.Player.MaxHp, player.Hp + amount);
            float healed = player.Hp - before;
            if (healed > 0f) _ctx.Events.Publish(new PlayerHealedEvent { Amount = healed, Hp = player.Hp });
        }

        private void Kill(EnemyEntity enemy)
        {
            enemy.Hp = 0f;
            _ctx.Enemies.Despawn(enemy);
            _ctx.Pickups.SpawnDrop(enemy);
            _ctx.Progression.AddKill();
            _ctx.Events.Publish(new EnemyDiedEvent
            {
                EnemyId = enemy.Id,
                Position = enemy.Position,
                SpriteKey = enemy.Data.SpriteKey,
                Scale = enemy.Data.Scale,
                Elite = enemy.Data.Elite,
                Boss = enemy.Data.Boss,
            });
        }
    }
}
