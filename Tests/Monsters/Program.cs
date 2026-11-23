using System;
using System.Linq;
using Turn.Core;
using Turn.Monsters;
using Turn.Systems;

internal static class Program
{
    private static void Main()
    {
        CatalogAndPools();
        PairSwapAndBuff();
        SplitAndClear();
        SlowAndProjectiles();
        RockImpactWarningAndDodge();
        FutureSoldierLaserWarningAndDodge();
        RobotKnockbackHitEventOnlyOnDamage();
        Console.WriteLine("Monster checks passed.");
    }

    private static ITurnContext NewWorld(PlaneId plane = PlaneId.Volcano)
    {
        var config = new TurnConfig();
        config.Environment.StartPlane = plane;
        config.Spawn.InitialCount = 0;
        config.Spawn.CountPerSpawn = 0;
        config.Monster.SplitInterval = .25f;
        if (plane == PlaneId.Primal) config.Spawn.KindWeights[(int)MonsterKind.VineDemon] = -1f;
        return TurnFactory.Create(config, 21);
    }

    private static void CatalogAndPools()
    {
        MonsterArchetype[] stats = MonsterCatalog.CreateDefaults();
        Check(stats.Length == 13 && stats.All(s => s != null && s.MaxHp > 0), "all archetypes configured");
        Check(MonsterCatalog.ForPlane(MonsterKind.NullPointer, PlaneId.Dream) == MonsterKind.Alarm, "office pair mapping");
        Check(MonsterCatalog.ForPlane(MonsterKind.Bug, PlaneId.Dream) == MonsterKind.BlackPot, "bug maps black pot");
        Check(MonsterCatalog.ForPlane(MonsterKind.Boar, PlaneId.Future) == MonsterKind.DataWorm, "P2 pair mapping");
        var random = new Random(7);
        var spawn = new SpawnConfig { P2StartSeconds = 90f };
        for (int i = 0; i < 200; i++) Check(MonsterSpawnPool.Pick(PlaneId.Primal, spawn, random, 0f) != MonsterKind.Boar, "P2 gated");
        Check(MonsterSpawnPool.Pick(PlaneId.Volcano, spawn, random, 0f) == MonsterKind.StoneGolem, "volcano-only spawn");
    }

    private static void PairSwapAndBuff()
    {
        ITurnContext world = NewWorld();
        MonsterEntity stone = world.Monsters.Spawn(new Vec2(3, 0), false);
        Check(stone.Kind == MonsterKind.StoneGolem && stone.XpValue == 6, "stone spawn and XP");
        world.Combat.DamageMonster(stone, 30f, DamageSource.Weapon);
        world.Monsters.ApplyBurn(stone, 6f, 3f);
        world.Monsters.ApplySlow(stone, .7f, 2f);
        int id = stone.Id;
        world.Plane.Switch();
        world.Monsters.SwapPlane(PlaneId.Volcano, PlaneId.Ocean);
        Check(stone.Id == id && stone.Kind == MonsterKind.Anglerfish && Math.Abs(stone.Hp - 22.5f) < .01f, "ratio and same instance");
        Check(stone.Burning && stone.SlowLeft > 0 && stone.SlowFactor == .7f, "all statuses survive");
        world.Monsters.Tick(.1f);
        Check(stone.SlowLeft < 2f, "slow timer ticks");
    }

    private static void SplitAndClear()
    {
        ITurnContext world = NewWorld(PlaneId.Office);
        MonsterEntity parent = world.Monsters.Spawn(new Vec2(6, 0), false);
        for (int i = 0; i < 12 && parent.Kind != MonsterKind.NullPointer; i++)
        {
            world.Monsters.Despawn(parent);
            world.Monsters.Tick(.01f);
            parent = world.Monsters.Spawn(new Vec2(6, 0), false);
        }
        Check(parent.Kind == MonsterKind.NullPointer, "office spawn parent");
        for (int i = 0; i < 50; i++) world.Monsters.Tick(.1f);
        Check(world.Monsters.Active.Count(m => m.Alive && m.Kind == MonsterKind.Buglet && m.ParentId == parent.Id) == world.Config.Monster.BugletLimitPerParent, "per-parent split cap");
        world.Plane.Switch();
        world.Monsters.SwapPlane(PlaneId.Office, PlaneId.Dream);
        Check(world.Monsters.Active.All(m => !m.Alive || m.Kind != MonsterKind.Buglet), "buglets clear on switch");
    }

    private static void SlowAndProjectiles()
    {
        ITurnContext world = NewWorld(PlaneId.Ocean);
        MonsterEntity fish = world.Monsters.Spawn(new Vec2(4, 0), false);
        Check(fish.Kind == MonsterKind.Anglerfish, "ocean fish");
        for (int i = 0; i < 30; i++) world.Monsters.Tick(.1f);
        Check(world.Projectiles.Active.Any(p => p.Enemy && p.EnemyKind == EnemyProjectileKind.Bubble), "fish fires bubble");
    }

    private static void RockImpactWarningAndDodge()
    {
        ITurnContext world = NewWorld(PlaneId.Primal);
        world.Config.Monster.RockWarning = .3f;
        world.Config.Monster.Archetypes[(int)MonsterKind.RockThrower].AttackInterval = 4f;
        world.Config.Monster.Archetypes[(int)MonsterKind.RockThrower].Windup = .1f;
        MonsterEntity rock = world.Monsters.Spawn(new Vec2(4f, 0), false);
        int warnings = 0;
        Vec2 locked = Vec2.Zero;
        world.Events.Subscribe<RockImpactWarnedEvent>(evt => { warnings++; locked = evt.Position; });
        for (int i = 0; i < 12 && warnings == 0; i++) world.Monsters.Tick(.1f);
        Check(warnings == 1 && rock.AiPhase == 4, $"rock warning after windup (warnings={warnings}, phase={rock.AiPhase}, pos={rock.Position})");
        float hp = world.Player.State.Hp;
        world.Monsters.Tick(.1f);
        Check(world.Player.State.Hp == hp, "rock warning causes no early damage");
        world.Monsters.Tick(.21f);
        Check(world.Player.State.Hp < hp, $"rock impacts locked location after warning (phase={rock.AiPhase}, timer={rock.AiTimer}, target={locked}, player={world.Player.State.Position})");
        Check(locked.SqrMagnitude < .001f, "rock locked original player position");

        ITurnContext dodge = NewWorld(PlaneId.Primal);
        dodge.Config.Monster.RockWarning = .3f;
        dodge.Config.Monster.Archetypes[(int)MonsterKind.RockThrower].Windup = .1f;
        MonsterEntity second = dodge.Monsters.Spawn(new Vec2(4f, 0), false);
        for (int i = 0; i < 12 && second.AiPhase != 4; i++) dodge.Monsters.Tick(.1f);
        Check(second.AiPhase == 4, "dodge case rock warning");
        float before = dodge.Player.State.Hp;
        dodge.Player.State.Position = new Vec2(3f, 3f);
        dodge.Monsters.Tick(.31f);
        Check(second.AiPhase == 0 && dodge.Player.State.Hp == before, "player can dodge locked rock impact");
    }

    private static void FutureSoldierLaserWarningAndDodge()
    {
        ITurnContext world = NewWorld(PlaneId.Future);
        world.Config.Spawn.KindWeights[(int)MonsterKind.RepairRobot] = -1f;
        world.Config.Spawn.KindWeights[(int)MonsterKind.DataWorm] = -1f;
        world.Config.Monster.LaserWarning = .4f;
        world.Config.Monster.LaserDuration = 1f;
        MonsterEntity soldier = world.Monsters.Spawn(new Vec2(4f, 0f), false);
        Check(soldier.Kind == MonsterKind.FutureSoldier, "future soldier chosen");
        int warnings = 0;
        world.Events.Subscribe<LaserAttackWarnedEvent>(_ => warnings++);
        for (int i = 0; i < 12 && warnings == 0; i++) world.Monsters.Tick(.1f);
        Check(warnings == 1 && soldier.AiPhase == 1, "laser warns before attack");
        float hp = world.Player.State.Hp;
        world.Monsters.Tick(.2f);
        Check(world.Player.State.Hp == hp, "laser warning deals no damage");
        world.Monsters.Tick(.21f);
        Check(soldier.AiPhase == 5 && world.Player.State.Hp == hp, "laser enters active beam after warning");
        world.Monsters.Tick(.1f);
        Check(world.Player.State.Hp < hp, "active laser hits on locked beam");

        ITurnContext dodge = NewWorld(PlaneId.Future);
        dodge.Config.Spawn.KindWeights[(int)MonsterKind.RepairRobot] = -1f;
        dodge.Config.Monster.LaserWarning = .4f;
        MonsterEntity other = dodge.Monsters.Spawn(new Vec2(4f, 0f), false);
        for (int i = 0; i < 12 && other.AiPhase != 1; i++) dodge.Monsters.Tick(.1f);
        Check(other.AiPhase == 1, "dodge case warns");
        float before = dodge.Player.State.Hp;
        dodge.Player.State.Position = new Vec2(0f, 3f);
        dodge.Monsters.Tick(.5f);
        dodge.Monsters.Tick(.1f);
        Check(dodge.Player.State.Hp == before, "player dodges locked laser direction");
    }

    private static void RobotKnockbackHitEventOnlyOnDamage()
    {
        ITurnContext world = NewWorld(PlaneId.Future);
        world.Config.Spawn.KindWeights[(int)MonsterKind.FutureSoldier] = -1f;
        world.Config.Spawn.KindWeights[(int)MonsterKind.DataWorm] = -1f;
        world.Config.Monster.Archetypes[(int)MonsterKind.RepairRobot].Windup = .1f;
        world.Config.Monster.RobotKnockbackDistance = 1.5f;
        MonsterEntity robot = world.Monsters.Spawn(new Vec2(1f, 0f), false);
        Check(robot.Kind == MonsterKind.RepairRobot, "repair robot chosen");
        int hits = 0;
        RobotKnockbackHitEvent last = default;
        world.Events.Subscribe<RobotKnockbackHitEvent>(evt => { hits++; last = evt; });
        for (int i = 0; i < 30 && hits == 0; i++) world.Monsters.Tick(.1f);
        Check(hits == 1 && last.MonsterId == robot.Id && last.Plane == PlaneId.Future &&
            Math.Abs(last.Distance - 1.5f) < .001f, "robot emits hit knockback fact");
        world.Player.State.InvincibleLeft = 1f;
        robot.ContactTimer = 0f;
        robot.AiPhase = 2;
        robot.AiTimer = .2f;
        robot.Position = world.Player.State.Position + new Vec2(.1f, 0f);
        world.Monsters.Tick(.01f);
        Check(hits == 1, "invincible player does not receive knockback event");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
