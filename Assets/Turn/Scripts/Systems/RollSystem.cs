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
        /// <summary>B18：上一次正常翻滚后免费再翻一次的剩余窗口（秒）。</summary>
        private float _doubleRollWindowLeft;
        /// <summary>B18：免费翻滚用掉后的独立冷却（秒）。</summary>
        private float _doubleRollCooldownLeft;

        public int RollCount { get; private set; }

        public RollSystem(ITurnContext ctx, RollConfig config, UpgradeConfig upgrades)
        {
            _ctx = ctx;
            _config = config;
            _upgrades = upgrades;
        }

        /// <summary>B13：冷却 = 基础 × (1 − 每级比例 × 层数)，不低于下限（策划 0.9 秒）。没有 B13 时就是基础冷却。</summary>
        public float EffectiveCooldown
        {
            get
            {
                int level = Level(BuildId.B13);
                if (level <= 0) return _config.Cooldown;
                float factor = MathF.Max(0f, 1f - level * _upgrades.RollCooldownReductionPerLevel);
                float floor = MathF.Min(_config.MinCooldown, _config.Cooldown);
                return MathF.Max(floor, _config.Cooldown * factor);
            }
        }

        public float EffectiveInvincibleTime
        {
            get
            {
                // B14：基础 0.35 + 0.15 × 层数（上限 2 层 → 0.65 秒）
                int level = Level(BuildId.B14);
                return _config.InvincibleTime + level * _upgrades.RollInvinciblePerLevel;
            }
        }

        public bool CanRoll
        {
            get
            {
                PlayerState s = _ctx.Player.State;
                if (_ctx.Session.Phase != RunPhase.Playing || !s.Alive || s.IsRolling) return false;
                return s.RollCooldownLeft <= 0f || FreeRollAvailable;
            }
        }

        /// <summary>B18：正常翻滚后的窗口内、独立冷却已好时，可以免费再翻一次（不刷怪、不装填、不重置主冷却）。</summary>
        public bool FreeRollAvailable
        {
            get { return _doubleRollWindowLeft > 0f && _doubleRollCooldownLeft <= 0f && Level(BuildId.B18) > 0; }
        }

        private int Level(BuildId id)
        {
            return _ctx.Progression.GetLevel(BuildIds.Query(id));
        }

        /// <summary>B17：惩罚数 = max(1, ceil(基础 × (1 − 每级比例 × 层数)))；基础为 0 时仍为 0。</summary>
        private int PenaltyCount()
        {
            int baseCount = _ctx.Config.Penalty.Count;
            if (baseCount <= 0) return 0;
            int level = Level(BuildId.B17);
            if (level <= 0) return baseCount;
            float factor = MathF.Max(0f, 1f - level * _upgrades.PenaltyReductionPerLevel);
            // 减去极小量，避免 5 × 0.6 = 3.0000002 被向上取整成 4
            return Math.Max(1, (int)MathF.Ceiling(baseCount * factor - 1e-4f));
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

            // B18 免费的第二次翻滚：主冷却还没好、但仍在窗口内。时序与正常翻滚完全相同，只跳过第 4 步（刷怪）与第 8 步（装填）。
            bool free = s.RollCooldownLeft > 0f;

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

            // 4) 翻滚惩罚：在玩家周围为新位面刷一批怪；B17 减免在这里算好 count 再传。B18 免费翻滚不刷。（程序B 实现刷怪）
            int penalty = free ? 0 : _ctx.Spawner.SpawnPenalty(PenaltyCount());

            // 8) 武器装填 / 充能补满（武器已随位面切换）。这是整个游戏里唯一的装填入口。（程序B 实现）
            // B18 免费翻滚不装填。
            int ammoBefore = _ctx.Weapons.Ammo;
            if (!free) _ctx.Weapons.Reload();

            // 9) 位面进入事件：环境结算（清减速 / 困意归零 / 火山岩浆喷发等）+ 鲨鱼生成 / 销毁。
            _ctx.Environment.OnPlaneEntered(to);
            _ctx.Sharks.SyncWithPlane();

            // 10) 开启 CD。B18：正常翻滚打开免费窗口；免费翻滚不动主冷却，只开 B18 自己的冷却。
            if (free)
            {
                _doubleRollWindowLeft = 0f;
                _doubleRollCooldownLeft = MathF.Max(0f, _upgrades.DoubleRollCooldown);
            }
            else
            {
                s.RollCooldownLeft = EffectiveCooldown;
                _doubleRollWindowLeft = Level(BuildId.B18) > 0 && _doubleRollCooldownLeft <= 0f ? MathF.Max(0f, _upgrades.DoubleRollWindow) : 0f;
            }

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
                Free = free,
            });
            return true;
        }

        public void Tick(float dt)
        {
            PlayerState s = _ctx.Player.State;
            if (s.RollCooldownLeft > 0f) s.RollCooldownLeft = MathF.Max(0f, s.RollCooldownLeft - dt);
            if (s.InvincibleLeft > 0f) s.InvincibleLeft = MathF.Max(0f, s.InvincibleLeft - dt);
            if (_doubleRollWindowLeft > 0f) _doubleRollWindowLeft = MathF.Max(0f, _doubleRollWindowLeft - dt);
            if (_doubleRollCooldownLeft > 0f) _doubleRollCooldownLeft = MathF.Max(0f, _doubleRollCooldownLeft - dt);

            if (!s.IsRolling) return;
            s.RollTimer -= dt;
            if (s.RollTimer > 0f) return;

            s.IsRolling = false;
            s.RollTimer = 0f;
            s.RollSpeed = 0f;
            _ctx.Events.Publish(new RollLandedEvent { Position = s.Position });
            TriggerShockwave(s.Position);
        }

        /// <summary>
        /// B15 翻滚冲击：落地时对范围内普通怪物造成伤害并向外击退（鲨鱼不受影响）。
        /// 1 层 = 半径 2.5 / 伤害 20 / 击退 ShockwaveKnockback；之后每层按 RadiusPerLevel / DamagePerLevel 递增（层数效果待策划定稿）。
        /// 怪物位置只能由 MonsterSystem 写，击退走 IMonsterSystem.Knockback。
        /// </summary>
        private void TriggerShockwave(Vec2 position)
        {
            int level = Level(BuildId.B15);
            if (level <= 0) return;

            float radius = _upgrades.ShockwaveBaseRadius + (level - 1) * _upgrades.ShockwaveRadiusPerLevel;
            float damage = _upgrades.ShockwaveBaseDamage + (level - 1) * _upgrades.ShockwaveDamagePerLevel;
            _ctx.Monsters.QueryCircle(position, radius, _hits);
            int hits = _hits.Count;
            float knockback = MathF.Max(0f, _upgrades.ShockwaveKnockback);
            for (int i = 0; i < _hits.Count; i++)
            {
                MonsterEntity m = _hits[i];
                _ctx.Combat.DamageMonster(m, damage, DamageSource.Shockwave);
                if (!m.Alive || knockback <= 0f) continue;
                Vec2 away = m.Position - position;
                Vec2 dir = away.SqrMagnitude > 0.0001f ? away.Normalized : Vec2.Right;
                _ctx.Monsters.Knockback(m, dir, knockback);
            }
            _hits.Clear();
            _ctx.Events.Publish(new ShockwaveTriggeredEvent { Position = position, Radius = radius, Hits = hits });
        }
    }
}
