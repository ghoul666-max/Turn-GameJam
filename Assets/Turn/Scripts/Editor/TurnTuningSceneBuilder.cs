using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Turn.View;

namespace Turn.EditorTools
{
    /// <summary>
    /// 数值面板场景 Assets/Turn/Scenes/TurnTuning.unity：
    ///   · 脚本编译后如果还没有这个场景，自动生成一次（并加进 Build Settings）；
    ///   · 菜单 Turn → ③ 打开数值面板场景并运行；
    ///   · 菜单 Turn → 数值面板：从 TurnGame 读取 Inspector 数值（把场景 / Prefab 上的当前数值写进存档，作为新的默认值）。
    /// 场景里只有一台相机 + 一个 TurnTuningPanel（standalone），面板本身用 IMGUI 画，不依赖 UI 预制体。
    /// </summary>
    [InitializeOnLoad]
    public static class TurnTuningSceneBuilder
    {
        public const string ScenePath = TurnWhiteboxBuilder.SceneDir + "/TurnTuning.unity";

        static TurnTuningSceneBuilder()
        {
            EditorApplication.delayCall += AutoCreate;
        }

        private static void AutoCreate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (File.Exists(ScenePath))
            {
                AddToBuildSettings(ScenePath);
                return;
            }
            if (!File.Exists(TurnWhiteboxBuilder.ScenePath)) return; // 白模还没生成
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) return; // 有未保存的新场景时不打扰，等用户点菜单
            }
            try
            {
                CreateScene(true);
                Debug.Log("[Turn] 已生成数值面板场景：" + ScenePath + "。菜单 Turn → ③ 打开数值面板场景并运行。");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 自动生成数值面板场景失败（可以用菜单 Turn → ③ 再试一次）：" + e.Message);
            }
        }

        [MenuItem("Turn/③ 打开数值面板场景并运行", false, 2)]
        public static void OpenAndPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene(false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Turn/数值面板：从 TurnGame 读取 Inspector 数值", false, 21)]
        public static void RecaptureFromGameScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Turn] 请先退出 Play 模式。");
                return;
            }
            TurnTuningData data = CaptureFromGameScene();
            if (data == null) return;
            TurnTuningFile file = TurnTuningStore.Load() ?? new TurnTuningFile();
            file.values = data;
            file.defaults = data.Clone();
            if (TurnTuningStore.Save(file)) Debug.Log("[Turn] 已把 TurnGame 的 Inspector 数值写进数值面板存档：" + TurnTuningStore.EditorAssetPath);
        }

        [MenuItem("Turn/数值面板：打开存档文件位置", false, 22)]
        public static void RevealSaveFile()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TurnTuningStore.EditorAssetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else Debug.Log("[Turn] 还没有存档。运行一次 TurnGame，或用菜单“从 TurnGame 读取 Inspector 数值”。");
        }

        // ---------------- 生成 ----------------

        private static void CreateScene(bool additive)
        {
            // 先保证有存档：面板一打开就是游戏里真实的 Inspector 数值，而不是脚本默认值
            if (TurnTuningStore.Load() == null)
            {
                TurnTuningData data = CaptureFromGameScene();
                if (data != null) TurnTuningStore.Save(new TurnTuningFile { values = data, defaults = data.Clone() });
            }

            Directory.CreateDirectory(TurnWhiteboxBuilder.SceneDir);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.12f, 1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(camGo, scene);

            var panelGo = new GameObject("TuningPanel");
            var panel = panelGo.AddComponent<TurnTuningPanel>();
            var so = new SerializedObject(panel);
            SerializedProperty p = so.FindProperty("standalone");
            if (p != null) p.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            SceneManager.MoveGameObjectToScene(panelGo, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (additive) EditorSceneManager.CloseScene(scene, true);
            AddToBuildSettings(ScenePath);
        }

        /// <summary>读 TurnGame 场景里的组件（场景没打开就临时以 Additive 打开，读完关掉）。</summary>
        private static TurnTuningData CaptureFromGameScene()
        {
            string path = TurnWhiteboxBuilder.ScenePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Turn] 找不到 " + path + "，先用菜单 Turn → ① 生成白模。");
                return null;
            }

            Scene game = SceneManager.GetSceneByPath(path);
            bool openedHere = false;
            if (!game.IsValid() || !game.isLoaded)
            {
                game = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                PlayerController player = null;
                WeaponController weapon = null;
                SpawnManager spawn = null;
                MapManager map = null;
                foreach (GameObject root in game.GetRootGameObjects())
                {
                    if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
                    if (weapon == null) weapon = root.GetComponentInChildren<WeaponController>(true);
                    if (spawn == null) spawn = root.GetComponentInChildren<SpawnManager>(true);
                    if (map == null) map = root.GetComponentInChildren<MapManager>(true);
                }
                return TurnTuningData.Capture(player, weapon, spawn, map);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(game, true);
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
    }
}
