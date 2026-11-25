using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// Inspector 参数 → 模拟层纯数据的唯一入口（对应旧白模的 SurvivorContentLoader）。
    /// 开局读一次：场景组件（PlayerController / WeaponController / MapManager / WorldBounds / SpawnManager / UpgradeManager）
    /// 与 Prefab 组件（MonsterPrefab 的 MonsterController、SharkPrefab 的 SharkAI）上的参数，以及两张地图的地形网格与刷怪点。
    /// 模拟层从此不再接触任何 Unity 对象。
    /// </summary>
    public static class TurnConfigReader
    {
        public static TurnConfig Read(PlayerController player, WeaponController weapon, MapManager map,
            WorldBounds bounds, SpawnManager spawn, UpgradeManager upgrade)
        {
            var c = new TurnConfig();

            if (player != null)
            {
                c.Player = player.Settings.ToConfig();
                c.Roll = player.RollSettings.ToConfig();
            }
            else Debug.LogError("[Turn] GameManager 没有引用 PlayerController。");

            if (weapon != null)
            {
                c.Arsenal = Clone(weapon.Arsenal);
                c.Weapon = weapon.Settings.ToConfig();
                for (int i = 0; i < PlaneIds.Count; i++)
                {
                    WeaponFormSettings form = weapon.GetForm((PlaneId)i);
                    c.Forms[i] = form != null ? form.ToConfig((PlaneId)i) : null; // 没做的位面留空，WeaponSystem 会补默认值
                }
            }
            else Debug.LogError("[Turn] GameManager 没有引用 WeaponController。");

            if (bounds != null) c.Bounds = bounds.ToData();
            else Debug.LogError("[Turn] GameManager 没有引用 WorldBounds。");

            if (map != null)
            {
                c.Environment = map.Environment.ToConfig();
                // 出战配置（方案 / 武器）由 GameManager 按选择页的结果写入，不从 Inspector 读
                if (map.Level != null) c.Level = map.Level.ToConfig();
                Rect rect = bounds != null ? bounds.Rect : new Rect(-20f, -12f, 40f, 24f);
                map.SyncTerrainShape(rect); // 水池与岩浆池一一对应（大小、形状完全一样）
                for (int i = 0; i < PlaneIds.Count; i++)
                {
                    PlaneId id = (PlaneId)i;
                    TerrainGrid grid = map.GetTerrain(id);
                    c.Terrain[i] = grid != null
                        ? grid.ToData(rect, map.TerrainCellSize)
                        : TerrainGridData.Empty(c.Bounds, map.TerrainCellSize);
                    var points = new List<Vec2>();
                    map.CollectSpawnPoints(id, points);
                    c.SpawnPoints[i].AddRange(points);
                    map.CollectObstacles(id, c.Obstacles[i]);
                }
            }
            else Debug.LogError("[Turn] GameManager 没有引用 MapManager。");

            if (spawn != null)
            {
                c.Horde = Clone(spawn.Horde);
                c.Spawn = spawn.Spawn.ToConfig();
                c.Penalty = spawn.Penalty.ToConfig();
                if (spawn.MonsterPrefab != null) c.Monster = spawn.MonsterPrefab.Settings.ToConfig();
                else Debug.LogError("[Turn] SpawnManager 没有引用 MonsterPrefab。");
                if (spawn.SharkPrefab != null) c.Shark = spawn.SharkPrefab.Settings.ToConfig();
                else Debug.LogError("[Turn] SpawnManager 没有引用 SharkPrefab。");
            }
            else Debug.LogError("[Turn] GameManager 没有引用 SpawnManager。");

            if (upgrade != null)
            {
                c.Builds = Clone(upgrade.Builds);
                c.Upgrades = upgrade.Upgrades.ToConfig();
                c.Experience = upgrade.Experience.ToConfig();
            }
            else Debug.LogError("[Turn] GameManager 没有引用 UpgradeManager。");

            return c;
        }

        /// <summary>深拷贝 Inspector 上的配置：模拟层拿到的是独立的一份，运行时不会改到组件上的数值。</summary>
        public static T Clone<T>(T source) where T : class, new()
        {
            return source == null ? new T() : JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        }
    }
}
