using Turn.Core;
using Turn.Systems;
using Turn.Weapons;

internal static class Program
{
    private static int _checks;
    private static void Main()
    {
        PokerHasOneRollingCardAndPlaysShownCard();
        PokerBigJokerKeepsExtraPlay();
        PokerRollIntegration();
        HourglassLinearFloorResetAndBuilds();
        CursorRectangleIgnoresWalkedPolygon();
        CursorThicknessAndDiagonalLimit();
        Console.WriteLine($"Utility changes passed ({_checks} assertions).");
    }

    private static ITurnContext World(WeaponKind main = WeaponKind.Poker, SubWeaponKind sub = SubWeaponKind.None)
    {
        var config = new TurnConfig();
        config.Horde.BaseCount = 0f;
        config.Horde.CountPerWave = 0f;
        config.Horde.EliteFromWave = 0;
        config.Horde.BossEveryWaves = 0;
        config.Loadout = new LoadoutConfig { Scheme = LoadoutScheme.MainAndSub, Main = main, Sub = sub };
        for (int i = 0; i < config.Horde.Kinds.Length; i++) config.Horde.Kinds[i].MaxHp = 10000f;
        ITurnContext ctx = TurnFactory.Create(config, 777);
        ctx.Session.Start();
        return ctx;
    }

    private static void PokerHasOneRollingCardAndPlaysShownCard()
    {
        ITurnContext ctx = World();
        var poker = new PokerWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        int draws = 0;
        ctx.Events.Subscribe<CardDrawnEvent>(_ => draws++);
        for (int i = 0; i < 20; i++) poker.Fire(ctx.Player.State.Position, null);
        Check(draws == 20 && poker.Hand.Count == 1 && poker.BaseAmmo == 1, "draw replaces one hand card without filling");
        var shown = new PlayingCard { Rank = 7, Suit = CardSuit.Diamond };
        poker.DebugAddCard(shown);
        var played = new List<PlayingCard>();
        ctx.Events.Subscribe<CardPlayedEvent>(evt => played.Add(evt.Card));
        poker.Burst();
        Check(played.Count == 1 && played[0].Rank == 7 && played[0].Suit == CardSuit.Diamond,
            "burst plays exact shown card, not random replacement");
        Check(poker.Hand.Count == 0 && poker.Shield == 7, "played hand empties and suit effect applies");
        poker.Burst();
        Check(played.Count == 1, "empty burst does not secretly draw");
        poker.Fire(ctx.Player.State.Position, null);
        Check(poker.Hand.Count == 1, "next normal draw replenishes hand");
    }

    private static void PokerBigJokerKeepsExtraPlay()
    {
        ITurnContext ctx = World();
        var poker = new PokerWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        poker.DebugAddCard(new PlayingCard { Rank = PlayingCard.BigJoker, Suit = CardSuit.None });
        var played = new List<PlayingCard>();
        ctx.Events.Subscribe<CardPlayedEvent>(evt => played.Add(evt.Card));
        poker.Burst();
        Check(played.Count >= 2 && played[0].Rank == PlayingCard.BigJoker, "big joker still draws and immediately plays extra card");
        Check(poker.JokerBonus >= ctx.Config.Arsenal.Poker.BigJokerBonus && poker.Hand.Count == 0,
            "joker bonus preserved without accumulating hidden hands");
    }

    private static void PokerRollIntegration()
    {
        ITurnContext ctx = World();
        int draws = 0;
        ctx.Events.Subscribe<CardDrawnEvent>(_ => draws++);
        ctx.Weapons.Tick(0f);
        for (int i = 0; i < 15; i++) ctx.Weapons.Tick(ctx.Config.Arsenal.Poker.DrawInterval + .01f);
        Check(draws == 16, "weapon system continues drawing at original interval after hand fills");
        Check(ctx.Weapons.Hand.Count == 1 && ctx.Weapons.Capacity == 1, "weapon system keeps rolling despite one-card cap");
        ctx.Weapons.DebugAddCard(new PlayingCard { Rank = 6, Suit = CardSuit.Diamond });
        var played = new List<PlayingCard>();
        ctx.Events.Subscribe<CardPlayedEvent>(evt => played.Add(evt.Card));
        Check(ctx.Roll.TryRoll(), "space roll enters subspace");
        Check(ctx.Player.State.InSubspace, "existing subspace action remains");
        Check(played.Count == 1 && played[0].Rank == 6 && played[0].Suit == CardSuit.Diamond,
            "roll burst plays current displayed card");
    }

    private static void HourglassLinearFloorResetAndBuilds()
    {
        ITurnContext ctx = World(WeaponKind.Poker, SubWeaponKind.Hourglass);
        var hourglass = new HourglassSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        ctx.Config.Arsenal.Hourglass.SecondsToFloor = 5f;
        Check(Near(hourglass.MonsterSpeedMultiplier, 1f), "hourglass starts full speed");
        hourglass.Tick(1f);
        Check(Near(hourglass.MonsterSpeedMultiplier, .9f), "one second gives 90 percent");
        hourglass.Tick(1.5f);
        Check(Near(hourglass.MonsterSpeedMultiplier, .75f), "midpoint is linear 75 percent");
        hourglass.Tick(2.5f);
        Check(Near(hourglass.MonsterSpeedMultiplier, .5f), "five seconds reaches floor exactly");
        hourglass.Tick(20f);
        Check(Near(hourglass.MonsterSpeedMultiplier, .5f), "floor stays fixed");
        hourglass.BeforeBurst(ctx.Player.State.Position);
        Check(Near(hourglass.MonsterSpeedMultiplier, 1f), "roll resets to full speed");
        ctx.Progression.DebugSetLevel(BuildId.HourglassFloor, 1);
        ctx.Progression.DebugSetLevel(BuildId.HourglassDecay, 1);
        float floor = MathF.Max(.05f, .5f - ctx.Progression.Value(BuildId.HourglassFloor));
        float rate = 1f + ctx.Progression.Value(BuildId.HourglassDecay);
        hourglass.Tick(5f / rate);
        Check(Near(hourglass.MonsterSpeedMultiplier, floor), "floor and decay builds still apply");
    }

    private static void CursorRectangleIgnoresWalkedPolygon()
    {
        ITurnContext ctx = World(WeaponKind.Crossbow, SubWeaponKind.Cursor);
        var cursor = new CursorSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 anchor = ctx.Player.State.Position;
        cursor.BeforeBurst(anchor);
        Check(cursor.Trail.Count == 4, "rectangle emits four corners from first roll");
        // A deliberately non-rectangular journey must not become the damage polygon.
        ctx.Player.Teleport(anchor + new Vec2(-5f, -4f)); cursor.Tick(.01f);
        ctx.Player.Teleport(anchor + new Vec2(4f, 3f)); cursor.Tick(.01f);
        MonsterEntity inside = ctx.Monsters.SpawnKind(anchor + new Vec2(3.5f, .5f), MonsterKind.Walker, 0);
        MonsterEntity outside = ctx.Monsters.SpawnKind(anchor + new Vec2(-3f, -3f), MonsterKind.Walker, 0);
        float insideHp = inside.Hp, outsideHp = outside.Hp;
        CursorStrikeEvent strike = default;
        ctx.Events.Subscribe<CursorStrikeEvent>(evt => strike = evt);
        cursor.BeforeBurst(anchor + new Vec2(4f, 3f));
        Check(inside.Hp < insideHp && outside.Hp == outsideHp, "rectangle includes its interior but excludes old path area");
        Check(strike.Outline.Length == 4 && Near(strike.Outline[1].X, anchor.X + 4f) && Near(strike.Outline[1].Y, anchor.Y),
            "strike outline is axis-aligned rectangle");
        Check(inside.PinnedLeft > 0f && inside.StunLeft > 0f, "cursor pin effects preserved");
        Check(cursor.Trail.Count == 4 && Near(cursor.Trail[0].X, anchor.X + 4f), "second roll anchors next rectangle");
    }

    private static void CursorThicknessAndDiagonalLimit()
    {
        ITurnContext ctx = World(WeaponKind.Crossbow, SubWeaponKind.Cursor);
        ctx.Config.Arsenal.Cursor.MaxLength = 5f;
        var cursor = new CursorSubWeapon(new WeaponKit(ctx, ctx.Config.Arsenal));
        Vec2 a = ctx.Player.State.Position;
        cursor.BeforeBurst(a);
        ctx.Player.Teleport(a + new Vec2(8f, 6f)); cursor.Tick(.01f);
        Check(Near(Vec2.Distance(cursor.Trail[0], cursor.Trail[2]), 5f), "MaxLength limits rectangle diagonal without moving anchor");
        Check(Near(cursor.Trail[2].X, a.X + 4f) && Near(cursor.Trail[2].Y, a.Y + 3f), "over-limit endpoint clips along intended diagonal");
        MonsterEntity edge = ctx.Monsters.SpawnKind(a + new Vec2(4.3f, 1f), MonsterKind.Walker, 0);
        float hp = edge.Hp;
        cursor.BeforeBurst(a + new Vec2(8f, 6f));
        Check(edge.Hp < hp, "boundary thickness includes nearby enemy");
        Vec2 nextAnchor = a + new Vec2(8f, 6f);
        ctx.Player.Teleport(nextAnchor + new Vec2(-3f, -4f)); cursor.Tick(.01f);
        Check(Near(cursor.Trail[0].X, nextAnchor.X) && Near(cursor.Trail[2].X, nextAnchor.X - 3f) &&
            Near(cursor.Trail[2].Y, nextAnchor.Y - 4f), "rectangle supports negative-axis dragging with fixed anchor");
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < .001f;
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }
}
