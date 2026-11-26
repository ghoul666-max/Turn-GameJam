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
    ///   3) 副武器 BeforeBurst(翻滚前站位)：例如引力黑洞先把怪拉拢；
    ///   4) 爆发：发布 SubspaceEnteredEvent —— 当前主武器的全图印记 / 痕迹同步爆发（武器订阅结算）；
    ///   5) 副武器 AfterBurst：例如空间锚回溯 + 连线斩击；
    ///   6) 方案 A 双主武器：Loadout.SwapMains() 切到另一把主武器；
    ///   7) 发布 RollPerformedEvent（表现层：音效 / 传送门特效）。
    /// 亚空间期间（SessionSystem 负责）：游戏时间 × SubspaceTimeScale，敌人 / 刷怪 / 武器 / 子弹 / 环境 / 副武器全部暂停。
    /// TickRealtime 按现实时间计满 SubspaceDuration 后自动退出：
    ///   8) 回到常态空间 → 9) Weapons.Reload()（补满当前生效主武器，整个游戏唯一的装填入口）
    ///   → 10) 开启当前生效主武器的翻滚冷却（方案 A = 切换后的那把）→ 11) 发布 SubspaceExitedEvent（清空印记）
    ///   → 12) 副武器 OnSubspaceExited（例如浮游炮分裂 / 合体）。
    /// 任意弹药状态都能翻滚；冷却中 / 亚空间中 / 非 Playing 阶段 / 已死亡时 TryRoll 返回 false（冷却中会发布 RollRejectedEvent）。
    ///
    /// 写 PlayerState 的 IsRolling / RollTimer / RollDirection / RollSpeed / RollCooldownLeft / InvincibleLeft / InSubspace / SubspaceLeft。
    /// 旧方案的切位面、怪物偏移换皮、惩罚刷怪、B13/B14/B15/B17/B18 翻滚词条已从这里移除（旧代码见 _backup_20261124_subspace）。
    /// </summary>
    public sealed class RollSystem : IRollSystem
    {
        private readonly ITurnContext _ctx;
        private readonly RollConfig _config;

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
            float duration = SubspaceDuration;
            Vec2 origin = s.Position;

            // 1) 进入亚空间：全程无敌
            s.InSubspace = true;
            s.SubspaceLeft = duration;

            // 2) 翻滚位移（PlayerSystem 在之后的 Tick 里按 RollDirection × RollSpeed 推进）
            Vec2 dir = s.MoveDir.SqrMagnitude > 0.01f ? s.MoveDir.Normalized : s.Facing.Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = Vec2.Right;
            float dashMultiplier = weapon == WeaponKind.Poker ? _config.PokerDurationMultiplier
                : weapon == WeaponKind.Crossbow ? _config.CrossbowDurationMultiplier : 1f;
            float dashTime = MathF.Max(0.05f, _config.Duration * MathF.Max(0.01f, dashMultiplier));
            s.IsRolling = true;
            s.RollTimer = dashTime;
            s.RollDirection = dir;
            s.RollSpeed = MathF.Max(0f, _config.Distance) / dashTime;
            RollCount++;

            // 3) 副武器：爆发前（引力黑洞拉拢）
            int ammo = _ctx.Weapons.Ammo;
            _ctx.SubWeapons.BeforeBurst(origin);

            // 4) 爆发：当前主武器的全图铺垫痕迹 / 印记同步触发（武器订阅这个事件，在回调里结算伤害）
            _ctx.Events.Publish(new SubspaceEnteredEvent
            {
                Position = s.Position,
                RollOrigin = origin,
                Weapon = weapon,
                Duration = duration,
                TimeScale = Clamp01(_config.SubspaceTimeScale),
            });

            // 5) 副武器：爆发后（空间锚回溯 + 斩击）
            _ctx.SubWeapons.AfterBurst(origin);

            // 6) 方案 A：切到另一把主武器（弹药在退出亚空间时补满）
            if (_ctx.Loadout.Scheme == LoadoutScheme.DualMain) _ctx.Loadout.SwapMains();

            // 7) 翻滚表现（音效 / 传送门）。不切位面、不刷怪、不在这里装填。
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

        public void ReduceCooldown(float seconds)
        {
            PlayerState s = _ctx.Player.State;
            if (seconds > 0f) s.RollCooldownLeft = MathF.Max(0f, s.RollCooldownLeft - seconds);
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
            // 8) 回到常态空间
            s.InSubspace = false;
            s.SubspaceLeft = 0f;

            // 9) 弹药补满当前生效的主武器（方案 A = 切换后的那把）。整个游戏里唯一的装填入口。
            _ctx.Weapons.Reload();

            // 10) 翻滚冷却：按当前生效的主武器
            WeaponKind current = _ctx.Loadout.Equipped;
            float cooldown = CooldownFor(current);
            s.RollCooldownLeft = cooldown;

            // 11) 清空全部铺垫痕迹 / 印记
            _ctx.Events.Publish(new SubspaceExitedEvent { Position = s.Position, Weapon = current, Cooldown = cooldown });

            // 12) 副武器：回到常态后（浮游炮分裂 / 合体）
            _ctx.SubWeapons.OnSubspaceExited();
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
