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
            // 单地图亚空间版（2026-11-26）：魔王魂，免费商用，署名可选（「音楽：魔王魂」）。
            // 战斗曲有两首候选：fantasy15「駆け抜ける戦場の嵐」（默认）/ fantasy12「戦術的構想」（battle_alt__，想换就在 Inspector 里拖）
            { "battleMusic", BgmDir + "/battle__maou_bgm_fantasy15.ogg" },
            { "bossMusic", BgmDir + "/boss__maou_game_boss08.ogg" },
            // 回到常态的渐强音（AudioClip 字段，不是 SoundCue）
            { "subspaceExitSwell", SfxDir + "/20-subspace/subspaceExitSwell__slow-motion-end1.wav" },
        };

        // 一次性音效：SoundCue 字段名 → 文件（文件夹编号 = 槽位顺序）
        private static readonly string[,] Cues =
        {
            { "roll", SfxDir + "/01-roll/forceField_000.ogg" },
            { "rollRejected", SfxDir + "/02-rollRejected/error_003.ogg" },
            { "playerHurt", SfxDir + "/03-playerHurt/impactPunch_medium_000.ogg" },
            { "damageBlocked", SfxDir + "/04-damageBlocked/forceField_002.ogg" },
            { "playerDeath", SfxDir + "/05-playerDeath/lowFrequency_explosion_000.ogg" },
            // 通用开火：0.24 秒的短促“咻”。原来的 thrusterFire_000 长 5 秒，连发时叠成十几秒的轰鸣杂音（海洋鱼叉最明显）
            { "shot", SfxDir + "/06-shot/laserSmall_000.ogg" },
            { "fireballShot", SfxDir + "/06-shot/fireball_shot.ogg" }, // 程序生成（原创，无授权限制）：清脆短促版
            { "ammoEmpty", SfxDir + "/07-ammoEmpty/error_001.ogg" },
            { "monsterDeath", SfxDir + "/08-monsterDeath/impactSoft_medium_000.ogg" },
            { "orbPickup", SfxDir + "/09-orbPickup/handleCoins.ogg" },
            { "levelUp", SfxDir + "/10-levelUp/jingles_NES01.ogg" },
            { "lavaEruption", SfxDir + "/11-lavaEruption/lowFrequency_explosion_001.ogg" },
            { "shockwave", SfxDir + "/13-shockwave/impactPlate_heavy_000.ogg" },
            { "phoneWarning", SfxDir + "/14-phoneWarning/zapTwoTone.ogg" },
            { "phoneLand", SfxDir + "/15-phoneLand/impactGlass_heavy_000.ogg" },
            { "pageTurn", SfxDir + "/16-pageTurn/bookFlip1.ogg" },
            { "victory", SfxDir + "/17-victory/jingles_PIZZI08.ogg" },

            // ---- 单地图亚空间版（2026-11-26）。20~27 号文件夹来自効果音ラボ（免费商用、无需署名、禁止单独再分发），
            //      已去掉首尾静音并把峰值统一到 −1 dB；其余备选文件（*_alt__、23-weaponExtra 里没接的）留着可以在 Inspector 里换 ----
            { "subspaceEnter", SfxDir + "/20-subspace/subspaceEnter__text-impact2.wav" },
            { "spearShot", SfxDir + "/21-weaponShot/spear__knife-throw1.wav" },
            { "bladeShot", SfxDir + "/21-weaponShot/blade__katana-gesture2.wav" },
            { "palmShot", SfxDir + "/21-weaponShot/palm__punch-middle2.wav" },
            { "crossbowShot", SfxDir + "/21-weaponShot/crossbow__arrow-release1.wav" },
            { "pokerShot", SfxDir + "/21-weaponShot/poker__card-turn-over1.wav" },
            { "shurikenShot", SfxDir + "/21-weaponShot/shuriken__dart1.wav" },
            { "staplerShot", SfxDir + "/21-weaponShot/stapler_placeholder__keyboard1.wav" }, // 占位：没找到订书机音
            { "swordShot", SfxDir + "/21-weaponShot/sword__highspeed-movement1.wav" },
            { "waterBurst", SfxDir + "/22-weaponBurst/water__sprinkle-water2.wav" },
            { "spearBurst", SfxDir + "/22-weaponBurst/spear__wepon-swing1.wav" },
            { "bladeBurst", SfxDir + "/22-weaponBurst/blade__iainuki1.wav" },
            { "palmBurst", SfxDir + "/22-weaponBurst/palm__super-arts-hit1.wav" },
            { "pokerBurst", SfxDir + "/22-weaponBurst/poker__card-shuffle2.wav" },
            { "shurikenBurst", SfxDir + "/22-weaponBurst/shuriken__boomerang1.wav" },
            { "swordBurst", SfxDir + "/22-weaponBurst/sword__sword-slash5.wav" },
            { "crossbowWallSlam", SfxDir + "/23-weaponExtra/crossbowWallSlam__blow4.wav" },
            { "spadeBomb", SfxDir + "/23-weaponExtra/spadeBomb__bomb2.wav" },
            // 怪物受击：沿用 Kenney 的短促打击（有声 0.09 s、起音 1 ms），比候选更适合最高频的音效
            { "monsterHit", SfxDir + "/13-shockwave/impactGeneric_light_000.ogg" },
            { "sniperWarn", SfxDir + "/24-monster/sniperWarn__beam-gun-charge1.wav" },
            { "sniperFire", SfxDir + "/24-monster/sniperFire__beamgun-shot1.wav" },
            { "rotterPoison", SfxDir + "/24-monster/rotterPoison__magic-poison1.wav" },
            { "eliteAppear", SfxDir + "/24-monster/eliteAppear__enemy-advent1.wav" },
            { "bossAppear", SfxDir + "/24-monster/bossAppear__dragon-cry2.wav" },
            { "gravityWell", SfxDir + "/25-subWeapon/gravityWell__magic-gravity1.wav" },
            { "anchorSlash", SfxDir + "/25-subWeapon/anchorSlash__magic-worp1.wav" },
            { "hourglassReset", SfxDir + "/25-subWeapon/hourglassReset__remember-the-past1.wav" },
            { "wasdKey", SfxDir + "/25-subWeapon/wasdKey__keyboard1.wav" },
            { "wasdBurst", SfxDir + "/25-subWeapon/wasdBurst__keyboard2.wav" },
            { "cursorStrike", SfxDir + "/25-subWeapon/cursorStrike__click1.wav" },
            { "magnetCollide", SfxDir + "/23-weaponExtra/spadeBomb__bomb2.wav" },
            { "upgradeConfirm", SfxDir + "/26-flow/upgradeConfirm__decision14.wav" },
            { "chestOpen", SfxDir + "/26-flow/chestOpen__treasure-chest1.wav" },
            { "waveStart", SfxDir + "/26-flow/waveStart__encount1.wav" },
            { "defeat", SfxDir + "/26-flow/defeat__curse-melody1.wav" },
        };

        // 音量 / 音高随机（SoundCue 字段.属性 → 值）
        private static readonly string[] CueVolumes =
        {
            "fireballShot.volume=0.8",      // 火球每 0.7 秒一发，音量略收
            "monsterHit.volume=0.35", "monsterHit.pitchJitter=0.12",
            "pokerShot.volume=0.7", "staplerShot.volume=0.6", "wasdKey.volume=0.5",
            "spearShot.volume=0.8", "bladeShot.volume=0.8", "palmShot.volume=0.8", "shurikenShot.volume=0.8",
            "waveStart.volume=0.6", "upgradeConfirm.volume=0.7", "sniperWarn.volume=0.9",
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
                      "，场景已保存。工位困意循环 drowsyLoop 暂无素材，保持空；订书机爆发、爱心弹命中、回到常态的一次性音效暂无素材，保持空。");
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
            // 鲨鱼出现：原来的 spaceEngineLow_000 是 5 秒的引擎低鸣，进海洋时像一段意义不明的杂音，现在留空（不播）
            ClearClip(so, "sharkAppear.clip");
            // 翻滚：亚空间版每次翻滚都会进入亚空间，由 subspaceEnter 负责，翻滚本身不再单独响（避免两个音叠在一起）
            ClearClip(so, "roll.clip");
            for (int i = 0; i < CueVolumes.Length; i++)
            {
                string[] kv = CueVolumes[i].Split('=');
                SerializedProperty prop = so.FindProperty(kv[0]);
                if (prop != null) prop.floatValue = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return filled;
        }

        private static void ClearClip(SerializedObject so, string property)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = null;
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
