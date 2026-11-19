using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survivor.Bootstrap;

namespace Survivor.EditorTools
{
    /// <summary>
    /// 白模场景：Assets/Survivor/Survivor.unity（空场景，名字必须是 “Survivor”，SurvivorRoot 靠场景名自动启动）。
    /// 卡牌白模仍然在 SampleScene 里自动启动，两边互不干扰。
    /// </summary>
    public static class SurvivorSceneTool
    {
        public const string ScenePath = "Assets/Survivor/" + SurvivorRoot.SceneName + ".unity";

        [MenuItem("Survivor/打开白模场景", false, 0)]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Survivor] 请先退出 Play 模式。");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Survivor] 已创建白模场景：" + ScenePath);
            }
            else
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            AddToBuildSettings();
        }

        [MenuItem("Survivor/打开白模场景并运行", false, 1)]
        public static void OpenAndPlay()
        {
            OpenScene();
            if (SceneManager.GetActiveScene().path == ScenePath) EditorApplication.isPlaying = true;
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath) return;
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
