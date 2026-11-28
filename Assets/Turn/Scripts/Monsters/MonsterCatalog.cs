using Turn.Core;

namespace Turn.Monsters
{
    /// <summary>设计表 M101–M306 的稳定默认值与跨位面对应表；不引用 UnityEngine。</summary>
    public static class MonsterCatalog
    {
        public static MonsterArchetype[] CreateDefaults()
        {
            var result = new MonsterArchetype[13];
            result[(int)MonsterKind.StoneGolem] = Stats(60, 2.2f, 15, 1.5f, .6f, .9f, 6);
            result[(int)MonsterKind.Anglerfish] = Stats(45, 2.4f, 8, 1.8f, .5f, 8, 6);
            result[(int)MonsterKind.NullPointer] = Stats(70, 1.8f, 10, 3, .4f, .8f, 7);
            result[(int)MonsterKind.Bug] = Stats(50, 1.5f, 7, 1.2f, .5f, 8, 5);
            result[(int)MonsterKind.Buglet] = Stats(20, 3.6f, 6, .8f, .2f, .8f, 2);
            result[(int)MonsterKind.Alarm] = Stats(80, 0, 5, .2f, 0, 2, 7);
            result[(int)MonsterKind.BlackPot] = Stats(90, 2, 18, 3, 1, 8, 7);
            result[(int)MonsterKind.RockThrower] = Stats(55, 2, 14, 2.5f, .7f, 8, 6);
            result[(int)MonsterKind.VineDemon] = Stats(65, 0, 12, 1.2f, .4f, 2, 6);
            result[(int)MonsterKind.Boar] = Stats(110, 2, 18, 4, 1.2f, 8, 7);
            result[(int)MonsterKind.FutureSoldier] = Stats(48, 3, 6, 3, .4f, 10, 6);
            result[(int)MonsterKind.RepairRobot] = Stats(75, 2, 16, 4, .6f, 5, 7);
            result[(int)MonsterKind.DataWorm] = Stats(35, 4.5f, 20, 3, .3f, 1, 5);
            return result;
        }

        /// <summary>
        /// 新策划案五种怪物的基础数值（= 首次出现那一波的数值），按 Walker / Spore / Charger / Rotter / Sniper 顺序。
        /// 像素换算：1 单位 = 40 像素。冲锋触发距离 / 狙击射程策划书没给，暂定 6 / 9。
        /// </summary>
        public static HordeKindConfig[] CreateHordeDefaults()
        {
            return new[]
            {
                //        生命  移速   伤害  间隔  蓄力  距离  经验 首波 权重  体型
                Horde(30f, 2f,    5f,  .5f, 0f,   0f,  5,  1,  5f),          // 游荡尸：80 px/s，碰撞 5，1 次 / 0.5 秒
                Horde(50f, 1f,    3f,  3f,  0f,   0f,  8,  3,  2f,   1.25f), // 弹幕孢：40 px/s，弹幕 3 / 发，每 3 秒朝玩家发一枚；体型 ×1.25（11/28）
                Horde(80f, 1.5f,  15f, 4f,  .8f,  6f,  12, 5,  1.5f, 1.5f),  // 疾冲兽：60 px/s，冲锋 15，蓄力 0.8，冷却 4；体型 ×1.5（11/28）
                Horde(60f, 1.75f, 4f,  .5f, 0f,   0f,  10, 7,  1.5f), // 毒腐体：70 px/s，碰撞 4
                Horde(70f, 1.25f, 20f, 5f,  1.5f, 9f,  15, 10, 1f),   // 狙击械：50 px/s，激光 20，蓄力 1.5，间隔 5
            };
        }

        private static HordeKindConfig Horde(float hp, float speed, float damage, float interval, float windup, float range, int xp, int debut, float weight, float scale = 1f)
        {
            return new HordeKindConfig
            {
                MaxHp = hp, Speed = speed, Damage = damage, AttackInterval = interval, Windup = windup,
                Range = range, Xp = xp, DebutWave = debut, Weight = weight, Scale = scale,
            };
        }

        /// <summary>按 MonsterKinds.HordeCount 补齐（Inspector 数组缺项 / 生命 ≤ 0 时用默认值）。</summary>
        public static HordeKindConfig[] NormalizeHorde(HordeKindConfig[] configured)
        {
            HordeKindConfig[] defaults = CreateHordeDefaults();
            if (configured == null) return defaults;
            var result = new HordeKindConfig[defaults.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = i < configured.Length && configured[i] != null && configured[i].MaxHp > 0f ? configured[i] : defaults[i];
            return result;
        }

        private static MonsterArchetype Stats(float hp, float speed, float damage, float interval, float windup, float range, int xp)
        {
            return new MonsterArchetype { MaxHp = hp, Speed = speed, Damage = damage, AttackInterval = interval, Windup = windup, Range = range, Xp = xp };
        }

        public static MonsterKind ForPlane(MonsterKind kind, PlaneId to)
        {
            if (kind == MonsterKind.Buglet) return kind;
            if (to == PlaneId.Volcano) return MonsterKind.StoneGolem;
            if (to == PlaneId.Ocean) return MonsterKind.Anglerfish;
            if (to == PlaneId.Office) return kind == MonsterKind.BlackPot || kind == MonsterKind.Bug ? MonsterKind.Bug : MonsterKind.NullPointer;
            if (to == PlaneId.Dream) return kind == MonsterKind.BlackPot || kind == MonsterKind.Bug ? MonsterKind.BlackPot : MonsterKind.Alarm;
            if (to == PlaneId.Primal)
            {
                if (kind == MonsterKind.RepairRobot || kind == MonsterKind.VineDemon) return MonsterKind.VineDemon;
                if (kind == MonsterKind.DataWorm || kind == MonsterKind.Boar) return MonsterKind.Boar;
                return MonsterKind.RockThrower;
            }
            if (kind == MonsterKind.VineDemon || kind == MonsterKind.RepairRobot) return MonsterKind.RepairRobot;
            if (kind == MonsterKind.Boar || kind == MonsterKind.DataWorm) return MonsterKind.DataWorm;
            return MonsterKind.FutureSoldier;
        }
    }
}
