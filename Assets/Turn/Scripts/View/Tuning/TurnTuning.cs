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
        /// <summary>旧存档缺少该字段时为 0，只保留原有的速度、伤害和穿透覆盖。</summary>
        public int version = 1;
        public float splashRadius = 1.2f;
        public float burnDps = 6f, burnDuration = 2f;
        public float poisonDps = 5f, poisonDuration = 3f;
        public int poisonMaxStacks = 3;
        public float harpoonFalloff = .30f;

        public static ProjectileTuning From(WeaponFormSettings s)
        {
            var t = new ProjectileTuning();
            if (s == null) return t;
            t.projectileSpeed = s.projectileSpeed;
            t.damage = s.damage;
            t.radius = s.radius;
            t.pierce = s.pierce;
            t.splashRadius = s.splashRadius;
            t.burnDps = s.burnDps;
            t.burnDuration = s.burnDuration;
            t.poisonDps = s.poisonDps;
            t.poisonDuration = s.poisonDuration;
            t.poisonMaxStacks = s.poisonMaxStacks;
            t.harpoonFalloff = s.harpoonFalloff;
            return t;
        }

        public void ApplyTo(WeaponFormConfig c)
        {
            if (c == null) return;
            c.ProjectileSpeed = projectileSpeed;
            c.Damage = damage;
            c.Radius = radius;
            c.Pierce = Mathf.Max(1, pierce);
            if (version <= 0) return;
            c.SplashRadius = Mathf.Max(0f, splashRadius);
            c.BurnDps = Mathf.Max(0f, burnDps);
            c.BurnDuration = Mathf.Max(0f, burnDuration);
            c.PoisonDps = Mathf.Max(0f, poisonDps);
            c.PoisonDuration = Mathf.Max(0f, poisonDuration);
            c.PoisonMaxStacks = Mathf.Max(1, poisonMaxStacks);
            c.HarpoonFalloff = Mathf.Clamp01(harpoonFalloff);
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
        public ProjectileTuning harpoon = new ProjectileTuning { projectileSpeed = 12f, damage = 24f, radius = 0.2f, pierce = 2 };
        public ProjectileTuning keyboard = new ProjectileTuning { projectileSpeed = 11f, damage = 8f };
        public ProjectileTuning paintbrush = new ProjectileTuning { projectileSpeed = 8f, damage = 12f };
        public ProjectileTuning crossbow = new ProjectileTuning { projectileSpeed = 18f, damage = 45f, pierce = 3 };
        public ProjectileTuning laser = new ProjectileTuning { projectileSpeed = 12f, damage = 38f };
        public UpgradeSettings upgrades = new UpgradeSettings();
        public ExperienceSettings experience = new ExperienceSettings();
        /// <summary>只有明确捕获或编辑战斗词条数值后才覆盖 Inspector；旧存档为 0。</summary>
        public int battleVersion;
        public MonsterSettings monster = new MonsterSettings();
        public SharkSettings shark = new SharkSettings();
        public SpawnSettings spawn = new SpawnSettings();
        public PenaltySettings penalty = new PenaltySettings();
        public float shallowRegenPerSecond = 3f;
        public PlaneMechanicsSettings mechanics = new PlaneMechanicsSettings();
        public LevelSettings level = new LevelSettings();
        /// <summary>新策划案（11/24）战斗分组：三把武器 / 怪物与波次 / 词条表与宝箱。battleVersion ≥ 2 时才覆盖 Inspector。</summary>
        public ArsenalConfig arsenal = new ArsenalConfig();
        public HordeConfig horde = new HordeConfig();
        public BuildConfig builds = new BuildConfig();
        /// <summary>相机 / 2.5D 视角（只影响画面）。旧存档没有这一组时 version = 0，面板 / 开局会换成默认值（= 原来的正俯视）。</summary>
        public CameraRigSettings camera = new CameraRigSettings();
        /// <summary>主角手绘外观（只影响画面，程序A）。旧存档没有这一组时 version = 0，换成默认值。</summary>
        public PlayerArtSettings playerArt = new PlayerArtSettings();

        /// <summary>当前战斗分组的数据版本（2 = 新策划案）。</summary>
        public const int CombatVersion = 2;

        public TurnTuningData Clone()
        {
            return JsonUtility.FromJson<TurnTuningData>(JsonUtility.ToJson(this));
        }

        /// <summary>从场景组件 / Prefab 上的 Inspector 参数拷贝一份（深拷贝，之后改面板不会改到组件）。</summary>
        public static TurnTuningData Capture(PlayerController player, WeaponController weapon, SpawnManager spawn, MapManager map,
            UpgradeManager upgrade = null, CameraFollow camera = null)
        {
            var d = new TurnTuningData();
            if (camera != null && camera.Settings != null) d.camera = camera.Settings.Clone();
            if (player != null && player.Art != null) d.playerArt = player.Art.Clone();
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
                d.keyboard = ProjectileTuning.From(weapon.GetForm(PlaneId.Office));
                d.paintbrush = ProjectileTuning.From(weapon.GetForm(PlaneId.Dream));
                d.crossbow = ProjectileTuning.From(weapon.GetForm(PlaneId.Primal));
                d.laser = ProjectileTuning.From(weapon.GetForm(PlaneId.Future));
            }
            if (spawn != null)
            {
                d.spawn = Copy(spawn.Spawn);
                d.penalty = Copy(spawn.Penalty);
                if (spawn.MonsterPrefab != null) d.monster = Copy(spawn.MonsterPrefab.Settings);
                if (spawn.SharkPrefab != null) d.shark = Copy(spawn.SharkPrefab.Settings);
            }
            if (weapon != null) d.arsenal = Copy(weapon.Arsenal);
            if (spawn != null) d.horde = Copy(spawn.Horde);
            if (upgrade != null)
            {
                d.upgrades = Copy(upgrade.Upgrades);
                d.experience = Copy(upgrade.Experience);
                d.builds = Copy(upgrade.Builds);
                d.battleVersion = CombatVersion;
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
            if (keyboard != null) keyboard.ApplyTo(c.Forms[(int)PlaneId.Office]);
            if (paintbrush != null) paintbrush.ApplyTo(c.Forms[(int)PlaneId.Dream]);
            if (crossbow != null) crossbow.ApplyTo(c.Forms[(int)PlaneId.Primal]);
            if (laser != null) laser.ApplyTo(c.Forms[(int)PlaneId.Future]);
            if (battleVersion > 0 && upgrades != null && upgrades.buildMaxLevels != null && upgrades.buildMaxLevels.Length == BuildIds.Count)
                c.Upgrades = upgrades.ToConfig();
            if (battleVersion > 0 && experience != null && experience.baseXpToLevel > 0) c.Experience = experience.ToConfig();
            if (battleVersion >= CombatVersion)
            {
                if (arsenal != null) c.Arsenal = Copy(arsenal).Normalize();
                if (horde != null) c.Horde = Copy(horde);
                if (builds != null) c.Builds = Copy(builds);
            }
            if (monster != null) c.Monster = monster.ToConfig();
            if (shark != null) c.Shark = shark.ToConfig();
            if (spawn != null) c.Spawn = spawn.ToConfig();
            if (penalty != null) c.Penalty = penalty.ToConfig();
            if (c.Environment != null) c.Environment.ShallowRegenPerSecond = shallowRegenPerSecond;
            if (mechanics != null) mechanics.ApplyTo(c.Environment); // 旧存档没有这一组（version = 0）时不覆盖
            if (level != null && level.version > 0)
            {
                level.Upgrade();
                c.Level = level.ToConfig();
            }
        }

        /// <summary>
        /// 旧存档迁移（亚空间之前保存的文件：翻滚分组没有 version 2）：翻滚分组换成新的默认值（保留翻滚位移时长 / 距离），
        /// 主角补上受伤无敌帧，关卡分组补上无尽 / 波次 / 解锁字段。返回是否改动了数据。
        /// </summary>
        public bool MigrateWorld()
        {
            bool changed = false;
            if (roll == null || roll.version < 2)
            {
                var fresh = new RollSettings();
                if (roll != null)
                {
                    fresh.duration = roll.duration;
                    fresh.distance = roll.distance;
                }
                roll = fresh;
                if (player != null) player.hurtInvincibleTime = new PlayerSettings().hurtInvincibleTime;
                changed = true;
            }
            if (level != null && level.version == 1)
            {
                level.Upgrade();
                changed = true;
            }
            if (roll != null && roll.version == 2)
            {
                // 双武器之前的存档：补上掌 / 弩的翻滚冷却，其余保留
                var fresh = new RollSettings();
                roll.palmCooldown = fresh.palmCooldown;
                roll.crossbowCooldown = fresh.crossbowCooldown;
                roll.version = 3;
                changed = true;
            }
            if (roll != null && roll.version == 3)
            {
                // 第二 / 三批武器之前的存档：补上扑克牌 / 手里剑 / 订书机 / 剑的翻滚冷却
                var fresh = new RollSettings();
                roll.pokerCooldown = fresh.pokerCooldown;
                roll.shurikenCooldown = fresh.shurikenCooldown;
                roll.staplerCooldown = fresh.staplerCooldown;
                roll.swordCooldown = fresh.swordCooldown;
                roll.version = fresh.version;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// 旧存档迁移（battleVersion = 0，即程序B 的 Build / 怪物池之前保存的文件）：
        /// 战斗侧分组（武器、六把子弹、怪物、升级、经验）整体换成当前 Inspector 数值；A 侧分组（玩家、翻滚、刷怪、鲨鱼、地图、关卡）保留。
        /// </summary>
        public void AdoptCombat(TurnTuningData src)
        {
            if (src == null) return;
            weapon = src.weapon;
            fireball = src.fireball;
            harpoon = src.harpoon;
            keyboard = src.keyboard;
            paintbrush = src.paintbrush;
            crossbow = src.crossbow;
            laser = src.laser;
            monster = src.monster;
            upgrades = src.upgrades;
            experience = src.experience;
            arsenal = src.arsenal;
            horde = src.horde;
            builds = src.builds;
            spawn = src.spawn;
            penalty = src.penalty;
            battleVersion = src.battleVersion;
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
        [Tooltip("编辑器每次保存时写入的版本戳（UTC Ticks）。打包进去的 Resources/TurnTuning 靠它判断比本地存档新不新")]
        public long revision;
        [Tooltip("这份存档基于哪个版本戳改出来的。打包后的本地存档只有和包里的 revision 一致才会被使用")]
        public long baseRevision;
        [Tooltip("true：开局用面板数值覆盖 Inspector；false：完全使用 Inspector 数值")]
        public bool useTuning = true;
        public TurnTuningData values = new TurnTuningData();
        public TurnTuningData defaults = new TurnTuningData();
    }

    /// <summary>
    /// 数值面板的读写。
    ///   编辑器：Assets/Turn/Resources/TurnTuning.json（在工程里，可以直接看 / 提交版本库）；
    ///   打包后：先读 persistentDataPath/TurnTuning.json（面板保存的位置），没有就读打包进去的 Resources/TurnTuning。
    ///   版本戳（11/28 修）：编辑器每次保存都写入新的 revision；打包后的本地存档记下它基于的包内 revision（baseRevision）。
    ///   换了新包（包内 revision 变了）或本地存档是旧格式（baseRevision = 0）时，本地存档作废，改用包里的数值——
    ///   否则旧包跑过一次留下的本地存档会永远挡住新包的数值。
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
#if !UNITY_EDITOR
            // 打包后：本地存档必须基于当前包内的数值版本，否则作废（见类注释）
            if (!string.IsNullOrEmpty(json))
            {
                long bundled = BundledRevision();
                long based = 0;
                try
                {
                    TurnTuningFile local = JsonUtility.FromJson<TurnTuningFile>(json);
                    if (local != null) based = local.baseRevision;
                }
                catch (Exception) { based = 0; }
                if (based == 0 || based != bundled)
                {
                    Debug.Log("[Turn] 本地数值存档基于旧版本（" + based + "，包内 " + bundled + "），已改用包里的数值：" + SavePath);
                    json = null;
                }
            }
#endif
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

        private static long _bundledRevision = -1;

        /// <summary>打包进去的 Resources/TurnTuning 的版本戳（没有 / 旧格式为 0）。</summary>
        public static long BundledRevision()
        {
            if (_bundledRevision >= 0) return _bundledRevision;
            _bundledRevision = 0;
            try
            {
                var asset = Resources.Load<TextAsset>(ResourceName);
                if (asset != null)
                {
                    TurnTuningFile f = JsonUtility.FromJson<TurnTuningFile>(asset.text);
                    if (f != null) _bundledRevision = f.revision;
                }
            }
            catch (Exception) { _bundledRevision = 0; }
            return _bundledRevision;
        }

        public static bool Save(TurnTuningFile file)
        {
            if (file == null) return false;
#if UNITY_EDITOR
            // 编辑器保存 = 新的一版数值：打包后会让玩家机器上的旧本地存档作废
            file.revision = DateTime.UtcNow.Ticks;
            file.baseRevision = file.revision;
#else
            // 打包后保存：记下基于哪个包内版本，换新包时自动作废
            file.baseRevision = BundledRevision();
            if (file.revision == 0) file.revision = file.baseRevision;
#endif
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
            SpawnManager spawn, MapManager map, UpgradeManager upgrade = null)
        {
            TurnTuningFile file = Load();
            if (file == null)
            {
                file = new TurnTuningFile();
                file.values = TurnTuningData.Capture(player, weapon, spawn, map, upgrade);
                file.defaults = file.values.Clone();
                Save(file);
                return;
            }
            if (upgrade != null && file.values != null && file.values.battleVersion < TurnTuningData.CombatVersion)
            {
                TurnTuningData fresh = TurnTuningData.Capture(player, weapon, spawn, map, upgrade);
                file.values.AdoptCombat(fresh);
                if (file.defaults == null) file.defaults = new TurnTuningData();
                file.defaults.AdoptCombat(fresh.Clone());
                Save(file);
                Debug.Log("[TurnTuning] 旧存档没有新策划案的战斗分组：武器 / 怪物 / 词条数值已换成当前 Inspector 数值并保存。");
            }
            if (file.values != null && file.values.MigrateWorld())
            {
                if (file.defaults != null) file.defaults.MigrateWorld();
                Save(file);
                Debug.Log("[TurnTuning] 旧存档没有亚空间分组：翻滚 / 关卡分组已换成新策划案默认值并保存。");
            }
            if (file.useTuning) file.values.ApplyTo(config);
        }
    }
}
