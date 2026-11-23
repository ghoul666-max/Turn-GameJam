using System;
using Turn.Core;

namespace Turn.Monsters
{
    /// <summary>从当前位面的类型池抽样，P2 只在约定时间后加入；默认普通类型等权。</summary>
    public static class MonsterSpawnPool
    {
        public static MonsterKind Pick(PlaneId plane, SpawnConfig config, Random random, float elapsed)
        {
            MonsterKind[] kinds;
            switch (plane)
            {
                case PlaneId.Volcano: return MonsterKind.StoneGolem;
                case PlaneId.Ocean: return MonsterKind.Anglerfish;
                case PlaneId.Office: kinds = new[] { MonsterKind.NullPointer, MonsterKind.Bug }; break;
                case PlaneId.Dream: kinds = new[] { MonsterKind.Alarm, MonsterKind.BlackPot }; break;
                case PlaneId.Primal: kinds = elapsed >= config.P2StartSeconds
                    ? new[] { MonsterKind.RockThrower, MonsterKind.VineDemon, MonsterKind.Boar }
                    : new[] { MonsterKind.RockThrower, MonsterKind.VineDemon }; break;
                default: kinds = elapsed >= config.P2StartSeconds
                    ? new[] { MonsterKind.FutureSoldier, MonsterKind.RepairRobot, MonsterKind.DataWorm }
                    : new[] { MonsterKind.FutureSoldier, MonsterKind.RepairRobot }; break;
            }
            float sum = 0f;
            for (int i = 0; i < kinds.Length; i++) sum += Weight(config, kinds[i]);
            if (sum <= 0f) return kinds[0]; // 当前 Spawn 接口必须返回类型；全部禁用时使用保底原型。
            float roll = (float)random.NextDouble() * sum;
            for (int i = 0; i < kinds.Length; i++)
            {
                roll -= Weight(config, kinds[i]);
                if (roll < 0f) return kinds[i];
            }
            return kinds[kinds.Length - 1];
        }

        private static float Weight(SpawnConfig config, MonsterKind kind)
        {
            int i = (int)kind;
            float[] weights = config.KindWeights;
            return weights != null && i < weights.Length ? (weights[i] < 0f ? 0f : weights[i] == 0f ? 1f : weights[i]) : 1f;
        }
    }
}
