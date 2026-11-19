using UnityEngine;
using Survivor.Core;

namespace Survivor.Data
{
    /// <summary>角色配置资产（Resources/Survivor/Character.asset）。初始武器引用一个 WeaponAsset。</summary>
    [CreateAssetMenu(fileName = "Character", menuName = "Survivor/角色", order = 2)]
    public sealed class CharacterAsset : ScriptableObject
    {
        public string displayName = "猎魔人";
        [Tooltip("角色立绘的素材键（SurvivorArt）")]
        public string spriteKey = "hunter";
        [Min(1f)] public float maxHp = 100f;
        [Tooltip("每秒移动的世界单位")]
        public float moveSpeed = 3f;
        [Tooltip("经验宝石开始被吸过来的半径")]
        public float pickupRadius = 1.6f;
        public WeaponAsset startingWeapon;

        /// <summary>初始武器由 SurvivorContentLoader 按 ID 对接到武器池里的同一份数据。</summary>
        public CharacterData ToCharacterData()
        {
            return new CharacterData
            {
                Name = displayName,
                SpriteKey = spriteKey,
                MaxHp = maxHp,
                MoveSpeed = moveSpeed,
                PickupRadius = pickupRadius,
            };
        }
    }
}
