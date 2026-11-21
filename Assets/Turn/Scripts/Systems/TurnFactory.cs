using System;
using Turn.Core;

namespace Turn.Systems
{
    /// <summary>ITurnContext 的实现，仅作为各系统引用的容器。（partial：与 ITurnContext 一样可在别的文件追加成员）</summary>
    public sealed partial class TurnContext : ITurnContext
    {
        public IEventBus Events { get; internal set; }
        public Random Random { get; internal set; }
        public TurnConfig Config { get; internal set; }
        public ISessionSystem Session { get; internal set; }
        public IBoundsSystem Bounds { get; internal set; }
        public IPlaneSystem Plane { get; internal set; }
        public ITerrainSystem Terrain { get; internal set; }
        public IObstacleSystem Obstacles { get; internal set; }
        public IPlayerSystem Player { get; internal set; }
        public IRollSystem Roll { get; internal set; }
        public ICombatSystem Combat { get; internal set; }
        public IMonsterSystem Monsters { get; internal set; }
        public ISharkSystem Sharks { get; internal set; }
        public IEnvironmentSystem Environment { get; internal set; }
        public ISpawnSystem Spawner { get; internal set; }
        public IWeaponSystem Weapons { get; internal set; }
        public IProjectileSystem Projectiles { get; internal set; }
        public IExperienceSystem Experience { get; internal set; }
        public IProgressionSystem Progression { get; internal set; }
    }

    /// <summary>唯一的组装点：要替换某个系统的实现，只改这里。</summary>
    public static class TurnFactory
    {
        public static ITurnContext Create(TurnConfig config, int seed)
        {
            var ctx = new TurnContext();
            ctx.Events = new EventBus();
            ctx.Random = new Random(seed);
            ctx.Config = config;

            ctx.Bounds = new BoundsSystem(ctx, config.Bounds);
            ctx.Plane = new PlaneSystem(ctx, config.Environment.StartPlane);
            ctx.Terrain = new TerrainSystem(ctx, config.Terrain);
            ctx.Obstacles = new ObstacleSystem(ctx);
            ctx.Combat = new CombatSystem(ctx);
            ctx.Player = new PlayerSystem(ctx, config.Player, config.Environment);
            ctx.Progression = new ProgressionSystem(ctx, config.Upgrades, config.Experience);
            ctx.Roll = new RollSystem(ctx, config.Roll, config.Upgrades);
            ctx.Monsters = new MonsterSystem(ctx, config.Monster, config.Environment);
            ctx.Sharks = new SharkSystem(ctx, config.Shark);
            ctx.Environment = new EnvironmentSystem(ctx, config.Environment, config.Monster);
            ctx.Spawner = new SpawnSystem(ctx, config.Spawn, config.Penalty);
            ctx.Weapons = new WeaponSystem(ctx, config.Weapon, config.Forms, config.Upgrades);
            ctx.Projectiles = new ProjectileSystem(ctx, config.Upgrades);
            ctx.Experience = new ExperienceSystem(ctx, config.Experience);
            ctx.Session = new SessionSystem(ctx);

            ctx.Player.Setup();
            return ctx;
        }
    }
}
