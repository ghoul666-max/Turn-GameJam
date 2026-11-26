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
        RecallSeedsDistinctTargets();
        RecallWithoutTargetsScatters();
        NormalKillDoesNotRecurseAndGroundSurvivesExit();
        BombChildrenOverlapOnce();
        RiftUsesEllipse();
        PokerConfigMultipliers();
        Console.WriteLine($"Spear/Poker checks passed ({checks} assertions).");
    }

    private static ITurnContext World(out TurnConfig config)
    {
        config = new TurnConfig();
        config.Arsenal.FixedDamageMultiplier = 1f;
        config.Horde.BaseCount = config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = config.Horde.BossEveryWaves = 0;
        foreach (var kind in config.Horde.Kinds) kind.MaxHp = 10000f;
        var ctx = TurnFactory.Create(config, 777);
        ctx.Session.Start();
        return ctx;
    }

    private static MonsterEntity Spawn(ITurnContext ctx, float x, float y)
        => ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(x, y), MonsterKind.Walker, 0);

    private static void RecallSeedsDistinctTargets()
    {
        var ctx = World(out var config);
        ctx.Loadout.Equip(WeaponKind.Spear);
        var spear = new SpearWeapon(new WeaponKit(ctx, config.Arsenal));
        var host = Spawn(ctx, 3f, 0f);
        host.Hp = 1f; host.SpearMarks = 1;
        var a = Spawn(ctx, 3f, 2f);
        var b = Spawn(ctx, 3f, -2f);
        spear.Burst();
        Check(!host.Alive, "recall kills host");
        Check(ctx.Projectiles.Active.Count == 2, "one recall kill seeds exactly two normal spears");
        var p0 = ctx.Projectiles.Active[0]; var p1 = ctx.Projectiles.Active[1];
        Check(p0.Velocity.Y * p1.Velocity.Y < 0f, "two nearby enemies get distinct aimed spears");
        foreach (var p in ctx.Projectiles.Active)
        {
            Check(p.Weapon == WeaponKind.Spear && p.CarriedMarks == 1, "normal spear kind and mark payload");
            Check(Near(p.Damage, config.Arsenal.Spear.Damage), "normal spear damage");
        }
        spear.OnHit(p0, a);
        Check(a.SpearMarks == 1, "derived spear plants new mark");
        ctx.Events.Publish(new SubspaceExitedEvent { Weapon = WeaponKind.Spear });
        Check(a.SpearMarks == 1, "new mark survives full subspace exit");
    }

    private static void RecallWithoutTargetsScatters()
    {
        var ctx = World(out var config);
        var spear = new SpearWeapon(new WeaponKit(ctx, config.Arsenal));
        var host = Spawn(ctx, 3f, 0f); host.Hp = 1f; host.SpearMarks = 1;
        spear.Burst();
        Check(ctx.Projectiles.Active.Count == 2, "no targets still spawns two random spears");
        var a = ctx.Projectiles.Active[0].Velocity; var b = ctx.Projectiles.Active[1].Velocity;
        Check(a.SqrMagnitude > 0f && b.SqrMagnitude > 0f && Vec2.SqrDistance(a, b) > .001f, "random scatter directions nonzero and distinct for fixed seed");
    }

    private static void NormalKillDoesNotRecurseAndGroundSurvivesExit()
    {
        var ctx = World(out var config);
        var spear = new SpearWeapon(new WeaponKit(ctx, config.Arsenal));
        var target = Spawn(ctx, 2f, 0f); target.Hp = 1f;
        var projectile = ctx.Projectiles.SpawnPlayer(WeaponKind.Spear, ctx.Player.State.Position, Vec2.Right,
            config.Arsenal.Spear.Radius, config.Arsenal.Spear.Damage, config.Arsenal.Spear.Range);
        projectile.CarriedMarks = 1;
        spear.OnHit(projectile, target);
        Check(!target.Alive && ctx.Projectiles.Active.Count == 1, "ordinary spear kill does not spawn children");
        Check(spear.GroundSpearCount == 1, "ordinary kill leaves ground spear");
        ctx.Events.Publish(new SubspaceExitedEvent());
        Check(spear.GroundSpearCount == 1, "new ground spear survives subspace exit");
        ctx.Events.Publish(new PlaneSwitchedEvent());
        Check(spear.GroundSpearCount == 0, "ground spear clears on actual plane change");
    }

    private static void BombChildrenOverlapOnce()
    {
        var ctx = World(out var config);
        var poker = new PokerWeapon(new WeaponKit(ctx, config.Arsenal));
        var target = Spawn(ctx, 2f, 0f);
        int explosions = 0;
        ctx.Events.Subscribe<SpadeBombEvent>(_ => explosions++);
        float before = target.Hp;
        poker.DebugAddCard(new PlayingCard { Rank = 4, Suit = CardSuit.Spade });
        poker.Burst();
        float primary = before - target.Hp;
        Check(explosions == 1 && primary > 0f, "primary explosion immediate");
        poker.TickAlways(config.Arsenal.Poker.SpadeChildDelay * .5f);
        Check(explosions == 1, "child explosions wait configured delay");
        poker.TickAlways(config.Arsenal.Poker.SpadeChildDelay);
        Check(explosions == 5, "exactly four child explosions");
        Check(Near(before - target.Hp, primary * 2.5f), "overlapping four children each deal .375 primary damage");
        poker.TickAlways(10f);
        Check(explosions == 5, "children never split recursively");
    }

    private static void RiftUsesEllipse()
    {
        var ctx = World(out var config);
        var poker = new PokerWeapon(new WeaponKit(ctx, config.Arsenal));
        var center = Spawn(ctx, 3f, 0f);
        float radius = config.Arsenal.Poker.ClubRadius;
        var inside = Spawn(ctx, 3f + radius * .8f, 0f);
        var outside = Spawn(ctx, 3f, radius * .8f);
        poker.DebugAddCard(new PlayingCard { Rank = 4, Suit = CardSuit.Club });
        poker.Burst(); poker.TickAlways(.01f);
        Check(center.Hp < 10000f && inside.Hp < 10000f, "ellipse damages center and inside horizontal axis");
        Check(Near(outside.Hp, 10000f), "ellipse excludes point inside old circle but outside short axis");
    }

    private static void PokerConfigMultipliers()
    {
        var ctx = World(out var config);
        config.Arsenal.Poker.JokerChanceMultiplier = 0f;
        var poker = new PokerWeapon(new WeaponKit(ctx, config.Arsenal));
        bool anyJoker = false;
        for (int i = 0; i < 500; i++) anyJoker |= poker.Draw().IsJoker;
        Check(!anyJoker, "zero configured joker multiplier suppresses jokers");
        poker.DebugAddCard(new PlayingCard { Rank = 4, Suit = CardSuit.Diamond });
        poker.Burst();
        Check(poker.Shield == 2, "shield multiplier halves four shield points");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < .02f;
    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new Exception("FAILED: " + description);
    }
}
