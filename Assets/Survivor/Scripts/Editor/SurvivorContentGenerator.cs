using System.IO;
using UnityEditor;
using UnityEngine;
using Survivor.Art;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.EditorTools
{
    /// <summary>
    /// 生成默认的武器 / 被动 / 角色资产（只补缺失的，不覆盖手改过的）：
    ///   Assets/Survivor/Resources/Survivor/Weapons/*.asset
    ///   Assets/Survivor/Resources/Survivor/Passives/*.asset
    ///   Assets/Survivor/Resources/Survivor/Character.asset
    /// 首次编译后自动执行一次；也可以从菜单 Survivor → 生成默认内容资产 手动执行。
    /// </summary>
    public static class SurvivorContentGenerator
    {
        public const string RootDir = "Assets/Survivor/Resources/Survivor";
        public const string WeaponDir = RootDir + "/Weapons";
        public const string PassiveDir = RootDir + "/Passives";
        public const string CharacterPath = RootDir + "/Character.asset";

        [InitializeOnLoadMethod]
        private static void AutoGenerate()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(CharacterPath)) return;
                Generate();
            };
        }

        [MenuItem("Survivor/生成默认内容资产（只补缺失）", false, 20)]
        public static void Generate()
        {
            Directory.CreateDirectory(WeaponDir);
            Directory.CreateDirectory(PassiveDir);
            AssetDatabase.Refresh();

            WeaponAsset whip = Weapon("whip", "鞭子", WeaponKind.Whip, SurvivorArt.IconWhip, SurvivorArt.Whip,
                L(12f, 1.35f, 1, 1f, 1f, 0.3f, 0, 1f, "向面朝方向横扫，击中范围内所有敌人。"),
                L(12f, 1.35f, 2, 1f, 1f, 0.3f, 0, 1f, "身后也会挥出一鞭。"),
                L(17f, 1.35f, 2, 1f, 1f, 0.3f, 0, 1f, "伤害 +5。"),
                L(17f, 1.35f, 2, 1.15f, 1f, 0.3f, 0, 1.1f, "范围 +15%。"),
                L(24f, 1.15f, 2, 1.3f, 1f, 0.3f, 0, 1.2f, "伤害 +7，范围再 +15%，冷却缩短。"));

            Weapon("wand", "魔杖", WeaponKind.Wand, SurvivorArt.IconWand, SurvivorArt.Orb,
                L(10f, 1.1f, 1, 1f, 1f, 1f, 1, 0.6f, "向最近的敌人发射魔弹。"),
                L(10f, 1.1f, 2, 1f, 1f, 1f, 1, 0.6f, "多发射 1 枚。"),
                L(10f, 0.9f, 2, 1f, 1f, 1f, 1, 0.6f, "冷却 -0.2 秒。"),
                L(14f, 0.9f, 3, 1f, 1.2f, 1f, 1, 0.6f, "多发射 1 枚，伤害 +4。"),
                L(20f, 0.9f, 3, 1.2f, 1.2f, 1f, 2, 0.7f, "伤害 +6，可多穿透 1 个敌人。"));

            Weapon("knife", "飞刀", WeaponKind.Knife, SurvivorArt.IconKnife, SurvivorArt.Knife,
                L(7f, 0.9f, 1, 1f, 1f, 1f, 1, 0.4f, "朝移动方向快速掷出飞刀。"),
                L(7f, 0.9f, 2, 1f, 1f, 1f, 1, 0.4f, "多掷 1 把。"),
                L(10f, 0.9f, 2, 1f, 1f, 1f, 2, 0.4f, "伤害 +3，可多穿透 1 个敌人。"),
                L(10f, 0.8f, 3, 1f, 1.1f, 1f, 2, 0.4f, "多掷 1 把，冷却缩短。"),
                L(13f, 0.8f, 4, 1f, 1.2f, 1f, 3, 0.5f, "多掷 1 把，伤害 +3，再多穿透 1 个。"));

            Weapon("garlic", "大蒜", WeaponKind.Aura, SurvivorArt.IconGarlic, SurvivorArt.Aura,
                L(5f, 0.75f, 1, 1f, 1f, 1f, 0, 0.6f, "持续伤害身边的敌人，并把它们推开。"),
                L(6f, 0.75f, 1, 1.2f, 1f, 1f, 0, 0.6f, "范围 +20%，伤害 +1。"),
                L(8f, 0.75f, 1, 1.2f, 1f, 1f, 0, 0.7f, "伤害 +2。"),
                L(8f, 0.65f, 1, 1.4f, 1f, 1f, 0, 0.7f, "范围再 +20%，冷却缩短。"),
                L(11f, 0.65f, 1, 1.6f, 1f, 1f, 0, 0.8f, "伤害 +3，范围再 +20%。"));

            Weapon("bible", "圣经", WeaponKind.Orbit, SurvivorArt.IconBook, SurvivorArt.Book,
                L(9f, 6f, 1, 1f, 1f, 3f, 0, 0.8f, "圣经环绕身边旋转 3 秒。"),
                L(9f, 6f, 2, 1f, 1f, 3f, 0, 0.8f, "多 1 本。"),
                L(12f, 6f, 2, 1.1f, 1.3f, 3.5f, 0, 0.8f, "伤害 +3，转速与范围提高，持续 +0.5 秒。"),
                L(12f, 6f, 3, 1.1f, 1.3f, 3.5f, 0, 0.8f, "多 1 本。"),
                L(16f, 5.5f, 4, 1.25f, 1.4f, 4f, 0, 1f, "多 1 本，伤害 +4，持续 4 秒。"));

            Weapon("lightning", "闪电戒指", WeaponKind.Lightning, SurvivorArt.IconBolt, SurvivorArt.Bolt,
                L(15f, 2.2f, 2, 1f, 1f, 1f, 0, 0f, "随机劈中附近的 2 个敌人。"),
                L(15f, 2.2f, 3, 1f, 1f, 1f, 0, 0f, "多劈 1 个。"),
                L(22f, 2.2f, 3, 1.3f, 1f, 1f, 0, 0f, "伤害 +7，落点范围 +30%。"),
                L(22f, 1.9f, 4, 1.3f, 1f, 1f, 0, 0f, "多劈 1 个，冷却缩短。"),
                L(30f, 1.9f, 5, 1.6f, 1f, 1f, 0, 0f, "多劈 1 个，伤害 +8，范围再提高。"));

            Passive("spinach", "菠菜", StatId.Might, 0.1f, 5, SurvivorArt.IconSpinach, "伤害 +10%。");
            Passive("tome", "空白之书", StatId.Cooldown, 0.08f, 5, SurvivorArt.IconTome, "武器冷却 -8%。");
            Passive("candle", "烛台", StatId.Area, 0.1f, 5, SurvivorArt.IconCandle, "攻击范围 +10%。");
            Passive("wings", "翅膀", StatId.MoveSpeed, 0.1f, 5, SurvivorArt.IconWings, "移动速度 +10%。");
            Passive("magnet", "吸引器", StatId.Magnet, 0.3f, 5, SurvivorArt.IconMagnet, "拾取范围 +30%。");
            Passive("heart", "空心之心", StatId.MaxHp, 0.2f, 5, SurvivorArt.IconHeart, "最大生命 +20%。");
            Passive("duplicator", "复制器", StatId.Amount, 1f, 2, SurvivorArt.IconRing, "所有武器的投射物数量 +1。");

            if (!File.Exists(CharacterPath))
            {
                var character = ScriptableObject.CreateInstance<CharacterAsset>();
                character.displayName = "猎魔人";
                character.spriteKey = SurvivorArt.Hunter;
                character.maxHp = 100f;
                character.moveSpeed = 3f;
                character.pickupRadius = 1.6f;
                character.startingWeapon = whip;
                AssetDatabase.CreateAsset(character, CharacterPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Survivor] 默认内容资产已就绪：" + RootDir);
        }

        private static WeaponAsset Weapon(string id, string displayName, WeaponKind kind, string artKey, string visualKey,
            params WeaponLevelEntry[] levels)
        {
            string path = WeaponDir + "/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponAsset>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<WeaponAsset>();
            asset.id = id;
            asset.displayName = displayName;
            asset.kind = kind;
            asset.artKey = artKey;
            asset.visualKey = visualKey;
            asset.levels.AddRange(levels);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Passive(string id, string displayName, StatId stat, float amountPerLevel, int maxLevel,
            string artKey, string description)
        {
            string path = PassiveDir + "/" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<PassiveAsset>(path) != null) return;

            var asset = ScriptableObject.CreateInstance<PassiveAsset>();
            asset.id = id;
            asset.displayName = displayName;
            asset.stat = stat;
            asset.amountPerLevel = amountPerLevel;
            asset.maxLevel = maxLevel;
            asset.artKey = artKey;
            asset.description = description;
            AssetDatabase.CreateAsset(asset, path);
        }

        private static WeaponLevelEntry L(float damage, float cooldown, int amount, float area, float speed,
            float duration, int pierce, float knockback, string description)
        {
            return new WeaponLevelEntry
            {
                damage = damage,
                cooldown = cooldown,
                amount = amount,
                area = area,
                speed = speed,
                duration = duration,
                pierce = pierce,
                knockback = knockback,
                description = description,
            };
        }
    }
}
