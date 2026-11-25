using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Systems;
using Turn.Weapons;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        SwordDashesWithoutTargetAndUsesFacing();
        PalmHitsFullCircleAcrossRings();
        PalmRespectsLineOfSight();
        SpearKillDropsAllSpearsAndStops();
        SpearRecallsGroundAndLivingMarks();
        SpearGroundClearsOnSubspaceExit();
        BladeSnapshotsLiveTarget();
        Console.WriteLine($"Melee changes passed ({checks} assertions).");
    }

    private static ITurnContext World(WeaponKind kind)
    {
        var c = new TurnConfig();
        c.Horde.BaseCount = 0f;
        c.Horde.CountPerWave = 0f;
        c.Horde.EliteFromWave = 0;
        c.Horde.BossEveryWaves = 0;
        for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 1000f;
        c.Loadout.Main = kind;
        c.Arsenal.Sword.DashDistance = 4.5f;
        var world = TurnFactory.Create(c, 991);
        world.Session.Start();
        return world;
    }

    private static MonsterEntity Spawn(ITurnContext ctx, Vec2 at)
        => ctx.Monsters.SpawnKind(at, MonsterKind.Walker, 0);

    private static void SwordDashesWithoutTargetAndUsesFacing()
    {
        var ctx = World(WeaponKind.Sword);
        Vec2 origin = ctx.Player.State.Position;
        ctx.Player.State.Facing = new Vec2(0f, 1f);
        int before = ctx.Weapons.Ammo;
        ctx.Weapons.Tick(0f);
        Check(ctx.Weapons.Ammo == before - 1, "targetless sword consumes normal ammo");
        Check(ctx.Player.State.WeaponInvincible, "sword dash begins without targets");
        float duration = ctx.Config.Arsenal.Sword.DashTime;
        ctx.Weapons.Tick(duration * .5f);
        Check(Near(ctx.Player.State.Position.X, origin.X) &&
              Near(ctx.Player.State.Position.Y - origin.Y, 2.25f), "sword uses recent facing and configured distance/time");
        ctx.Weapons.Tick(duration * .5f);
        Check(Near(ctx.Player.State.Position.Y - origin.Y, 4.5f), "sword completes 4.5 unit dash");
    }

    private static void PalmHitsFullCircleAcrossRings()
    {
        var ctx = World(WeaponKind.Palm);
        Vec2 center = ctx.Player.State.Position;
        ctx.Player.State.Facing = Vec2.Right;
        float range = ctx.Config.Arsenal.Palm.Range;
        var front = Spawn(ctx, center + new Vec2(.5f, 0f));
        var rear = Spawn(ctx, center + new Vec2(-range * .9f, 0f));
        var side = Spawn(ctx, center + new Vec2(0f, range * .7f));
        var outside = Spawn(ctx, center + new Vec2(range + 2f, 0f));
        ctx.Weapons.Tick(0f);
        Check(front.Hp < 1000f && rear.Hp < 1000f && side.Hp < 1000f, "palm hits front back and side across distance rings");
        Check(outside.Hp == 1000f, "palm excludes outside radius");
        Check(front.PalmMarks > 0 && rear.PalmMarks > 0 && side.PalmMarks > 0, "full-circle palm preserves mark application");
    }

    private static void PalmRespectsLineOfSight()
    {
        var ctx = World(WeaponKind.Palm);
        // A separate fake obstacle context is unnecessary: the existing obstacle system snapshots config;
        // construct it before factory creation for an actual line-of-sight test.
        var c = new TurnConfig();
        c.Horde.BaseCount = c.Horde.CountPerWave = 0;
        c.Horde.EliteFromWave = c.Horde.BossEveryWaves = 0;
        for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 1000f;
        c.Loadout.Main = WeaponKind.Palm;
        c.Obstacles[0].Add(new ObstacleData { Shape = ObstacleShape.Box, Center = new Vec2(1f, 0f), HalfSize = new Vec2(.1f, 1f), Mask = ObstacleMask.Projectiles });
        var blocked = TurnFactory.Create(c, 999);
        blocked.Session.Start();
        var monster = Spawn(blocked, new Vec2(2f, 0f));
        blocked.Weapons.Tick(0f);
        Check(monster.Hp == 1000f, "palm keeps obstacle line of sight");
    }

    private static void SpearKillDropsAllSpearsAndStops()
    {
        var ctx = World(WeaponKind.Spear);
        var spear = new SpearWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        var monster = Spawn(ctx, ctx.Player.State.Position + new Vec2(4f, 0f));
        monster.Hp = 1f;
        monster.SpearMarks = 2;
        var p = new ProjectileEntity { Damage = 100f, CarriedMarks = 1 };
        bool continues = spear.OnHit(p, monster);
        Check(!continues && !monster.Alive, "killing spear stops rather than piercing");
        Check(spear.GroundSpearCount == 3 && monster.SpearMarks == 0 && p.CarriedMarks == 0,
            "current plus prior marks become exactly three ground spears");
        var fields = new List<WeaponField>();
        spear.CollectFields(fields);
        Check(fields.Count == 3 && fields.TrueForAll(f => f.Kind == WeaponFieldKind.GroundSpear && Near(f.Position.X, monster.Position.X)),
            "ground spears appear as dedicated fields at death position");
    }

    private static void SpearRecallsGroundAndLivingMarks()
    {
        var ctx = World(WeaponKind.Spear);
        var spear = new SpearWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 center = ctx.Player.State.Position;
        var dead = Spawn(ctx, center + new Vec2(4f, 0f));
        dead.Hp = 1f;
        dead.SpearMarks = 1;
        spear.OnHit(new ProjectileEntity { Damage = 100f, CarriedMarks = 1 }, dead);
        var along = Spawn(ctx, center + new Vec2(2f, 0f));
        var host = Spawn(ctx, center + new Vec2(4f, 2f));
        host.SpearMarks = 1;
        int recalls = 0;
        ctx.Events.Subscribe<SpearRecalledEvent>(_ => recalls++);
        spear.Burst();
        Check(recalls == 3, "burst recalls both two ground and one living spear");
        Check(along.Hp < 1000f, "ground recall deals damage along path to player");
        Check(spear.GroundSpearCount == 0 && host.SpearMarks == 0, "recall clears ground and living marks");
    }

    private static void SpearGroundClearsOnSubspaceExit()
    {
        var ctx = World(WeaponKind.Spear);
        var spear = new SpearWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        var monster = Spawn(ctx, ctx.Player.State.Position + new Vec2(4f, 0f));
        monster.Hp = 1f;
        spear.OnHit(new ProjectileEntity { Damage = 100f, CarriedMarks = 1 }, monster);
        ctx.Events.Publish(new SubspaceExitedEvent());
        Check(spear.GroundSpearCount == 0, "subspace exit clears stale ground spears");
    }

    private static void BladeSnapshotsLiveTarget()
    {
        var ctx = World(WeaponKind.Blade);
        var blade = new BladeWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        var target = Spawn(ctx, ctx.Player.State.Position + new Vec2(2f, 0f));
        target.Position = ctx.Player.State.Position + new Vec2(0f, 2f);
        BladeWaveFiredEvent evt = default;
        ctx.Events.Subscribe<BladeWaveFiredEvent>(e => evt = e);
        blade.Fire(ctx.Player.State.Position, target);
        Check(Near(evt.TargetPosition.X, target.Position.X) && Near(evt.TargetPosition.Y, target.Position.Y), "blade event snapshots actual target coordinate at firing");
        Check(evt.Direction.Y > .99f && target.Hp < 1000f, "blade instantly attacks current target direction");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < .001f;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
}
