using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Systems
{
    /// <summary>ISurvivorContext 的实现，仅作为各系统引用的容器。</summary>
    public sealed class SurvivorContext : ISurvivorContext
    {
        public IEventBus Events { get; internal set; }
        public System.Random Random { get; internal set; }
        public StageData Stage { get; internal set; }
        public ISessionSystem Session { get; internal set; }
        public IPlayerSystem Player { get; internal set; }
        public ICombatSystem Combat { get; internal set; }
        public IEnemySystem Enemies { get; internal set; }
        public ISpawnSystem Spawner { get; internal set; }
        public IWeaponSystem Weapons { get; internal set; }
        public IProjectileSystem Projectiles { get; internal set; }
        public IPickupSystem Pickups { get; internal set; }
        public IProgressionSystem Progression { get; internal set; }
    }

    /// <summary>唯一的组装点：要替换某个系统的实现，只改这里。</summary>
    public static class SurvivorFactory
    {
        public static ISurvivorContext Create(CharacterData character, IEnumerable<WeaponData> weaponPool,
            IEnumerable<PassiveData> passivePool, StageData stage, int seed)
        {
            var ctx = new SurvivorContext();
            ctx.Events = new EventBus();
            ctx.Random = new System.Random(seed);
            ctx.Stage = stage;

            ctx.Combat = new CombatSystem(ctx);
            ctx.Player = new PlayerSystem(ctx);
            ctx.Enemies = new EnemySystem(ctx, stage.MaxEnemies);
            ctx.Spawner = new SpawnSystem(ctx, stage);
            ctx.Weapons = new WeaponSystem(ctx);
            ctx.Projectiles = new ProjectileSystem(ctx);
            ctx.Pickups = new PickupSystem(ctx);
            ctx.Progression = new ProgressionSystem(ctx, weaponPool, passivePool);
            ctx.Session = new SessionSystem(ctx, stage.Duration);

            ctx.Player.Setup(character);
            if (character.StartingWeapon != null) ctx.Weapons.TryAdd(character.StartingWeapon);
            return ctx;
        }
    }
}
