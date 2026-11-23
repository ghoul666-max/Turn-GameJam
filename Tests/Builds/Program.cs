using System;
using System.Linq;
using Turn.Core;
using Turn.Systems;

internal static class Program
{
    private static int _checks;
    private static void Main()
    {
        BuildPoolAndLegacyMapping();
        DamageLifestealAndLevelGrowth();
        TwoPlaneWeaponBuffs();
        KillRefundExcludesSingleShotAndShark();
        HazardDamageAndCriticalSlow();
        SwitchAftershockAndLinger();
        MagnetAndRange();
        Console.WriteLine($"Build checks passed ({_checks} assertions).");
    }

    private static ITurnContext World(Action<TurnConfig> configure = null)
    {
        var config = new TurnConfig();
        config.Weapon.PlaneCapacities = new[] { 8, 8, 8, 8, 1, 1 };
        config.Spawn.InitialCount = 0;
        for (int i = 0; i < PlaneIds.Count; i++)
            config.Forms[i] = new WeaponFormConfig { Map = (PlaneId)i, ProjectileSpeed = 10f, Damage = 10f, Pierce = 1 };
        configure?.Invoke(config);
        return TurnFactory.Create(config, 222);
    }

    private static void Apply(ITurnContext ctx, BuildId id)
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            ctx.Progression.RollOptions();
            var list = ctx.Progression.Options;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Build != id) continue;
                Check(ctx.Progression.TryApply(i), $"apply {id}");
                return;
            }
        }
        throw new Exception($"Could not sample {id} from Build pool");
    }

    private static void BuildPoolAndLegacyMapping()
    {
        var ctx = World();
        ctx.Progression.AddXp(500);
        ctx.Progression.RollOptions();
        Check(ctx.Progression.Options.Count == 3, "three options");
        Check(ctx.Progression.Options.Select(x => x.Build).Distinct().Count() == 3, "unique options");
        Check(ctx.Progression.Options.All(x => BuildIds.IsValid(x.Build)), "nineteen only");
        Check(ctx.Progression.GetLevel(UpgradeId.Magazine) == 0 && ctx.Progression.GetLevel(UpgradeId.Burn) == 0, "no legacy upgrades");
        Apply(ctx, BuildId.B13);
        Check(ctx.Progression.GetLevel(UpgradeId.RollCooldown) == 1, "legacy roll cooldown maps B13");
        Apply(ctx, BuildId.B14);
        Check(ctx.Progression.GetLevel(UpgradeId.RollInvincible) == 1, "legacy invincibility maps B14");
        Apply(ctx, BuildId.B15);
        Check(ctx.Progression.GetLevel(UpgradeId.Shockwave) == 1, "legacy shockwave maps B15");
        Check(ctx.Progression.GetLevel(BuildIds.Query(BuildId.B15)) == 1, "B id query through interface");
    }

    private static void DamageLifestealAndLevelGrowth()
    {
        var ctx = World();
        Check(ctx.Progression.XpToNext == 4, "level 1 xp threshold");
        ctx.Progression.AddXp(4);
        Check(ctx.Progression.Level == 2 && ctx.Progression.XpToNext == 8, "level 2 xp threshold");
        Check(Near(ctx.Player.State.MaxHp, 110f) && Near(ctx.Player.MoveSpeed, 4.28f), "level 2 stats");
        ctx.Combat.DamagePlayer(30, DamageSource.Monster);
        ctx.Progression.AddXp(8);
        Check(Near(ctx.Player.State.Hp, 90f), "level up heals only max HP gain");
        Apply(ctx, BuildId.B01);
        Apply(ctx, BuildId.B07);
        var m = ctx.Monsters.Spawn(new Vec2(2, 0), false);
        float hp = m.Hp;
        float before = ctx.Player.State.Hp;
        ctx.Combat.DamageMonster(m, 10f, DamageSource.Weapon);
        Check(Near(hp - m.Hp, 12f), "B01 weapon damage");
        Check(Near(ctx.Player.State.Hp - before, .6f), "B07 lifesteal based on applied damage");
    }

    private static void TwoPlaneWeaponBuffs()
    {
        var ctx = World(c => { c.Weapon.FireInterval = .7f; c.Forms[0].Damage = 18; c.Forms[1].Damage = 24; });
        ctx.Progression.AddXp(200);
        Apply(ctx, BuildId.B02);
        Apply(ctx, BuildId.B03);
        Apply(ctx, BuildId.B04);
        ctx.Monsters.Spawn(new Vec2(2, 0), false);
        ctx.Weapons.Tick(1f);
        Check(ctx.Projectiles.Active.Count == 2, "B03 fires two volcano projectiles");
        Check(Near(ctx.Projectiles.Active[0].Velocity.Magnitude, 14f), "B04 fireball speed");
        Check(Near(ctx.Weapons.FireTimer, .616f), "B02 attack interval");
        Check(ctx.Roll.TryRoll(), "successful roll reloads the shared magazine");
        ctx.Weapons.Tick(1f);
        Check(ctx.Projectiles.Active.Count == 4, "B03 fires two ocean projectiles");
        Check(Near(ctx.Projectiles.Active[2].Velocity.Magnitude, 14f), "B04 harpoon speed");
    }

    private static void KillRefundExcludesSingleShotAndShark()
    {
        var ctx = World(c => { c.Weapon.InitialAmmo = 3; c.Weapon.PlaneCapacities = new[] { 8, 8, 8, 8, 1, 1 }; });
        ctx.Progression.AddXp(100);
        Apply(ctx, BuildId.B11);
        MonsterEntity m = ctx.Monsters.Spawn(new Vec2(2f, 0f), false);
        ctx.Combat.DamageMonster(m, 100f, DamageSource.Shark);
        int ammo = ctx.Weapons.Ammo;
        ctx.Weapons.Tick(.01f);
        Check(ctx.Weapons.Ammo == ammo, "shark kill never refunds ammo");

        ctx.Plane.EnterPair(PlanePairId.PrimalFuture);
        MonsterEntity bowTarget = ctx.Monsters.Spawn(new Vec2(2f, 0f), false);
        int before = ctx.Weapons.Ammo;
        ctx.Combat.DamageMonster(bowTarget, 1000f, DamageSource.Weapon);
        ctx.Weapons.Tick(.01f);
        Check(ctx.Weapons.Ammo <= before, "crossbow kill cannot refund ammo");

        ctx.Plane.EnterPair(PlanePairId.VolcanoOcean);
        MonsterEntity fireTarget = ctx.Monsters.Spawn(new Vec2(2f, 0f), false);
        ctx.Combat.DamageMonster(fireTarget, 1000f, DamageSource.Weapon);
        ctx.Weapons.Tick(.01f);
        Check(ctx.Weapons.Ammo > 0, "fireball kill eligible for refund");
    }

    private static void HazardDamageAndCriticalSlow()
    {
        var ctx = World(c =>
        {
            c.Upgrades.CriticalChancePerLevel = 1f;
            c.Upgrades.CriticalMultiplier = 2f;
            c.Terrain[(int)PlaneId.Volcano] = new TerrainGridData(40, 24, 1f,
                new Vec2(-20f, -12f), Enumerable.Repeat(TerrainType.Lava, 40 * 24).ToArray());
        });
        ctx.Progression.AddXp(100);
        Apply(ctx, BuildId.B06);
        Apply(ctx, BuildId.B08);
        Apply(ctx, BuildId.B10);
        ctx.Environment.OnPlaneEntered(PlaneId.Volcano);
        MonsterEntity monster = ctx.Monsters.Spawn(new Vec2(3f, 0f), false);
        float hp = monster.Hp;
        ctx.Projectiles.Spawn(PlaneId.Volcano, monster.Position, Vec2.Zero, .2f, 10f, 1, 3f);
        ctx.Projectiles.Tick(.01f);
        Check(Near(hp - monster.Hp, 20f), "B08 critical doubles projectile damage");
        Check(monster.SlowLeft > 0f && Near(monster.SlowFactor, .7f), "B06 slows hit monster");
    }

    private static void SwitchAftershockAndLinger()
    {
        var ctx = World(c => { c.Weapon.ProjectileLifetime = 2f; c.Weapon.PlaneSwitchLifetimeMultiplier = 1.6f; });
        ctx.Progression.AddXp(100);
        Apply(ctx, BuildId.B16);
        Apply(ctx, BuildId.B19);
        var p = ctx.Projectiles.Spawn(PlaneId.Volcano, Vec2.Zero, Vec2.Zero, .2f, 10f, 1, 10f);
        ctx.Plane.Switch();
        ctx.Projectiles.OnPlaneSwitched(PlaneId.Volcano, PlaneId.Ocean);
        Check(Near(p.LifetimeLeft, 2f * 1.6f * 1.6f), "B19 increases initial plane-switch lifetime once");
        Check(p.AftershockLeft > 0f, "B16 starts aftershock for existing bullet");
        ctx.Plane.Switch();
        ctx.Projectiles.OnPlaneSwitched(PlaneId.Ocean, PlaneId.Volcano);
        Check(Near(p.LifetimeLeft, 2f * 1.6f * 1.6f), "B19 never repeatedly extends one bullet");
        MonsterEntity monster = ctx.Monsters.Spawn(new Vec2(1f, 0f), false);
        float hp = monster.Hp;
        p.Position = monster.Position;
        ctx.Projectiles.Tick(.01f);
        Check(Near(hp - monster.Hp, 13f), "B16 increases existing bullet damage by 30 percent");
    }

    private static void MagnetAndRange()
    {
        var ctx = World(c =>
        {
            c.Weapon.PlaneTargetRanges[0] = 2f;
            c.Experience.MagnetRadius = 2f;
            c.Experience.MagnetSpeed = 0f;
        });
        ctx.Progression.AddXp(100);
        Apply(ctx, BuildId.B05);
        Apply(ctx, BuildId.B12);
        ctx.Monsters.Spawn(new Vec2(2.4f, 0f), false);
        ctx.Weapons.Tick(1f);
        Check(ctx.Projectiles.Active.Count > 0, "B05 extends target acquisition range");
        ctx.Experience.SpawnOrb(new Vec2(2.4f, 0f), 1);
        ctx.Experience.Tick(.01f);
        Check(ctx.Experience.Active.Count == 1 && ctx.Experience.Active[0].Magnetized,
            "B12 extends experience magnet range");
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < .001f;
    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new Exception("Build test failed: " + description);
    }
}
