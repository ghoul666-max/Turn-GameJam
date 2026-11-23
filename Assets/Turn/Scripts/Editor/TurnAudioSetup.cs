using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Turn.View;

namespace Turn.EditorTools
{
    /// <summary>
    /// 音频接入（程序A）。素材在 Assets/Turn/Audio/{SFX,BGM}，来源与授权见 D:\BGM\README.md / 项目根目录 THIRD_PARTY_AUDIO.md。
    ///   · 导入规则（自动）：SFX = Decompress On Load + 单声道 + Vorbis 70；BGM = Streaming + Vorbis 80 + 不预加载。
    ///   · 菜单 Turn → 音频：把素材填进 GameManager 的 TurnAudio 槽位：没有 TurnAudio 就加一个，按下表填 clip 并保存 TurnGame 场景。
    ///   · 一键生成白模（①）重建场景时也会自动调用 Apply，不会丢音频。
    /// 一个槽只能放一个 clip（SoundCue 是单 clip），每个文件夹里的其余变体留作备选，想换就在 Inspector 里拖。
    /// </summary>
    public static class TurnAudioSetup
    {
        public const string AudioDir = TurnWhiteboxBuilder.RootDir + "/Audio";
        public const string SfxDir = AudioDir + "/SFX";
        public const string BgmDir = AudioDir + "/BGM";

        // 背景音乐：TurnAudio 字段名 → 文件
        private static readonly string[,] Music =
        {
            { "titleMusic", BgmDir + "/title__Funkorama.mp3" },
            { "volcanoMusic", BgmDir + "/volcano__Volatile Reaction.mp3" },
            { "oceanMusic", BgmDir + "/ocean__Ossuary 5 - Rest.mp3" },
            { "officeMusic", BgmDir + "/office__Meditation Impromptu 01.mp3" },
            { "dreamMusic", BgmDir + "/dream__Ossuary 6 - Air.mp3" },
            { "primalMusic", BgmDir + "/primal__Killers.mp3" },
            { "futureMusic", BgmDir + "/future__Digital Lemonade.mp3" },
        };

        // 一次性音效：SoundCue 字段名 → 文件（文件夹编号 = 槽位顺序）
        private static readonly string[,] Cues =
        {
            { "roll", SfxDir + "/01-roll/forceField_000.ogg" },
            { "rollRejected", SfxDir + "/02-rollRejected/error_003.ogg" },
            { "playerHurt", SfxDir + "/03-playerHurt/impactPunch_medium_000.ogg" },
            { "damageBlocked", SfxDir + "/04-damageBlocked/forceField_002.ogg" },
            { "playerDeath", SfxDir + "/05-playerDeath/lowFrequency_explosion_000.ogg" },
            { "shot", SfxDir + "/06-shot/thrusterFire_000.ogg" },
            { "ammoEmpty", SfxDir + "/07-ammoEmpty/error_001.ogg" },
            { "monsterDeath", SfxDir + "/08-monsterDeath/impactSoft_medium_000.ogg" },
            { "orbPickup", SfxDir + "/09-orbPickup/handleCoins.ogg" },
            { "levelUp", SfxDir + "/10-levelUp/jingles_NES01.ogg" },
            { "lavaEruption", SfxDir + "/11-lavaEruption/lowFrequency_explosion_001.ogg" },
            { "sharkAppear", SfxDir + "/12-sharkAppear/spaceEngineLow_000.ogg" },
            { "shockwave", SfxDir + "/13-shockwave/impactPlate_heavy_000.ogg" },
            { "phoneWarning", SfxDir + "/14-phoneWarning/zapTwoTone.ogg" },
            { "phoneLand", SfxDir + "/15-phoneLand/impactGlass_heavy_000.ogg" },
            { "pageTurn", SfxDir + "/16-pageTurn/bookFlip1.ogg" },
            { "victory", SfxDir + "/17-victory/jingles_PIZZI08.ogg" },
        };

        [MenuItem("Turn/音频：把素材填进 GameManager 的 TurnAudio 槽位", false, 30)]
        public static void ApplyMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Turn] 请先退出 Play 模式。");
                return;
            }
            if (!AssetDatabase.IsValidFolder(AudioDir))
            {
                Debug.LogWarning("[Turn] 找不到 " + AudioDir + "，先把 D:\\BGM 的 SFX / BGM 文件夹拷进去。");
                return;
            }

            GameManager gm = FindGameManager();
            if (gm == null)
            {
                if (!File.Exists(TurnWhiteboxBuilder.ScenePath))
                {
                    Debug.LogWarning("[Turn] 找不到 " + TurnWhiteboxBuilder.ScenePath + "，请先用 Turn → ① 生成白模。");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(TurnWhiteboxBuilder.ScenePath, OpenSceneMode.Single);
                gm = FindGameManager();
                if (gm == null)
                {
                    Debug.LogWarning("[Turn] " + TurnWhiteboxBuilder.ScenePath + " 里没有 GameManager。");
                    return;
                }
            }

            ReimportAudio();
            int missing;
            int filled = Apply(gm.gameObject, out missing);
            Scene scene = gm.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = gm.gameObject;
            Debug.Log("[Turn] TurnAudio 已填好 " + filled + " 个槽位" + (missing > 0 ? "，" + missing + " 个文件没找到（见上面的警告）" : "") +
                      "，场景已保存。工位困意循环 drowsyLoop 暂无素材，保持空。");
        }

        /// <summary>给 GameManager 物体加上（或复用）TurnAudio，并按映射表填 clip。返回填好的槽位数。</summary>
        public static int Apply(GameObject gameManager, out int missing)
        {
            missing = 0;
            TurnAudio audio = gameManager.GetComponent<TurnAudio>();
            if (audio == null) audio = Undo.AddComponent<TurnAudio>(gameManager);

            var so = new SerializedObject(audio);
            int filled = 0;
            for (int i = 0; i < Music.GetLength(0); i++)
            {
                if (Assign(so, Music[i, 0], Music[i, 1])) filled++;
                else missing++;
            }
            for (int i = 0; i < Cues.GetLength(0); i++)
            {
                if (Assign(so, Cues[i, 0] + ".clip", Cues[i, 1])) filled++;
                else missing++;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return filled;
        }

        private static bool Assign(SerializedObject so, string property, string path)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogWarning("[Turn] TurnAudio 上没有字段 " + property + "（字段改名了？同步改 TurnAudioSetup 的映射表）。");
                return false;
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning("[Turn] 找不到音频 " + path + "，槽位 " + property + " 保持原样。");
                return false;
            }
            prop.objectReferenceValue = clip;
            return true;
        }

        /// <summary>按导入规则重新导入一遍（文件可能在规则脚本编译之前就被导入了）。</summary>
        private static void ReimportAudio()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        private static GameManager FindGameManager()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GameManager gm = root.GetComponentInChildren<GameManager>(true);
                    if (gm != null) return gm;
                }
            }
            return null;
        }
    }

    /// <summary>Assets/Turn/Audio 下音频的导入参数（新拖进来的素材也自动生效）。</summary>
    public sealed class TurnAudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(TurnAudioSetup.AudioDir + "/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = path.StartsWith(TurnAudioSetup.BgmDir + "/");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.8f : 0.7f;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
        }
    }
}
