using UnityEditor;
using UnityEngine;

namespace Turn.EditorTools
{
    /// <summary>
    /// Assets/Turn/Resources/Monsters/ 下的贴图统一按「清晰的怪物序列帧」导入：
    /// Sprite（Single）、开 Mipmap + 三线性过滤（缩小显示不锯齿）、不压缩、Clamp、最大 512。
    /// 美术以后往 Monsters/&lt;种类&gt;/ 里换图或加帧，不用手动改导入设置；帧按文件名排序播放。
    /// 菜单 Turn → 重新导入怪物贴图：手动强制刷一遍。
    /// </summary>
    public sealed class TurnMonsterArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Turn/Resources/Monsters";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Folder + "/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = path.EndsWith("monster_shadow.png") ? 128f : 256f;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
        }

        [MenuItem("Turn/重新导入怪物贴图", false, 31)]
        private static void Reimport()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("[Turn] 已重新导入怪物贴图 " + guids.Length + " 张");
        }
    }
}
