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
        var ctx = World(c => { for (int i = 0; i < c.Horde.Kinds.Length; i++) c.Horde.Kinds[i].MaxHp = 100f; });
        ApplyBuild(ctx, BuildId.WaterBurstRing);
        MonsterEntity near = Spawn(ctx, new Vec2(2f, 0f));
        MonsterEntity far = Spawn(ctx, new Vec2(8f, 0f));
        ctx.Roll.TryRoll();
        Check(Near(near.Hp, 92f), "burst ring: 8% max hp around the player");
        Check(Near(far.Hp, 100f), "outside the ring untouched");
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
