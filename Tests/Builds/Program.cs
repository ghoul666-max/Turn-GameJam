using System;
using Turn.Core;
using Turn.Systems;

/// <summary>程序B：三把武器（铺垫 → 爆发）、弹药、词条与宝箱选项的纯模拟回归测试（新策划案 11/24）。运行：dotnet run。</summary>
internal static class Program
{
    private static void Main()
    {
        WaterBulletLeavesPuddle();
        FountainsStackOnOverlap();
        PuddlesClearedAfterSubspace();
        SpearMarksAndRecall();
        SpearKillPiercesAndPassesMarks();
        BladeHitsFirstRingAndSlashScales();
        AmmoRunsOutAndStops();
        AmmoBuildRaisesCapacity();
        OptionsComeFromEquippedWeapon();
        ChestHasCurseOption();
        Console.WriteLine($"Build checks passed ({_checks} assertions).");
    }

    private static void Tough(TurnConfig c)
    {
        for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 1000f;
    }

    private static void WaterBulletLeavesPuddle()
    {
        var ctx = World(Tough);
        MonsterEntity m = Spawn(ctx, new Vec2(4f, 0f));
        ctx.Weapons.Tick(0f);
        Check(ctx.Weapons.Ammo == 29, "one shot costs one water");
        RunProjectiles(ctx, 1f);
        Check(ctx.Projectiles.Active.Count == 0, "bullet done");
        Check(Near(m.Hp, 995f), "water bullet hits for 5");
        Check(ctx.Weapons.Puddles.Count == 1, "hit leaves one puddle");
        Check(Vec2.Distance(ctx.Weapons.Puddles[0].Position, m.Position) < .01f, "puddle at the hit enemy");
        Check(Near(ctx.Weapons.Puddles[0].Radius, 1f), "puddle radius 1 (40 px)");
    }

    private static void FountainsStackOnOverlap()
    {
        var ctx = World(Tough);
        MonsterEntity m = Spawn(ctx, new Vec2(4f, 0f));
        for (int i = 0; i < 3; i++)
        {
            ctx.Weapons.Tick(1f);
            RunProjectiles(ctx, 1f);
        }
        Check(ctx.Weapons.Puddles.Count == 3, "three overlapping puddles");
        float before = m.Hp;
        int fountains = 0;
        ctx.Events.Subscribe<FountainBurstEvent>(e => fountains++);
        Check(ctx.Roll.TryRoll(), "roll");
        Check(fountains == 3, "every puddle erupts");
        Check(Near(before - m.Hp, 36f), "overlapping fountains add up: 3 x 12");
    }

    private static void PuddlesClearedAfterSubspace()
    {
        var ctx = World(Tough);
        Spawn(ctx, new Vec2(4f, 0f));
        ctx.Weapons.Tick(0f);
        RunProjectiles(ctx, 1f);
        Check(ctx.Weapons.Puddles.Count == 1, "puddle laid");
        ctx.Roll.TryRoll();
        Check(ctx.Weapons.Puddles.Count == 1, "puddles stay during subspace");
        for (int i = 0; i < 70; i++) ctx.Session.Tick(1f / 60f);
        Check(!ctx.Roll.InSubspace, "back to normal");
        Check(ctx.Weapons.Puddles.Count <= 1, "old puddles cleared on exit (at most one new shot landed)");
    }

    private static void SpearMarksAndRecall()
    {
        var ctx = World(Tough);
        Check(ctx.Loadout.Equip(WeaponKind.Spear), "equip spear");
        Check(ctx.Weapons.Capacity == 20, "spear ammo 20");
        MonsterEntity m = Spawn(ctx, new Vec2(3f, 0f));
        for (int i = 0; i < 2; i++)
        {
            ctx.Weapons.Tick(1f);
            RunProjectiles(ctx, 1f);
        }
        Check(m.SpearMarks == 2, "two spears stuck = two marks");
        Check(Near(m.Hp, 1000f - 16f), "spear hits for 8");
        int recalls = 0;
        ctx.Events.Subscribe<SpearRecalledEvent>(e => recalls++);
        ctx.Roll.TryRoll();
        Check(recalls == 2, "each spear is recalled");
        Check(Near(m.Hp, 1000f - 16f - 30f), "recall: 15 per spear along the path");
        Check(m.SpearMarks == 0, "marks cleared after recall");
    }

    private static void SpearKillPiercesAndPassesMarks()
    {
        var ctx = World(Tough);
        ctx.Loadout.Equip(WeaponKind.Spear);
        MonsterEntity weak = Spawn(ctx, new Vec2(2f, 0f));
        MonsterEntity tank = Spawn(ctx, new Vec2(4f, 0f));
        weak.Hp = 1f;
        weak.SpearMarks = 2; // 被击杀者身上原有的矛一起传下去
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 1f);
        Check(!weak.Alive, "first target killed");
        Check(Near(tank.Hp, 1000f - 8f * .8f), "pierced spear deals 80%");
        Check(tank.SpearMarks == 3, "marks passed on: own spear + 2 from the killed enemy");
    }

    private static void BladeHitsFirstRingAndSlashScales()
    {
        var ctx = World(Tough);
        ctx.Loadout.Equip(WeaponKind.Blade);
        MonsterEntity a = Spawn(ctx, new Vec2(2f, 0f));
        MonsterEntity b = Spawn(ctx, new Vec2(2.5f, .4f));
        MonsterEntity far = Spawn(ctx, new Vec2(5.5f, 0f));
        ctx.Weapons.Tick(1f);
        Check(Near(a.Hp, 994f) && Near(b.Hp, 994f), "sword wave hits the first ring for 6");
        Check(a.BladeMarks == 1 && b.BladeMarks == 1, "each hit enemy gets one mark");
        Check(Near(far.Hp, 1000f) && far.BladeMarks == 0, "enemies behind the ring are not hit");
        Check(ctx.Weapons.TotalBladeMarks == 2, "two marks on the field");
        float slash = 25f * (1f + .08f * 2f);
        ctx.Roll.TryRoll();
        Check(Near(a.Hp, 994f - slash, .05f) && Near(far.Hp, 1000f - slash, .05f), "slash = 25 x (1 + 8% x marks), whole row");
    }

    private static void AmmoRunsOutAndStops()
    {
        var ctx = World(c => { Tough(c); c.Weapon.InitialAmmo = 1; });
        Spawn(ctx, new Vec2(4f, 0f));
        int empty = 0;
        ctx.Events.Subscribe<AmmoEmptiedEvent>(e => empty++);
        ctx.Weapons.Tick(1f);
        Check(ctx.Weapons.Ammo == 0, "last bullet fired");
        RunProjectiles(ctx, 1f);
        ctx.Weapons.Tick(1f);
        Check(ctx.Projectiles.Active.Count == 0 && empty == 1, "empty magazine stops firing");
        Check(ctx.Roll.TryRoll(), "can roll on empty");
        for (int i = 0; i < 65; i++) ctx.Session.Tick(1f / 60f);
        Check(ctx.Weapons.Ammo >= ctx.Weapons.Capacity - 1, "refilled after subspace");
    }

    private static void AmmoBuildRaisesCapacity()
    {
        var ctx = World();
        ApplyBuild(ctx, BuildId.WaterAmmo);
        Check(ctx.Weapons.Capacity >= 34 && ctx.Weapons.Capacity <= 35, "ammo +15%: 30 -> 34/35");
        Check(ctx.Weapons.CapacityOf(WeaponKind.Spear) == 20, "builds are per weapon");
    }

    private static void OptionsComeFromEquippedWeapon()
    {
        var ctx = World();
        foreach (WeaponKind w in new[] { WeaponKind.WaterGun, WeaponKind.Spear, WeaponKind.Blade })
        {
            ctx.Loadout.Equip(w);
            for (int n = 0; n < 20; n++)
            {
                ctx.Progression.AddXp(ctx.Progression.XpToNext);
                ctx.Progression.RollOptions();
                Check(ctx.Progression.Options.Count == 3, "three options");
                for (int i = 0; i < ctx.Progression.Options.Count; i++)
                    Check(ctx.Progression.Options[i].Weapon == w, "options from the equipped weapon's pool");
                ctx.Progression.SkipPending();
            }
        }
    }

    private static void ChestHasCurseOption()
    {
        var ctx = World();
        ctx.Progression.AddChest();
        ctx.Progression.RollChestOptions();
        var list = ctx.Progression.Options;
        Check(list.Count == 3 && ctx.Progression.OptionsFromChest, "chest: three options");
        int curseIndex = -1;
        for (int i = 0; i < list.Count; i++) if (list[i].Curse != ChestCurse.None) curseIndex = i;
        Check(curseIndex >= 0, "one option carries a cost");
        ChestCurse curse = list[curseIndex].Curse;
        int before = TotalLevels(ctx, WeaponKind.WaterGun);
        float maxHp = ctx.Player.State.MaxHp;
        Check(ctx.Progression.TryApply(curseIndex), "take the cursed option");
        Check(ctx.Progression.CurseStacks(curse) == 1, "curse stacks");
        Check(TotalLevels(ctx, WeaponKind.WaterGun) == before + 3, "cursed option grants 3 random levels");
        if (curse == ChestCurse.Frail) Check(Near(ctx.Player.State.MaxHp, maxHp - 15f), "frail: max hp -15");
        Check(ctx.Progression.PendingChests == 0, "chest consumed");
    }

    private static int TotalLevels(ITurnContext ctx, WeaponKind w)
    {
        int total = 0;
        for (int i = 0; i < BuildIds.Count; i++)
            if (BuildIds.WeaponOf((BuildId)i) == w) total += ctx.Progression.GetLevel((BuildId)i);
        return total;
    }

    private static int _checks;

    /// <summary>一局测试世界：不自动刷怪、不刷精英 / Boss、三把武器开局全部解锁。</summary>
    private static ITurnContext World(Action<TurnConfig> configure = null)
    {
        var config = new TurnConfig();
        config.Horde.BaseCount = 0f;
        config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = 0;
        config.Horde.BossEveryWaves = 0;
        config.Loadout.UnlockWaves = new[] { 1, 1, 1 };
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
