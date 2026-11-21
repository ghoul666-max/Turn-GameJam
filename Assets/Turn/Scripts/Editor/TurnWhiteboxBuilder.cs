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
    /// 一键生成 Turn 白模（与旧白模“代码生成部件”同一套做法）：
    ///   Assets/Turn/Art/*.png                 —— 占位形状 Sprite（方块 / 圆 / 环 / 菱形 / 三角 / 鱼叉 / 影子）
    ///   Assets/Turn/Animations/PlayerAnimator  —— Idle / Move / Roll / Hurt / Death 五个状态，整数参数 State
    ///   Assets/Turn/Prefabs/*.prefab          —— 题目要求的 8 个 Prefab
    ///   Assets/Turn/Scenes/TurnGame.unity     —— 按题目 Hierarchy 搭好的场景，所有 Inspector 引用已拖好
    /// 菜单：Turn → ① 一键生成白模；Turn → ② 打开白模场景并运行。
    /// 重复执行会覆盖上面这些生成物（场景里手改的参数也会被覆盖，调参请改完后不要再点 ①）。
    /// 本类拆成三个 partial：本文件 = 共用常量 / 菜单 / Sprite / Prefab 总装 / 通用工具；
    /// TurnWhiteboxBuilder.World.cs = 玩家、鲨鱼、特效、场景、HUD（程序A）；TurnWhiteboxBuilder.Combat.cs = 怪物、子弹、经验球（程序B）。
    /// </summary>
    public static partial class TurnWhiteboxBuilder
    {
        public const string RootDir = "Assets/Turn";
        public const string ArtDir = RootDir + "/Art";
        public const string PrefabDir = RootDir + "/Prefabs";
        public const string AnimDir = RootDir + "/Animations";
        public const string SceneDir = RootDir + "/Scenes";
        public const string ScenePath = SceneDir + "/TurnGame.unity";
        public const string AnimatorPath = AnimDir + "/PlayerAnimator.controller";
        public const string FallbackMaterialPath = ArtDir + "/TurnSpriteUnlit.mat";
        private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const int SpritePixelsPerUnit = 64;

        public const string TagMonster = "Monster";
        public const string TagShark = "Shark";
        public const string TagProjectile = "Projectile";
        public const string TagOrb = "ExperienceOrb";
        public const string TagMapRoot = "MapRoot";

        private static readonly Vector2 MapSize = new Vector2(40f, 24f);
        private const float CellSize = 1f;

        private static Material _material;
        private static Font _font;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        private sealed class PrefabSet
        {
            public GameObject Player;
            public GameObject Monster;
            public GameObject Fireball;
            public GameObject Harpoon;
            public GameObject Shark;
            public GameObject Orb;
            public GameObject Portal;
            public GameObject Shockwave;
        }

        // ================================================================
        // 菜单
        // ================================================================

        [MenuItem("Turn/① 一键生成白模（素材 + Prefab + 场景）", false, 0)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Turn] 请先退出 Play 模式再生成。");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EnsureFolders();
                EnsureTags(TagMonster, TagShark, TagProjectile, TagOrb, TagMapRoot);
                BuildSprites();
                _material = LoadMaterial();
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                RuntimeAnimatorController animator = BuildAnimator();
                PrefabSet prefabs = BuildPrefabs(animator);
                BuildScene(prefabs);
                AssetDatabase.SaveAssets();
                Debug.Log("[Turn] 白模已生成：" + ScenePath + "。点 Play 或菜单 Turn → ② 打开白模场景并运行。");
            }
            catch (Exception e)
            {
                Debug.LogError("[Turn] 生成失败：" + e);
            }
        }

        [MenuItem("Turn/② 打开白模场景并运行", false, 1)]
        public static void OpenAndPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(ScenePath))
            {
                BuildAll();
                if (!File.Exists(ScenePath)) return;
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Turn/重新随机生成两张地图的地形", false, 20)]
        public static void RegenerateTerrain()
        {
            Scene scene = SceneManager.GetActiveScene();
            WorldBounds bounds = null;
            MapManager map = null;
            var grids = new List<TerrainGrid>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (bounds == null) bounds = root.GetComponentInChildren<WorldBounds>(true);
                if (map == null) map = root.GetComponentInChildren<MapManager>(true);
                grids.AddRange(root.GetComponentsInChildren<TerrainGrid>(true));
            }
            if (bounds == null || grids.Count == 0)
            {
                Debug.LogWarning("[Turn] 当前场景里没有 WorldBounds / TerrainGrid。");
                return;
            }
            var rng = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < grids.Count; i++)
            {
                Undo.RecordObject(grids[i], "Regenerate Terrain");
                grids[i].SetSeed(rng.Next(1, 100000));
                grids[i].Generate(bounds.Rect, map != null ? map.TerrainCellSize : CellSize);
                EditorUtility.SetDirty(grids[i]);
            }
            if (map != null) map.SyncTerrainShape(); // 水池与岩浆池一一对应
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Turn] 已重新生成 " + grids.Count + " 张地形网格。");
        }

        // ================================================================
        // 准备
        // ================================================================

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(AnimDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();
        }

        private static void EnsureTags(params string[] tags)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty list = so.FindProperty("tags");
            if (list == null) return;

            for (int t = 0; t < tags.Length; t++)
            {
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).stringValue == tags[t]) exists = true;
                }
                if (exists) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = tags[t];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LoadMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (mat != null) return mat;
            mat = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, FallbackMaterialPath);
            return mat;
        }

        // ================================================================
        // 占位 Sprite（白色形状，靠 SpriteRenderer.color 上色）
        // ================================================================

        private static void BuildSprites()
        {
            Sprites.Clear();
            WriteSprite("square", 64, 64, (x, y, w, h) => 1f);
            WriteSprite("circle", 64, 64, (x, y, w, h) => Disc(x, y, w, h, 0.5f));
            WriteSprite("ring", 64, 64, (x, y, w, h) => Disc(x, y, w, h, 0.5f) * (1f - Disc(x, y, w, h, 0.4f)));
            WriteSprite("diamond", 64, 64, (x, y, w, h) =>
            {
                float u = Mathf.Abs((x + 0.5f) / w - 0.5f);
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f);
                return Mathf.Clamp01((0.5f - (u + v)) * w * 0.5f);
            });
            WriteSprite("triangle", 64, 64, (x, y, w, h) =>
            {
                // 指向右边：底边在左
                float u = (x + 0.5f) / w;
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f);
                return Mathf.Clamp01(((1f - u) * 0.5f - v) * h);
            });
            WriteSprite("harpoon", 64, 16, (x, y, w, h) =>
            {
                float u = (x + 0.5f) / w;
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f);
                if (u > 0.72f) return Mathf.Clamp01(((1f - u) / 0.28f * 0.5f - v) * h); // 枪头
                return v < 0.12f ? 1f : 0f;                                            // 枪杆
            });
            WriteSprite("shadow", 64, 64, (x, y, w, h) =>
            {
                float dx = (x + 0.5f) / w - 0.5f;
                float dy = (y + 0.5f) / h - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                return Mathf.Clamp01(1f - d) * 0.9f;
            });
            AssetDatabase.Refresh();

            foreach (string key in new[] { "square", "circle", "ring", "diamond", "triangle", "harpoon", "shadow" })
            {
                string path = ArtDir + "/" + key + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = SpritePixelsPerUnit;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) Debug.LogWarning("[Turn] Sprite 导入失败：" + path);
                Sprites[key] = sprite;
            }
        }

        private static float Disc(int x, int y, int w, int h, float radius)
        {
            float dx = (x + 0.5f) / w - 0.5f;
            float dy = (y + 0.5f) / h - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((radius - d) * w); // 1 像素宽的抗锯齿边
        }

        private static void WriteSprite(string key, int w, int h, Func<int, int, int, int, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y, w, h)) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(ArtDir + "/" + key + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Sprite S(string key)
        {
            Sprite s;
            return Sprites.TryGetValue(key, out s) ? s : null;
        }

        // ================================================================
        // Prefab
        // ================================================================

        private static PrefabSet BuildPrefabs(RuntimeAnimatorController animator)
        {
            var set = new PrefabSet();
            // 子弹 / 特效 / 经验球先生成，玩家和其他节点要引用它们
            set.Fireball = SavePrefab(BuildFireball(), "FireballProjectilePrefab");
            set.Harpoon = SavePrefab(BuildHarpoon(), "HarpoonProjectilePrefab");
            set.Orb = SavePrefab(BuildOrb(), "ExperienceOrbPrefab");
            set.Portal = SavePrefab(BuildPortal(), "PortalVFXPrefab");
            set.Shockwave = SavePrefab(BuildShockwave(), "RollShockwaveVFXPrefab");
            set.Monster = SavePrefab(BuildMonster(), "MonsterPrefab");
            set.Shark = SavePrefab(BuildShark(), "SharkPrefab");
            set.Player = SavePrefab(BuildPlayer(animator, set), "PlayerPrefab");
            return set;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            ApplyMaterial(root);
            string path = PrefabDir + "/" + name + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (asset == null) Debug.LogError("[Turn] 保存 Prefab 失败：" + path);
            return asset;
        }

        // ================================================================
        // 通用小工具
        // ================================================================

        private static Transform Child(string name, Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go.transform;
        }

        private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Color color, int order,
            Vector3 localPos, Vector3 localScale)
        {
            Transform t = Child(name, parent, localPos, localScale);
            var r = t.gameObject.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            if (_material != null) r.sharedMaterial = _material;
            return r;
        }

        private static void ApplyMaterial(GameObject root)
        {
            if (_material == null || root == null) return;
            foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = _material;
        }

        private static void ApplyMaterialInScene(Scene scene)
        {
            if (_material == null) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root)) continue; // Prefab 实例里已经是对的
                ApplyMaterial(root);
            }
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path) return;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- SerializedObject 写入（组件字段是 [SerializeField] private）----

        private static SerializedProperty Find(SerializedObject so, Object target, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) Debug.LogError("[Turn] 找不到序列化字段 " + target.GetType().Name + "." + path);
            return p;
        }

        private static void Set(Object target, string path, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string path, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string path, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string path, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Object target, string path, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string path, Vector2 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string path, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, path);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
