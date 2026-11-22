using System;
using Turn.Core;
using Turn.Systems;

// Standalone pure-C# regression checks: dotnet run --project Tests/Combat/TurnCombatChecks.csproj
internal static class Program
{
    private static void Main()
    {
        MonsterShiftAndRatio();
        MonsterBoundaryClamp();
        ProjectileLifetimeAndPlane();
        ProjectileRangeAndExpiry();
        Console.WriteLine("Combat cross-plane checks passed.");
    }

    private static ITurnContext NewWorld()
    {
        var config = new TurnConfig();
        config.Monster.VolcanoMaxHp = 60f;
        config.Monster.OceanMaxHp = 45f;
        config.Weapon.ProjectileLifetime = 3f;
        return TurnFactory.Create(config, 42);
    }

    private static void MonsterShiftAndRatio()
    {
        ITurnContext ctx = NewWorld();
        MonsterEntity m = ctx.Monsters.Spawn(new Vec2(0, 0), false);
        Check(m.MaxHp == 60f && m.Form == PlaneId.Volcano, "volcano spawn form");
        ctx.Combat.DamageMonster(m, 30f, DamageSource.Weapon);
        ctx.Monsters.ApplyBurn(m, 4f, 3f);
        ctx.Monsters.ShiftAll(new Vec2(0.8f, 0));
        Check(Near(m.Position.X, 0.8f), "monster shift");
        ctx.Monsters.SwapPlane(PlaneId.Volcano, PlaneId.Ocean);
        Check(m.Form == PlaneId.Ocean && Near(m.MaxHp, 45f) && Near(m.Hp, 22.5f), "ocean HP ratio");
        Check(m.Burning && Near(m.BurnLeft, 3f), "buff retained");
        int id = m.Id;
        ctx.Monsters.SwapPlane(PlaneId.Ocean, PlaneId.Volcano);
        Check(m.Id == id && Near(m.MaxHp, 60f) && Near(m.Hp, 30f), "identity and round trip ratio");
    }

    private static void MonsterBoundaryClamp()
    {
        ITurnContext ctx = NewWorld();
        MonsterEntity edge = ctx.Monsters.Spawn(new Vec2(19.5f, 0), false);
        ctx.Monsters.ShiftAll(new Vec2(2f, 0));
        Check(Near(edge.Position.X, 20f - ctx.Config.Monster.Radius), "shift obeys unified bounds");
    }

    private static void ProjectileLifetimeAndPlane()
    {
        ITurnContext ctx = NewWorld();
        MonsterEntity m = ctx.Monsters.Spawn(new Vec2(5f, 0), false);
        ProjectileEntity p = ctx.Projectiles.Spawn(PlaneId.Volcano, Vec2.Zero, new Vec2(1f, 0), 0.2f, 10f, 1, 10f);
        ctx.Projectiles.Tick(0.5f);
        float distance = p.DistanceLeft;
        ctx.Plane.Switch();
        ctx.Monsters.SwapPlane(PlaneId.Volcano, PlaneId.Ocean);
        ctx.Projectiles.OnPlaneSwitched(PlaneId.Volcano, PlaneId.Ocean);
        Check(p.LifetimeExtended && Near(p.LifetimeLeft, 2.5f * 1.6f), "extend remaining time once");
        Check(Near(p.DistanceLeft, distance), "range unchanged by plane switch");
        ctx.Projectiles.Tick(0.5f);
        Check(Near(p.Position.X, 1f) && Near(p.LifetimeLeft, 3.5f), "invisible projectile still simulates");
        ctx.Plane.Switch();
        ctx.Monsters.SwapPlane(PlaneId.Ocean, PlaneId.Volcano);
        ctx.Projectiles.OnPlaneSwitched(PlaneId.Ocean, PlaneId.Volcano);
        Check(Near(p.LifetimeLeft, 3.5f), "no repeat extension");
        ctx.Projectiles.Tick(0.5f);
        Check(p.Alive && m.Hp == m.MaxHp, "no premature hit");

        // A projectile of the inactive plane cannot hit the active-plane monster even at overlap.
        ProjectileEntity old = ctx.Projectiles.Spawn(PlaneId.Ocean, m.Position, Vec2.Zero, 0.2f, 10f, 1, 5f);
        ctx.Projectiles.Tick(0.01f);
        Check(old.Alive && Near(m.Hp, m.MaxHp), "cross-plane collision blocked");
    }

    private static void ProjectileRangeAndExpiry()
    {
        ITurnContext ctx = NewWorld();
        MonsterEntity m = ctx.Monsters.Spawn(new Vec2(1f, 0), false);
        ProjectileEntity p = ctx.Projectiles.Spawn(PlaneId.Volcano, Vec2.Zero, new Vec2(1f, 0), 0.2f, 10f, 1, 0.5f);
        ctx.Plane.Switch();
        ctx.Monsters.SwapPlane(PlaneId.Volcano, PlaneId.Ocean);
        ctx.Projectiles.OnPlaneSwitched(PlaneId.Volcano, PlaneId.Ocean);
        ctx.Projectiles.Tick(0.5f);
        Check(!p.Alive && Near(m.Hp, m.MaxHp), "distance cap still expires off-plane");
        ProjectileEntity timeout = ctx.Projectiles.Spawn(PlaneId.Ocean, Vec2.Zero, Vec2.Zero, 0.2f, 10f, 1, 10f);
        ctx.Projectiles.Tick(3.01f);
        Check(!timeout.Alive, "time cap expires independently of distance");
        ProjectileEntity active = ctx.Projectiles.Spawn(PlaneId.Ocean, m.Position, Vec2.Zero, 0.2f, 10f, 1, 10f);
        ctx.Projectiles.Tick(0.01f);
        Check(m.Hp < m.MaxHp, "same-plane projectile hits");
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("FAIL: " + message);
    }
}
