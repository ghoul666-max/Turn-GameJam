using System;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    [Serializable]
    public sealed class SpawnSettings
    {
        [Tooltip("开局生成的怪物数")]
        [Min(0)] public int initialCount = 6;
        [Tooltip("常规刷怪间隔（秒）")]
        public float interval = 2.5f;
        [Tooltip("每批常规刷怪数量")]
        [Min(0)] public int countPerSpawn = 2;
        [Tooltip("每隔多少秒，每批多刷 rampAmount 只（0 = 不增长）")]
        public float rampInterval = 30f;
        [Min(0)] public int rampAmount = 1;
        [Tooltip("常规刷怪的存活上限（翻滚惩罚不受此限制）")]
        [Min(1)] public int maxAlive = 120;
        [Tooltip("常规刷怪离玩家的最小 / 最大距离（优先使用当前地图的 SpawnPoints）")]
        public float minDistance = 7f;
        public float maxDistance = 11f;

        public SpawnConfig ToConfig()
        {
            return new SpawnConfig
            {
                InitialCount = initialCount,
                Interval = interval,
                CountPerSpawn = countPerSpawn,
                RampInterval = rampInterval,
                RampAmount = rampAmount,
                MaxAlive = maxAlive,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
            };
        }
    }

    [Serializable]
    public sealed class PenaltySettings
    {
        [Tooltip("每次成功翻滚新增的怪物数量")]
        [Min(0)] public int countPerRoll = 3;
        [Tooltip("怪物生成区域")]
        public PenaltyArea area = PenaltyArea.AroundPlayer;
        [Tooltip("怪物生成距离：离玩家的最小 / 最大距离")]
        public float minDistance = 4f;
        public float maxDistance = 8f;

        public PenaltyConfig ToConfig()
        {
            return new PenaltyConfig { Count = countPerRoll, Area = area, MinDistance = minDistance, MaxDistance = maxDistance };
        }
    }

    /// <summary>
    /// 刷怪配置（节点 SpawnManager）：常规刷怪、翻滚惩罚刷怪，以及怪物 / 鲨鱼的 Prefab。
    /// 怪物与鲨鱼的数值在各自 Prefab 的 MonsterController / SharkAI 上调。
    /// 刷怪规则在模拟层的 SpawnSystem / SharkSystem 里。
    /// </summary>
    public sealed class SpawnManager : MonoBehaviour
    {
        [Header("Prefab（生成器自动拖好）")]
        [SerializeField] private MonsterController monsterPrefab;
        [SerializeField] private SharkAI sharkPrefab;

        [Header("常规刷怪")]
        [SerializeField] private SpawnSettings spawn = new SpawnSettings();
        [Header("翻滚惩罚")]
        [SerializeField] private PenaltySettings penalty = new PenaltySettings();

        public MonsterController MonsterPrefab { get { return monsterPrefab; } }
        public SharkAI SharkPrefab { get { return sharkPrefab; } }
        public SpawnSettings Spawn { get { return spawn; } }
        public PenaltySettings Penalty { get { return penalty; } }
    }
}
