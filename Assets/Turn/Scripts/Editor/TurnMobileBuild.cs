using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Turn.EditorTools
{
    /// <summary>
    /// Explicit mobile preparation/build actions only. Merely importing this script changes nothing.
    /// Never selects a platform, edits scene lists, supplies signing credentials or runs a build automatically.
    /// </summary>
    public static class TurnMobileBuild
    {
        private const string Menu = "Turn/Mobile/";

        [MenuItem(Menu + "Configure Android (landscape, ARM64 IL2CPP)")]
        public static void ConfigureAndroid()
        {
            if (!ModuleAvailable(BuildTarget.Android)) return;
            if (!EditorUtility.DisplayDialog("Turn · Android configuration",
                "Apply landscape-left/right only, Android IL2CPP and ARM64?\n" +
                "This explicitly changes Player Settings, but does not switch the active platform, build, edit scenes or alter signing.",
                "Apply", "Cancel")) return;
            SetLandscape();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.Log("[Turn Mobile] Android configured: landscape-left/right, IL2CPP, ARM64. " +
                "Choose your own unique application identifier in Player Settings. Signing is unchanged. " +
                "Switch to Android explicitly in Build Profiles before building.");
        }

        [MenuItem(Menu + "Configure iOS (landscape)")]
        public static void ConfigureIos()
        {
            if (!ModuleAvailable(BuildTarget.iOS)) return;
            if (!EditorUtility.DisplayDialog("Turn · iOS configuration",
                "Apply landscape-left/right only and iOS IL2CPP?\n" +
                "This explicitly changes Player Settings, but does not switch the active platform, build, edit scenes or alter signing.",
                "Apply", "Cancel")) return;
            SetLandscape();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            Debug.Log("[Turn Mobile] iOS configured. Supply your own Bundle Identifier in Player Settings. " +
                "Signing/team settings are unchanged. Export and sign with Xcode on macOS; Windows cannot sign or device-test iOS.");
        }

        [MenuItem(Menu + "Preflight Android")]
        public static void CheckAndroid() { Preflight(BuildTarget.Android, out _); }

        [MenuItem(Menu + "Preflight iOS")]
        public static void CheckIos() { Preflight(BuildTarget.iOS, out _); }

        [MenuItem(Menu + "Build Android APK...")]
        public static void BuildApk() { BuildAndroid(false); }

        [MenuItem(Menu + "Build Android App Bundle...")]
        public static void BuildAppBundle() { BuildAndroid(true); }

        [MenuItem(Menu + "Export iOS Xcode project... (macOS)")]
        public static void ExportIos()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogError("[Turn Mobile] Use a macOS Unity Editor with iOS Build Support to export via this menu. " +
                    "Xcode signing, installation and device testing require macOS/Xcode; no signing credentials are configured here.");
                return;
            }
            if (!Preflight(BuildTarget.iOS, out string[] scenes)) return;
            string folder = EditorUtility.SaveFolderPanel("Export iOS Xcode project", "", "Turn-iOS");
            if (string.IsNullOrEmpty(folder)) return;
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Debug.LogError("[Turn Mobile] Choose an empty export folder to avoid overwriting an existing Xcode project.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Export iOS", "Export enabled Build Settings scenes to:\n" + folder +
                "\n\nSigning is not changed. Continue?", "Export", "Cancel")) return;
            RunBuild(BuildTarget.iOS, scenes, folder);
        }

        private static void SetLandscape()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        private static bool ModuleAvailable(BuildTarget target)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (BuildPipeline.IsBuildTargetSupported(group, target)) return true;
            Debug.LogError("[Turn Mobile] " + target + " Build Support is unavailable. In Unity Hub, add modules to this exact Editor version: " +
                (target == BuildTarget.Android ? "Android Build Support, Android SDK & NDK Tools and OpenJDK." : "iOS Build Support (macOS required for Xcode signing/device testing)."));
            return false;
        }

        private static bool Preflight(BuildTarget target, out string[] scenes)
        {
            scenes = Array.Empty<string>();
            if (!ModuleAvailable(target)) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError("[Turn Mobile] Exit Play Mode and wait for script compilation before building.");
                return false;
            }
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.LogError("[Turn Mobile] Active platform is " + EditorUserBuildSettings.activeBuildTarget + ". " +
                    "Explicitly switch to " + target + " in File > Build Profiles / Build Settings, then retry. This menu never switches it automatically.");
                return false;
            }
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(s => string.IsNullOrEmpty(s) || !File.Exists(s)))
            {
                Debug.LogError("[Turn Mobile] Add at least one valid enabled scene to Build Settings / Build Profiles scene list. " +
                    "Review startup scene order (normally TurnGame first). No scene list is generated or replaced by this tool.");
                return false;
            }
            NamedBuildTarget named = target == BuildTarget.Android ? NamedBuildTarget.Android : NamedBuildTarget.iOS;
            string id = PlayerSettings.GetApplicationIdentifier(named);
            if (!ValidIdentifier(id))
            {
                Debug.LogError("[Turn Mobile] Set a unique application / Bundle Identifier in Player Settings, e.g. com.yourstudio.turn " +
                    "(example only; replace yourstudio). Default Company, example, placeholder and yourstudio identifiers are rejected. No identifier or credential is invented.");
                return false;
            }
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight ||
                PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                Debug.LogError("[Turn Mobile] Landscape-only configuration is missing. Run the chosen platform's Configure menu first.");
                return false;
            }
            if (PlayerSettings.GetScriptingBackend(named) != ScriptingImplementation.IL2CPP ||
                (target == BuildTarget.Android && PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64))
            {
                Debug.LogError("[Turn Mobile] This mobile build path requires IL2CPP and Android ARM64. Run Configure for your chosen platform, then retry.");
                return false;
            }
            Debug.Log("[Turn Mobile] Preflight passed: " + target + ", " + id + ", " + scenes.Length + " enabled scene(s). " +
                "This does not validate SDK licenses, provisioning, device compatibility or performance. Signing settings remain user-managed.");
            return true;
        }

        private static bool ValidIdentifier(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string lower = id.ToLowerInvariant();
            if (lower.Contains("defaultcompany") || lower.Contains("example") || lower.Contains("placeholder") || lower.Contains("yourstudio")) return false;
            string[] parts = id.Split('.');
            return parts.Length >= 2 && parts.All(p => p.Length > 0 && char.IsLetter(p[0]) &&
                p.All(c => char.IsLetterOrDigit(c) || c == '_'));
        }

        private static void BuildAndroid(bool bundle)
        {
            if (!Preflight(BuildTarget.Android, out string[] scenes)) return;
            string path = EditorUtility.SaveFilePanel(bundle ? "Build Android App Bundle" : "Build Android APK", "", "Turn", bundle ? "aab" : "apk");
            if (string.IsNullOrEmpty(path)) return;
            if (!EditorUtility.DisplayDialog("Build Android", "Build enabled scenes to:\n" + path +
                "\n\nThis does not install the app or change signing. Continue?", "Build", "Cancel")) return;
            bool previous = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                RunBuild(BuildTarget.Android, scenes, path);
            }
            finally { EditorUserBuildSettings.buildAppBundle = previous; }
        }

        private static void RunBuild(BuildTarget target, string[] scenes, string path)
        {
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = target,
                    locationPathName = path,
                    options = BuildOptions.None,
                });
                if (report != null && report.summary.result == BuildResult.Succeeded)
                    Debug.Log("[Turn Mobile] Build succeeded: " + path + ". Device validation has not been performed.");
                else Debug.LogError("[Turn Mobile] Build did not succeed. Review the Console / Build Report for SDK, scene, compiler or signing errors.");
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
