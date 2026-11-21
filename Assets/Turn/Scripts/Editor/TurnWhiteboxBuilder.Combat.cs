using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Turn.Core;
using Turn.View;
using Object = UnityEngine.Object;

namespace Turn.EditorTools
{
    /// <summary>
    /// 白模生成器的「战斗」部分（程序B）：怪物、子弹、经验球 Prefab。
    /// 共用常量、菜单入口、Sprite、Prefab 总装与通用工具在 TurnWhiteboxBuilder.cs；场景与世界部件在 TurnWhiteboxBuilder.World.cs。
    /// </summary>
    public static partial class TurnWhiteboxBuilder
    {
        private static GameObject BuildMonster()
        {
            GameObject root = new GameObject("Monster");
            root.tag = TagMonster;
            var controller = root.AddComponent<MonsterController>();

            MakeSprite("Shadow", root.transform, S("shadow"), new Color(0f, 0f, 0f, 0.3f), ViewMath.ShadowOrder,
                new Vector3(0f, -0.38f, 0f), new Vector3(0.9f, 0.32f, 1f));

            // 火山皮肤：橙红菱形“熔岩怪”
            Transform volcano = Child("VolcanoSkin", root.transform, Vector3.zero, Vector3.one);
            SpriteRenderer volcanoBody = MakeSprite("Body", volcano, S("diamond"), new Color(0.85f, 0.3f, 0.15f, 1f), ViewMath.MonsterOrder,
                Vector3.zero, new Vector3(0.95f, 0.95f, 1f));
            MakeSprite("Core", volcano, S("circle"), new Color(1f, 0.8f, 0.25f, 1f), ViewMath.MonsterOrder + 1,
                Vector3.zero, new Vector3(0.3f, 0.3f, 1f));

            // 海洋皮肤：青绿圆形“海草怪”
            Transform ocean = Child("OceanSkin", root.transform, Vector3.zero, Vector3.one);
            SpriteRenderer oceanBody = MakeSprite("Body", ocean, S("circle"), new Color(0.2f, 0.72f, 0.55f, 1f), ViewMath.MonsterOrder,
                Vector3.zero, new Vector3(0.82f, 0.82f, 1f));
            MakeSprite("Eye", ocean, S("circle"), new Color(0.05f, 0.15f, 0.15f, 1f), ViewMath.MonsterOrder + 1,
                new Vector3(0.12f, 0.1f, 0f), new Vector3(0.18f, 0.18f, 1f));
            ocean.gameObject.SetActive(false);

            Transform hpBar = Child("HpBar", root.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.8f, 0.09f, 1f));
            MakeSprite("HpBack", hpBar, S("square"), new Color(0.1f, 0.05f, 0.05f, 0.9f), ViewMath.HpBarOrder, Vector3.zero, Vector3.one);
            SpriteRenderer fill = MakeSprite("HpFill", hpBar, S("square"), new Color(0.9f, 0.2f, 0.2f, 1f), ViewMath.HpBarOrder + 1,
                Vector3.zero, Vector3.one);
            hpBar.gameObject.SetActive(false);

            SpriteRenderer burn = MakeSprite("BurnMark", root.transform, S("circle"), new Color(1f, 0.55f, 0.1f, 1f), ViewMath.HpBarOrder + 2,
                new Vector3(0.36f, 0.36f, 0f), new Vector3(0.22f, 0.22f, 1f));
            burn.enabled = false;

            Set(controller, "volcanoSkin", volcano.gameObject);
            Set(controller, "oceanSkin", ocean.gameObject);
            Set(controller, "volcanoRenderer", volcanoBody);
            Set(controller, "oceanRenderer", oceanBody);
            Set(controller, "hpFill", fill.transform);
            Set(controller, "hpBar", hpBar.gameObject);
            Set(controller, "burnMark", burn);
            return root;
        }

        private static GameObject BuildFireball()
        {
            GameObject root = new GameObject("Fireball");
            root.tag = TagProjectile;
            var p = root.AddComponent<Projectile>();
            Transform visual = Child("Visual", root.transform, Vector3.zero, Vector3.one);
            MakeSprite("Flame", visual, S("diamond"), new Color(1f, 0.45f, 0.1f, 1f), ViewMath.ProjectileOrder,
                Vector3.zero, new Vector3(0.55f, 0.55f, 1f));
            MakeSprite("Core", visual, S("circle"), new Color(1f, 0.9f, 0.4f, 1f), ViewMath.ProjectileOrder + 1,
                Vector3.zero, new Vector3(0.28f, 0.28f, 1f));
            Set(p, "visual", visual);
            SetFloat(p, "spinSpeed", 540f);
            return root;
        }

        private static GameObject BuildHarpoon()
        {
            GameObject root = new GameObject("Harpoon");
            root.tag = TagProjectile;
            var p = root.AddComponent<Projectile>();
            Transform visual = Child("Visual", root.transform, Vector3.zero, Vector3.one);
            MakeSprite("Spear", visual, S("harpoon"), new Color(0.85f, 0.92f, 1f, 1f), ViewMath.ProjectileOrder,
                Vector3.zero, new Vector3(0.9f, 0.9f, 1f));
            Set(p, "visual", visual);
            SetFloat(p, "spinSpeed", 0f);
            return root;
        }

        private static GameObject BuildOrb()
        {
            GameObject root = new GameObject("ExperienceOrb");
            root.tag = TagOrb;
            var orb = root.AddComponent<ExperienceOrb>();
            Transform visual = Child("Visual", root.transform, Vector3.zero, Vector3.one);
            MakeSprite("Gem", visual, S("diamond"), new Color(0.4f, 1f, 0.7f, 1f), ViewMath.OrbOrder,
                Vector3.zero, new Vector3(0.32f, 0.32f, 1f));
            Set(orb, "visual", visual);
            return root;
        }
    }
}
