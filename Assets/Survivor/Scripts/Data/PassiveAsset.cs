using UnityEngine;
using Survivor.Core;

namespace Survivor.Data
{
    /// <summary>被动道具配置资产。Create → Survivor → 被动道具。放在 Resources/Survivor/Passives 下即进入候选池。</summary>
    [CreateAssetMenu(fileName = "NewPassive", menuName = "Survivor/被动道具", order = 1)]
    public sealed class PassiveAsset : ScriptableObject
    {
        [Tooltip("唯一 ID（英文），留空则使用资产文件名")]
        public string id;
        public string displayName;
        public StatId stat = StatId.Might;
        [Tooltip("每级加成：0.1 = 10%；Amount 填整数个数；Cooldown 填减少的比例")]
        public float amountPerLevel = 0.1f;
        [Min(1)] public int maxLevel = 5;
        public string artKey;
        [TextArea] public string description;

        public PassiveData ToPassiveData()
        {
            return new PassiveData
            {
                Id = string.IsNullOrEmpty(id) ? name : id,
                Name = string.IsNullOrEmpty(displayName) ? name : displayName,
                ArtKey = artKey,
                Stat = stat,
                AmountPerLevel = amountPerLevel,
                MaxLevel = Mathf.Max(1, maxLevel),
                Description = description ?? "",
            };
        }
    }
}
