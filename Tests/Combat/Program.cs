using System;
using Turn.Core;
using Turn.Systems;

/// <summary>程序B：击杀掉落、经验曲线、宝箱流程、受伤词条、拾取范围的纯模拟回归测试（新策划案 11/24）。运行：dotnet run。</summary>
internal static class Program
{
    private static void Main()
    {
        KillDropsXp();
        XpCurve();
        EliteDropsChestAndOpensSelection();
        PickupRange();
        HurtBuildsMarkEnemies();
        BurstRingHitsAround();
        SimultaneousPlayerHitsShareProtection();
        FixedAndPercentageDamageMultipliers();
        BossChestOffersCarriedBuildsWithoutCurse();
        BossChestHandlesLimitedCandidatesAndMixedQueue();
        GravityProtectsOnlyAfterActualPullAndExit();
        Console.WriteLine($"Combat checks passed ({_checks} assertions).");
    }

    private static void KillDropsXp()
    {
        var ctx = World();
        MonsterEntity m = Spawn(ctx, new Vec2(6f, 0f));
        int kills = 0;
        ctx.Events.Subscribe<WeaponKillConfirmedEvent>(e => kills++);
        ctx.Combat.DamageMonster(m, 100f, DamageSource.Weapon);
        Check(!m.Alive && kills == 1, "kill confirmed");
        Check(ctx.Experience.Active.Count == 1 && ctx.Experience.Active[0].Value == 5, "walker drops 5 xp");
        Check(ctx.Experience.Chests.Count == 0, "normal monsters drop no chest");
        Check(ctx.Progression.Kills == 1, "kill counted");
    }

    private static void XpCurve()
    {
        var ctx = World();
        Check(ctx.Progression.XpToNext == 50, "level 1 needs 50");
        ctx.Progression.AddXp(50);
        Check(ctx.Progression.Level == 2 && ctx.Progression.XpToNext == 60, "each level +20%");
        ctx.Progression.AddXp(60);
        Check(ctx.Progression.XpToNext == 72, "72 at level 3");
        Check(ctx.Progression.PendingLevelUps == 2, "two level-ups pending");
    }

    private static void EliteDropsChestAndOpensSelection()
    {
        var ctx = World();
        MonsterEntity elite = Spawn(ctx, new Vec2(6f, 0f), MonsterKind.Walker, 1);
        Check(elite.Rank == 1 && Near(elite.MaxHp, 150f), "elite hp x5");
        Check(elite.Radius > ctx.Config.Horde.Radius, "elite is bigger");
        ctx.Combat.DamageMonster(elite, 1000f, DamageSource.Weapon);
        Check(ctx.Experience.Chests.Count == 1, "elite drops a chest");
        Check(ctx.Experience.Active[0].Value == 25, "elite xp x5");

        // 宝箱放到玩家脚边：靠近自动打开 → 下一步弹三选一
        var chest = ctx.Experience.Chests[0];
        chest.Position = ctx.Player.State.Position;
        ctx.Session.Tick(1f / 60f);
        Check(ctx.Progression.PendingChests == 0 || ctx.Session.Phase == RunPhase.Chest, "chest opened");
        Check(ctx.Session.Phase == RunPhase.Chest, "chest selection pauses the game");
        Check(ctx.Session.TryChooseUpgrade(0), "choose");
        Check(ctx.Session.Phase == RunPhase.Playing || ctx.Session.Phase == RunPhase.LevelUp, "back to the game");
    }

    private static void PickupRange()
    {
        var ctx = World();
        ctx.Experience.SpawnOrb(ctx.Player.State.Position + new Vec2(2.3f, 0f), 7);
        ctx.Experience.SpawnOrb(ctx.Player.State.Position + new Vec2(4f, 0f), 7);
        for (int i = 0; i < 60; i++) ctx.Experience.Tick(1f / 60f);
        Check(ctx.Progression.Xp == 7, "orb inside 2.5 (100 px) is collected, the one outside is not");
    }

    private static void HurtBuildsMarkEnemies()
    {
        var ctx = World(c => { for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 1000f; });
        ctx.Loadout.Equip(WeaponKind.Spear);
        ApplyBuild(ctx, BuildId.SpearHurtMarks);
        MonsterEntity m = Spawn(ctx, new Vec2(3f, 0f));
        Check(ctx.Combat.DamagePlayer(1f, DamageSource.Monster), "hurt");
        Check(m.SpearMarks == 2, "spear: nearest enemy gets 2 marks when hurt");

        var water = World();
        ApplyBuild(water, BuildId.WaterHurtPuddle);
        Check(water.Combat.DamagePlayer(1f, DamageSource.Monster), "hurt");
        Check(water.Weapons.Puddles.Count == 1, "water: a puddle under the player when hurt");
    }

    private static void BurstRingHitsAround()
    {
        var legacy = Turn.Weapons.BuildCatalog.CreateDefaults();
        legacy[(int)BuildId.WaterBurstRing] = new BuildDef { MaxLevel = 3, Value = .08f, Value2 = 3f };
        Check(Turn.Weapons.BuildCatalog.Normalize(legacy)[(int)BuildId.WaterBurstRing].MaxLevel == 0,
            "legacy burst ring configured value is forcibly disabled");
        var ctx = World(c =>
        {
            for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 100f;
            c.Builds.Builds = legacy;
        });
        MonsterEntity near = Spawn(ctx, new Vec2(2f, 0f));
        MonsterEntity far = Spawn(ctx, new Vec2(8f, 0f));
        int rings = 0;
        ctx.Events.Subscribe<BurstRingEvent>(_ => rings++);
        Check(ctx.Roll.TryRoll(), "roll still succeeds with old ring configuration");
        Check(Near(near.Hp, 100f) && Near(far.Hp, 100f), "roll does not deal removed free ring damage");
        Check(rings == 0, "removed ring publishes no damage effect");
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ctx.Progression.AddXp(ctx.Progression.XpToNext);
            ctx.Progression.RollOptions();
            foreach (var option in ctx.Progression.Options)
                Check(option.Build != BuildId.WaterBurstRing, "removed ring never enters upgrade options");
            ctx.Progression.SkipPending();
        }
    }

    private static void SimultaneousPlayerHitsShareProtection()
    {
        var ctx = World();
        float hp = ctx.Player.State.Hp;
        int applied = 0;
        for (int i = 0; i < 20; i++) if (ctx.Combat.DamagePlayer(2f, DamageSource.Monster)) applied++;
        Check(applied == 1 && Near(hp - ctx.Player.State.Hp, 2f), "twenty same-frame monster hits only damage once");
        ctx.Player.Tick(ctx.Player.Config.HurtInvincibleTime * .9f);
        Check(!ctx.Combat.DamagePlayer(2f, DamageSource.Monster), "protection still active before interval");
        ctx.Player.Tick(ctx.Player.Config.HurtInvincibleTime * .2f);
        Check(ctx.Combat.DamagePlayer(2f, DamageSource.Monster), "next hit applies after global protection expires");
    }

    private static void FixedAndPercentageDamageMultipliers()
    {
        var ctx = World(c => { for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 1000f; });
        var kit = new Turn.Weapons.WeaponKit(ctx, ctx.Config.Arsenal);
        var m = Spawn(ctx, new Vec2(3f, 0f));
        Check(Near(kit.Hit(m, 10f, DamageSource.Weapon), 12.5f), "fixed weapon damage x1.25");
        Check(Near(kit.Hit(m, 10f, DamageSource.SubWeapon, percentageDamage: true), 10f), "max hp percentage damage is not multiplied");
        var normal = Spawn(ctx, new Vec2(8f, 0f), MonsterKind.Walker, 0);
        var elite = Spawn(ctx, new Vec2(10f, 0f), MonsterKind.Walker, 1);
        var boss = Spawn(ctx, new Vec2(12f, 0f), MonsterKind.Walker, 2);
        Check(Near(boss.MaxHp, 1000f * ctx.Config.Horde.BossHp * 2f), "boss only gains doubled HP");
        Check(Near(boss.DamageScale, ctx.Config.Horde.BossDamage), "boss damage stays unchanged");
        ctx.Monsters.Tick(.01f);
        Check(Near(normal.Velocity.Magnitude / elite.Velocity.Magnitude, .85f), "only normal chase speed x.85");
    }

    private static void BossChestOffersCarriedBuildsWithoutCurse()
    {
        var ctx = World();
        ctx.Experience.SpawnChest(ctx.Player.State.Position, 2);
        ctx.Experience.Tick(.01f);
        ctx.Progression.RollChestOptions();
        Check(ctx.Progression.Options.Count == 3, "boss chest three carried options");
        foreach (var option in ctx.Progression.Options)
            Check(option.Curse == ChestCurse.None && ctx.Progression.IsCarried(option.Build), "boss chest equipment directed without curse");
    }

    private static void BossChestHandlesLimitedCandidatesAndMixedQueue()
    {
        var limited = World();
        for (int i = 0; i < BuildIds.Count; i++)
        {
            BuildId id = (BuildId)i;
            if (limited.Progression.IsCarried(id)) limited.Progression.DebugSetLevel(id, limited.Progression.MaxLevel(id));
        }
        limited.Progression.DebugSetLevel(BuildId.WaterAmmo, 0);
        limited.Experience.SpawnChest(limited.Player.State.Position, 2);
        limited.Experience.Tick(.01f);
        limited.Progression.RollChestOptions();
        Check(limited.Progression.Options.Count == 1 && limited.Progression.Options[0].Build == BuildId.WaterAmmo,
            "boss with one eligible build shows one option");
        limited.Progression.DebugSetLevel(BuildId.WaterAmmo, limited.Progression.MaxLevel(BuildId.WaterAmmo));
        limited.Progression.RollChestOptions();
        Check(limited.Progression.Options.Count == 0, "all builds maxed produces no unrelated boss reward");

        var mixed = World();
        mixed.Experience.SpawnChest(mixed.Player.State.Position, 1);
        mixed.Experience.SpawnChest(mixed.Player.State.Position, 2);
        mixed.Experience.Tick(.01f);
        mixed.Progression.RollChestOptions();
        bool curse = false;
        foreach (var option in mixed.Progression.Options) if (option.Curse != ChestCurse.None) curse = true;
        Check(curse, "first elite chest retains curse option");
        Check(mixed.Progression.TryApply(0), "consume elite chest first");
        mixed.Progression.RollChestOptions();
        foreach (var option in mixed.Progression.Options)
            Check(option.Curse == ChestCurse.None, "following boss chest keeps boss reward policy");
    }

    private static void GravityProtectsOnlyAfterActualPullAndExit()
    {
        var ctx = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = WeaponKind.WaterGun, Sub = SubWeaponKind.GravityWell });
        Spawn(ctx, new Vec2(2f, 0f));
        Check(ctx.Roll.TryRoll(), "gravity roll succeeds");
        Check(ctx.Player.State.HurtInvincibleLeft <= 0f, "gravity exit protection not spent during subspace");
        for (int i = 0; i < 80 && ctx.Player.State.InSubspace; i++) ctx.Session.Tick(1f / 60f);
        Check(!ctx.Player.State.InSubspace && ctx.Player.State.HurtInvincibleLeft <= 0f,
            "first persistent-hole spawn has no subspace displacement and grants no free exit protection");
        var displaced = World();
        displaced.Events.Publish(new GravityDisplacedEvent { Moved = 1 });
        displaced.Events.Publish(new SubspaceExitedEvent());
        Check(displaced.Player.State.HurtInvincibleLeft > .4f, "Player contract: confirmed gravity displacement grants protection at exit");
        var empty = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = WeaponKind.WaterGun, Sub = SubWeaponKind.GravityWell });
        empty.Roll.TryRoll();
        for (int i = 0; i < 80 && empty.Player.State.InSubspace; i++) empty.Session.Tick(1f / 60f);
        Check(empty.Player.State.HurtInvincibleLeft <= 0f, "empty gravity pull grants no extra protection");
        var noMove = World(c =>
        {
            c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = WeaponKind.WaterGun, Sub = SubWeaponKind.GravityWell };
            c.Arsenal.Gravity.PullDistance = 0f;
        });
        Spawn(noMove, new Vec2(2f, 0f));
        noMove.Roll.TryRoll();
        for (int i = 0; i < 80 && noMove.Player.State.InSubspace; i++) noMove.Session.Tick(1f / 60f);
        Check(noMove.Player.State.HurtInvincibleLeft <= 0f, "gravity finds monster but zero displacement grants no protection");
        var reset = World();
        reset.Events.Publish(new GravityDisplacedEvent { Moved = 1 });
        reset.Player.Setup();
        reset.Events.Publish(new SubspaceExitedEvent());
        Check(reset.Player.State.HurtInvincibleLeft <= 0f, "player Setup clears pending gravity protection");
    }

    private static int _checks;

    /// <summary>一局测试世界：不自动刷怪、不刷精英 / Boss；默认方案 B + 水枪 + 不带副武器（Loadout.Equip 可换主武器）。</summary>
    private static ITurnContext World(Action<TurnConfig> configure = null)
    {
        var config = new TurnConfig();
        config.Horde.BaseCount = 0f;
        config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = 0;
        config.Horde.BossEveryWaves = 0;
        configure?.Invoke(config);
        ITurnContext ctx = TurnFactory.Create(config, 777);
        ctx.Session.Start();
        return ctx;
    }

    private static MonsterEntity Spawn(ITurnContext ctx, Vec2 offset, MonsterKind kind = MonsterKind.Walker, int rank = 0)
    {
        return ctx.Monsters.SpawnKind(ctx.Player.State.Position + offset, kind, rank);
    }

    /// <summary>反复升级直到抽到指定词条并选中它。</summary>
    private static void ApplyBuild(ITurnContext ctx, BuildId id)
    {
        for (int attempt = 0; attempt < 2000; attempt++)
        {
            ctx.Progression.AddXp(ctx.Progression.XpToNext);
            ctx.Progression.RollOptions();
            var list = ctx.Progression.Options;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Build != id || list[i].Curse != ChestCurse.None) continue;
                Check(ctx.Progression.TryApply(i), "apply " + id);
                while (ctx.Progression.PendingLevelUps > 0) ctx.Progression.SkipPending();
                return;
            }
            ctx.Progression.SkipPending();
        }
        throw new Exception("Could not sample " + id);
    }

    private static void RunProjectiles(ITurnContext ctx, float seconds)
    {
        for (float t = 0f; t < seconds; t += 1f / 60f) ctx.Projectiles.Tick(1f / 60f);
    }

    private static bool Near(float a, float b, float eps = .01f) => Math.Abs(a - b) < eps;

    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new Exception("Test failed: " + description);
    }
}
