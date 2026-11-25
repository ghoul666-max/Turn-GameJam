using System;
using Turn.Core;
using Turn.Monsters;
using Turn.Systems;

/// <summary>程序B：五种怪物、波次成长 / 解锁、精英、边缘刷新的纯模拟回归测试（新策划案 11/24）。运行：dotnet run。</summary>
internal static class Program
{
    private const float Step = 1f / 60f;

    private static void Main()
    {
        WaveScalingFromDebut();
        DebutGatesSpawnPool();
        WalkerContactCap();
        SporeFiresRings();
        ChargerChargesAndHits();
        RotterLeavesPool();
        SniperLaser();
        EdgeSpawnAndElites();
        Console.WriteLine($"Monster checks passed ({_checks} assertions).");
    }

    private static void RunMonsters(ITurnContext ctx, float seconds)
    {
        int steps = (int)MathF.Round(seconds / Step);
        for (int i = 0; i < steps; i++)
        {
            ctx.Player.Tick(Step);
            ctx.Monsters.Tick(Step);
            ctx.Projectiles.Tick(Step);
        }
    }

    private static void WaveScalingFromDebut()
    {
        var ctx = World(c => c.Level.WaveSeconds = .5f);
        Check(Near(Spawn(ctx, new Vec2(8f, 0f)).MaxHp, 30f), "walker 30 hp at wave 1");
        for (int i = 0; i < 63; i++) ctx.Session.Tick(Step);
        Check(ctx.Session.Wave == 3, "wave 3");
        MonsterEntity walker = Spawn(ctx, new Vec2(8f, 0f));
        Check(Near(walker.MaxHp, 36f) && Near(walker.DamageScale, 1.1f), "+10% hp / +5% damage per wave");
        Check(Near(Spawn(ctx, new Vec2(8f, 2f), MonsterKind.Spore).MaxHp, 50f), "spore 50 hp at its debut wave 3");
    }

    private static void DebutGatesSpawnPool()
    {
        var horde = new HordeConfig();
        var random = new Random(5);
        for (int i = 0; i < 200; i++) Check(MonsterSpawnPool.Pick(horde, 1, random) == MonsterKind.Walker, "wave 1: walkers only");
        bool sniper = false, charger = false;
        for (int i = 0; i < 2000; i++)
        {
            MonsterKind k = MonsterSpawnPool.Pick(horde, 10, random);
            sniper |= k == MonsterKind.Sniper;
            charger |= k == MonsterKind.Charger;
        }
        Check(sniper && charger, "wave 10: all five unlocked");
        for (int i = 0; i < 500; i++) Check(MonsterSpawnPool.Pick(horde, 6, random) != MonsterKind.Sniper, "sniper locked before wave 10");
    }

    private static void WalkerContactCap()
    {
        var ctx = World();
        Spawn(ctx, new Vec2(.2f, 0f));
        int hits = 0;
        ctx.Events.Subscribe<PlayerDamagedEvent>(e => hits++);
        RunMonsters(ctx, .45f);
        Check(hits == 1, "at most one contact hit per 0.5 s");
        RunMonsters(ctx, .7f);
        Check(hits == 3 && Near(ctx.Player.State.Hp, 85f), "5 damage per hit, every 0.5 s");
    }

    private static void SporeFiresRings()
    {
        var ctx = World();
        MonsterEntity spore = Spawn(ctx, new Vec2(6f, 0f), MonsterKind.Spore);
        spore.AiTimer = .5f; // 固定第一圈的时间（生成时是随机错开的）
        int lastId = 0, rings = 0;
        for (int i = 0; i < 60 * 5; i++)
        {
            RunMonsters(ctx, Step);
            int fresh = 0;
            var list = ctx.Projectiles.Active;
            int maxId = lastId;
            for (int k = 0; k < list.Count; k++)
            {
                if (list[k].Id <= lastId) continue;
                fresh++;
                if (list[k].Id > maxId) maxId = list[k].Id;
            }
            if (fresh > 0)
            {
                Check(fresh == 8, "each ring has 8 bullets");
                rings++;
            }
            lastId = maxId;
        }
        Check(rings == 2, "one ring every 3 s (rings at 0.5 s and 3.5 s)");
    }

    private static void ChargerChargesAndHits()
    {
        var ctx = World();
        MonsterEntity c = Spawn(ctx, new Vec2(4f, 0f), MonsterKind.Charger);
        bool windup = false;
        float hp = ctx.Player.State.Hp;
        for (int i = 0; i < 240 && Near(ctx.Player.State.Hp, hp); i++)
        {
            RunMonsters(ctx, Step);
            windup |= c.AiPhase == 1;
        }
        Check(windup, "charger winds up before charging");
        Check(Near(hp - ctx.Player.State.Hp, 15f), "charge hits for 15");
    }

    private static void RotterLeavesPool()
    {
        var ctx = World();
        MonsterEntity r = Spawn(ctx, new Vec2(.5f, 0f), MonsterKind.Rotter);
        ctx.Combat.DamageMonster(r, 1000f, DamageSource.Weapon);
        Check(ctx.Monsters.Pools.Count == 1, "rotter leaves a poison pool");
        Check(Near(ctx.Monsters.Pools[0].Radius, 1.5f), "pool radius 1.5 (60 px)");
        float hp = ctx.Player.State.Hp;
        RunMonsters(ctx, 1.1f);
        Check(Near(hp - ctx.Player.State.Hp, 4f), "standing in the pool: 2 per 0.5 s");
        RunMonsters(ctx, 4.5f);
        Check(ctx.Monsters.Pools.Count == 0, "pool lasts 5 s");
    }

    private static void SniperLaser()
    {
        var ctx = World();
        Spawn(ctx, new Vec2(8f, 0f), MonsterKind.Sniper);
        bool warned = false, fired = false, hit = false;
        ctx.Events.Subscribe<LaserAttackWarnedEvent>(e => warned = true);
        ctx.Events.Subscribe<LaserFiredEvent>(e => { fired = true; hit = e.HitPlayer; });
        for (int i = 0; i < 60 * 8 && !fired; i++) RunMonsters(ctx, Step);
        Check(warned && fired, "sniper warns then fires");
        Check(hit && Near(ctx.Player.State.MaxHp - ctx.Player.State.Hp, 20f), "laser hits a standing player for 20");
    }

    private static void EdgeSpawnAndElites()
    {
        var ctx = World(c => { c.Horde.EliteFromWave = 2; c.Level.WaveSeconds = .5f; });
        BoundsRect rect = ctx.Bounds.Rect;
        for (int i = 0; i < 50; i++)
        {
            Vec2 p = ctx.Spawner.EdgePoint();
            float edge = MathF.Min(MathF.Min(p.X - rect.Min.X, rect.Max.X - p.X), MathF.Min(p.Y - rect.Min.Y, rect.Max.Y - p.Y));
            Check(edge < 1f, "spawn points are on the map edge");
        }
        for (int i = 0; i < 40; i++) ctx.Session.Tick(Step);
        bool elite = false;
        for (int i = 0; i < ctx.Monsters.Active.Count; i++) elite |= ctx.Monsters.Active[i].Rank == 1;
        Check(ctx.Session.Wave == 2 && elite, "an elite spawns when wave 2 starts");
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
