using System;
using Turn.Core;
using Turn.Systems;
using Turn.Weapons;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        var ctx = World();
        var kit = new WeaponKit(ctx, ctx.Config.Arsenal);
        var bow = new CrossbowWeapon(kit);
        Check(Near(bow.SizeMultiplier, 1), "fresh run base size");
        for (int i = 0; i < 4; i++) ctx.Events.Publish(new WeaponKillConfirmedEvent { Weapon = WeaponKind.WaterGun, Source = DamageSource.Weapon });
        Check(Near(bow.SizeMultiplier, 1), "four kills no size step");
        ctx.Events.Publish(new WeaponKillConfirmedEvent { Weapon = WeaponKind.Palm, Source = DamageSource.SubWeapon });
        Check(Near(bow.SizeMultiplier, 1.01f), "five all-source kills additive 1 percent");
        bow.OnUnequipped(false);
        Check(Near(bow.SizeMultiplier, 1.01f), "switch does not reset size");
        for (int i = 5; i < 2000; i++) ctx.Events.Publish(new WeaponKillConfirmedEvent());
        Check(Near(bow.SizeMultiplier, 5), "500 percent cap");
        ctx.Events.Publish(new WeaponKillConfirmedEvent());
        Check(Near(bow.SizeMultiplier, 5), "cap stable");
        var fresh = new CrossbowWeapon(new WeaponKit(World(), ctx.Config.Arsenal));
        Check(Near(fresh.SizeMultiplier, 1), "new run growth resets");
        var target = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(3, 0), MonsterKind.Walker, 0);
        bow.Fire(ctx.Player.State.Position, target);
        var p = ctx.Projectiles.Active[ctx.Projectiles.Active.Count - 1];
        Check(Near(p.Radius, ctx.Config.Arsenal.Crossbow.Radius * 5), "collision radius grows");
        Check(Near(p.DistanceLeft, ctx.Config.Arsenal.Blade.Range * ctx.Config.Arsenal.Crossbow.SwordWaveRangeMultiplier), "range derived from sword wave");
        Check(Near(p.Velocity.Magnitude, ctx.Config.Arsenal.Crossbow.Speed), "configured speed used");
        for (int i = 0; i < 10; i++)
        {
            var monster = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(4+i, 3), MonsterKind.Walker, 0);
            Check(bow.OnHit(p, monster), "continues beyond old pierce cap");
        }
        kit.AddRefund(WeaponKind.WaterGun, 1);
        Check(kit.TakeRefund(WeaponKind.Sword) == 0, "late water refund cannot credit sword");
        Check(kit.TakeRefund(WeaponKind.WaterGun) == 1 && kit.TakeRefund(WeaponKind.WaterGun) == 0, "refund credited once to source");
        var defs = BuildCatalog.CreateDefaults();
        defs[(int)BuildId.SwordBurstRing] = new BuildDef { MaxLevel = 3, Value = .08f };
        var normalized = BuildCatalog.Normalize(defs);
        Check(normalized[(int)BuildId.SwordBurstRing].MaxLevel == 0, "old config cannot re-enable free roll damage");
        Check(normalized[(int)BuildId.SwordKillRefund].MaxLevel > 0, "intentional sword refund build preserved");
        var marked = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(4, 5), MonsterKind.Walker, 0);
        marked.SpearMarks = 2;
        ctx.Events.Publish(new SubspaceExitedEvent());
        Check(marked.SpearMarks == 2, "new spear marks survive exit");
        SingleHitAndKnockback();
        LateRefundCannotRefillSword();
        Console.WriteLine($"Crossbow/Sword checks passed ({checks} assertions).");
    }
    private static void SingleHitAndKnockback()
    {
        var ctx = World();
        ctx.Loadout.Equip(WeaponKind.Crossbow);
        var a = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(2, 0), MonsterKind.Walker, 0);
        var b = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(4, 0), MonsterKind.Walker, 0);
        var p = ctx.Projectiles.SpawnPlayer(WeaponKind.Crossbow, ctx.Player.State.Position, Vec2.Right * 12, .6f, 10, 20);
        float ax = a.Position.X;
        for (int i = 0; i < 60; i++) ctx.Projectiles.Tick(1f/60);
        Check(p.HitIds.Contains(a.Id) && p.HitIds.Contains(b.Id), "single projectile penetrates both monsters");
        Check(p.HitCount == 2 && p.HitIds.Count == 2, "knockback overlap never re-hits same monster");
        Check(a.Position.X > ax, "surviving target knocked back");
    }
    private static void LateRefundCannotRefillSword()
    {
        var c = new TurnConfig();
        c.Horde.BaseCount = 0; c.Horde.CountPerWave = 0;
        c.Horde.EliteFromWave = 0; c.Horde.BossEveryWaves = 0;
        c.Builds.Builds[(int)BuildId.WaterKillRefund].Value = 1f;
        var ctx = TurnFactory.Create(c, 900); ctx.Session.Start();
        ctx.Loadout.Equip(WeaponKind.WaterGun);
        for (int attempt = 0; ctx.Progression.GetLevel(BuildId.WaterKillRefund) == 0 && attempt < 1000; attempt++)
        {
            ctx.Progression.AddXp(ctx.Progression.XpToNext); ctx.Progression.RollOptions();
            for (int i = 0; i < ctx.Progression.Options.Count; i++)
                if (ctx.Progression.Options[i].Build == BuildId.WaterKillRefund) { ctx.Progression.TryApply(i); break; }
            while (ctx.Progression.PendingLevelUps > 0) ctx.Progression.SkipPending();
        }
        Check(ctx.Progression.GetLevel(BuildId.WaterKillRefund) > 0, "acquired guaranteed refund build");
        var waterTarget = ctx.Monsters.SpawnKind(ctx.Player.State.Position + new Vec2(2,0), MonsterKind.Walker, 0);
        ctx.Weapons.Tick(.01f);
        int waterAmmo = ctx.Weapons.AmmoOf(WeaponKind.WaterGun);
        ctx.Loadout.Equip(WeaponKind.Sword);
        for (int i = 0; i < 2000 && ctx.Weapons.Ammo > 0; i++) ctx.Weapons.Tick(.02f);
        ctx.Weapons.Tick(1f);
        Check(ctx.Weapons.Ammo == 0, "sword depleted without time regen");
        var p = ctx.Projectiles.SpawnPlayer(WeaponKind.WaterGun, waterTarget.Position, Vec2.Right, .1f, 100000, 1);
        ctx.Weapons.OnProjectileHit(p, waterTarget);
        ctx.Weapons.Tick(.01f);
        Check(ctx.Weapons.Ammo == 0, "late water projectile kill does not refill depleted sword");
        Check(ctx.Weapons.AmmoOf(WeaponKind.WaterGun) == waterAmmo + 1, "normal water refund still works on source magazine");
        ctx.Weapons.Reload();
        Check(ctx.Weapons.Ammo == ctx.Weapons.Capacity, "normal explicit reload preserved");
    }

    private static ITurnContext World()
    {
        var c = new TurnConfig();
        c.Horde.BaseCount = 0; c.Horde.CountPerWave = 0; c.Horde.EliteFromWave = 0; c.Horde.BossEveryWaves = 0;
        foreach (var k in c.Horde.Kinds) k.MaxHp = 10000;
        var ctx = TurnFactory.Create(c, 777); ctx.Session.Start(); return ctx;
    }
    private static bool Near(float a, float b) => MathF.Abs(a-b) < .001f;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
