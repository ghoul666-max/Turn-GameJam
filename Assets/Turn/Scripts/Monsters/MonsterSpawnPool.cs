using System;
using Turn.Core;

namespace Turn.Monsters
{
    /// <summary>新策划案：从已解锁（DebutWave ≤ 当前波次）的五种怪物里按权重随机。</summary>
    public static class MonsterSpawnPool
    {
        public static MonsterKind Pick(HordeConfig horde, int wave, Random random)
        {
            HordeKindConfig[] kinds = horde.Kinds;
            float sum = 0f;
            for (int i = 0; i < MonsterKinds.HordeCount; i++)
            {
                HordeKindConfig k = i < kinds.Length ? kinds[i] : null;
                if (k != null && k.DebutWave <= wave) sum += MathF.Max(0f, k.Weight);
            }
            if (sum <= 0f) return MonsterKinds.First; // 全部禁用时保底刷游荡尸
            float roll = (float)random.NextDouble() * sum;
            for (int i = 0; i < MonsterKinds.HordeCount; i++)
            {
                HordeKindConfig k = i < kinds.Length ? kinds[i] : null;
                if (k == null || k.DebutWave > wave) continue;
                roll -= MathF.Max(0f, k.Weight);
                if (roll < 0f) return (MonsterKind)((int)MonsterKinds.First + i);
            }
            return MonsterKinds.First;
        }

        /// <summary>精英 / Boss 的种类：已解锁的里面等概率随机。</summary>
        public static MonsterKind PickUnlocked(HordeConfig horde, int wave, Random random)
        {
            int count = 0;
            for (int i = 0; i < MonsterKinds.HordeCount; i++)
                if (i < horde.Kinds.Length && horde.Kinds[i] != null && horde.Kinds[i].DebutWave <= wave) count++;
            if (count <= 0) return MonsterKinds.First;
            int pick = random.Next(count);
            for (int i = 0; i < MonsterKinds.HordeCount; i++)
            {
                if (i >= horde.Kinds.Length || horde.Kinds[i] == null || horde.Kinds[i].DebutWave > wave) continue;
                if (pick-- == 0) return (MonsterKind)((int)MonsterKinds.First + i);
            }
            return MonsterKinds.First;
        }
    }
}
