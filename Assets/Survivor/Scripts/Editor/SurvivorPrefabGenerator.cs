using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Survivor.Art;
using Survivor.View;

namespace Survivor.EditorTools
{
    /// <summary>
    /// 把“代码生成的像素素材”与“代码搭出来的部件”落成磁盘上的资产（与卡牌白模的 PrefabGenerator 同一套做法）：
    ///   Assets/Survivor/Resources/Survivor/Art/*.png        —— SurvivorArt 的每张图（Point 过滤、不压缩、PPU 16）
    ///   Assets/Survivor/Resources/Survivor/Prefabs/*.prefab —— Actor / Projectile / Pickup / Fx / DamageNumber / UpgradeCard
    /// 首次编译后自动执行一次（只补缺失的）；菜单 Survivor → 重建 会用代码里的最新定义整体覆盖。
    /// </summary>
    public static class SurvivorPrefabGenerator
    {
        private const string ArtDir = "Assets/Survivor/Resources/Survivor/Art";
        private const string PrefabDir = "Assets/Survivor/Resources/Survivor/Prefabs";
        private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        private static readonly string[] PrefabNames =
        {
            PrefabLibrary.Actor, PrefabLibrary.Projectile, PrefabLibrary.Pickup,
            PrefabLibrary.Fx, PrefabLibrary.DamageNumber, PrefabLibrary.UpgradeCard,
        };

        [InitializeOnLoadMethod]
        private static void AutoGenerate()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (IsComplete()) return;
                Build(false);
            };
        }

        [MenuItem("Survivor/重建像素素材与 Prefab（覆盖现有）", false, 21)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Survivor] 请先退出 Play 模式再重建。");
                return;
            }
            Build(true);
        }

        private static bool IsComplete()
        {
            string[] keys = SurvivorArt.AllKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!File.Exists(ArtPath(keys[i]))) return false;
            }
            for (int i = 0; i < PrefabNames.Length; i++)
            {
                if (!File.Exists(PrefabPath(PrefabNames[i]))) return false;
            }
            return true;
        }

        private static void Build(bool overwrite)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);

            ExportArt(overwrite);
            BuildPrefabs(overwrite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Survivor] 像素素材与 Prefab 已就绪：" + ArtDir + "、" + PrefabDir);
        }

        // ---------------- 素材 ----------------

        private static void ExportArt(bool overwrite)
        {
            string[] keys = SurvivorArt.AllKeys;

            // 1) 用代码生成并写成 PNG
            for (int i = 0; i < keys.Length; i++)
            {
                string path = ArtPath(keys[i]);
                if (!overwrite && File.Exists(path)) continue;

                Sprite generated = SurvivorArt.Generate(keys[i]);
                Texture2D texture = generated.texture;
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(generated);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();

            // 2) 统一导入设置：像素风必须是 Point 过滤 + 不压缩
            for (int i = 0; i < keys.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(ArtPath(keys[i])) as TextureImporter;
                if (importer == null) continue;

                bool ok = importer.textureType == TextureImporterType.Sprite
                          && importer.spriteImportMode == SpriteImportMode.Single
                          && importer.filterMode == FilterMode.Point
                          && importer.textureCompression == TextureImporterCompression.Uncompressed
                          && Mathf.Approximately(importer.spritePixelsPerUnit, SurvivorArt.PixelsPerUnit)
                          && !importer.mipmapEnabled;
                if (ok) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = SurvivorArt.PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            // 3) 让 SurvivorArt 返回磁盘上的资产，这样 Prefab 里的图片引用才能被保存下来
            SurvivorArt.ClearCache();
            for (int i = 0; i < keys.Length; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath(keys[i]));
                if (sprite != null) SurvivorArt.Register(keys[i], sprite);
                else Debug.LogWarning("[Survivor] 素材导入后未找到 Sprite：" + ArtPath(keys[i]));
            }
        }

        // ---------------- Prefab ----------------

        private static void BuildPrefabs(bool overwrite)
        {
            // 运行时用的系统字体 / 运行时创建的材质都不是资产，存不进 Prefab；生成时临时换成资产版本。
            UIKit.FontOverride = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SpriteKit.MaterialOverride = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            try
            {
                SavePrefab(PrefabLibrary.Actor, overwrite, ActorView.BuildTemplate);
                SavePrefab(PrefabLibrary.Projectile, overwrite, ProjectileView.BuildTemplate);
                SavePrefab(PrefabLibrary.Pickup, overwrite, PickupView.BuildTemplate);
                SavePrefab(PrefabLibrary.Fx, overwrite, FxSprite.BuildTemplate);
                SavePrefab(PrefabLibrary.DamageNumber, overwrite, DamageNumber.BuildTemplate);
                SavePrefab(PrefabLibrary.UpgradeCard, overwrite, UpgradeCardView.BuildTemplate);
            }
            finally
            {
                UIKit.FontOverride = null;
                SpriteKit.MaterialOverride = null;
            }
        }

        private static void SavePrefab(string name, bool overwrite, Func<GameObject> buildTemplate)
        {
            string path = PrefabPath(name);
            if (!overwrite && File.Exists(path)) return;

            GameObject template = null;
            try
            {
                template = buildTemplate();
                template.name = name;
                PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Survivor] 生成 Prefab 失败：" + name + "\n" + e);
            }
            finally
            {
                if (template != null) UnityEngine.Object.DestroyImmediate(template);
            }
        }

        private static string ArtPath(string key)
        {
            return ArtDir + "/" + key + ".png";
        }

        private static string PrefabPath(string name)
        {
            return PrefabDir + "/" + name + ".prefab";
        }
    }
}
