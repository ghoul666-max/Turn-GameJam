using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (Section("monster", "普通怪物"))
            {
                MonsterSettings m = d.monster;
                m.maxHp = F("m.hp", "最大生命", m.maxHp, 1f, 500f);
                m.moveSpeed = F("m.speed", "移动速度", m.moveSpeed, 0f, 10f);
                m.contactDamage = F("m.dmg", "接触伤害", m.contactDamage, 0f, 100f);
                m.contactInterval = F("m.interval", "接触伤害间隔（秒）", m.contactInterval, 0.05f, 5f);
                m.radius = F("m.radius", "碰撞半径", m.radius, 0.05f, 2f);
                m.lavaHitDamage = F("m.lava", "岩浆一次性伤害", m.lavaHitDamage, 0f, 200f);
                m.xp = I("m.xp", "掉落经验", m.xp, 0, 20);
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
        }

        private void Projectile(string key, ProjectileTuning t)
        {
            t.projectileSpeed = F(key + ".speed", "子弹速度", t.projectileSpeed, 0.5f, 40f);
            t.damage = F(key + ".dmg", "伤害", t.damage, 0f, 200f);
            t.radius = F(key + ".radius", "判定半径", t.radius, 0.02f, 2f);
            t.pierce = I(key + ".pierce", "可击中怪物数（穿透）", t.pierce, 1, 20);
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
