using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 翻滚 = Turn（新策划案 11/24）：常态空间 → 亚空间的图层翻转 + 铺垫伤害 → 爆发伤害的节奏翻转。
    ///
    /// TryRoll（一次调用里按顺序原子完成）：
    ///   1) 进入亚空间：PlayerState.InSubspace = true，全程无敌（CombatSystem 只看 PlayerState.Invincible）；
    ///   2) 翻滚位移：沿移动方向（没有输入时沿朝向）冲出 RollConfig.Distance，位移本身按游戏时间推进（亚空间里是慢放的）；
    ///   3) 爆发：发布 SubspaceEnteredEvent —— 全图印记 / 痕迹同步爆发的唯一时机（程序B 的武器订阅结算）；
    ///   4) 发布 RollPerformedEvent（表现层：音效 / 传送门特效）。
    /// 亚空间期间（SessionSystem 负责）：游戏时间 × SubspaceTimeScale，敌人 / 刷怪 / 武器 / 子弹 / 环境全部暂停。
    /// TickRealtime 按现实时间计满 SubspaceDuration 后自动退出：
    ///   5) 回到常态空间 → 6) Weapons.Reload()（整个游戏唯一的装填入口）→ 7) 开启翻滚时那把武器的冷却 → 8) 发布 SubspaceExitedEvent（程序B 清空印记）。
    /// 任意弹药状态都能翻滚；冷却中 / 亚空间中 / 非 Playing 阶段 / 已死亡时 TryRoll 返回 false（冷却中会发布 RollRejectedEvent）。
    ///
    /// 写 PlayerState 的 IsRolling / RollTimer / RollDirection / RollSpeed / RollCooldownLeft / InvincibleLeft / InSubspace / SubspaceLeft。
    /// 旧方案的切位面、怪物偏移换皮、惩罚刷怪、B13/B14/B15/B17/B18 翻滚词条已从这里移除（旧代码见 _backup_20261124_subspace）。
    /// </summary>
    public sealed class RollSystem : IRollSystem
    {
        private readonly ITurnContext _ctx;
        private readonly RollConfig _config;
        /// <summary>翻滚时装备的武器：退出亚空间时按它开冷却（亚空间里不能换武器，这里只是保险）。</summary>
        private WeaponKind _subspaceWeapon;

        public int RollCount { get; private set; }

        public RollSystem(ITurnContext ctx, RollConfig config)
        {
            _ctx = ctx;
            _config = config ?? new RollConfig();
        }

        public bool InSubspace { get { return _ctx.Player.State.InSubspace; } }

        public float SubspaceLeft { get { return _ctx.Player.State.InSubspace ? _ctx.Player.State.SubspaceLeft : 0f; } }

        public float EffectiveCooldown { get { return CooldownFor(_ctx.Loadout.Equipped); } }

        public float CooldownFor(WeaponKind weapon)
        {
            float[] list = _config.WeaponCooldowns;
            int i = (int)weapon;
            float cd = list != null && i >= 0 && i < list.Length ? list[i] : _config.Cooldown;
            return MathF.Max(0f, cd);
        }

        public float EffectiveInvincibleTime { get { return SubspaceDuration; } }

        private float SubspaceDuration { get { return MathF.Max(0.05f, _config.SubspaceDuration); } }

        public bool CanRoll
        {
            get
            {
                PlayerState s = _ctx.Player.State;
                if (_ctx.Session.Phase != RunPhase.Playing || !s.Alive || s.InSubspace) return false;
                return s.RollCooldownLeft <= 0f;
            }
        }

        public bool TryRoll()
        {
            PlayerState s = _ctx.Player.State;
            if (!CanRoll)
            {
                if (_ctx.Session.Phase == RunPhase.Playing && s.Alive && !s.InSubspace)
                {
                    _ctx.Events.Publish(new RollRejectedEvent { CooldownLeft = s.RollCooldownLeft });
                }
                return false;
            }

            WeaponKind weapon = _ctx.Loadout.Equipped;
            _subspaceWeapon = weapon;
            float duration = SubspaceDuration;

            // 1) 进入亚空间：全程无敌
            s.InSubspace = true;
            s.SubspaceLeft = duration;

            // 2) 翻滚位移（PlayerSystem 在之后的 Tick 里按 RollDirection × RollSpeed 推进）
            Vec2 dir = s.MoveDir.SqrMagnitude > 0.01f ? s.MoveDir.Normalized : s.Facing.Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            float dashTime = MathF.Max(0.05f, _config.Duration);
            s.IsRolling = true;
            s.RollTimer = dashTime;
            s.RollDirection = dir;
            s.RollSpeed = MathF.Max(0f, _config.Distance) / dashTime;
            RollCount++;

            // 3) 爆发：全图铺垫痕迹 / 印记同步触发（程序B 的武器订阅这个事件，在回调里结算伤害）
            int ammo = _ctx.Weapons.Ammo;
            _ctx.Events.Publish(new SubspaceEnteredEvent
            {
                Position = s.Position,
                Weapon = weapon,
                Duration = duration,
                TimeScale = Clamp01(_config.SubspaceTimeScale),
            });

            // 4) 翻滚表现（音效 / 传送门）。不切位面、不刷怪、不在这里装填。
            PlaneId plane = _ctx.Plane.Active;
            _ctx.Events.Publish(new RollPerformedEvent
            {
                Position = s.Position,
                Direction = dir,
                FromPlane = plane,
                ToPlane = plane,
                AmmoBefore = ammo,
                AmmoAfter = ammo,
                PenaltySpawned = 0,
                InvincibleTime = duration,
                Free = false,
            });
            return true;
        }

        public void Tick(float dt)
        {
            PlayerState s = _ctx.Player.State;
            // 冷却从回到常态空间开始计
            if (!s.InSubspace && s.RollCooldownLeft > 0f) s.RollCooldownLeft = MathF.Max(0f, s.RollCooldownLeft - dt);
            if (s.InvincibleLeft > 0f) s.InvincibleLeft = MathF.Max(0f, s.InvincibleLeft - dt);

            if (!s.IsRolling) return;
            s.RollTimer -= dt;
            if (s.RollTimer > 0f) return;

            s.IsRolling = false;
            s.RollTimer = 0f;
            s.RollSpeed = 0f;
            _ctx.Events.Publish(new RollLandedEvent { Position = s.Position });
        }

        public void TickRealtime(float realDt)
        {
            PlayerState s = _ctx.Player.State;
            if (!s.InSubspace) return;
            s.SubspaceLeft -= MathF.Max(0f, realDt);
            if (s.SubspaceLeft > 0f) return;
            ExitSubspace(s);
        }

        private void ExitSubspace(PlayerState s)
        {
            // 5) 回到常态空间
            s.InSubspace = false;
            s.SubspaceLeft = 0f;

            // 6) 弹药补满。整个游戏里唯一的装填入口。（程序B 实现）
            _ctx.Weapons.Reload();

            // 7) 翻滚冷却：按翻滚时那把武器
            float cooldown = CooldownFor(_subspaceWeapon);
            s.RollCooldownLeft = cooldown;

            // 8) 清空全部铺垫痕迹 / 印记（程序B 订阅）
            _ctx.Events.Publish(new SubspaceExitedEvent { Position = s.Position, Weapon = _subspaceWeapon, Cooldown = cooldown });
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
