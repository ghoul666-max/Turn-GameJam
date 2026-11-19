using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 翻滚（Turn）。整个游戏的核心操作，四项效果只在 TryRoll 这一个方法里、按固定顺序原子执行：
    ///   ① 翻滚位移 + 无敌帧   ② 切换地图（+ 鲨鱼同步 + 切到火山时岩浆喷发一次）   ③ 装满当前弹匣   ④ 翻滚惩罚刷怪
    /// 任何一项都不会单独发生：切图、装填、惩罚刷怪的唯一调用方就是这里。
    /// 冷却中（或翻滚中、非 Playing 阶段、玩家已死亡）调用 TryRoll 直接返回 false，四项都不执行。
    /// 写 PlayerState 的 IsRolling / RollTimer / RollDirection / RollSpeed / RollCooldownLeft / InvincibleLeft。
    /// </summary>
    public sealed class RollSystem : IRollSystem
    {
        private readonly ITurnContext _ctx;
        private readonly RollConfig _config;
        private readonly UpgradeConfig _upgrades;
        private readonly List<MonsterEntity> _hits = new List<MonsterEntity>();

        public int RollCount { get; private set; }

        public RollSystem(ITurnContext ctx, RollConfig config, UpgradeConfig upgrades)
        {
            _ctx = ctx;
            _config = config;
            _upgrades = upgrades;
        }

        /// <summary>冷却 = 基础 − 词条等级 × 每级缩短，不低于下限。</summary>
        public float EffectiveCooldown
        {
            get
            {
                int level = _ctx.Progression.GetLevel(UpgradeId.RollCooldown);
                return MathF.Max(_config.MinCooldown, _config.Cooldown - level * _upgrades.RollCooldownPerLevel);
            }
        }

        public float EffectiveInvincibleTime
        {
            get
            {
                int level = _ctx.Progression.GetLevel(UpgradeId.RollInvincible);
                return _config.InvincibleTime + level * _upgrades.RollInvinciblePerLevel;
            }
        }

        public bool CanRoll
        {
            get
            {
                PlayerState s = _ctx.Player.State;
                return _ctx.Session.Phase == RunPhase.Playing && s.Alive && !s.IsRolling && s.RollCooldownLeft <= 0f;
            }
        }

        public bool TryRoll()
        {
            PlayerState s = _ctx.Player.State;
            if (!CanRoll)
            {
                if (_ctx.Session.Phase == RunPhase.Playing && s.Alive)
                {
                    _ctx.Events.Publish(new RollRejectedEvent { CooldownLeft = s.RollCooldownLeft });
                }
                return false;
            }

            // ① 翻滚位移 + 无敌帧 + 全局冷却
            Vec2 dir = s.MoveDir.SqrMagnitude > 0.01f ? s.MoveDir.Normalized : s.Facing.Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            float duration = MathF.Max(0.05f, _config.Duration);
            float invincible = EffectiveInvincibleTime;

            s.IsRolling = true;
            s.RollTimer = duration;
            s.RollDirection = dir;
            s.RollSpeed = _config.Distance / duration;
            s.RollCooldownLeft = EffectiveCooldown;
            s.InvincibleLeft = MathF.Max(s.InvincibleLeft, invincible);
            RollCount++;

            // ② 切换地图：只改激活地图；玩家与怪物的世界坐标、血量、Buff 都不动。鲨鱼按新地图生成 / 销毁。
            MapId from = _ctx.Map.Active;
            _ctx.Map.Switch();
            _ctx.Sharks.SyncWithMap();
            _ctx.Lava.SyncWithMap(); // 切到火山：岩浆范围内的怪物受一次伤害，然后岩浆消失（惩罚怪在这之后生成，不吃这一下）

            // ③ 装满当前弹匣（形态已随地图切换）。这是整个游戏里唯一的装填入口。
            int ammoBefore = _ctx.Weapons.Ammo;
            _ctx.Weapons.Reload();

            // ④ 翻滚惩罚：固定一批新怪物加入现有管理体系，不影响原有怪物。
            int penalty = _ctx.Spawner.SpawnPenalty();

            _ctx.Events.Publish(new RollPerformedEvent
            {
                Position = s.Position,
                Direction = dir,
                FromMap = from,
                ToMap = _ctx.Map.Active,
                AmmoBefore = ammoBefore,
                AmmoAfter = _ctx.Weapons.Ammo,
                PenaltySpawned = penalty,
                InvincibleTime = invincible,
            });
            return true;
        }

        public void Tick(float dt)
        {
            PlayerState s = _ctx.Player.State;
            if (s.RollCooldownLeft > 0f) s.RollCooldownLeft = MathF.Max(0f, s.RollCooldownLeft - dt);
            if (s.InvincibleLeft > 0f) s.InvincibleLeft = MathF.Max(0f, s.InvincibleLeft - dt);

            if (!s.IsRolling) return;
            s.RollTimer -= dt;
            if (s.RollTimer > 0f) return;

            s.IsRolling = false;
            s.RollTimer = 0f;
            s.RollSpeed = 0f;
            _ctx.Events.Publish(new RollLandedEvent { Position = s.Position });
            TriggerShockwave(s.Position);
        }

        /// <summary>“翻滚落地冲击波”词条：落地时对范围内普通怪物造成伤害（鲨鱼不受伤害）。</summary>
        private void TriggerShockwave(Vec2 position)
        {
            int level = _ctx.Progression.GetLevel(UpgradeId.Shockwave);
            if (level <= 0) return;

            float radius = _upgrades.ShockwaveBaseRadius + (level - 1) * _upgrades.ShockwaveRadiusPerLevel;
            float damage = _upgrades.ShockwaveBaseDamage + (level - 1) * _upgrades.ShockwaveDamagePerLevel;
            _ctx.Monsters.QueryCircle(position, radius, _hits);
            int hits = _hits.Count;
            for (int i = 0; i < _hits.Count; i++) _ctx.Combat.DamageMonster(_hits[i], damage, DamageSource.Shockwave);
            _ctx.Events.Publish(new ShockwaveTriggeredEvent { Position = position, Radius = radius, Hits = hits });
        }
    }
}
