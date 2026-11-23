using System;
using System.IO;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>一种子弹（火球 / 鱼叉）可调的数值。不含 Prefab 引用，方便存成 JSON。</summary>
    [Serializable]
    public sealed class ProjectileTuning
    {
        public float projectileSpeed = 9f;
        public float damage = 12f;
        public float radius = 0.25f;
        public int pierce = 1;

        public static ProjectileTuning From(WeaponFormSettings s)
        {
            var t = new ProjectileTuning();
            if (s == null) return t;
            t.projectileSpeed = s.projectileSpeed;
            t.damage = s.damage;
            t.radius = s.radius;
            t.pierce = s.pierce;
            return t;
        }

        public void ApplyTo(WeaponFormConfig c)
        {
            if (c == null) return;
            c.ProjectileSpeed = projectileSpeed;
            c.Damage = damage;
            c.Radius = radius;
            c.Pierce = Mathf.Max(1, pierce);
        }
    }

    /// <summary>
    /// 数值面板能改的全部参数：主角、翻滚、武器、两种子弹、普通怪物、鲨鱼、刷怪、翻滚惩罚、浅水回血。
    /// 直接复用各组件上的 *Settings 类（字段名与 Inspector 一致），所以和 Inspector 一一对应。
    /// </summary>
    [Serializable]
    public sealed class TurnTuningData
    {
        public PlayerSettings player = new PlayerSettings();
        public RollSettings roll = new RollSettings();
        public WeaponSettings weapon = new WeaponSettings();
        public ProjectileTuning fireball = new ProjectileTuning();
        public ProjectileTuning harpoon = new ProjectileTuning { projectileSpeed = 13f, damage = 15f, radius = 0.2f, pierce = 2 };
        public MonsterSettings monster = new MonsterSettings();
        public SharkSettings shark = new SharkSettings();
        public SpawnSettings spawn = new SpawnSettings();
        public PenaltySettings penalty = new PenaltySettings();
        public float shallowRegenPerSecond = 3f;
        public PlaneMechanicsSettings mechanics = new PlaneMechanicsSettings();
        public LevelSettings level = new LevelSettings();

        public TurnTuningData Clone()
        {
            return JsonUtility.FromJson<TurnTuningData>(JsonUtility.ToJson(this));
        }

        /// <summary>从场景组件 / Prefab 上的 Inspector 参数拷贝一份（深拷贝，之后改面板不会改到组件）。</summary>
        public static TurnTuningData Capture(PlayerController player, WeaponController weapon, SpawnManager spawn, MapManager map)
        {
            var d = new TurnTuningData();
            if (player != null)
            {
                d.player = Copy(player.Settings);
                d.roll = Copy(player.RollSettings);
            }
            if (weapon != null)
            {
                d.weapon = Copy(weapon.Settings);
                d.fireball = ProjectileTuning.From(weapon.GetForm(PlaneId.Volcano));
                d.harpoon = ProjectileTuning.From(weapon.GetForm(PlaneId.Ocean));
            }
            if (spawn != null)
            {
                d.spawn = Copy(spawn.Spawn);
                d.penalty = Copy(spawn.Penalty);
                if (spawn.MonsterPrefab != null) d.monster = Copy(spawn.MonsterPrefab.Settings);
                if (spawn.SharkPrefab != null) d.shark = Copy(spawn.SharkPrefab.Settings);
            }
            if (map != null)
            {
                d.shallowRegenPerSecond = map.Environment.shallowRegenPerSecond;
                d.mechanics = Copy(map.Environment.mechanics);
                d.level = Copy(map.Level);
            }
            return d;
        }

        /// <summary>覆盖一局的配置（在 TurnFactory.Create 之前调用）。</summary>
        public void ApplyTo(TurnConfig c)
        {
            if (c == null) return;
            if (player != null) c.Player = player.ToConfig();
            if (roll != null) c.Roll = roll.ToConfig();
            if (weapon != null) c.Weapon = weapon.ToConfig();
            if (fireball != null) fireball.ApplyTo(c.Forms[(int)PlaneId.Volcano]);
            if (harpoon != null) harpoon.ApplyTo(c.Forms[(int)PlaneId.Ocean]);
            if (monster != null) c.Monster = monster.ToConfig();
            if (shark != null) c.Shark = shark.ToConfig();
            if (spawn != null) c.Spawn = spawn.ToConfig();
            if (penalty != null) c.Penalty = penalty.ToConfig();
            if (c.Environment != null) c.Environment.ShallowRegenPerSecond = shallowRegenPerSecond;
            if (mechanics != null) mechanics.ApplyTo(c.Environment); // 旧存档没有这一组（version = 0）时不覆盖
            if (level != null && level.version > 0) c.Level.SurviveSeconds = level.surviveSeconds;
        }

        private static T Copy<T>(T source) where T : class, new()
        {
            return source == null ? new T() : JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        }
    }

    /// <summary>存档文件：当前值 + 默认值（第一次从 Inspector 读到的值，用于“恢复默认”）。</summary>
    [Serializable]
    public sealed class TurnTuningFile
    {
        public int version = 1;
        [Tooltip("true：开局用面板数值覆盖 Inspector；false：完全使用 Inspector 数值")]
        public bool useTuning = true;
        public TurnTuningData values = new TurnTuningData();
        public TurnTuningData defaults = new TurnTuningData();
    }

    /// <summary>
    /// 数值面板的读写。
    ///   编辑器：Assets/Turn/Resources/TurnTuning.json（在工程里，可以直接看 / 提交版本库）；
    ///   打包后：先读 persistentDataPath/TurnTuning.json（面板保存的位置），没有就读打包进去的 Resources/TurnTuning。
    /// </summary>
    public static class TurnTuningStore
    {
        public const string ResourceName = "TurnTuning";
        public const string EditorAssetPath = "Assets/Turn/Resources/TurnTuning.json";
        public const string GameSceneName = "TurnGame";
        public const string TuningSceneName = "TurnTuning";

        public static string SavePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", EditorAssetPath);
#else
                return Path.Combine(Application.persistentDataPath, "TurnTuning.json");
#endif
            }
        }

        /// <summary>读存档；从来没保存过返回 null。</summary>
        public static TurnTuningFile Load()
        {
            string json = null;
            try
            {
                string path = SavePath;
                if (File.Exists(path)) json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 读取数值面板存档失败：" + e.Message);
            }
            if (string.IsNullOrEmpty(json))
            {
                var asset = Resources.Load<TextAsset>(ResourceName);
                if (asset != null) json = asset.text;
            }
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var file = JsonUtility.FromJson<TurnTuningFile>(json);
                if (file == null) return null;
                if (file.values == null) file.values = new TurnTuningData();
                if (file.defaults == null) file.defaults = file.values.Clone();
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 数值面板存档格式错误，已忽略：" + e.Message);
                return null;
            }
        }

        public static bool Save(TurnTuningFile file)
        {
            if (file == null) return false;
            try
            {
                string path = SavePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(file, true));
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.AssetDatabase.Refresh();
                else UnityEditor.EditorApplication.delayCall += () => UnityEditor.AssetDatabase.Refresh();
#endif
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Turn] 保存数值面板失败：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// GameManager 开局调用：
        ///   还没有存档 → 把当前 Inspector 数值记成存档（当前值 + 默认值），本局不做任何覆盖；
        ///   有存档且启用 → 用面板数值覆盖 Inspector 读到的配置。
        /// </summary>
        public static void ApplyOrCapture(TurnConfig config, PlayerController player, WeaponController weapon,
            SpawnManager spawn, MapManager map)
        {
            TurnTuningFile file = Load();
            if (file == null)
            {
                file = new TurnTuningFile();
                file.values = TurnTuningData.Capture(player, weapon, spawn, map);
                file.defaults = file.values.Clone();
                Save(file);
                return;
            }
            if (file.useTuning) file.values.ApplyTo(config);
        }
    }
}
