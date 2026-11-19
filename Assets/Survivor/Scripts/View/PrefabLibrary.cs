using System;
using UnityEngine;

namespace Survivor.View
{
    /// <summary>
    /// 可复用部件的统一出口（与卡牌白模同一套做法）。
    /// 优先实例化 Resources/Survivor/Prefabs/&lt;name&gt;.prefab（可以在编辑器里直接调外观）；
    /// Prefab 不存在时退回到各 View 的 BuildTemplate() 用代码现搭，保证任何情况下都能跑。
    /// </summary>
    public static class PrefabLibrary
    {
        public const string ResourceDir = "Survivor/Prefabs/";

        public const string Actor = "Actor";
        public const string Projectile = "Projectile";
        public const string Pickup = "Pickup";
        public const string Fx = "Fx";
        public const string DamageNumber = "DamageNumber";
        public const string UpgradeCard = "UpgradeCard";

        public static GameObject Spawn(string name, Transform parent, Func<GameObject> buildTemplate)
        {
            GameObject go;
            var prefab = Resources.Load<GameObject>(ResourceDir + name);
            if (prefab != null)
            {
                go = UnityEngine.Object.Instantiate(prefab, parent, false);
            }
            else
            {
                go = buildTemplate();
                go.transform.SetParent(parent, false);
            }
            go.name = name;
            UIKit.ApplyFont(go);
            SpriteKit.ApplyMaterial(go);
            return go;
        }
    }
}
