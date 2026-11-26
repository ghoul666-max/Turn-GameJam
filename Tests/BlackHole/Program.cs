using Turn.Core;
using Turn.Systems;
using Turn.Weapons;

internal static class Program
{
    private static int _checks;
    private static void Main()
    {
        PermanentAndSpaceLifecycle();
        QueueAndFixedMidpoints();
        ContinuousDamageAndNoGrowth();
        MagnetAlias();
        DistantHoleAttractsExperience();
        SmallTimestepsStillPull();
        BothBuildFamiliesAreCarried();
        Console.WriteLine($"Black-hole checks passed ({_checks} assertions).");
    }

    private static ITurnContext World(SubWeaponKind sub = SubWeaponKind.None)
    {
        var config = new TurnConfig();
        config.Horde.BaseCount = 0f;
        config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = 0;
        config.Horde.BossEveryWaves = 0;
        config.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = WeaponKind.Crossbow, Sub = sub };
        for (int i = 0; i < config.Horde.Kinds.Length; i++) config.Horde.Kinds[i].MaxHp = 10000f;
        config.Arsenal.Gravity.MergeSpeed = 8f;
        config.Arsenal.Gravity.DamagePerSecond = 6f;
        config.Arsenal.Gravity.Radius = 5f;
        config.Arsenal.Gravity.PullDistance = 0f;
        ITurnContext ctx = TurnFactory.Create(config, 777);
        ctx.Session.Start();
        return ctx;
    }

    private static void PermanentAndSpaceLifecycle()
    {
        ITurnContext ctx = World();
        var hole = new GravityWellSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 a = ctx.Player.State.Position;
        hole.BeforeBurst(a);
        hole.Tick(1000f);
        Check(hole.Anchors.Count == 1 && Near(hole.Anchors[0], a), "stationary hole is permanent");
        hole.OnSubspaceExited();
        ctx.Events.Publish(new SubspaceExitedEvent());
        Check(hole.Anchors.Count == 1, "ordinary roll subspace exit does not clear holes");
        hole.BeforeBurst(a + new Vec2(8f, 0f));
        hole.BeforeBurst(a + new Vec2(0f, 8f));
        ctx.Events.Publish(new PlaneSwitchedEvent());
        hole.Tick(100f);
        Check(hole.Anchors.Count == 0, "actual plane exit clears holes and pending origins");
        hole.BeforeBurst(a);
        ctx.Events.Publish(new PlayerDiedEvent());
        Check(hole.Anchors.Count == 0, "death clears holes");
        hole.BeforeBurst(a);
        ctx.Events.Publish(new LevelClearedEvent());
        Check(hole.Anchors.Count == 0, "level end clears holes");
        hole.BeforeBurst(a);
        ctx.Events.Publish(new RunStartedEvent());
        Check(hole.Anchors.Count == 0, "new run clears holes");
    }

    private static void QueueAndFixedMidpoints()
    {
        ITurnContext ctx = World();
        var hole = new GravityWellSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 a = ctx.Player.State.Position;
        Vec2 b = a + new Vec2(8f, 0f);
        Vec2 c = a + new Vec2(0f, 8f);
        Vec2 d = a + new Vec2(-8f, 0f);
        int collisions = 0;
        var centers = new List<Vec2>();
        ctx.Events.Subscribe<MagnetCollideEvent>(e => { collisions++; centers.Add(e.Center); });
        hole.BeforeBurst(a); hole.BeforeBurst(b); hole.BeforeBurst(c); hole.BeforeBurst(d);
        Check(hole.Anchors.Count == 2, "queued origins do not spawn extra active holes");
        hole.Tick(.25f);
        Check(Near(hole.Anchors[0], a + new Vec2(2f, 0f)) && Near(hole.Anchors[1], a + new Vec2(6f, 0f)),
            "pair moves symmetrically toward fixed midpoint");
        hole.Tick(.25f);
        Vec2 ab = (a + b) * .5f;
        Check(collisions == 1 && hole.Anchors.Count == 2 && Near(hole.Anchors[0], ab) && Near(hole.Anchors[1], c),
            "collision leaves one midpoint hole and starts oldest queued origin");
        hole.Tick(100f);
        Vec2 abc = (ab + c) * .5f;
        Vec2 final = (abc + d) * .5f;
        Check(collisions == 3 && Near(centers[0], ab) && Near(centers[1], abc) && Near(centers[2], final),
            "FIFO origins merge at successive fixed midpoints even in large timestep");
        Check(hole.Anchors.Count == 1 && Near(hole.Anchors[0], final), "queue drains leaving exactly one permanent hole");
        // Identical origins should terminate without division by zero or an endless loop.
        hole.BeforeBurst(final); hole.BeforeBurst(final); hole.Tick(.01f);
        Check(hole.Anchors.Count == 1 && collisions == 5, "coincident holes safely merge queued pairs");
    }

    private static void ContinuousDamageAndNoGrowth()
    {
        ITurnContext ctx = World();
        var hole = new GravityWellSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 a = ctx.Player.State.Position;
        MonsterEntity target = ctx.Monsters.SpawnKind(a + new Vec2(0f, 1f), MonsterKind.Walker, 0);
        hole.BeforeBurst(a);
        float hp = target.Hp;
        hole.Tick(.5f); hole.Tick(.5f);
        float initialDamage = hp - target.Hp;
        Check(Near(initialDamage, 6f * ctx.Config.Arsenal.FixedDamageMultiplier), "stationary hole applies configured damage per second and shared multiplier");
        hole.BeforeBurst(a); hole.Tick(.01f);
        hp = target.Hp;
        hole.Tick(1f);
        Check(Near(hp - target.Hp, initialDamage), "merge does not increase permanent hole DPS");
        MonsterEntity outside = ctx.Monsters.SpawnKind(a + new Vec2(0f, 6f), MonsterKind.Walker, 0);
        hp = outside.Hp;
        hole.Tick(1f);
        Check(Near(outside.Hp, hp), "merge does not increase permanent hole radius");
        // Only the departing hole can affect this enemy; midpoint is outside configured radius.
        ctx.Events.Publish(new PlaneSwitchedEvent());
        hole.BeforeBurst(a); hole.BeforeBurst(a + new Vec2(16f, 0f));
        hp = target.Hp;
        hole.Tick(.1f);
        Check(target.Hp < hp, "moving hole still applies continuous damage");
    }

    private static void MagnetAlias()
    {
        ITurnContext ctx = World();
        var magnet = new MagnetSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 a = ctx.Player.State.Position;
        magnet.BeforeBurst(a); magnet.BeforeBurst(a + new Vec2(4f, 0f)); magnet.Tick(1f);
        Check(magnet.Kind == SubWeaponKind.Magnet && magnet.Anchors.Count == 1 && Near(magnet.Anchors[0], a + new Vec2(2f, 0f)),
            "legacy Magnet alias uses unified black-hole rules");
    }

    private static void DistantHoleAttractsExperience()
    {
        ITurnContext ctx = World(SubWeaponKind.GravityWell);
        Vec2 player = ctx.Player.State.Position;
        Vec2 center = player + new Vec2(12f, 0f);
        ctx.Config.Arsenal.Gravity.PickupRadius = 5f;
        ctx.Config.Arsenal.Gravity.PickupSpeed = 8f;
        ctx.SubWeapons.BeforeBurst(center);
        ctx.Experience.SpawnOrb(center + new Vec2(1f, 0f), 3);
        OrbEntity orb = ctx.Experience.Active[0];
        float initialDistance = Vec2.Distance(orb.Position, player);
        Check(initialDistance > ctx.Config.Experience.PickupRange, "pickup orb starts outside normal player attraction range");
        int collected = 0;
        ctx.Events.Subscribe<OrbCollectedEvent>(e => collected += e.Value);
        ctx.Experience.Tick(.1f);
        Check(orb.Magnetized && Vec2.Distance(orb.Position, player) < initialDistance,
            "distant hole magnetizes nearby orb and moves it toward player, not hole");
        for (int i = 0; i < 100 && orb.Alive; i++) ctx.Experience.Tick(.1f);
        Check(!orb.Alive && ctx.Experience.Active.Count == 0 && collected == 3,
            "hole-attracted orb travels to player and is collected exactly once");
    }

    private static void SmallTimestepsStillPull()
    {
        ITurnContext ctx = World();
        ctx.Config.Arsenal.Gravity.PullDistance = 3f;
        ctx.Config.Arsenal.Gravity.DamagePerSecond = 0f;
        var hole = new GravityWellSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 center = ctx.Player.State.Position;
        MonsterEntity target = ctx.Monsters.SpawnKind(center + new Vec2(3f, 0f), MonsterKind.Walker, 0);
        hole.BeforeBurst(center);
        float before = Vec2.Distance(target.Position, center);
        for (int i = 0; i < 100; i++) hole.Tick(.001f);
        Check(MathF.Abs(before - Vec2.Distance(target.Position, center) - .3f) < .01f,
            "1000Hz updates retain configured pull speed without per-frame distance threshold");
    }

    private static void BothBuildFamiliesAreCarried()
    {
        foreach (SubWeaponKind sub in new[] { SubWeaponKind.GravityWell, SubWeaponKind.Magnet })
        {
            ITurnContext ctx = World(sub);
            Check(ctx.Progression.IsCarried(BuildId.GravityRadius) && ctx.Progression.IsCarried(BuildId.MagnetBlastDamage),
                "both legacy build families are carried by unified skill alias " + sub);
        }
        ITurnContext other = World(SubWeaponKind.Hourglass);
        Check(!other.Progression.IsCarried(BuildId.GravityRadius) && !other.Progression.IsCarried(BuildId.MagnetBlastDamage),
            "unrelated subweapon does not receive black-hole builds");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < .01f;
    private static bool Near(Vec2 a, Vec2 b) => Vec2.Distance(a, b) < .01f;
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }
}
