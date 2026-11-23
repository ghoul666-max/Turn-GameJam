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
