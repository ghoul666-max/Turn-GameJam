using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 翻滚（Turn）。整个游戏的核心操作：一次成功的 TryRoll 在这一个方法里按策划书「翻滚时序」原子执行全部效果，
    /// 切位面、怪物偏移换皮、弹幕保留、装填、惩罚刷怪、位面进入结算的唯一调用方就是这里（程序A 编排，部分步骤由程序B 实现）。
    /// 冷却中（或翻滚中、非 Playing 阶段、玩家已死亡）调用 TryRoll 直接返回 false，什么都不执行。
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

            // 策划书「翻滚时序」。步骤编号与策划书一致；第 4 步（惩罚刷怪）暂定挪到第 6b 步之后，
            // 让惩罚怪直接按新位面生成（11/21 已确认）。第 7 步策划书缺失。

            // 1) 无敌帧
            float invincible = EffectiveInvincibleTime;
            s.InvincibleLeft = MathF.Max(s.InvincibleLeft, invincible);

            // 2) 传送门 VFX：表现层收到本方法末尾的 RollPerformedEvent 后在 Position 生成（同一帧）。

            // 3) 翻滚位移（PlayerSystem 在之后的 Tick 里按 RollDirection × RollSpeed 推进）
            Vec2 dir = s.MoveDir.SqrMagnitude > 0.01f ? s.MoveDir.Normalized : s.Facing.Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            float duration = MathF.Max(0.05f, _config.Duration);
            s.IsRolling = true;
            s.RollTimer = duration;
            s.RollDirection = dir;
            s.RollSpeed = _config.Distance / duration;
            RollCount++;

            // 5) 位面 Root 显隐互换：只改激活位面并发布 PlaneSwitchedEvent（表现层据此显隐）。
            PlaneId from = _ctx.Plane.Active;
            _ctx.Plane.Switch();
            PlaneId to = _ctx.Plane.Active;

            // 6) 所有怪物：位置 += 总偏移量（= 本次翻滚的总位移，保持与玩家的相对位置）；换 Skin / AI；重置冲锋态。（程序B 实现）
            _ctx.Monsters.ShiftAll(dir * _config.Distance);
            _ctx.Monsters.SwapPlane(from, to);

            // 6b) 已飞出的弹幕保留、寿命 ×1.6、只对同层生效。（程序B 实现）
            _ctx.Projectiles.OnPlaneSwitched(from, to);

            // 4) 翻滚惩罚：在玩家周围为新位面刷一批怪（B17 惩罚减免在 M3 接入：在这里算好 count 再传）。（程序B 实现刷怪）
            int penalty = _ctx.Spawner.SpawnPenalty(_ctx.Config.Penalty.Count);

            // 8) 武器装填 / 充能补满（武器已随位面切换）。这是整个游戏里唯一的装填入口。（程序B 实现）
            int ammoBefore = _ctx.Weapons.Ammo;
            _ctx.Weapons.Reload();

            // 9) 位面进入事件：环境结算（清减速 / 困意归零 / 火山岩浆喷发等）+ 鲨鱼生成 / 销毁。
            _ctx.Environment.OnPlaneEntered(to);
            _ctx.Sharks.SyncWithPlane();

            // 10) 开启 CD
            s.RollCooldownLeft = EffectiveCooldown;

            _ctx.Events.Publish(new RollPerformedEvent
            {
                Position = s.Position,
                Direction = dir,
                FromPlane = from,
                ToPlane = to,
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
