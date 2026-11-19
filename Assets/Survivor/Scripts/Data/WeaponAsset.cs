using System;
using System.Collections.Generic;
using UnityEngine;
using Survivor.Core;
using Survivor.Weapons;

namespace Survivor.Data
{
    /// <summary>武器某一级的数值（资产里的一行）。</summary>
    [Serializable]
    public sealed class WeaponLevelEntry
    {
        public float damage = 10f;
        [Tooltip("两次发动之间的秒数")]
        public float cooldown = 1f;
        [Min(1)] public int amount = 1;
        [Tooltip("范围倍率")]
        public float area = 1f;
        [Tooltip("速度倍率")]
        public float speed = 1f;
        [Tooltip("持续秒数：鞭子挥动 / 圣经环绕")]
        public float duration = 1f;
        [Tooltip("可击中的敌人数；0 = 无限")]
        public int pierce = 1;
        public float knockback = 1f;
        [TextArea] public string description;

        public WeaponStats ToStats()
        {
            return new WeaponStats
            {
                Damage = damage,
                Cooldown = cooldown,
                Amount = amount,
                Area = area,
                Speed = speed,
                Duration = duration,
                Pierce = pierce,
                Knockback = knockback,
                Description = description ?? "",
            };
        }
    }

    /// <summary>
    /// 武器配置资产。Project 窗口右键 Create → Survivor → 武器 新建。
    /// 只是配置：开局时通过 ToWeaponData() 转成纯 C# 的 WeaponData，系统层不接触本类。
    /// 放在 Resources/Survivor/Weapons 下的武器都会进入升级候选池。
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Survivor/武器", order = 0)]
    public sealed class WeaponAsset : ScriptableObject
    {
        [Tooltip("唯一 ID（英文），留空则使用资产文件名")]
        public string id;
        public string displayName;
        [Tooltip("决定发动方式（WeaponBehaviors）")]
        public WeaponKind kind = WeaponKind.Wand;
        [Tooltip("界面图标的素材键（SurvivorArt）")]
        public string artKey;
        [Tooltip("投射物的素材键（SurvivorArt）")]
        public string visualKey;
        [Tooltip("第 1 行 = 1 级；行数 = 最高等级")]
        public List<WeaponLevelEntry> levels = new List<WeaponLevelEntry>();

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }

        public WeaponData ToWeaponData()
        {
            var data = new WeaponData();
            data.Id = Id;
            data.Name = string.IsNullOrEmpty(displayName) ? name : displayName;
            data.ArtKey = artKey;
            data.VisualKey = visualKey;
            data.Kind = kind;
            data.Behavior = WeaponBehaviors.Create(kind);
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null) data.Levels.Add(levels[i].ToStats());
            }
            return data;
        }
    }
}
