using System;
using Turn.Core;
using Turn.Systems;

/// <summary>
/// 程序A：翻滚 → 亚空间（11/24）、出战方案 A / B 的翻滚时序（11/25）、受伤无敌帧、波次、维修机器人击退的纯模拟回归测试。
/// 运行：在本目录 dotnet run。
/// </summary>
internal static class Program
{
    private const float Step = 1f / 60f;
    private static int _checks;

    private static void Main()
    {
        RollEntersSubspaceAndBursts();
        SubspaceLastsOneRealSecond();
        SubspaceSlowsTimeAndFreezesEnemies();
        RollAtAnyAmmoAndCooldownPerWeapon();
        RejectedWhileCoolingOrInSubspace();
        HurtInvincibilityFrames();
        WavesAreEndless();
        DualMainSwitchesOnRoll();
        MainAndSubKeepsWeapon();
        UpgradePoolFollowsLoadout();
        LevelUpWaitsForNormalSpace();
        RobotKnockbackMovesPlayer();
        Console.WriteLine($"Roll checks passed ({_checks} assertions).");
    }

    private static ITurnContext World(Action<TurnConfig> configure = null)
    {
        var config = new TurnConfig();
        // 不自动刷怪、不刷精英 / Boss
        config.Horde.BaseCount = 0f;
        config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = 0;
        config.Horde.BossEveryWaves = 0;
        configure?.Invoke(config);
        ITurnContext ctx = TurnFactory.Create(config, 333);
        ctx.Session.Start();
        return ctx;
    }

    /// <summary>按现实时间推进 seconds 秒（固定步长，和 GameManager 一样）。</summary>
    private static void Run(ITurnContext ctx, float seconds)
    {
        int steps = (int)MathF.Round(seconds / Step);
        for (int i = 0; i < steps; i++) ctx.Session.Tick(Step);
    }

    private static void RollEntersSubspaceAndBursts()
    {
        var ctx = World();
        int entered = 0;
        SubspaceEnteredEvent evt = default;
        bool invincibleAtBurst = false;
        ctx.Events.Subscribe<SubspaceEnteredEvent>(e =>
        {
            entered++;
            evt = e;
            invincibleAtBurst = ctx.Player.State.Invincible; // 爆发结算时玩家已经无敌
        });
        PlaneId plane = ctx.Plane.Active;
        Check(ctx.Roll.TryRoll(), "roll succeeds");
        Check(entered == 1, "burst event fires exactly once, in the same call");
        Check(invincibleAtBurst && ctx.Player.State.InSubspace && ctx.Roll.InSubspace, "in subspace + invincible");
        Check(evt.Weapon == WeaponKind.WaterGun, "burst uses the equipped weapon (water gun at start)");
        Check(Near(evt.Duration, 1f) && Near(evt.TimeScale, .3f), "duration 1 s, time scale 0.3");
        Check(ctx.Plane.Active == plane, "roll no longer switches plane");
        Check(!ctx.Combat.DamagePlayer(50f, DamageSource.Monster), "no damage in subspace");
        Check(Near(ctx.Player.State.Hp, ctx.Player.State.MaxHp), "hp untouched");
    }

    private static void SubspaceLastsOneRealSecond()
    {
        var ctx = World(c => c.Weapon.InitialAmmo = 0);
        int exited = 0;
        SubspaceExitedEvent exit = default;
        ctx.Events.Subscribe<SubspaceExitedEvent>(e => { exited++; exit = e; });
        Check(ctx.Weapons.Ammo == 0, "start with an empty magazine");
        Check(ctx.Roll.TryRoll(), "roll on empty magazine");
        Check(ctx.Weapons.Ammo == 0, "no reload on entering subspace");
        Run(ctx, .95f);
        Check(ctx.Roll.InSubspace && exited == 0, "still in subspace after 0.95 real seconds (not 0.95 x 0.3)");
        Run(ctx, .1f);
        Check(!ctx.Roll.InSubspace && exited == 1, "back to normal space after 1 real second");
        Check(ctx.Weapons.Ammo == ctx.Weapons.Capacity, "magazine refilled on exit");
        Check(Near(exit.Cooldown, 2f) && JustStarted(ctx.Player.State.RollCooldownLeft, 2f), "water gun roll cooldown 2 s starts on exit");
        Check(!ctx.Player.State.Invincible, "invincibility ends with subspace");
    }

    private static void SubspaceSlowsTimeAndFreezesEnemies()
    {
        var ctx = World();
        Vec2 player = ctx.Player.State.Position;
        MonsterEntity m = ctx.Monsters.Spawn(player + new Vec2(4f, 0f), false);
        Check(ctx.Roll.TryRoll(), "roll");
        Vec2 before = m.Position;
        float time = ctx.Session.Time;
        Run(ctx, .5f);
        Check(Near(m.Position.X, before.X) && Near(m.Position.Y, before.Y), "monsters do not move in subspace");
        Check(Near(ctx.Session.Time - time, .5f * .3f, .01f), "game time runs at 0.3x in subspace");
        Run(ctx, .6f); // 退出亚空间
        time = ctx.Session.Time;
        Run(ctx, .2f);
        Check(Near(ctx.Session.Time - time, .2f, .01f), "game time back to 1x in normal space");
    }

    private static void RollAtAnyAmmoAndCooldownPerWeapon()
    {
        var ctx = World();
        Check(ctx.Weapons.Ammo == ctx.Weapons.Capacity, "full magazine at start");
        Check(ctx.Roll.CanRoll, "can roll with a full magazine");
        Check(Near(ctx.Roll.CooldownFor(WeaponKind.WaterGun), 2f) && Near(ctx.Roll.CooldownFor(WeaponKind.Spear), 2.5f)
              && Near(ctx.Roll.CooldownFor(WeaponKind.Blade), 3f), "per-weapon cooldowns 2 / 2.5 / 3");
        Check(ctx.Loadout.Equip(WeaponKind.Spear), "equip spear");
        Check(Near(ctx.Roll.EffectiveCooldown, 2.5f), "effective cooldown follows the equipped weapon");
        Check(ctx.Roll.TryRoll(), "roll with spear");
        Check(!ctx.Loadout.Equip(WeaponKind.Blade), "cannot switch weapons in subspace");
        Run(ctx, 1.05f);
        Check(JustStarted(ctx.Player.State.RollCooldownLeft, 2.5f), "spear cooldown 2.5 s");
        Run(ctx, 2.5f);
        Check(ctx.Roll.CanRoll, "cooldown over");
        Check(ctx.Loadout.Equip(WeaponKind.Blade) && ctx.Roll.TryRoll(), "roll with blade");
        Run(ctx, 1.05f);
        Check(JustStarted(ctx.Player.State.RollCooldownLeft, 3f), "blade cooldown 3 s");
    }

    private static void RejectedWhileCoolingOrInSubspace()
    {
        var ctx = World();
        int rejected = 0;
        ctx.Events.Subscribe<RollRejectedEvent>(e => rejected++);
        Check(ctx.Roll.TryRoll(), "first roll");
        Check(!ctx.Roll.TryRoll() && rejected == 0, "second press inside subspace does nothing (no reject flash)");
        Run(ctx, 1.05f);
        Check(!ctx.Roll.TryRoll() && rejected == 1, "rejected while cooling down");
        Check(ctx.Roll.RollCount == 1, "only one roll counted");
    }

    private static void HurtInvincibilityFrames()
    {
        var ctx = World();
        Check(ctx.Combat.DamagePlayer(5f, DamageSource.Monster), "first hit lands");
        Check(ctx.Player.State.Invincible, "hurt i-frames start");
        Check(!ctx.Combat.DamagePlayer(5f, DamageSource.Monster), "second hit blocked by i-frames");
        Run(ctx, .25f);
        Check(!ctx.Combat.DamagePlayer(5f, DamageSource.Monster), "still blocked at 0.25 s");
        Run(ctx, .3f);
        Check(ctx.Combat.DamagePlayer(5f, DamageSource.Monster), "hit lands again after 0.5 s");
        Check(Near(ctx.Player.State.Hp, ctx.Player.State.MaxHp - 10f), "two hits taken");
    }

    private static void WavesAreEndless()
    {
        var ctx = World(c => c.Level.WaveSeconds = 1f);
        int lastWave = 0;
        ctx.Events.Subscribe<WaveStartedEvent>(e => lastWave = e.Wave);
        Check(ctx.Session.Wave == 1, "wave 1 at start");
        Run(ctx, 1.05f);
        Check(ctx.Session.Wave == 2 && lastWave == 2, "wave 2 after 1 s");
        Run(ctx, 2f);
        Check(ctx.Session.Wave == 4, "wave 4 after 3 s");
        Check(ctx.Session.Phase == RunPhase.Playing && ctx.Session.LevelTimeLeft == 0f, "endless: never clears");

        var slow = World(c => c.Level.WaveSeconds = 1f);
        Check(slow.Roll.TryRoll(), "roll");
        Run(slow, 1f); // 亚空间里 1 现实秒 = 0.3 游戏秒
        Check(slow.Session.Wave == 1, "waves follow game time (slowed in subspace)");
    }

    /// <summary>方案 A 双主武器：翻滚 → 当前主武器爆发 → 切到另一把 → 回到常态时补满新武器、按新武器开冷却。</summary>
    private static void DualMainSwitchesOnRoll()
    {
        var ctx = World(c =>
        {
            c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.WaterGun, SecondMain = WeaponKind.Spear };
            c.Weapon.InitialAmmo = 0;
        });
        ILoadoutSystem l = ctx.Loadout;
        Check(l.Equipped == WeaponKind.WaterGun && l.Standby == WeaponKind.Spear, "starts with the first main weapon");
        Check(l.Carries(WeaponKind.Spear) && !l.Carries(WeaponKind.Blade), "carries exactly two mains");
        Check(l.Sub == SubWeaponKind.None, "scheme A has no sub weapon");

        WeaponKind burst = WeaponKind.Blade;
        bool swapped = false;
        ctx.Events.Subscribe<SubspaceEnteredEvent>(e => burst = e.Weapon);
        ctx.Events.Subscribe<WeaponEquippedEvent>(e => swapped |= e.ByRoll && e.From == WeaponKind.WaterGun && e.To == WeaponKind.Spear);
        Check(ctx.Roll.TryRoll(), "roll");
        Check(burst == WeaponKind.WaterGun, "burst uses the weapon you rolled with");
        Check(swapped && l.Equipped == WeaponKind.Spear && l.Standby == WeaponKind.WaterGun, "switched to the other main after the burst");
        Check(ctx.Weapons.AmmoOf(WeaponKind.Spear) == 0, "not refilled until subspace ends");
        Run(ctx, 1.05f);
        Check(ctx.Weapons.AmmoOf(WeaponKind.Spear) == ctx.Weapons.CapacityOf(WeaponKind.Spear), "switched-in weapon refilled");
        Check(ctx.Weapons.AmmoOf(WeaponKind.WaterGun) == 0, "the other main keeps its own ammo");
        Check(JustStarted(ctx.Player.State.RollCooldownLeft, 2.5f), "cooldown of the switched-in weapon (spear 2.5 s)");

        Run(ctx, 2.6f);
        Check(ctx.Roll.TryRoll() && burst == WeaponKind.Spear && l.Equipped == WeaponKind.WaterGun, "second roll: spear bursts, back to the water gun");
        Run(ctx, 1.05f);
        Check(JustStarted(ctx.Player.State.RollCooldownLeft, 2f), "water gun cooldown 2 s");

        var same = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.Blade, SecondMain = WeaponKind.Blade });
        Check(same.Loadout.Standby != same.Loadout.Equipped, "two mains are always different");
    }

    /// <summary>方案 B：翻滚不切换主武器；不带副武器时就是单武器。</summary>
    private static void MainAndSubKeepsWeapon()
    {
        var ctx = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = WeaponKind.Blade, Sub = SubWeaponKind.None });
        Check(ctx.Loadout.Equipped == WeaponKind.Blade && ctx.Loadout.Standby == WeaponKind.Blade, "one main weapon");
        Check(ctx.SubWeapons.Kind == SubWeaponKind.None, "no sub weapon");
        Check(ctx.Roll.TryRoll(), "roll");
        Check(ctx.Loadout.Equipped == WeaponKind.Blade, "scheme B never switches");
        Run(ctx, 1.05f);
        Check(JustStarted(ctx.Player.State.RollCooldownLeft, 3f), "blade cooldown 3 s");
        Check(ctx.SubWeapons.Supports(SubWeaponKind.None), "None is always supported");
    }

    /// <summary>升级候选池 = 这一局带着的装备：方案 A 两把主武器的词条混在一起。</summary>
    private static void UpgradePoolFollowsLoadout()
    {
        var ctx = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.WaterGun, SecondMain = WeaponKind.Blade });
        bool water = false, blade = false;
        for (int n = 0; n < 60; n++)
        {
            ctx.Progression.AddXp(ctx.Progression.XpToNext);
            ctx.Progression.RollOptions();
            var list = ctx.Progression.Options;
            for (int i = 0; i < list.Count; i++)
            {
                WeaponKind w = BuildIds.WeaponOf(list[i].Build);
                Check(!BuildIds.IsSub(list[i].Build) && (w == WeaponKind.WaterGun || w == WeaponKind.Blade), "options only from the two carried mains");
                water |= w == WeaponKind.WaterGun;
                blade |= w == WeaponKind.Blade;
            }
            ctx.Progression.SkipPending();
        }
        Check(water && blade, "both mains' upgrades show up");
    }

    private static void LevelUpWaitsForNormalSpace()
    {
        var ctx = World();
        Check(ctx.Roll.TryRoll(), "roll");
        ctx.Progression.AddXp(1000);
        Run(ctx, .5f);
        Check(ctx.Session.Phase == RunPhase.Playing, "no level-up popup inside subspace");
        Run(ctx, .6f);
        Check(ctx.Session.Phase == RunPhase.LevelUp, "level-up opens after returning to normal space");
    }

    private static void RobotKnockbackMovesPlayer()
    {
        var ctx = World();
        Vec2 start = ctx.Player.State.Position;
        ctx.Events.Publish(new RobotKnockbackHitEvent { Plane = ctx.Plane.Active, Direction = Vec2.Right, Distance = 1.5f });
        ctx.Player.Tick(.01f);
        Check(Near(ctx.Player.State.Position.X - start.X, 1.5f), "robot hit pushes player 1.5");

        Vec2 now = ctx.Player.State.Position;
        PlaneId other = ctx.Plane.Active == PlaneId.Volcano ? PlaneId.Ocean : PlaneId.Volcano;
        ctx.Events.Publish(new RobotKnockbackHitEvent { Plane = other, Direction = Vec2.Right, Distance = 1.5f });
        ctx.Player.Tick(.01f);
        Check(Near(ctx.Player.State.Position.X, now.X), "hits from another plane are ignored");
    }

    private static bool Near(float a, float b, float eps = .001f) => Math.Abs(a - b) < eps;

    /// <summary>冷却刚开始不久：剩余值 ≤ 总冷却，且只走了几个模拟步（退出亚空间后 Run 还会多推进几步）。</summary>
    private static bool JustStarted(float left, float total) => left <= total + .001f && left > total - .15f;

    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new Exception("Roll test failed: " + description);
    }
}
