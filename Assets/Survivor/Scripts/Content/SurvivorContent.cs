using Survivor.Core;

namespace Survivor.Content
{
    /// <summary>
    /// 敌人与关卡时间轴的数值定义（代码）。武器 / 被动 / 角色是 ScriptableObject，见 Data 层。
    /// 素材键要和 SurvivorArt 里的常量一致（这里不引用 Unity，所以直接写字符串）。
    /// </summary>
    public static class SurvivorContent
    {
        public static EnemyData Bat()
        {
            return new EnemyData
            {
                Id = "bat", Name = "蝙蝠", SpriteKey = "bat",
                MaxHp = 6f, Speed = 2.4f, Damage = 2f, Radius = 0.35f, Xp = 1, HealDropChance = 0.004f,
            };
        }

        public static EnemyData Zombie()
        {
            return new EnemyData
            {
                Id = "zombie", Name = "僵尸", SpriteKey = "zombie",
                MaxHp = 16f, Speed = 1.35f, Damage = 4f, Radius = 0.4f, Xp = 1, HealDropChance = 0.004f,
            };
        }

        public static EnemyData Skeleton()
        {
            return new EnemyData
            {
                Id = "skeleton", Name = "骷髅", SpriteKey = "skeleton",
                MaxHp = 28f, Speed = 1.75f, Damage = 6f, Radius = 0.4f, Xp = 2, HealDropChance = 0.004f,
            };
        }

        public static EnemyData Ghost()
        {
            return new EnemyData
            {
                Id = "ghost", Name = "幽灵", SpriteKey = "ghost",
                MaxHp = 14f, Speed = 2.8f, Damage = 4f, Radius = 0.38f, Xp = 2, KnockbackResist = 0.3f, HealDropChance = 0.004f,
            };
        }

        public static EnemyData Golem()
        {
            return new EnemyData
            {
                Id = "golem", Name = "石魔", SpriteKey = "golem",
                MaxHp = 420f, Speed = 1.25f, Damage = 12f, Radius = 0.9f, Scale = 2f, Xp = 30,
                KnockbackResist = 0.8f, HealDropChance = 1f, Elite = true,
            };
        }

        public static EnemyData Reaper()
        {
            return new EnemyData
            {
                Id = "reaper", Name = "死神领主", SpriteKey = "reaper",
                MaxHp = 4000f, Speed = 1.7f, Damage = 25f, Radius = 1.3f, Scale = 3f, Xp = 100,
                KnockbackResist = 1f, HealDropChance = 1f, Boss = true,
            };
        }

        /// <summary>5 分钟的墓园关：每分钟换一批怪、密度上升；定时出精英、包围圈，4:00 出首领，存活到 5:00 胜利。</summary>
        public static StageData CreateStage()
        {
            EnemyData bat = Bat();
            EnemyData zombie = Zombie();
            EnemyData skeleton = Skeleton();
            EnemyData ghost = Ghost();
            EnemyData golem = Golem();
            EnemyData reaper = Reaper();

            var stage = new StageData { Name = "月下墓园", Theme = 0, Duration = 300f, MaxEnemies = 350 };

            stage.Waves.Add(Wave(0f, 60f, 1.2f, 2, bat, bat, zombie));
            stage.Waves.Add(Wave(60f, 120f, 0.9f, 3, zombie, zombie, bat, skeleton));
            stage.Waves.Add(Wave(120f, 180f, 0.8f, 4, skeleton, zombie, ghost));
            stage.Waves.Add(Wave(180f, 240f, 0.7f, 5, ghost, skeleton, skeleton, bat));
            stage.Waves.Add(Wave(240f, 300f, 0.6f, 6, skeleton, ghost, zombie, bat));

            stage.Events.Add(Event(25f, bat, 20, SpawnPattern.Ring, "蝙蝠群！"));
            stage.Events.Add(Event(60f, golem, 1, SpawnPattern.Scatter, "精英出现！"));
            stage.Events.Add(Event(100f, zombie, 28, SpawnPattern.Ring, "尸潮包围！"));
            stage.Events.Add(Event(120f, golem, 2, SpawnPattern.Scatter, "精英出现！"));
            stage.Events.Add(Event(170f, ghost, 30, SpawnPattern.Ring, "幽灵环绕！"));
            stage.Events.Add(Event(180f, golem, 2, SpawnPattern.Scatter, "精英出现！"));
            stage.Events.Add(Event(240f, reaper, 1, SpawnPattern.Scatter, "首领：死神领主"));
            stage.Events.Add(Event(265f, skeleton, 36, SpawnPattern.Ring, "骸骨军团！"));
            return stage;
        }

        private static SpawnWave Wave(float start, float end, float interval, int count, params EnemyData[] pool)
        {
            var wave = new SpawnWave { Start = start, End = end, Interval = interval, Count = count };
            wave.Pool.AddRange(pool);
            return wave;
        }

        private static SpawnEventData Event(float time, EnemyData enemy, int count, SpawnPattern pattern, string announce)
        {
            return new SpawnEventData { Time = time, Enemy = enemy, Count = count, Pattern = pattern, Announce = announce };
        }
    }
}
