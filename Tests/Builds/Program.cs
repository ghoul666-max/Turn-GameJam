using System;
using System.Collections.Generic;
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
        PalmStrikesFacingAndSkyPalm();
        CrossbowWallSlamAndIntent();
        CrossbowGrowthPersistsOnSwitch();
        GravityWellPullsBeforeBurst();
        SpaceAnchorRecallAndSlash();
        DronesSplitAndMerge();
        DebugSetLevelForPicker();
        PokerDrawsAndPlaysSuits();
        PokerJokerClearsOnSwitch();
        PokerHeartsHomeAndExplode();
        ShurikenSpinsRecallsAndOrbits();
        StaplerGroupsAndScatters();
        SwordDashTrailAndShadow();
        HourglassSlowsAndResets();
        WasdKeysAndFourWayBurst();
        MagnetPolesCollide();
        CursorSelectsAndPins();
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
        weak.SpearMarks = 2;
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 1f);
        Check(!weak.Alive, "first target killed");
        Check(Near(tank.Hp, 1000f) && tank.SpearMarks == 0, "killing spear stops and does not transfer marks to next target");
        Check(CountFields(ctx, WeaponFieldKind.GroundSpear) == 3, "current spear and two prior marks remain on the ground");
        MonsterEntity path = Spawn(ctx, new Vec2(1f, 0f));
        int recalls = 0;
        ctx.Events.Subscribe<SpearRecalledEvent>(_ => recalls++);
        Check(ctx.Roll.TryRoll(), "ground spears recalled by roll burst");
        Check(recalls == 3 && path.Hp < 1000f, "three ground spears damage the path back to player");
        Check(CountFields(ctx, WeaponFieldKind.GroundSpear) == 0, "ground spears consumed by recall");
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

    private static LoadoutConfig Single(WeaponKind main, SubWeaponKind sub = SubWeaponKind.None)
    {
        return new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = main, Sub = sub };
    }

    private static void RunSession(ITurnContext ctx, float seconds)
    {
        for (float t = 0f; t < seconds; t += 1f / 60f) ctx.Session.Tick(1f / 60f);
    }

    private static void PalmStrikesFacingAndSkyPalm()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Palm); });
        Check(ctx.Weapons.Current == WeaponKind.Palm && ctx.Weapons.Supports(WeaponKind.Palm), "palm equipped");
        MonsterEntity front = Spawn(ctx, new Vec2(2f, 0f));
        MonsterEntity back = Spawn(ctx, new Vec2(-2f, 0f));
        ctx.Weapons.Tick(1f);
        Check(Near(front.Hp, 993f) && front.PalmMarks == 1, "palm hits front for 7 and applies one mark");
        Check(Near(back.Hp, 993f) && back.PalmMarks == 1, "full-circle palm hits behind independent of facing");
        Check(front.Position.X - ctx.Player.State.Position.X > 2.9f && back.Position.X - ctx.Player.State.Position.X < -2.9f,
            "palm knocks both sides radially outward");
        ctx.Roll.TryRoll();
        Check(Near(front.Hp, 993f - 20f * 1.15f) && Near(back.Hp, 993f - 20f * 1.15f), "sky palm uses marks on both sides");
        Check(front.PalmMarks == 0 && back.PalmMarks == 0, "palm marks cleared after sky palm");

        var rearOnly = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Palm); });
        Spawn(rearOnly, new Vec2(-2f, 0f));
        int before = rearOnly.Weapons.Ammo;
        rearOnly.Weapons.Tick(1f);
        Check(rearOnly.Weapons.Ammo == before - 1, "rear target triggers normal palm attack and ammo cost");
        var empty = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Palm); });
        int emptyAmmo = empty.Weapons.Ammo;
        empty.Weapons.Tick(1f);
        Check(empty.Weapons.Ammo == emptyAmmo, "empty circle does not waste palm ammo");
    }

    private static void CrossbowWallSlamAndIntent()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Crossbow); c.Arsenal.Crossbow.Ammo = 3; });
        Check(ctx.Weapons.Capacity == 3, "crossbow ammo from config");
        ctx.Player.Teleport(new Vec2(12f, 0f));
        MonsterEntity m = Spawn(ctx, new Vec2(7.2f, 0f)); // 贴近右边界（x = 20）
        int slams = 0;
        ctx.Events.Subscribe<WallSlammedEvent>(e => slams++);
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 1f);
        Check(Near(m.Hp, 970f), "bolt hits for 30");
        Check(slams == 1 && ctx.Weapons.KillingIntent == 0 && m.StunLeft > 0f, "wall slam still stuns but grants no kill-size growth");
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 1f);
        Check(Near(m.Hp, 940f), "wall slam does not add old killing-intent damage multiplier");
        Check(slams == 2 && ctx.Weapons.KillingIntent == 0, "repeated wall slams remain effective without counting as kills");

        var free = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Crossbow); });
        MonsterEntity open = Spawn(free, new Vec2(3f, 0f));
        free.Weapons.Tick(1f);
        RunProjectiles(free, 1f);
        Check(free.Weapons.KillingIntent == 0 && open.Position.X - free.Player.State.Position.X > 5f, "open field: knockback without wall slam");
        Check(free.Weapons.Ammo == 0, "base crossbow ammo is 1");
    }

    private static void CrossbowGrowthPersistsOnSwitch()
    {
        var ctx = World(c =>
        {
            Tough(c);
            c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.Crossbow, SecondMain = WeaponKind.WaterGun };
        });
        ctx.Player.Teleport(new Vec2(12f, 0f));
        Spawn(ctx, new Vec2(7.2f, 0f));
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 1f);
        for (int i = 0; i < ctx.Config.Arsenal.Crossbow.KillsPerSizeStep; i++)
        {
            var victim = Spawn(ctx, new Vec2(-5f, i));
            ctx.Combat.DamageMonster(victim, victim.Hp + 1f, DamageSource.Weapon);
        }
        Check(ctx.Weapons.KillingIntent == 1, "configured player kills grant one size-growth step");
        int bursts = 0;
        ctx.Events.Subscribe<FountainBurstEvent>(e => bursts++);
        Check(ctx.Roll.TryRoll(), "roll");
        Check(ctx.Loadout.Equipped == WeaponKind.WaterGun && ctx.Weapons.KillingIntent == 1, "switching away preserves this-run crossbow size growth");
        Check(bursts == 0, "crossbow has no burst of its own");
    }

    private static void GravityWellPullsBeforeBurst()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Blade, SubWeaponKind.GravityWell); });
        Check(ctx.SubWeapons.Kind == SubWeaponKind.GravityWell, "gravity well equipped");
        MonsterEntity m = Spawn(ctx, new Vec2(3.2f, 2.4f)); // 距离 4，原本不在陌刀横斩的高度内
        MonsterEntity charging = Spawn(ctx, new Vec2(-4f, 0f), MonsterKind.Charger);
        charging.AiPhase = 2;
        Vec2 origin = ctx.Player.State.Position;
        float hpBefore = m.Hp;
        ctx.Roll.TryRoll();
        Check(Near(Vec2.Distance(m.Position, origin), 4f), "black hole spawn does not perform removed instantaneous pull");
        Check(Near(hpBefore - m.Hp, 0f), "enemy outside blade row is not retroactively pulled into burst");
        ctx.SubWeapons.Tick(1f);
        Check(Vec2.Distance(m.Position, origin) < 1.1f, "persistent hole pulls continuously over one second (4 -> 1)");
        Check(Near(Vec2.Distance(charging.Position, origin), 4f), "charging enemies remain immune to continuous pull");
        Check(Near(hpBefore - m.Hp, ctx.Config.Arsenal.Gravity.DamagePerSecond), "hole applies configured damage per second");
        Check(ctx.SubWeapons.Anchors.Count == 1, "persistent hole remains after affecting enemies");
    }

    private static void SpaceAnchorRecallAndSlash()
    {
        // 弩没有爆发，避免主武器爆发干扰斩击伤害
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Crossbow, SubWeaponKind.SpaceAnchor); });
        Vec2 a = ctx.Player.State.Position;
        ctx.Roll.TryRoll();
        Check(ctx.SubWeapons.Anchors.Count == 1 && Vec2.Distance(ctx.SubWeapons.Anchors[0], a) < .01f, "first roll records an anchor");
        RunSession(ctx, 3.2f);
        Vec2 b = a + new Vec2(6f, 0f);
        ctx.Player.Teleport(b);
        MonsterEntity m = Spawn(ctx, new Vec2(-3f, 0f)); // 在 b 与 a 的连线中间
        float hp = m.Hp;
        Check(ctx.Roll.TryRoll(), "second roll");
        Check(Vec2.Distance(ctx.Player.State.Position, a) < .05f, "recalled to the anchor");
        Check(Near(hp - m.Hp, 30f), "slash along the anchor line: 30");
        Check(ctx.SubWeapons.Anchors.Count == 1 && Vec2.Distance(ctx.SubWeapons.Anchors[0], b) < .01f, "new anchor at the roll position");
    }

    private static void DronesSplitAndMerge()
    {
        // 掌只打面向（右）的扇形；怪物放在身后，伤害只可能来自浮游炮
        var shoot = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Palm, SubWeaponKind.Drones); });
        Check(shoot.SubWeapons.UnitCount == 1, "start with 1 drone");
        int droneHits = 0;
        shoot.Events.Subscribe<MonsterDamagedEvent>(e => { if (e.Source == DamageSource.SubWeapon) droneHits++; });
        Spawn(shoot, new Vec2(-4f, 0f));
        RunSession(shoot, 1f);
        Check(droneHits > 0 && shoot.SubWeapons.Satellites.Count == 1, "drone orbits and shoots");

        var ctx = World(c => { c.Loadout = Single(WeaponKind.Palm, SubWeaponKind.Drones); c.Arsenal.Drones.SplitChance = 1f; });
        Check(ctx.Roll.TryRoll(), "roll 1");
        RunSession(ctx, 1.05f);
        Check(ctx.SubWeapons.UnitCount == 2, "split: +1");
        RunSession(ctx, 3f);
        Check(ctx.Roll.TryRoll(), "roll 2");
        RunSession(ctx, 1.05f);
        Check(ctx.SubWeapons.UnitCount == 3, "split again: additive, 3 (not 4)");

        var merge = World(c => { c.Loadout = Single(WeaponKind.WaterGun, SubWeaponKind.Drones); c.Arsenal.Drones.SplitChance = 0f; });
        merge.Roll.TryRoll();
        RunSession(merge, 1.05f);
        Check(merge.SubWeapons.UnitCount == 1, "merging at 1 keeps 1");

        var tuned = World(c =>
        {
            c.Loadout = Single(WeaponKind.WaterGun, SubWeaponKind.Drones);
            c.Arsenal.Drones.StartCount = 5;
            c.Arsenal.Drones.SplitChance = 0f;
            c.Arsenal.Drones.MergeRemove = 2;
            c.Arsenal.Drones.MinCount = 2;
        });
        Check(tuned.SubWeapons.UnitCount == 5, "start count is any integer now (5)");
        tuned.Roll.TryRoll();
        RunSession(tuned, 1.05f);
        Check(tuned.SubWeapons.UnitCount == 3, "merge removes MergeRemove (5 - 2 = 3)");
        RunSession(tuned, 2.1f);
        tuned.Roll.TryRoll();
        RunSession(tuned, 1.05f);
        Check(tuned.SubWeapons.UnitCount == 2, "merge stops at MinCount (2)");

        var capped = World(c =>
        {
            c.Loadout = Single(WeaponKind.WaterGun, SubWeaponKind.Drones);
            c.Arsenal.Drones.SplitChance = 1f;
            c.Arsenal.Drones.SplitAdd = 3;
            c.Arsenal.Drones.MaxCount = 3;
        });
        capped.Roll.TryRoll();
        RunSession(capped, 1.05f);
        Check(capped.SubWeapons.UnitCount == 3, "split adds SplitAdd but respects MaxCount (1 + 3 -> cap 3)");
    }

    /// <summary>F7 强化词条面板用的调试接口。</summary>
    private static void DebugSetLevelForPicker()
    {
        var ctx = World(c => c.Loadout = Single(WeaponKind.Palm, SubWeaponKind.Drones));
        Check(ctx.Progression.IsCarried(BuildId.PalmAmmo) && ctx.Progression.IsCarried(BuildId.DroneDamage), "carried: palm + drones builds");
        Check(!ctx.Progression.IsCarried(BuildId.WaterAmmo) && !ctx.Progression.IsCarried(BuildId.AnchorDamage), "not carried: water / anchor builds");
        int applied = 0;
        ctx.Events.Subscribe<UpgradeAppliedEvent>(e => applied++);
        int max = ctx.Progression.MaxLevel(BuildId.PalmAmmo);
        int cap = ctx.Weapons.Capacity;
        ctx.Progression.DebugSetLevel(BuildId.PalmAmmo, 99);
        Check(ctx.Progression.GetLevel(BuildId.PalmAmmo) == max && applied == 1, "debug level clamps to max and publishes once");
        Check(ctx.Weapons.Capacity > cap, "debug level takes effect immediately (palm capacity up)");
        ctx.Progression.DebugSetLevel(BuildId.PalmAmmo, -5);
        Check(ctx.Progression.GetLevel(BuildId.PalmAmmo) == 0 && ctx.Weapons.Capacity == cap, "debug level clamps to 0");
        ctx.Progression.DebugSetLevel(BuildId.PalmAmmo, 0);
        Check(applied == 2, "no event when the level does not change");
    }

    // ---------------- 11/25 第二 / 三批武器 ----------------

    private static PlayingCard Card(int rank, CardSuit suit) { return new PlayingCard { Rank = rank, Suit = suit }; }

    private static int CountFields(ITurnContext ctx, WeaponFieldKind kind)
    {
        int n = 0;
        IReadOnlyList<WeaponField> fields = ctx.Weapons.Fields;
        for (int i = 0; i < fields.Count; i++) if (fields[i].Kind == kind) n++;
        return n;
    }

    private static int CountProjectiles(ITurnContext ctx, WeaponKind weapon, int carried = -1)
    {
        int n = 0;
        IReadOnlyList<ProjectileEntity> all = ctx.Projectiles.Active;
        for (int i = 0; i < all.Count; i++)
            if (all[i].Alive && !all[i].Enemy && all[i].SubOwner == SubWeaponKind.None && all[i].Weapon == weapon && (carried < 0 || all[i].CarriedMarks == carried)) n++;
        return n;
    }

    private static void PokerDrawsAndPlaysSuits()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        Check(ctx.Weapons.Supports(WeaponKind.Poker) && ctx.Weapons.Capacity == 1, "poker: one rolling displayed card");
        int draws = 0;
        ctx.Events.Subscribe<CardDrawnEvent>(_ => draws++);
        ctx.Weapons.Tick(ctx.Config.Arsenal.Poker.DrawInterval);
        ctx.Weapons.Tick(ctx.Config.Arsenal.Poker.DrawInterval);
        Check(draws == 2 && ctx.Weapons.Hand.Count == 1 && ctx.Weapons.Ammo == 1, "poker keeps replacing one card without any enemy");
        Check(ctx.Weapons.DeckSize == 54, "full deck: 52 + 2 jokers");

        var joker = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        joker.Weapons.DebugAddCard(Card(PlayingCard.SmallJoker, CardSuit.None));
        Check(joker.Roll.TryRoll(), "roll plays displayed small joker");
        Check(joker.Weapons.Hand.Count == 0 && Near(joker.Weapons.JokerBonus, .08f), "small joker grants +8 percent and leaves no hidden hand");

        var diamond = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        diamond.Weapons.DebugAddCard(Card(7, CardSuit.Diamond));
        Check(diamond.Roll.TryRoll(), "roll plays displayed diamond");
        Check(diamond.Weapons.ShieldCharges == (int)MathF.Round(7f * diamond.Config.Arsenal.Poker.ShieldMultiplier) && diamond.Weapons.Hand.Count == 0, "diamond 7 grants configured halved shield charges");
        RunSession(diamond, 1.05f);
        int shield = diamond.Weapons.ShieldCharges;
        Check(!diamond.Combat.DamagePlayer(5f, DamageSource.Monster) && diamond.Weapons.ShieldCharges == shield - 1, "shield blocks a monster hit");
        Check(diamond.Combat.DamagePlayer(5f, DamageSource.Laser), "shield does not block lasers");

        var spade = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        MonsterEntity a = Spawn(spade, new Vec2(5f, 0f));
        MonsterEntity b = Spawn(spade, new Vec2(5.4f, 0f));
        MonsterEntity lone = Spawn(spade, new Vec2(-6f, 0f));
        spade.Weapons.DebugAddCard(Card(10, CardSuit.Spade));
        Check(spade.Roll.TryRoll(), "roll plays displayed spade");
        Check(Near(a.Hp, 940f, .1f) && Near(b.Hp, a.Hp, .1f), "spade 10 hits densest cluster for 6 x 10");
        Check(Near(lone.Hp, 1000f), "lone enemy is outside bomb");

        var heart = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        heart.Weapons.DebugAddCard(Card(1, CardSuit.Heart));
        Check(heart.Roll.TryRoll(), "roll plays displayed heart");
        Check(CountProjectiles(heart, WeaponKind.Poker) == heart.Config.Arsenal.Poker.HeartsPerPoint, "heart A emits configured doubled heart bullets");

        var club = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker); });
        MonsterEntity m = Spawn(club, new Vec2(4f, 2f));
        club.Weapons.DebugAddCard(Card(5, CardSuit.Club));
        club.Roll.TryRoll();
        Check(CountFields(club, WeaponFieldKind.Rift) == 1, "club opens a rift at nearest enemy");
        Vec2 riftAt = club.Weapons.Fields[0].Position;
        MonsterEntity side = Spawn(club, riftAt - club.Player.State.Position + new Vec2(1.5f, 0f));
        RunSession(club, 1.05f + .6f);
        Check(m.Hp < 1000f && side.Hp < 1000f, "rift deals damage over time");
        Check(Vec2.Distance(side.Position, riftAt) < 1.4f, "rift pulls enemies in");

        var trim = World(c => c.Loadout = Single(WeaponKind.Poker));
        trim.Progression.DebugSetLevel(BuildId.PokerTrimDeck, 99);
        Check(trim.Weapons.DeckSize == 6 && !trim.Progression.IsCarried(BuildId.WaterAmmo), "trim deck max level leaves four K and two jokers");
        for (int i = 0; i < 200; i++)
        {
            trim.Weapons.Tick(1f);
            Check(trim.Weapons.Hand.Count == 1, "trimmed deck still continuously rolls one card");
            PlayingCard drawn = trim.Weapons.Hand[0];
            Check(drawn.IsJoker || drawn.Rank == 13, "trimmed deck only draws K or jokers");
        }
    }

    private static void PokerHeartsHomeAndExplode()
    {
        // 扇形外（右上 37°）的敌人：不追踪的爱心弹（朝右 ±28° 内、不反弹）打不到，追踪的会拐过去
        foreach (bool homing in new[] { false, true })
        {
            var ctx = World(c =>
            {
                Tough(c);
                c.Loadout = Single(WeaponKind.Poker);
                c.Arsenal.Poker.HeartBounces = 0;
                c.Arsenal.Poker.HeartHomingTurn = homing ? 360f : 0f;
            });
            MonsterEntity above = Spawn(ctx, new Vec2(4f, 3f));
            ctx.Weapons.DebugAddCard(Card(1, CardSuit.Heart));
            ctx.Roll.TryRoll();
            above.Position = ctx.Player.State.Position + new Vec2(4f, 3f);
            RunProjectiles(ctx, 2f);
            Check(homing ? above.Hp < 1000f : Near(above.Hp, 1000f), homing ? "homing hearts curve into the enemy" : "without homing the hearts fly straight past");
        }

        // 命中爆炸：直接命中的那只 = 伤害 + 爆炸，旁边的只吃爆炸
        var blast = World(c =>
        {
            Tough(c);
            c.Loadout = Single(WeaponKind.Poker);
            c.Arsenal.Poker.HeartHomingTurn = 0f;
            c.Arsenal.Poker.HeartSpread = 0f;
            c.Arsenal.Poker.HeartsPerPoint = 1;
        });
        blast.Weapons.DebugAddCard(Card(1, CardSuit.Heart));
        blast.Roll.TryRoll();
        Vec2 p = blast.Player.State.Position;
        MonsterEntity hit = Spawn(blast, new Vec2(3f, 0f));
        MonsterEntity near = Spawn(blast, new Vec2(3.6f, 0.3f));
        hit.Position = p + new Vec2(3f, 0f);
        near.Position = p + new Vec2(3.6f, 0.3f);
        RunProjectiles(blast, 1f);
        Check(Near(hit.Hp, 1000f - 3f - 3f * 0.6f), "direct hit: 3 + blast 1.8");
        Check(Near(near.Hp, 1000f - 3f * 0.6f), "neighbour inside the blast: 1.8");
    }

    private static void PokerJokerClearsOnSwitch()
    {
        var ctx = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.Poker, SecondMain = WeaponKind.WaterGun });
        ctx.Weapons.DebugAddCard(Card(PlayingCard.BigJoker, CardSuit.None));
        ctx.Roll.TryRoll();
        Check(ctx.Loadout.Equipped == WeaponKind.WaterGun && Near(ctx.Weapons.JokerBonus, 0f), "joker bonus clears when switching away from poker");
    }

    private static void ShurikenSpinsRecallsAndOrbits()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Shuriken); });
        MonsterEntity target = Spawn(ctx, new Vec2(7f, 0f));
        ctx.Weapons.Tick(1f);
        for (int i = 0; i < 11; i++) ctx.Weapons.Tick(0.05f);
        Check(CountFields(ctx, WeaponFieldKind.Shuriken) == 1, "one shuriken on the field");
        Vec2 star = ctx.Weapons.Fields[0].Position;
        Check(Vec2.Distance(star, ctx.Player.State.Position) > 4.5f, "flies ~5");
        for (int i = 0; i < 4; i++) ctx.Weapons.Tick(0.05f);
        star = ctx.Weapons.Fields[0].Position;
        for (int i = 0; i < 16; i++) ctx.Weapons.Tick(0.05f);
        Check(Vec2.Distance(ctx.Weapons.Fields[0].Position, star) < 0.01f, "then stops and stays in place");
        MonsterEntity onPath = Spawn(ctx, new Vec2(2.5f, 0f));
        MonsterEntity spun = Spawn(ctx, star - ctx.Player.State.Position + new Vec2(0f, 0.5f));
        for (int i = 0; i < 8; i++) ctx.Weapons.Tick(0.05f);
        Check(spun.Hp < 1000f, "spinning shuriken damages nearby enemies");
        float before = onPath.Hp;
        ctx.Roll.TryRoll();
        Check(before - onPath.Hp >= 14f - 0.01f, "recall pierces enemies on the way back (14)");
        Check(CountFields(ctx, WeaponFieldKind.Shuriken) == 0 && CountFields(ctx, WeaponFieldKind.OrbitShuriken) == 0, "scheme B: cleared after recall, no orbit");

        var dual = World(c => c.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.DualMain, Main = WeaponKind.Shuriken, SecondMain = WeaponKind.WaterGun });
        Spawn(dual, new Vec2(6f, 0f));
        dual.Weapons.Tick(1f);
        dual.Weapons.Tick(1f);
        int thrown = CountFields(dual, WeaponFieldKind.Shuriken);
        dual.Roll.TryRoll();
        RunSession(dual, 1.05f);
        Check(dual.Loadout.Equipped == WeaponKind.WaterGun && CountFields(dual, WeaponFieldKind.OrbitShuriken) == thrown && thrown > 0, "scheme A: recalled shurikens orbit you while the other main is active");
        RunSession(dual, 2.1f);
        dual.Roll.TryRoll();
        Check(dual.Loadout.Equipped == WeaponKind.Shuriken && CountFields(dual, WeaponFieldKind.OrbitShuriken) == 0, "orbit disappears at the next switch");
    }

    private static void StaplerGroupsAndScatters()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Stapler); });
        MonsterEntity a = Spawn(ctx, new Vec2(3f, 0f));
        MonsterEntity b = Spawn(ctx, new Vec2(3.6f, 0f));
        ctx.Weapons.Tick(1f);
        RunProjectiles(ctx, 0.5f);
        Check(Near(a.Hp, 994f), "staple hits the nearest for 6");
        Check(a.StapleGroup != 0 && a.StapleGroup == b.StapleGroup, "knocked back into another monster: stapled together");
        Check(a.StapleMarks == 1 && b.StapleMarks == 1, "the whole group gets a staple mark");
        Check(b.SlowFactor < 1f, "stapled monsters are slowed");
        ctx.Roll.TryRoll();
        Check(a.StapleMarks == 0 && a.StapleGroup == 0, "burst clears the old marks and groups");
        Check(CountProjectiles(ctx, WeaponKind.Stapler, 1) == 10, "each marked monster scatters 5 secondary staples");
    }

    private static void SwordDashTrailAndShadow()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Sword); });
        MonsterEntity m = Spawn(ctx, new Vec2(2f, 0f));
        Vec2 start = ctx.Player.State.Position;
        ctx.Weapons.Tick(1f);
        Check(ctx.Player.State.WeaponInvincible && ctx.Player.State.Invincible, "invincible while dashing");
        for (int i = 0; i < 5; i++) ctx.Weapons.Tick(0.05f);
        Check(!ctx.Player.State.WeaponInvincible, "dash over: vulnerable again");
        Check(Near(ctx.Player.State.Position.X - start.X, ctx.Config.Arsenal.Sword.DashDistance, .05f), "sword dash travels configured distance toward recent facing");
        Check(Near(m.Hp, 986f), "dash stab: 14");
        Check(CountFields(ctx, WeaponFieldKind.SwordTrail) == 1, "dash leaves a sword trail");
        MonsterEntity weak = Spawn(ctx, start - ctx.Player.State.Position + new Vec2(1f, 0f));
        weak.Hp = 1f;
        int cap = ctx.Weapons.CapacityOf(WeaponKind.Sword);
        ctx.Roll.TryRoll();
        Check(Near(m.Hp, 986f - 22f * 1.05f, 0.05f), "shadow strike along the trail: 22 x (1 + 5% per trail)");
        Check(!weak.Alive && ctx.Weapons.SwordBonusCharges == 1 && ctx.Weapons.CapacityOf(WeaponKind.Sword) == cap + 1, "shadow kill: +1 dash charge");
        Check(CountFields(ctx, WeaponFieldKind.SwordTrail) == 0, "trails cleared after the burst");

        var empty = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Sword); });
        Vec2 emptyStart = empty.Player.State.Position;
        empty.Player.State.Facing = new Vec2(0f, 1f);
        int ammo = empty.Weapons.Ammo;
        empty.Weapons.Tick(0f);
        Check(empty.Weapons.Ammo == ammo - 1 && empty.Player.State.WeaponInvincible,
            "sword starts targetless dash and spends normal ammo");
        empty.Weapons.Tick(empty.Config.Arsenal.Sword.DashTime);
        Check(Near(empty.Player.State.Position.X, emptyStart.X) &&
            Near(empty.Player.State.Position.Y - emptyStart.Y, empty.Config.Arsenal.Sword.DashDistance, .05f),
            "targetless dash follows recent facing with unchanged configured time");
    }

    private static void HourglassSlowsAndResets()
    {
        var ctx = World(c => c.Loadout = Single(WeaponKind.Poker, SubWeaponKind.Hourglass));
        Check(Near(ctx.SubWeapons.MonsterSpeedMultiplier, 1f), "hourglass starts at full speed");
        RunSession(ctx, 2.5f);
        Check(Near(ctx.SubWeapons.MonsterSpeedMultiplier, .75f, .02f), "hourglass linear midpoint at 2.5 seconds");
        RunSession(ctx, 2.5f);
        Check(Near(ctx.SubWeapons.MonsterSpeedMultiplier, .5f, .02f), "hourglass reaches half speed at five seconds");
        RunSession(ctx, 60f);
        Check(Near(ctx.SubWeapons.MonsterSpeedMultiplier, .5f, .001f), "stays exactly at floor");
        ctx.Roll.TryRoll();
        Check(Near(ctx.SubWeapons.MonsterSpeedMultiplier, 1f), "roll resets enemy speed");
    }

    private static void WasdKeysAndFourWayBurst()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Poker, SubWeaponKind.Wasd); });
        MonsterEntity m = Spawn(ctx, new Vec2(2.6f, 0f));
        ctx.Player.SetMoveInput(new Vec2(1f, 0f));
        ctx.Player.Tick(0f);
        ctx.SubWeapons.Tick(0.11f);
        Check(Near(m.Hp, 992f), "holding D: key zone hits for 8");
        ctx.SubWeapons.Tick(0.11f);
        ctx.SubWeapons.Tick(0.11f);
        Check(Near(m.Hp, 992f), "each enemy at most once per 3 s");
        ctx.Player.SetMoveInput(Vec2.Zero);
        ctx.Player.Tick(0f);
        ctx.Roll.TryRoll();
        Check(Near(m.Hp, 972f), "roll: four-way burst 20");
    }

    private static void MagnetPolesCollide()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Crossbow, SubWeaponKind.Magnet); c.Arsenal.Gravity.DamagePerSecond = 0f; c.Arsenal.Gravity.PullDistance = 0f; });
        Vec2 a = ctx.Player.State.Position;
        ctx.Roll.TryRoll();
        Check(ctx.SubWeapons.Anchors.Count == 1, "odd roll: positive pole");
        RunSession(ctx, 2.5f);
        ctx.Player.Teleport(a + new Vec2(6f, 0f));
        MonsterEntity mid = Spawn(ctx, new Vec2(-3f, 0f));
        MonsterEntity far = Spawn(ctx, new Vec2(-3f, 5f));
        Check(ctx.Roll.TryRoll(), "even roll");
        Check(ctx.SubWeapons.Anchors.Count == 2 && Near(mid.Hp, 1000f), "second roll creates two moving holes without immediate collision damage");
        float travel = 3f / ctx.Config.Arsenal.Gravity.MergeSpeed;
        ctx.SubWeapons.Tick(travel * .5f);
        Check(ctx.SubWeapons.Anchors.Count == 2 && Near(mid.Hp, 1000f), "holes remain separate before reaching midpoint");
        ctx.SubWeapons.Tick(travel * .5f + .001f);
        Check(Near(mid.Hp, 1000f - ctx.Config.Arsenal.Magnet.PathDamage - ctx.Config.Arsenal.Magnet.BlastDamage), "collision applies configured path and midpoint explosion damage");
        Check(mid.StunLeft > 0f && Near(far.Hp, 1000f), "blast stuns; enemies off the line are safe");
        Check(ctx.SubWeapons.Anchors.Count == 1 && Vec2.Distance(ctx.SubWeapons.Anchors[0], a + new Vec2(3f, 0f)) < .01f,
            "collision leaves one permanent hole at the original pair midpoint");
    }

    private static void CursorSelectsAndPins()
    {
        var ctx = World(c => { Tough(c); c.Loadout = Single(WeaponKind.Crossbow, SubWeaponKind.Cursor); });
        Vec2 a = ctx.Player.State.Position;
        ctx.Roll.TryRoll();
        Check(ctx.SubWeapons.Trail.Count == 4, "first roll starts four-corner rectangle");
        RunSession(ctx, 2.5f);
        // The route detour is irrelevant: the final position and first-roll anchor define the box.
        ctx.Player.Teleport(a + new Vec2(-4f, 0f));
        ctx.SubWeapons.Tick(.01f);
        ctx.Player.Teleport(a + new Vec2(4f, 4f));
        ctx.SubWeapons.Tick(.01f);
        MonsterEntity inside = Spawn(ctx, new Vec2(-2f, -2f));
        MonsterEntity outside = Spawn(ctx, new Vec2(-7f, -2f));
        Check(ctx.Roll.TryRoll(), "second roll: rectangle strike");
        Check(Near(inside.Hp, 965f) && inside.PinnedLeft > 0f && inside.StunLeft > 0f, "inside rectangle: 35 + pinned");
        Check(Near(outside.Hp, 1000f), "old detour outside final rectangle: untouched");
        Check(ctx.SubWeapons.Trail.Count == 4, "next rectangle starts right away");
    }

    private static int TotalLevels(ITurnContext ctx, WeaponKind w)
    {
        int total = 0;
        for (int i = 0; i < BuildIds.Count; i++)
            if (BuildIds.WeaponOf((BuildId)i) == w) total += ctx.Progression.GetLevel((BuildId)i);
        return total;
    }

    private static int _checks;

    /// <summary>一局测试世界：不自动刷怪、不刷精英 / Boss；默认方案 B + 水枪 + 不带副武器（Loadout.Equip 可换主武器）。</summary>
    private static ITurnContext World(Action<TurnConfig> configure = null)
    {
        var config = new TurnConfig();
        config.Arsenal.FixedDamageMultiplier = 1f;
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
