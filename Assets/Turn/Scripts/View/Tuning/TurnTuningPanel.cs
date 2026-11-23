using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 数值面板（IMGUI，不依赖任何 UI 预制体 / 字体资源）。两种用法：
    ///   · 数值面板场景 TurnTuning（standalone = true）：一直显示，“保存” / “保存并开始游戏”；
    ///   · 游戏场景 TurnGame（GameManager 自动挂上，standalone = false）：F1 打开 / 关闭，打开时游戏暂停，
    ///     “保存并重开本局”立刻用新数值重开。
    /// 每个参数一行：拖动滑条，或者直接在右边输入框里输数字（可以超出滑条范围，但不低于下限）。
    /// 数值存到 TurnTuningStore（编辑器里是 Assets/Turn/Resources/TurnTuning.json），开局时覆盖 Inspector 数值。
    /// </summary>
    public sealed class TurnTuningPanel : MonoBehaviour
    {
        [Tooltip("勾选 = 数值面板场景（一直显示，带“开始游戏”）；不勾 = 游戏内浮层（F1 开关）")]
        [SerializeField] private bool standalone;
        [SerializeField] private string gameScene = TurnTuningStore.GameSceneName;

        private TurnTuningFile _file;
        private bool _open;
        private bool _dirty;
        private Vector2 _scroll;
        private string _status = "";
        private float _statusTime = -10f;
        private readonly Dictionary<string, string> _text = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _folds = new Dictionary<string, bool>();
        private readonly List<KeyValuePair<string, bool>> _foldRequests = new List<KeyValuePair<string, bool>>();
        private Action _restart;
        private Func<TurnTuningData> _capture;

        private GUIStyle _title;
        private GUIStyle _header;
        private GUIStyle _label;
        private GUIStyle _hint;
        private GUIStyle _panel;
        private Texture2D _panelTex;

        /// <summary>面板是否正在显示（游戏内打开时 GameManager 暂停模拟、屏蔽玩法输入）。</summary>
        public bool IsOpen { get { return standalone || _open; } }

        /// <summary>GameManager 调用：游戏内模式。restart = 重开本局；capture = 从场景 Inspector 读取当前数值。</summary>
        public void SetupInGame(Action restart, Func<TurnTuningData> capture)
        {
            standalone = false;
            _restart = restart;
            _capture = capture;
        }

        private void Start()
        {
            if (standalone) Reload();
        }

        private void OnDestroy()
        {
            if (_panelTex != null) Destroy(_panelTex);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            GUIUtility.keyboardControl = 0;
            if (open) Reload();
        }

        private void Reload()
        {
            _file = TurnTuningStore.Load();
            if (_file == null)
            {
                _file = new TurnTuningFile();
                TurnTuningData captured = _capture != null ? _capture() : null;
                _file.values = captured ?? new TurnTuningData();
                _file.defaults = _file.values.Clone();
                SetStatus(captured != null ? "已从场景 Inspector 读取当前数值" : "还没有存档：先运行一次 TurnGame，或用菜单 Turn → 数值面板：从 TurnGame 读取");
            }
            _text.Clear();
            _dirty = false;
        }

        private void SetStatus(string text)
        {
            _status = text;
            _statusTime = Time.unscaledTime;
        }

        private bool SaveNow()
        {
            Sanitize(_file.values);
            bool ok = TurnTuningStore.Save(_file);
            SetStatus(ok ? "已保存：" + TurnTuningStore.SavePath : "保存失败，详见 Console");
            if (ok) _dirty = false;
            return ok;
        }

        private void LoadScene(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName)) SceneManager.LoadScene(sceneName);
            else SetStatus("场景 " + sceneName + " 不在 Build Settings 里");
        }

        // ================================================================
        // 绘制
        // ================================================================

        private void OnGUI()
        {
            Event e = Event.current;
            if (!standalone && e.type == EventType.KeyDown && e.keyCode == KeyCode.F1)
            {
                SetOpen(!_open);
                e.Use();
                return; // 控件数量变了：这一轮不再画，下一帧 Layout 重新排版
            }
            if (e.type == EventType.Layout && _foldRequests.Count > 0)
            {
                // 折叠 / 展开只在 Layout 阶段生效，保证同一帧里各事件的控件数量一致
                for (int i = 0; i < _foldRequests.Count; i++) _folds[_foldRequests[i].Key] = _foldRequests[i].Value;
                _foldRequests.Clear();
            }

            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 820f, 1f, 3f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            if (!IsOpen)
            {
                GUI.Label(new Rect(w - 150f, h - 26f, 140f, 22f), "F1：数值面板", _hint);
                GUI.matrix = oldMatrix;
                return;
            }
            if (_file == null) Reload();

            float pw = Mathf.Min(680f, w - 20f);
            Rect r = new Rect((w - pw) * 0.5f, 10f, pw, h - 20f);
            GUI.Box(r, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f));
            DrawToolbar();
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawFields(_file.values);
            GUILayout.Space(10f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.matrix = oldMatrix;
        }

        private void DrawToolbar()
        {
            GUILayout.Label(standalone ? "Turn 数值面板" : "Turn 数值面板（游戏已暂停 · F1 关闭）", _title);

            bool use = GUILayout.Toggle(_file.useTuning, " 使用面板数值（关闭 = 完全使用场景 / Prefab 上的 Inspector 数值）");
            if (use != _file.useTuning)
            {
                _file.useTuning = use;
                _dirty = true;
            }

            bool showObstacles = GUILayout.Toggle(Obstacle.ShowDebug, " 显示遮挡物判定框（调试，F2 同效；红 = 实体，黄 = 只挡人，蓝 = 只挡弹；不保存）");
            if (showObstacles != Obstacle.ShowDebug) Obstacle.ShowDebug = showObstacles;

            GUILayout.BeginHorizontal();
            if (standalone)
            {
                if (GUILayout.Button("保存", GUILayout.Height(28f))) SaveNow();
                if (GUILayout.Button("保存并开始游戏", GUILayout.Height(28f)) && SaveNow()) LoadScene(gameScene);
            }
            else
            {
                if (GUILayout.Button("保存并重开本局", GUILayout.Height(28f)) && SaveNow())
                {
                    SetOpen(false);
                    if (_restart != null) _restart();
                }
                if (GUILayout.Button("关闭（不保存）", GUILayout.Height(28f))) SetOpen(false);
            }
            if (GUILayout.Button("恢复默认值", GUILayout.Height(28f)))
            {
                _file.values = _file.defaults != null ? _file.defaults.Clone() : new TurnTuningData();
                ClearEdits();
                SetStatus("已恢复默认值（还没保存）");
            }
            if (!standalone && _capture != null && GUILayout.Button("从 Inspector 读取", GUILayout.Height(28f)))
            {
                _file.values = _capture();
                ClearEdits();
                SetStatus("已读取场景 / Prefab 上的 Inspector 数值（还没保存）");
            }
            GUILayout.EndHorizontal();

            string line = Time.unscaledTime - _statusTime < 4f ? _status : (_dirty ? "有未保存的修改" : "拖动滑条或直接输入数字；保存后下一局生效");
            GUILayout.Label(line, _hint);
        }

        private void ClearEdits()
        {
            _text.Clear();
            _dirty = true;
            GUIUtility.keyboardControl = 0;
        }

        private void DrawFields(TurnTuningData d)
        {
            if (Section("level", "关卡"))
            {
                // 旧存档没有这一组（version = 0）：换成默认值（300 秒），保存后生效
                if (d.level == null || d.level.version <= 0) d.level = new LevelSettings();
                d.level.surviveSeconds = F("lv.survive", "每关存活多少秒过关（0 = 不过关）", d.level.surviveSeconds, 0f, 900f);
            }
            if (Section("player", "主角"))
            {
                PlayerSettings p = d.player;
                p.moveSpeed = F("p.speed", "移动速度", p.moveSpeed, 0.5f, 15f);
                p.maxHp = F("p.hp", "最大生命", p.maxHp, 1f, 500f);
                p.radius = F("p.radius", "碰撞半径", p.radius, 0.05f, 2f);
            }
            if (Section("roll", "翻滚"))
            {
                RollSettings r = d.roll;
                r.cooldown = F("r.cd", "冷却（秒）", r.cooldown, 0.1f, 10f);
                r.minCooldown = F("r.mincd", "升级后最低冷却（秒）", r.minCooldown, 0f, 5f);
                r.invincibleTime = F("r.inv", "无敌时间（秒）", r.invincibleTime, 0f, 3f);
                r.duration = F("r.dur", "翻滚时长（秒）", r.duration, 0.05f, 1.5f);
                r.distance = F("r.dist", "翻滚距离", r.distance, 0f, 10f);
            }
            if (Section("weapon", "武器（火球 / 鱼叉共用）"))
            {
                WeaponSettings w = d.weapon;
                w.magazineSize = I("w.mag", "弹匣容量", w.magazineSize, 1, 50);
                w.ammoPerShot = I("w.ammo", "每发消耗弹药", w.ammoPerShot, 1, 10);
                w.fireInterval = F("w.interval", "攻击间隔（秒）", w.fireInterval, 0.05f, 3f);
                w.targetRange = F("w.target", "自动索敌范围", w.targetRange, 1f, 30f);
                w.attackRange = F("w.range", "射程（子弹最远距离）", w.attackRange, 1f, 40f);
                w.projectileLifetime = F("w.lifetime", "子弹存活秒数", w.projectileLifetime, 0.1f, 20f);
                w.planeSwitchLifetimeMultiplier = F("w.planeLife", "首次切层剩余寿命倍率", w.planeSwitchLifetimeMultiplier, 1f, 4f);
            }
            if (Section("fireball", "火球（火山地图）")) Projectile("fb", d.fireball);
            if (Section("harpoon", "鱼叉（海洋地图）")) Projectile("hp", d.harpoon);
            if (Section("keyboard", "键盘（工位地图）")) Projectile("kb", d.keyboard);
            if (Section("paintbrush", "画笔（梦境地图）")) Projectile("pb", d.paintbrush);
            if (Section("crossbow", "弩（原始地图）")) Projectile("cb", d.crossbow);
            if (Section("laser", "激光枪（未来地图）")) Projectile("ls", d.laser);
            if (Section("builds", "Build 池（B01-B19）"))
            {
                d.battleVersion = 1;
                UpgradeSettings u = d.upgrades;
                if (u != null)
                {
                    u.optionCount = I("build.options", "每次升级候选数量", u.optionCount, 1, 8);
                    u.commonWeight = I("build.common", "普通权重", u.commonWeight, 0, 100);
                    u.rareWeight = I("build.rare", "稀有权重", u.rareWeight, 0, 100);
                    u.epicWeight = I("build.epic", "史诗权重", u.epicWeight, 0, 100);
                    if (u.buildMaxLevels != null)
                        for (int i = 0; i < u.buildMaxLevels.Length; i++)
                            u.buildMaxLevels[i] = I("build.max" + i, "B" + (i + 1).ToString("00") + " 上限", u.buildMaxLevels[i], 0, 10);
                    u.damagePerLevel = F("build.damage", "B01 每层伤害提升", u.damagePerLevel, 0f, 1f);
                    u.fireIntervalReductionPerLevel = F("build.speed", "B02 每层攻速提升", u.fireIntervalReductionPerLevel, 0f, .5f);
                    u.criticalChancePerLevel = F("build.crit", "B08 每层暴击率", u.criticalChancePerLevel, 0f, 1f);
                    u.hazardDamagePerLevel = F("build.hazard", "B10 每层地形增伤", u.hazardDamagePerLevel, 0f, 2f);
                    u.projectileLifetimeBonusPerLevel = F("build.linger", "B19 每层弹幕延寿", u.projectileLifetimeBonusPerLevel, 0f, 2f);
                }
            }
            if (Section("experience", "经验曲线"))
            {
                ExperienceSettings e = d.experience;
                if (e != null)
                {
                    e.baseXpToLevel = I("xp.base", "首级经验", e.baseXpToLevel, 1, 100);
                    e.xpGrowthPerLevel = I("xp.growth", "每级经验增长", e.xpGrowthPerLevel, 0, 100);
                    e.magnetRadius = F("xp.magnet", "经验吸附半径", e.magnetRadius, 0f, 20f);
                }
            }
            if (Section("monster", "普通怪物"))
            {
                MonsterSettings m = d.monster;
                m.volcanoMaxHp = F("m.hpVolcano", "火山怪最大生命（石头人）", m.volcanoMaxHp, 1f, 500f);
                m.oceanMaxHp = F("m.hpOcean", "海洋怪最大生命（安康鱼）", m.oceanMaxHp, 1f, 500f);
                m.maxHp = F("m.hp", "其他位面怪最大生命", m.maxHp, 1f, 500f);
                m.moveSpeed = F("m.speed", "移动速度", m.moveSpeed, 0f, 10f);
                m.contactDamage = F("m.dmg", "接触伤害", m.contactDamage, 0f, 100f);
                m.contactInterval = F("m.interval", "接触伤害间隔（秒）", m.contactInterval, 0.05f, 5f);
                m.radius = F("m.radius", "碰撞半径", m.radius, 0.05f, 2f);
                m.lavaHitDamage = F("m.lava", "岩浆一次性伤害", m.lavaHitDamage, 0f, 200f);
                m.xp = I("m.xp", "掉落经验", m.xp, 0, 20);
                if (m.archetypes != null)
                {
                    for (int i = 0; i < m.archetypes.Length; i++)
                    {
                        MonsterArchetype a = m.archetypes[i];
                        if (a == null) continue;
                        string prefix = "m.type" + i;
                        if (!Section(prefix, ((MonsterKind)i).ToString())) continue;
                        a.MaxHp = F(prefix + ".hp", "最大生命", a.MaxHp, 1f, 500f);
                        a.Speed = F(prefix + ".speed", "移动速度", a.Speed, 0f, 12f);
                        a.Damage = F(prefix + ".damage", "攻击伤害", a.Damage, 0f, 100f);
                        a.AttackInterval = F(prefix + ".interval", "攻击间隔", a.AttackInterval, .1f, 10f);
                        a.Windup = F(prefix + ".windup", "攻击前摇", a.Windup, 0f, 5f);
                        a.Range = F(prefix + ".range", "攻击范围", a.Range, 0f, 15f);
                        a.Xp = I(prefix + ".xp", "击杀经验", a.Xp, 0, 100);
                    }
                }
                m.splitInterval = F("m.splitSec", "虫崽分裂间隔", m.splitInterval, .1f, 20f);
                m.bubbleSpeed = F("m.bubbleSpeed", "泡泡速度", m.bubbleSpeed, .1f, 20f);
                m.enemyProjectileLifetime = F("m.enemyLife", "敌弹寿命", m.enemyProjectileLifetime, .1f, 20f);
                m.rockWarning = F("m.rockWarn", "石块落点预警秒数", m.rockWarning, 0f, 3f);
                m.rockImpactRadius = F("m.rockRadius", "石块落点伤害半径", m.rockImpactRadius, .1f, 5f);
                m.laserWarning = F("m.laserWarn", "未来战士激光预警秒数", m.laserWarning, .05f, 3f);
                m.laserDuration = F("m.laserDuration", "未来战士激光照射秒数", m.laserDuration, .1f, 5f);
                m.laserWidth = F("m.laserWidth", "未来战士激光宽度", m.laserWidth, .01f, 2f);
                m.robotKnockbackDistance = F("m.robotKnockback", "维修机器人击退距离", m.robotKnockbackDistance, 0f, 5f);
            }
            if (Section("shark", "鲨鱼（海洋地图）"))
            {
                SharkSettings s = d.shark;
                s.spawnCount = I("s.count", "数量", s.spawnCount, 0, 10);
                s.moveSpeed = F("s.speed", "移动速度", s.moveSpeed, 0f, 15f);
                s.contactDamage = F("s.dmg", "接触伤害", s.contactDamage, 0f, 100f);
                s.attackInterval = F("s.interval", "攻击间隔（秒）", s.attackInterval, 0.05f, 5f);
                s.radius = F("s.radius", "碰撞半径", s.radius, 0.05f, 3f);
                s.minSpawnDistance = F("s.spawnDist", "生成时离玩家最小距离", s.minSpawnDistance, 0f, 20f);
            }
            if (Section("spawn", "刷怪"))
            {
                SpawnSettings s = d.spawn;
                s.initialCount = I("sp.initial", "开局数量", s.initialCount, 0, 100);
                s.interval = F("sp.interval", "刷怪间隔（秒）", s.interval, 0.1f, 20f);
                s.countPerSpawn = I("sp.count", "每批数量", s.countPerSpawn, 0, 50);
                s.rampInterval = F("sp.ramp", "每隔多少秒每批 +N", s.rampInterval, 0f, 300f);
                s.rampAmount = I("sp.rampAmount", "每次增长 N", s.rampAmount, 0, 20);
                s.maxAlive = I("sp.max", "存活上限", s.maxAlive, 1, 500);
                s.minDistance = F("sp.min", "离玩家最小距离", s.minDistance, 0f, 30f);
                s.maxDistance = F("sp.maxDist", "离玩家最大距离", s.maxDistance, 0f, 40f);
            }
            if (Section("penalty", "翻滚惩罚刷怪"))
            {
                PenaltySettings p = d.penalty;
                p.countPerRoll = I("pe.count", "每次翻滚新增怪物", p.countPerRoll, 0, 50);
                p.minDistance = F("pe.min", "离玩家最小距离", p.minDistance, 0f, 30f);
                p.maxDistance = F("pe.max", "离玩家最大距离", p.maxDistance, 0f, 40f);
            }
            if (Section("env", "环境"))
            {
                d.shallowRegenPerSecond = F("env.regen", "浅水每秒回血（玩家）", d.shallowRegenPerSecond, 0f, 50f);
            }
            if (Section("mechanics", "位面机制（工位 / 梦境）"))
            {
                // 旧存档没有这一组（读出来 version = 0、数值全是 0）：换成策划书默认值，保存后生效
                if (d.mechanics == null || d.mechanics.version <= 0) d.mechanics = new PlaneMechanicsSettings();
                PlaneMechanicsSettings m = d.mechanics;
                m.drowsyDelay = F("pm.drowsyDelay", "工位：停留多少秒开始掉血", m.drowsyDelay, 0f, 30f);
                m.drowsyDps = F("pm.drowsyDps", "工位：困意每秒伤害", m.drowsyDps, 0f, 50f);
                m.drowsyDarkenTime = F("pm.darken", "工位：画面暗到最深的秒数", m.drowsyDarkenTime, 0.1f, 60f);
                m.dreamSlowInterval = F("pm.slowInterval", "梦境：每隔多少秒叠一层减速", m.dreamSlowInterval, 0.05f, 10f);
                m.dreamSlowPerStack = F("pm.slowStack", "梦境：每层减速比例", m.dreamSlowPerStack, 0f, 0.5f);
                m.dreamSlowMax = F("pm.slowMax", "梦境：减速上限", m.dreamSlowMax, 0f, 0.95f);
                m.phoneWarning = F("pm.phoneWarn", "来电：预警秒数", m.phoneWarning, 0.05f, 5f);
                m.phoneRadius = F("pm.phoneRadius", "来电：砸落半径", m.phoneRadius, 0.1f, 10f);
                m.phoneDamage = F("pm.phoneDmg", "来电：伤害", m.phoneDamage, 0f, 200f);
                m.phoneIntervalMin = F("pm.phoneMin", "来电：最短间隔（秒）", m.phoneIntervalMin, 0.5f, 30f);
                m.phoneIntervalMax = F("pm.phoneMax", "来电：最长间隔（秒）", m.phoneIntervalMax, 0.5f, 30f);
            }
        }

        private void Projectile(string key, ProjectileTuning t)
        {
            t.projectileSpeed = F(key + ".speed", "子弹速度", t.projectileSpeed, 0.5f, 40f);
            t.damage = F(key + ".dmg", "伤害", t.damage, 0f, 200f);
            t.radius = F(key + ".radius", "判定半径", t.radius, 0.02f, 2f);
            t.pierce = I(key + ".pierce", "可击中怪物数（穿透）", t.pierce, 1, 20);
            t.splashRadius = F(key + ".area", "爆炸/毒区半径", t.splashRadius, 0f, 8f);
            t.burnDps = F(key + ".burn", "灼烧每秒伤害", t.burnDps, 0f, 30f);
            t.burnDuration = F(key + ".burnSec", "灼烧秒数", t.burnDuration, 0f, 15f);
            t.poisonDps = F(key + ".poison", "毒区每秒伤害", t.poisonDps, 0f, 30f);
            t.poisonDuration = F(key + ".poisonSec", "毒区秒数", t.poisonDuration, 0f, 15f);
            t.poisonMaxStacks = I(key + ".poisonStack", "毒区最大叠层", t.poisonMaxStacks, 1, 10);
            t.harpoonFalloff = F(key + ".falloff", "鱼叉穿透后伤害衰减", t.harpoonFalloff, 0f, 1f);
        }

        private bool Section(string key, string title)
        {
            bool open;
            if (!_folds.TryGetValue(key, out open)) open = true;
            GUILayout.Space(6f);
            bool clicked = GUILayout.Toggle(open, (open ? "[-] " : "[+] ") + title, _header);
            if (clicked != open) _foldRequests.Add(new KeyValuePair<string, bool>(key, clicked));
            return open;
        }

        private float F(string key, string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190f));

            float shown = Mathf.Clamp(value, min, max);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(9f);
            float slid = GUILayout.HorizontalSlider(shown, min, max, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            if (!Mathf.Approximately(slid, shown))
            {
                value = Mathf.Max(min, RoundStep(slid, max - min));
                _text.Remove(key);
                _dirty = true;
            }

            string str;
            if (!_text.TryGetValue(key, out str)) str = value.ToString("0.###", CultureInfo.InvariantCulture);
            string typed = GUILayout.TextField(str, GUILayout.Width(72f));
            if (typed != str)
            {
                _text[key] = typed;
                float parsed;
                if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    value = Mathf.Max(min, parsed);
                    _dirty = true;
                }
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private int I(string key, string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190f));

            float shown = Mathf.Clamp(value, min, max);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(9f);
            float slid = GUILayout.HorizontalSlider(shown, min, max, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            int rounded = Mathf.RoundToInt(slid);
            if (rounded != Mathf.RoundToInt(shown))
            {
                value = Mathf.Max(min, rounded);
                _text.Remove(key);
                _dirty = true;
            }

            string str;
            if (!_text.TryGetValue(key, out str)) str = value.ToString(CultureInfo.InvariantCulture);
            string typed = GUILayout.TextField(str, GUILayout.Width(72f));
            if (typed != str)
            {
                _text[key] = typed;
                int parsed;
                if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    value = Mathf.Max(min, parsed);
                    _dirty = true;
                }
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private static float RoundStep(float v, float range)
        {
            float step = range <= 2f ? 0.01f : range <= 20f ? 0.05f : range <= 100f ? 0.5f : 1f;
            return Mathf.Round(v / step) * step;
        }

        /// <summary>保存前修正互相矛盾的数值（最大距离不小于最小距离等）。</summary>
        private static void Sanitize(TurnTuningData d)
        {
            if (d == null) return;
            if (d.spawn != null && d.spawn.maxDistance < d.spawn.minDistance) d.spawn.maxDistance = d.spawn.minDistance;
            if (d.penalty != null && d.penalty.maxDistance < d.penalty.minDistance) d.penalty.maxDistance = d.penalty.minDistance;
            if (d.roll != null && d.roll.minCooldown > d.roll.cooldown) d.roll.minCooldown = d.roll.cooldown;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(1f, 0.78f, 0.35f);
            _header.hover.textColor = new Color(1f, 0.9f, 0.6f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _label.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _hint.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

            _panelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _panelTex.SetPixel(0, 0, new Color(0.07f, 0.07f, 0.09f, 0.94f));
            _panelTex.Apply();
            _panelTex.hideFlags = HideFlags.DontSave;
            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = _panelTex;
        }
    }
}
