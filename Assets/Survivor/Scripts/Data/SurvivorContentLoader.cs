using System.Collections.Generic;
using UnityEngine;
using Survivor.Core;

namespace Survivor.Data
{
    public sealed class SurvivorContentBundle
    {
        public CharacterData Character;
        public readonly List<WeaponData> Weapons = new List<WeaponData>();
        public readonly List<PassiveData> Passives = new List<PassiveData>();
    }

    /// <summary>从 Resources 读取配置资产并转成纯数据。资产 → 运行时数据的唯一入口。</summary>
    public static class SurvivorContentLoader
    {
        public const string CharacterPath = "Survivor/Character";
        public const string WeaponDir = "Survivor/Weapons";
        public const string PassiveDir = "Survivor/Passives";

        /// <summary>找不到角色资产时返回 null。</summary>
        public static SurvivorContentBundle Load()
        {
            var character = Resources.Load<CharacterAsset>(CharacterPath);
            if (character == null)
            {
                Debug.LogError("[Survivor] 找不到 Resources/" + CharacterPath +
                               ".asset，请在菜单执行 Survivor → 生成默认内容资产。");
                return null;
            }

            var bundle = new SurvivorContentBundle();
            var byId = new Dictionary<string, WeaponData>();

            WeaponAsset[] weapons = Resources.LoadAll<WeaponAsset>(WeaponDir);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                WeaponData data = weapons[i].ToWeaponData();
                if (data.MaxLevel == 0)
                {
                    Debug.LogWarning("[Survivor] 武器没有任何等级数据，已跳过：" + weapons[i].name);
                    continue;
                }
                if (data.Behavior == null) Debug.LogWarning("[Survivor] 武器种类没有对应的行为：" + weapons[i].name);
                if (byId.ContainsKey(data.Id))
                {
                    Debug.LogWarning("[Survivor] 武器 ID 重复，已跳过：" + data.Id);
                    continue;
                }
                byId.Add(data.Id, data);
                bundle.Weapons.Add(data);
            }

            PassiveAsset[] passives = Resources.LoadAll<PassiveAsset>(PassiveDir);
            for (int i = 0; i < passives.Length; i++)
            {
                if (passives[i] != null) bundle.Passives.Add(passives[i].ToPassiveData());
            }

            bundle.Character = character.ToCharacterData();
            if (character.startingWeapon != null)
            {
                WeaponData start;
                bundle.Character.StartingWeapon = byId.TryGetValue(character.startingWeapon.Id, out start)
                    ? start
                    : character.startingWeapon.ToWeaponData();
            }
            else
            {
                Debug.LogWarning("[Survivor] 角色没有设置初始武器。");
            }
            return bundle;
        }
    }
}
