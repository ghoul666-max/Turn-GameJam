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
    /// 历史记录：每次保存（有改动时）自动记一条完整快照 + 改动明细（TurnTuningHistory），
    /// 「历史记录」页可以展开看改了什么、和当前面板对比、载入某一版（载入后要再保存才生效）、删除。
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

        // 历史记录
        private TurnTuningHistoryFile _history;
        private TurnTuningData _lastSaved;
        private bool _lastSavedUse = true;
        private string _saveNote = "";
        private bool _showHistory;
        private bool _showHistoryNext;
        private Vector2 _historyScroll;
        private int _pendingLoad = -1;
        private int _pendingDelete = -1;
        private int _pendingCompare = -1;
        private int _compareIndex = -1;
        private string _compareText = "";
        private int _unsavedCount;
        private float _unsavedCountTime = -10f;
        private GUIStyle _wrap;

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
            if (!open) CameraFollow.Preview = null; // 关闭面板：相机回到本局开局时的参数
            GUIUtility.keyboardControl = 0;
            if (open) Reload();
        }

        private void Reload()
        {
            _file = TurnTuningStore.Load();
            bool fromDisk = _file != null;
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
            // 历史记录：「上一次保存」= 磁盘上这份；从来没保存过时为 null（下一次保存记为「首次保存」）
            _lastSaved = fromDisk ? _file.values.Clone() : null;
            _lastSavedUse = _file.useTuning;
            _history = TurnTuningHistory.Load();
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
            if (!ok) return false;
            _dirty = false;

            // 历史记录：和上一次保存相比有改动才记
            if (_history == null) _history = TurnTuningHistory.Load();
            bool changed = _lastSaved == null || _lastSavedUse != _file.useTuning || TurnTuningHistory.CountDiff(_lastSaved, _file.values) > 0;
            if (changed)
            {
                TurnTuningHistoryEntry entry = TurnTuningHistory.Record(_history, _lastSaved, _file.values, _file.useTuning, _lastSavedUse,
                    (_saveNote ?? "").Trim());
                if (entry != null)
                    SetStatus("已保存并记入历史（" + (entry.changeCount < 0 ? "首次保存" : "改了 " + entry.changeCount + " 项") + "）");
            }
            _lastSaved = _file.values.Clone();
            _lastSavedUse = _file.useTuning;
            _saveNote = "";
            _compareIndex = -1;
            return true;
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
            if (e.type == EventType.Layout) ApplyHistoryRequests();

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
            // 相机 / 2.5D 视角：面板打开时实时预览正在编辑的参数（不保存也能看效果）
            if (!standalone && _file.values != null) CameraFollow.Preview = _file.useTuning ? _file.values.camera : null;

            float pw = Mathf.Min(680f, w - 20f);
            Rect r = new Rect((w - pw) * 0.5f, 10f, pw, h - 20f);
            GUI.Box(r, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f));
            DrawToolbar();
            if (_showHistory)
            {
                _historyScroll = GUILayout.BeginScrollView(_historyScroll);
                DrawHistory();
            }
            else
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                DrawFields(_file.values);
            }
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

            GUILayout.BeginHorizontal();
            GUILayout.Label("本次保存备注", _label, GUILayout.Width(90f));
            _saveNote = GUILayout.TextField(_saveNote ?? "", 60, GUILayout.ExpandWidth(true));
            int count = _history != null ? _history.entries.Count : 0;
            if (GUILayout.Button(_showHistoryNext ? "返回参数" : "历史记录（" + count + "）", GUILayout.Width(120f), GUILayout.Height(22f)))
            {
                _showHistoryNext = !_showHistoryNext;
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();

            string line = Time.unscaledTime - _statusTime < 4f ? _status : (_dirty ? "有未保存的修改" : "拖动滑条或直接输入数字；保存后下一局生效");
            GUILayout.Label(line, _hint);
        }

        // ================================================================
        // 历史记录
        // ================================================================

        /// <summary>切换页面 / 载入 / 删除 / 对比都会改变控件数量：只在 Layout 阶段执行。</summary>
        private void ApplyHistoryRequests()
        {
            _showHistory = _showHistoryNext;
            if (_history == null) return;
            List<TurnTuningHistoryEntry> list = _history.entries;
            if (_pendingLoad >= 0 && _pendingLoad < list.Count && _file != null)
            {
                TurnTuningHistoryEntry entry = list[_pendingLoad];
                _file.values = entry.values != null ? entry.values.Clone() : new TurnTuningData();
                _file.useTuning = entry.useTuning;
                ClearEdits();
                _showHistory = _showHistoryNext = false;
                SetStatus("已载入 " + entry.time + " 的数值（还没保存，保存后生效）");
            }
            if (_pendingDelete >= 0 && _pendingDelete < list.Count)
            {
                list.RemoveAt(_pendingDelete);
                TurnTuningHistory.Save(_history);
                _compareIndex = -1;
                SetStatus("已删除一条历史记录");
            }
            if (_pendingCompare >= 0 && _pendingCompare < list.Count && _file != null)
            {
                if (_compareIndex == _pendingCompare) _compareIndex = -1; // 再点一次收起
                else
                {
                    _compareIndex = _pendingCompare;
                    var lines = new List<string>();
                    TurnTuningHistory.Diff(list[_compareIndex].values, _file.values, lines);
                    _compareText = lines.Count == 0 ? "和当前面板完全相同" : "从这一版到当前面板（" + lines.Count + " 项）：\n" + TurnTuningHistory.Join(lines, TurnTuningHistory.MaxDetailLines);
                }
            }
            _pendingLoad = _pendingDelete = _pendingCompare = -1;
        }

        private void DrawHistory()
        {
            if (_history == null) _history = TurnTuningHistory.Load();
            List<TurnTuningHistoryEntry> list = _history.entries;
            GUILayout.Label("每次保存（有改动时）自动记一条，最多 " + TurnTuningHistory.MaxEntries + " 条。载入某一版后要再保存才生效。", _hint);
            GUILayout.Label("文件：" + TurnTuningHistory.FilePath, _hint);
            if (_lastSaved != null)
            {
                // 「当前面板 vs 上次保存」每秒最多算一次
                if (Event.current.type == EventType.Layout && Time.unscaledTime - _unsavedCountTime > 1f)
                {
                    _unsavedCountTime = Time.unscaledTime;
                    _unsavedCount = TurnTuningHistory.CountDiff(_lastSaved, _file.values);
                }
                GUILayout.Label(_unsavedCount > 0 ? "当前面板比上次保存多改了 " + _unsavedCount + " 项（还没保存）" : "当前面板和上次保存一致", _label);
            }
            if (list.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("还没有历史记录：改完参数点保存就会记下第一条。", _label);
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                TurnTuningHistoryEntry entry = list[i];
                string title = (i == list.Count - 1 ? "【最新】" : "") + entry.time
                               + (string.IsNullOrEmpty(entry.note) ? "" : "  「" + entry.note + "」")
                               + "  " + (entry.changeCount < 0 ? "首次保存" : "改了 " + entry.changeCount + " 项")
                               + (entry.useTuning ? "" : "（面板数值关闭）");
                if (Section("hist." + entry.time + "#" + i, title, false))
                {
                    GUILayout.Label(entry.changeCount < 0 ? "首次保存，没有可比较的上一版。" :
                        (string.IsNullOrEmpty(entry.changes) ? "（只改了开关）" : entry.changes), _wrap);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("载入这一版", GUILayout.Height(24f))) _pendingLoad = i;
                    if (GUILayout.Button(_compareIndex == i ? "收起对比" : "和当前面板对比", GUILayout.Height(24f))) _pendingCompare = i;
                    if (GUILayout.Button("删除", GUILayout.Width(60f), GUILayout.Height(24f))) _pendingDelete = i;
                    GUILayout.EndHorizontal();
                    if (_compareIndex == i) GUILayout.Label(_compareText, _wrap);
                }
            }
        }

        private void ClearEdits()
        {
            _text.Clear();
            _dirty = true;
            GUIUtility.keyboardControl = 0;
        }

        private void DrawFields(TurnTuningData d)
        {
            if (Section("camera", "相机 / 2.5D 视角（只影响画面；面板打开时实时预览，保存后下一局沿用）"))
            {
                // 旧存档没有这一组（version = 0）：换成默认值 = 原来的正俯视
                if (d.camera == null || d.camera.version <= 0) d.camera = new CameraRigSettings();
                CameraRigSettings c = d.camera;
                c.pitch = F("cam.pitch", "倾斜角（度，0 = 原来的正俯视）", c.pitch, 0f, 70f);
                c.perspective = Toggle("透视投影（近大远小；关 = 正交）", c.perspective);
                // 三行一直显示（IMGUI 同一帧里控件数量不能变）
                c.orthographicSize = F("cam.size", "正交：画面半高（原来 7）", c.orthographicSize, 2f, 30f);
                c.fieldOfView = F("cam.fov", "透视：竖直视角（度）", c.fieldOfView, 10f, 90f);
                c.distance = F("cam.dist", "透视：相机距离（越大越远）", c.distance, 3f, 80f);
                c.lookAhead = F("cam.ahead", "注视点往前（地图上方）偏移", c.lookAhead, -5f, 5f);
                c.standUp = Toggle("倾斜时玩家 / 怪物立起来面向镜头", c.standUp);
                c.standLift = F("cam.lift", "立起物体离地高度", c.standLift, -1f, 2f);
                c.depthSort = Toggle("倾斜时按前后遮挡（画面靠下的挡住靠上的）", c.depthSort);
                c.hardFollow = Toggle("硬跟随（玩家永远在画面正中，原作做法；关 = 按灵敏度平滑）", c.hardFollow);
                c.sharpness = F("cam.sharp", "跟随灵敏度（硬跟随关闭时）", c.sharpness, 0.5f, 30f);
                GUILayout.Label("—— 纸片角色（倾斜且立起来时生效）——", _wrap);
                c.vertical = Toggle("竖直站立（立牌垂直于地面，原作做法；关 = 正对镜头）", c.vertical);
                c.stretch = Toggle("竖直站立时竖向拉长 1/sin(倾斜角)（45° = √2）", c.stretch);
                c.autoFoot = Toggle("自动脚底（贴图底边落在地面上）", c.autoFoot);
                c.paperTurn = Toggle("纸片翻面（转身时绕竖轴转过去，代替左右镜像）", c.paperTurn);
                c.playerTurnSpeed = F("cam.pTurn", "玩家转身速度（度/秒，原作 270）", c.playerTurnSpeed, 30f, 2000f);
                c.monsterTurnSpeed = F("cam.mTurn", "怪物转身速度（度/秒，原作 180）", c.monsterTurnSpeed, 30f, 2000f);
                c.turnAngle = F("cam.tAngle", "斜向移动多转的角度（度，原作 30）", c.turnAngle, 0f, 80f);
                c.playerDiagonalMin = F("cam.pDiagMin", "玩家斜向阈值下限（|x|，原作 0.5）", c.playerDiagonalMin, 0f, 1f);
                c.playerDiagonalMax = F("cam.pDiagMax", "玩家斜向阈值上限（原作 0.8）", c.playerDiagonalMax, 0f, 1f);
                c.monsterDiagonalMin = F("cam.mDiagMin", "怪物斜向阈值下限（原作 0.2）", c.monsterDiagonalMin, 0f, 1f);
                c.monsterDiagonalMax = F("cam.mDiagMax", "怪物斜向阈值上限（原作 0.7）", c.monsterDiagonalMax, 0f, 1f);
                GUILayout.Label("—— 脚下阴影（倾斜时；倾斜角 0 始终是圆影）——", _wrap);
                c.silhouetteShadow = Toggle("剪影投影（当前帧染黑铺在地面，翻面时跟着变窄）", c.silhouetteShadow);
                c.blobShadow = Toggle("圆形脚底阴影", c.blobShadow);
                c.shadowDirection = F("cam.shDir", "剪影倒向（度，0 = 右，90 = 画面里侧）", c.shadowDirection, -180f, 180f);
                c.shadowLength = F("cam.shLen", "剪影长度（× 角色高度）", c.shadowLength, 0f, 3f);
                c.shadowAlpha = F("cam.shAlpha", "剪影浓度", c.shadowAlpha, 0f, 1f);
                GUILayout.Label("提示：玩法判定不变（伤害范围、拾取、碰撞都还是地面上的圆）。默认照搬《通神榜》：透视 42°、俯角 45°、距离 10；视角 40° / 距离 20 ≈ 原来的正交 7。", _wrap);
            }
            if (Section("level", "关卡 / 波次"))
            {
                // 旧存档没有这一组（version = 0）：换成默认值，保存后生效；version 1（亚空间之前）补上新字段
                if (d.level == null || d.level.version <= 0) d.level = new LevelSettings();
                d.level.Upgrade();
                d.level.endless = Toggle("无尽生存（永不过关）", d.level.endless);
                d.level.waveSeconds = F("lv.wave", "每波时长（秒，0 = 永远第 1 波）", d.level.waveSeconds, 0f, 180f);
                d.level.surviveSeconds = F("lv.survive", "旧流程：每关存活多少秒过关（关掉无尽才生效）", d.level.surviveSeconds, 0f, 900f);
            }
            if (Section("player", "主角"))
            {
                PlayerSettings p = d.player;
                p.moveSpeed = F("p.speed", "移动速度（1 单位 = 40 像素）", p.moveSpeed, 0.5f, 15f);
                p.maxHp = F("p.hp", "1 级生命（之后每级成长见「经验曲线」）", p.maxHp, 1f, 500f);
                p.radius = F("p.radius", "碰撞半径", p.radius, 0.05f, 2f);
                p.hurtInvincibleTime = F("p.hurtInv", "受伤无敌帧（秒）", p.hurtInvincibleTime, 0f, 2f);
                p.gravityExitProtectionTime = F("p.gravityExit", "黑洞退出保护（秒）", p.gravityExitProtectionTime, 0f, 2f);
            }
            if (Section("roll", "翻滚 / 亚空间"))
            {
                RollSettings r = d.roll;
                r.waterGunCooldown = F("r.cdWater", "水枪：翻滚冷却（秒）", r.waterGunCooldown, 0f, 10f);
                r.spearCooldown = F("r.cdSpear", "投矛：翻滚冷却（秒）", r.spearCooldown, 0f, 10f);
                r.bladeCooldown = F("r.cdBlade", "陌刀：翻滚冷却（秒）", r.bladeCooldown, 0f, 10f);
                if (!WeaponKinds.IsRetired(WeaponKind.Palm)) r.palmCooldown = F("r.cdPalm", "掌：翻滚冷却（秒）", r.palmCooldown, 0f, 10f);
                r.crossbowCooldown = F("r.cdBow", "弩：翻滚冷却（秒）", r.crossbowCooldown, 0f, 10f);
                r.pokerCooldown = F("r.cdPoker", "扑克牌：翻滚冷却（秒）", r.pokerCooldown, 0f, 10f);
                r.shurikenCooldown = F("r.cdShuriken", "手里剑：翻滚冷却（秒）", r.shurikenCooldown, 0f, 10f);
                if (!WeaponKinds.IsRetired(WeaponKind.Stapler)) r.staplerCooldown = F("r.cdStapler", "订书机：翻滚冷却（秒）", r.staplerCooldown, 0f, 10f);
                r.swordCooldown = F("r.cdSword", "剑：翻滚冷却（秒）", r.swordCooldown, 0f, 10f);
                r.lightningCooldown = F("r.cdLightning", "闪电：翻滚冷却（秒）", r.lightningCooldown, 0f, 10f);
                r.subspaceDuration = F("r.subDur", "亚空间时长（现实秒）", r.subspaceDuration, 0.05f, 5f);
                r.subspaceTimeScale = F("r.subScale", "亚空间时间倍率", r.subspaceTimeScale, 0f, 1f);
                r.duration = F("r.dur", "翻滚位移时长（游戏秒）", r.duration, 0.05f, 1.5f);
                r.distance = F("r.dist", "翻滚距离", r.distance, 0f, 10f);
                r.pokerDurationMultiplier = F("r.pkDur", "扑克牌位移耗时倍率", r.pokerDurationMultiplier, 0.1f, 2f);
                r.crossbowDurationMultiplier = F("r.bwDur", "弩位移耗时倍率", r.crossbowDurationMultiplier, 0.1f, 2f);
            }
            DrawCombat(d);
            if (Section("shark", "鲨鱼（海洋地图，位面机制开关打开时才有）"))
            {
                SharkSettings s = d.shark;
                s.spawnCount = I("s.count", "数量", s.spawnCount, 0, 10);
                s.moveSpeed = F("s.speed", "移动速度", s.moveSpeed, 0f, 15f);
                s.contactDamage = F("s.dmg", "接触伤害", s.contactDamage, 0f, 100f);
                s.attackInterval = F("s.interval", "攻击间隔（秒）", s.attackInterval, 0.05f, 5f);
                s.radius = F("s.radius", "碰撞半径", s.radius, 0.05f, 3f);
                s.minSpawnDistance = F("s.spawnDist", "生成时离玩家最小距离", s.minSpawnDistance, 0f, 20f);
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

        /// <summary>新策划案（11/24）的战斗分组：三把武器、怪物与波次、精英 / Boss、词条表、宝箱、经验。（程序B）</summary>
        private void DrawCombat(TurnTuningData d)
        {
            if (d.arsenal == null) d.arsenal = new ArsenalConfig();
            d.arsenal.Normalize();
            if (d.horde == null) d.horde = new HordeConfig();
            if (d.builds == null) d.builds = new BuildConfig();
            ArsenalConfig a = d.arsenal;
            if (Section("water", "水枪"))
            {
                WaterGunConfig w = a.WaterGun;
                w.Ammo = I("wg.ammo", "弹药上限", w.Ammo, 1, 200);
                w.FireInterval = F("wg.interval", "攻击间隔（秒 / 发）", w.FireInterval, 0.03f, 3f);
                w.BulletSpeed = F("wg.speed", "水弹速度", w.BulletSpeed, 0.5f, 40f);
                w.Damage = F("wg.dmg", "水弹伤害", w.Damage, 0f, 200f);
                w.Range = F("wg.range", "索敌 / 射程", w.Range, 1f, 30f);
                w.PuddleRadius = F("wg.puddle", "水渍半径（1 = 40 像素）", w.PuddleRadius, 0.1f, 5f);
                w.FountainDamage = F("wg.fountain", "喷泉伤害", w.FountainDamage, 0f, 300f);
                w.OverlapBonus = F("wg.overlap", "重叠加成（每多一滩）", w.OverlapBonus, 0f, 2f);
                w.SpreadDegrees = F("wg.spread", "散射夹角（度）", w.SpreadDegrees, 0f, 60f);
            }
            if (Section("spear", "投矛"))
            {
                SpearConfig s = a.Spear;
                s.Ammo = I("sp2.ammo", "弹药上限", s.Ammo, 1, 200);
                s.FireInterval = F("sp2.interval", "攻击间隔（秒 / 发）", s.FireInterval, 0.03f, 3f);
                s.Speed = F("sp2.speed", "长矛速度", s.Speed, 0.5f, 40f);
                s.Damage = F("sp2.dmg", "长矛伤害", s.Damage, 0f, 200f);
                s.Range = F("sp2.range", "索敌 / 射程", s.Range, 1f, 30f);
                s.PierceFalloff = F("sp2.falloff", "每次穿透伤害衰减", s.PierceFalloff, 0f, 1f);
                s.RecallDamage = F("sp2.recall", "回收伤害 / 根", s.RecallDamage, 0f, 300f);
                s.RecallWidth = F("sp2.width", "回收路径半宽", s.RecallWidth, 0.05f, 3f);
                s.StackBonus = F("sp2.stack", "每多一层矛的回收加成", s.StackBonus, 0f, 2f);
                s.RecallKillSpearCount = I("sp2.seedCount", "回收击杀衍生矛数量", s.RecallKillSpearCount, 0, 20);
                s.RecallKillTargetRange = F("sp2.seedRange", "衍生矛索敌范围", s.RecallKillTargetRange, 0f, 40f);
            }
            if (Section("blade", "陌刀"))
            {
                BladeConfig b = a.Blade;
                b.Ammo = I("bl.ammo", "弹药上限", b.Ammo, 1, 200);
                b.FireInterval = F("bl.interval", "攻击间隔（秒 / 发）", b.FireInterval, 0.03f, 3f);
                b.Damage = F("bl.dmg", "剑气伤害", b.Damage, 0f, 200f);
                b.WaveSpeed = F("bl.speed", "剑气弹速度", b.WaveSpeed, 0.5f, 40f);
                b.WaveHalfWidth = F("bl.width", "剑气弹半宽（月牙宽度的一半）", b.WaveHalfWidth, 0.05f, 5f);
                b.Range = F("bl.range", "剑气射程（7.5 = 300 像素）", b.Range, 1f, 30f);
                b.SlashDamage = F("bl.slash", "一刀斩基础伤害", b.SlashDamage, 0f, 500f);
                b.MarkBonus = F("bl.mark", "每层印记增伤", b.MarkBonus, 0f, 1f);
                b.SlashHalfHeight = F("bl.height", "斩击半高", b.SlashHalfHeight, 0.1f, 10f);
            }
            if (!WeaponKinds.IsRetired(WeaponKind.Palm) && Section("palm", "掌")) // 11/27 掌已下架：面板不再显示
            {
                PalmConfig p = a.Palm;
                p.Ammo = I("pm2.ammo", "弹药上限", p.Ammo, 1, 200);
                p.FireInterval = F("pm2.interval", "攻击间隔（秒 / 发）", p.FireInterval, 0.03f, 3f);
                p.Damage = F("pm2.dmg", "掌击伤害", p.Damage, 0f, 200f);
                GUILayout.Label("掌击为 360° 圆形，命中范围内全部目标（保留遮挡判定）");
                p.Range = F("pm2.range", "掌击距离", p.Range, 0.5f, 15f);
                p.Knockback = F("pm2.knock", "击退距离", p.Knockback, 0f, 10f);
                p.SkyRadius = F("pm2.skyR", "天降掌半径", p.SkyRadius, 0.5f, 20f);
                p.SkyDamage = F("pm2.skyDmg", "天降掌伤害", p.SkyDamage, 0f, 500f);
                p.MarkBonus = F("pm2.mark", "每层掌印增伤", p.MarkBonus, 0f, 2f);
            }
            if (Section("crossbow2", "弩"))
            {
                CrossbowConfig c = a.Crossbow;
                c.Ammo = I("bw.ammo", "弹药上限", c.Ammo, 1, 20);
                c.FireInterval = F("bw.interval", "攻击间隔（秒 / 发）", c.FireInterval, 0.03f, 3f);
                c.Speed = F("bw.speed", "弩箭速度", c.Speed, 0.5f, 60f);
                c.Damage = F("bw.dmg", "弩箭伤害", c.Damage, 0f, 500f);
                c.Range = F("bw.range", "索敌 / 射程", c.Range, 1f, 40f);
                GUILayout.Label("弩箭贯穿敌人，同一颗箭对同一目标仅伤害一次；飞行距离由剑气射程倍率决定");
                c.PierceFalloff = F("bw.falloff", "每次穿透伤害衰减", c.PierceFalloff, 0f, 1f);
                c.Knockback = F("bw.knock", "击退距离", c.Knockback, 0f, 15f);
                c.WallStun = F("bw.stun", "撞墙眩晕（秒）", c.WallStun, 0f, 10f);
                c.Radius = F("bw.radius", "基础弩箭碰撞半径", c.Radius, .05f, 5f);
                c.KillsPerSizeStep = I("bw.kills", "每次尺寸成长所需击杀", c.KillsPerSizeStep, 1, 100);
                c.SizePerKillStep = F("bw.growth", "每段增加基础尺寸比例", c.SizePerKillStep, 0f, 1f);
                c.MaxSizeMultiplier = F("bw.sizeCap", "最终尺寸倍率上限", c.MaxSizeMultiplier, 1f, 10f);
                c.SwordWaveRangeMultiplier = F("bw.waveRange", "射程/剑气射程倍率", c.SwordWaveRangeMultiplier, .1f, 10f);
                c.SplashRadius = F("bw.splash", "杀气溢出溅射半径", c.SplashRadius, 0.1f, 10f);
            }
            if (Section("drones", "副武器 · 浮游炮"))
            {
                DronesConfig dr = a.Drones;
                dr.StartCount = I("dr.start", "开局台数", dr.StartCount, 1, 64);
                dr.SplitAdd = I("dr.add", "分裂一次 +几台", dr.SplitAdd, 0, 16);
                dr.MergeRemove = I("dr.remove", "合体一次 −几台", dr.MergeRemove, 0, 16);
                dr.MinCount = I("dr.min", "合体最少保留几台", dr.MinCount, 1, 16);
                dr.MaxCount = I("dr.cap", "台数上限（0 = 不限）", dr.MaxCount, 0, 256);
                dr.Damage = F("dr.dmg", "单台每发伤害", dr.Damage, 0f, 100f);
                dr.FireInterval = F("dr.interval", "攻击间隔（秒）", dr.FireInterval, 0.05f, 5f);
                dr.Range = F("dr.range", "索敌范围", dr.Range, 1f, 30f);
                dr.BulletSpeed = F("dr.speed", "子弹速度", dr.BulletSpeed, 0.5f, 40f);
                dr.OrbitRadius = F("dr.orbit", "环绕半径", dr.OrbitRadius, 0.2f, 6f);
                dr.MaxActive = I("dr.max", "同时开火台数上限（性能）", dr.MaxActive, 1, 128);
                dr.SplitChance = F("dr.split", "翻滚分裂基础概率", dr.SplitChance, 0f, 1f);
            }
            if (Section("gravity", "副武器 · 引力黑洞"))
            {
                GravityWellConfig g = a.Gravity;
                g.Radius = F("gw.radius", "引力场半径", g.Radius, 0.5f, 20f);
                g.PullDistance = F("gw.pull", "每秒牵引距离", g.PullDistance, 0f, 20f);
                g.MinGap = F("gw.gap", "拉到离中心多近停", g.MinGap, 0f, 5f);
                g.DamagePerSecond = F("gw.dps", "黑洞每秒伤害", g.DamagePerSecond, 0f, 200f);
                g.MergeSpeed = F("gw.mergeSpeed", "相向移动速度", g.MergeSpeed, .1f, 40f);
                g.PickupRadius = F("gw.pickupRadius", "经验球吸取范围", g.PickupRadius, 0f, 30f);
                g.PickupSpeed = F("gw.pickupSpeed", "经验球吸取速度", g.PickupSpeed, 0f, 40f);
                g.SecondaryRadius = F("gw.secR", "次级引力点半径", g.SecondaryRadius, 0.5f, 10f);
                g.SecondaryPull = F("gw.secPull", "次级引力点拉近距离", g.SecondaryPull, 0f, 10f);
                g.SecondaryOffset = F("gw.secOff", "次级引力点离中心距离", g.SecondaryOffset, 0f, 10f);
            }
            if (Section("anchor", "副武器 · 空间锚"))
            {
                SpaceAnchorConfig an = a.Anchor;
                an.SlashDamage = F("an.dmg", "连线斩击伤害", an.SlashDamage, 0f, 500f);
                an.Width = F("an.width", "斩击路径半宽", an.Width, 0.05f, 5f);
            }
            if (Section("poker", "扑克牌"))
            {
                PokerConfig p = a.Poker;
                p.HandSize = 1;
                GUILayout.Label("手牌固定一张：按抽牌间隔持续换牌，空格打出当前牌");
                p.DrawInterval = F("pk.draw", "抽牌间隔（秒）", p.DrawInterval, 0.05f, 10f);
                p.HeartsPerPoint = I("pk.hpp", "♥ 每点几发爱心弹", p.HeartsPerPoint, 0, 20);
                p.HeartDamage = F("pk.hdmg", "♥ 爱心弹伤害", p.HeartDamage, 0f, 100f);
                p.HeartSpeed = F("pk.hspd", "♥ 爱心弹速度", p.HeartSpeed, 0.5f, 40f);
                p.HeartRange = F("pk.hrange", "♥ 爱心弹射程", p.HeartRange, 1f, 60f);
                p.HeartBounces = I("pk.hbounce", "♥ 可反弹次数", p.HeartBounces, 0, 20);
                p.HeartSpread = F("pk.hspread", "♥ 扇形张角（度）", p.HeartSpread, 0f, 360f);
                p.HeartHomingTurn = F("pk.hhoming", "♥ 追踪转向速度（度 / 秒，0 = 不追踪）", p.HeartHomingTurn, 0f, 1440f);
                p.HeartHomingRange = F("pk.hhrange", "♥ 追踪索敌半径", p.HeartHomingRange, 0f, 30f);
                p.HeartBlastRadius = F("pk.hblastR", "♥ 命中爆炸半径（0 = 不爆炸）", p.HeartBlastRadius, 0f, 5f);
                p.HeartBlastRatio = F("pk.hblast", "♥ 爆炸伤害 = 爱心弹伤害 ×", p.HeartBlastRatio, 0f, 5f);
                p.MaxHearts = I("pk.hmax", "♥ 一次爆发最多几发（性能）", p.MaxHearts, 1, 2000);
                p.SpadeDamagePerPoint = F("pk.sdmg", "♠ 每点伤害", p.SpadeDamagePerPoint, 0f, 100f);
                p.SpadeRadius = F("pk.srad", "♠ 基础半径", p.SpadeRadius, 0.1f, 10f);
                p.SpadeRadiusPerPoint = F("pk.sradp", "♠ 每点半径", p.SpadeRadiusPerPoint, 0f, 2f);
                p.SpadeChildCount = I("pk.children", "♠ 二段小炸弹数量", p.SpadeChildCount, 0, 20);
                p.SpadeChildDamageRatio = F("pk.childDamage", "♠ 单颗小炸弹伤害倍率", p.SpadeChildDamageRatio, 0f, 3f);
                p.SpadeChildRadiusRatio = F("pk.childRadius", "♠ 小炸弹半径倍率", p.SpadeChildRadiusRatio, .05f, 2f);
                p.SpadeChildOffsetRatio = F("pk.childOffset", "♠ 小炸弹散布距离倍率", p.SpadeChildOffsetRatio, 0f, 2f);
                p.SpadeChildDelay = F("pk.childDelay", "♠ 二段爆炸延迟秒", p.SpadeChildDelay, 0f, 2f);
                p.ShieldMultiplier = F("pk.shieldMultiplier", "♦ 护盾量倍率", p.ShieldMultiplier, 0f, 3f);
                p.JokerChanceMultiplier = F("pk.jokerChance", "大小王概率倍率", p.JokerChanceMultiplier, 0f, 2f);
                p.ClubAspectRatio = F("pk.eyeAspect", "♣ 眼状裂隙短长轴比", p.ClubAspectRatio, .05f, 1f);
                p.ShieldMax = I("pk.shield", "♦ 护盾最多几层", p.ShieldMax, 1, 200);
                p.ClubRadius = F("pk.crad", "♣ 裂隙半径", p.ClubRadius, 0.2f, 10f);
                p.ClubDps = F("pk.cdps", "♣ 裂隙每秒伤害", p.ClubDps, 0f, 200f);
                p.ClubPullSpeed = F("pk.cpull", "♣ 吸附速度", p.ClubPullSpeed, 0f, 20f);
                p.ClubDuration = F("pk.cdur", "♣ 基础持续（秒）", p.ClubDuration, 0f, 20f);
                p.ClubDurationPerPoint = F("pk.cdurp", "♣ 每点持续（秒）", p.ClubDurationPerPoint, 0f, 5f);
                p.SmallJokerBonus = F("pk.small", "小王：永久加成", p.SmallJokerBonus, 0f, 1f);
                p.BigJokerBonus = F("pk.big", "大王：永久加成", p.BigJokerBonus, 0f, 1f);
                p.TargetRange = F("pk.range", "♠ / ♣ 目标搜索范围", p.TargetRange, 1f, 40f);
            }
            if (Section("shuriken", "手里剑"))
            {
                ShurikenConfig s = a.Shuriken;
                s.Ammo = I("sk.ammo", "弹药上限", s.Ammo, 1, 100);
                s.FireInterval = F("sk.interval", "发射间隔（秒 / 发）", s.FireInterval, 0.03f, 5f);
                s.Speed = F("sk.speed", "飞行速度", s.Speed, 0.5f, 40f);
                s.FlyDistance = F("sk.fly", "飞行距离", s.FlyDistance, 0.2f, 20f);
                s.SpinRadius = F("sk.spinR", "旋转判定半径", s.SpinRadius, 0.1f, 5f);
                s.SpinDamage = F("sk.spinDmg", "旋转每次伤害", s.SpinDamage, 0f, 100f);
                s.SpinInterval = F("sk.spinInt", "旋转伤害间隔（秒）", s.SpinInterval, 0.05f, 3f);
                s.RecallDamage = F("sk.recall", "回收每枚伤害", s.RecallDamage, 0f, 300f);
                s.RecallWidth = F("sk.recallW", "回收路径半宽", s.RecallWidth, 0.05f, 5f);
                s.OrbitRadius = F("sk.orbitR", "环绕半径（方案 A）", s.OrbitRadius, 0.3f, 6f);
                s.OrbitSpeed = F("sk.orbitS", "环绕角速度", s.OrbitSpeed, 0f, 20f);
                s.MaxOnField = I("sk.max", "场上最多几枚（性能）", s.MaxOnField, 1, 300);
                s.TargetRange = F("sk.range", "索敌范围", s.TargetRange, 1f, 30f);
            }
            if (!WeaponKinds.IsRetired(WeaponKind.Stapler) && Section("stapler", "订书机")) // 11/27 订书机已下架：面板不再显示
            {
                StaplerConfig s = a.Stapler;
                s.Ammo = I("st.ammo", "弹药上限", s.Ammo, 1, 200);
                s.FireInterval = F("st.interval", "发射间隔（秒 / 发）", s.FireInterval, 0.03f, 5f);
                s.Damage = F("st.dmg", "订书钉伤害", s.Damage, 0f, 200f);
                s.Speed = F("st.speed", "订书钉速度", s.Speed, 0.5f, 40f);
                s.Range = F("st.range", "射程 / 索敌", s.Range, 1f, 30f);
                s.Pierce = I("st.pierce", "可命中敌人数", s.Pierce, 1, 20);
                s.Knockback = F("st.knock", "击退距离", s.Knockback, 0f, 10f);
                s.GroupMax = I("st.group", "一组最多钉合几只", s.GroupMax, 2, 30);
                s.GroupSlow = F("st.slow", "钉合减速比例", s.GroupSlow, 0f, 0.95f);
                s.GroupSlowTime = F("st.slowT", "钉合减速时长（秒）", s.GroupSlowTime, 0.1f, 30f);
                s.SecondaryCount = I("st.count", "每只散射几枚次级钉", s.SecondaryCount, 1, 30);
                s.SecondaryDamage = F("st.sdmg", "次级钉伤害", s.SecondaryDamage, 0f, 200f);
                s.MarkBonus = F("st.mark", "每层钉痕散射加成", s.MarkBonus, 0f, 3f);
                s.SecondaryRange = F("st.srange", "次级钉射程", s.SecondaryRange, 0.5f, 20f);
                s.MaxSecondary = I("st.smax", "一次爆发最多几枚（性能）", s.MaxSecondary, 1, 3000);
            }
            if (Section("sword", "剑"))
            {
                SwordConfig s = a.Sword;
                s.Ammo = I("sw.ammo", "突进次数上限（弹药）", s.Ammo, 1, 100);
                s.FireInterval = F("sw.interval", "突进间隔（秒）", s.FireInterval, 0.05f, 5f);
                s.DashDistance = F("sw.dist", "突进距离", s.DashDistance, 0.2f, 15f);
                s.DashTime = F("sw.time", "突进用时（秒，期间无敌）", s.DashTime, 0.02f, 2f);
                s.Width = F("sw.width", "刺击判定半宽", s.Width, 0.05f, 5f);
                s.Damage = F("sw.dmg", "刺击伤害", s.Damage, 0f, 300f);
                s.TargetRange = F("sw.range", "面向多远有敌人才突进", s.TargetRange, 0.5f, 20f);
                GUILayout.Label("沿最近移动朝向突进：无怪物也可正常出手");
                s.ShadowDamage = F("sw.shadow", "影刺伤害", s.ShadowDamage, 0f, 500f);
                s.TrailBonus = F("sw.trail", "每条剑痕影刺加成", s.TrailBonus, 0f, 1f);
                s.KillsPerCharge = F("sw.kills", "影刺击杀几名 +1 突进次数", s.KillsPerCharge, 0.2f, 20f);
                s.MaxTrails = I("sw.maxTrails", "剑痕最多几条（性能）", s.MaxTrails, 1, 300);
            }
            if (Section("hourglass", "副武器 · 时间沙漏"))
            {
                HourglassConfig hg = a.Hourglass;
                hg.SecondsToFloor = F("hg.floorTime", "线性降至下限所需秒数", hg.SecondsToFloor, .1f, 30f);
                hg.FloorSpeed = F("hg.floor", "最低移速下限", hg.FloorSpeed, 0.05f, 1f);
            }
            if (!SubWeaponKinds.IsHiddenInPanels(SubWeaponKind.Wasd) && Section("wasd", "副武器 · WASD")) // 11/27 WASD 已下架：面板不再显示
            {
                WasdConfig w = a.Wasd;
                w.Damage = F("wd.dmg", "按键伤害", w.Damage, 0f, 200f);
                w.Distance = F("wd.dist", "判定区离玩家距离", w.Distance, 0f, 10f);
                w.Radius = F("wd.radius", "判定区半径", w.Radius, 0.1f, 10f);
                w.HitCooldown = F("wd.cd", "每只敌人几秒最多吃 1 次", w.HitCooldown, 0f, 20f);
                w.BurstDamage = F("wd.bdmg", "四向齐发伤害", w.BurstDamage, 0f, 500f);
                w.BurstDistance = F("wd.bdist", "四向齐发距离", w.BurstDistance, 0f, 10f);
                w.BurstRadius = F("wd.bradius", "四向齐发半径", w.BurstRadius, 0.1f, 10f);
            }
            if (Section("magnet", "黑洞 · 合并爆炸（旧磁铁配置）"))
            {
                MagnetConfig m = a.Magnet;
                m.PathDamage = F("mg.path", "路径伤害", m.PathDamage, 0f, 500f);
                m.PathWidth = F("mg.width", "路径半宽", m.PathWidth, 0.05f, 5f);
                m.BlastDamage = F("mg.blast", "中点爆炸伤害", m.BlastDamage, 0f, 1000f);
                m.BlastRadius = F("mg.radius", "爆炸半径", m.BlastRadius, 0.3f, 15f);
                m.StunTime = F("mg.stun", "爆炸眩晕（秒）", m.StunTime, 0f, 10f);
                m.SpawnPullRadius = F("mg.spawnR", "「磁场」吸附半径", m.SpawnPullRadius, 0.3f, 10f);
                m.PathPullRadius = F("mg.pathR", "「磁流」路径两侧拉扯范围", m.PathPullRadius, 0.3f, 10f);
            }
            if (Section("lightning", "闪电"))
            {
                LightningConfig lt = a.Lightning;
                lt.Ammo = I("lt.ammo", "弹药上限", lt.Ammo, 1, 200);
                lt.FireInterval = F("lt.interval", "攻击间隔（秒 / 发）", lt.FireInterval, 0.03f, 5f);
                lt.Damage = F("lt.dmg", "闪电命中伤害（每只）", lt.Damage, 0f, 200f);
                lt.Range = F("lt.range", "第一下的射程 / 索敌", lt.Range, 1f, 30f);
                lt.ChainCount = I("lt.chain", "一道闪电最多命中几只（含第一下）", lt.ChainCount, 1, 30);
                lt.JumpRange = F("lt.jump", "连锁跳跃距离", lt.JumpRange, 0.3f, 20f);
                lt.DetonateDamage = F("lt.det", "每层雷痕引爆伤害", lt.DetonateDamage, 0f, 500f);
                lt.InfectionRange = F("lt.infect", "传染：向多远内最近的敌人发闪电", lt.InfectionRange, 0.5f, 40f);
                lt.MaxDetonations = I("lt.maxDet", "一次翻滚最多引爆几只（性能）", lt.MaxDetonations, 1, 3000);
                lt.MaxInfections = I("lt.maxInf", "一次翻滚最多传染几次（性能）", lt.MaxInfections, 0, 3000);
            }
            if (Section("cursor", "副武器 · 光标"))
            {
                CursorConfig c = a.Cursor;
                c.Damage = F("cs.dmg", "降维打击伤害", c.Damage, 0f, 1000f);
                c.PinTime = F("cs.pin", "定身时长（秒）", c.PinTime, 0f, 10f);
                c.MaxLength = F("cs.len", "矩形对角线最长距离", c.MaxLength, 1f, 200f);
                c.Thickness = F("cs.thick", "边界厚度", c.Thickness, 0.05f, 5f);
                c.SampleSpacing = F("cs.spacing", "轨迹采样间距", c.SampleSpacing, 0.05f, 3f);
            }
            if (Section("arsenal", "武器共用"))
            {
                a.FixedDamageMultiplier = F("ar.fixedDamage", "固定伤害倍率（不含最大生命百分比）", a.FixedDamageMultiplier, 0f, 5f);
                a.CritMultiplier = F("ar.crit", "暴击倍率", a.CritMultiplier, 1f, 5f);
                a.BurstRingRadius = F("ar.ring", "爆发一圈的半径（词条没配时）", a.BurstRingRadius, 0.5f, 15f);
                a.HurtMarkRange = F("ar.hurt", "受伤叠印记的搜索半径", a.HurtMarkRange, 0.5f, 30f);
            }

            HordeConfig h = d.horde;
            if (Section("horde", "怪物与波次"))
            {
                h.NormalChaseSpeedMultiplier = F("hd.chaseMult", "普通小怪追击速度倍率", h.NormalChaseSpeedMultiplier, 0f, 2f);
                h.HpPerWave = F("hd.hp", "每波血量 +", h.HpPerWave, 0f, 1f);
                h.DamagePerWave = F("hd.dmg", "每波伤害 +", h.DamagePerWave, 0f, 1f);
                h.SpawnInterval = F("hd.interval", "刷怪间隔（秒）", h.SpawnInterval, 0.1f, 20f);
                h.BaseCount = F("hd.base", "第 1 波每批数量", h.BaseCount, 0f, 30f);
                h.CountPerWave = F("hd.perWave", "每波每批 +", h.CountPerWave, 0f, 10f);
                h.MaxAlive = I("hd.max", "存活上限", h.MaxAlive, 1, 500);
                h.MinPlayerDistance = F("hd.minDist", "刷新点离玩家最小距离", h.MinPlayerDistance, 0f, 30f);
                h.Radius = F("hd.radius", "怪物碰撞半径", h.Radius, 0.05f, 2f);
                h.SporeBulletSpeed = F("hd.sporeV", "弹幕孢：弹幕速度", h.SporeBulletSpeed, 0.1f, 20f);
                h.ChargeSpeed = F("hd.chargeV", "疾冲兽：冲锋速度", h.ChargeSpeed, 0.5f, 30f);
                h.ChargeMaxDistance = F("hd.chargeD", "疾冲兽：冲锋最远距离", h.ChargeMaxDistance, 0.5f, 40f);
                h.PoolRadius = F("hd.poolR", "毒腐体：毒潭半径", h.PoolRadius, 0.1f, 10f);
                h.PoolDuration = F("hd.poolT", "毒腐体：毒潭持续（秒）", h.PoolDuration, 0.1f, 30f);
                h.PoolDamage = F("hd.poolDmg", "毒腐体：毒潭伤害 / 次", h.PoolDamage, 0f, 50f);
                h.PoolInterval = F("hd.poolI", "毒腐体：毒潭结算间隔", h.PoolInterval, 0.05f, 5f);
                h.SniperMinRange = F("hd.snMin", "狙击械：保持的最近距离", h.SniperMinRange, 0f, 30f);
                h.SniperMaxRange = F("hd.snMax", "狙击械：保持的最远距离", h.SniperMaxRange, 0f, 40f);
                h.LaserWidth = F("hd.laserW", "狙击械：激光半宽", h.LaserWidth, 0.01f, 3f);
            }
            if (h.Kinds != null)
            {
                for (int i = 0; i < h.Kinds.Length && i < MonsterKinds.HordeCount; i++)
                {
                    HordeKindConfig k = h.Kinds[i];
                    if (k == null) continue;
                    string prefix = "hk" + i;
                    if (!Section(prefix, "怪物 · " + MonsterKinds.DisplayName((MonsterKind)((int)MonsterKinds.First + i)))) continue;
                    k.MaxHp = F(prefix + ".hp", "生命（首次出现那一波）", k.MaxHp, 1f, 2000f);
                    k.Speed = F(prefix + ".speed", "移动速度", k.Speed, 0f, 15f);
                    k.Damage = F(prefix + ".dmg", "伤害", k.Damage, 0f, 200f);
                    k.AttackInterval = F(prefix + ".interval", "攻击 / 接触间隔（秒）", k.AttackInterval, 0.05f, 20f);
                    k.Windup = F(prefix + ".windup", "蓄力（秒）", k.Windup, 0f, 5f);
                    k.Range = F(prefix + ".range", "触发距离", k.Range, 0f, 30f);
                    k.Xp = I(prefix + ".xp", "掉落经验", k.Xp, 0, 200);
                    k.DebutWave = I(prefix + ".debut", "第几波出现", k.DebutWave, 1, 100);
                    k.Weight = F(prefix + ".weight", "刷新权重", k.Weight, 0f, 20f);
                }
            }
            if (Section("elite", "精英 / Boss"))
            {
                h.EliteFromWave = I("el.from", "精英：从第几波开始（0 = 不刷）", h.EliteFromWave, 0, 100);
                h.EliteHp = F("el.hp", "精英：生命倍率", h.EliteHp, 1f, 50f);
                h.EliteDamage = F("el.dmg", "精英：伤害倍率", h.EliteDamage, 1f, 10f);
                h.EliteScale = F("el.scale", "精英：体型倍率", h.EliteScale, 1f, 4f);
                h.EliteXp = I("el.xp", "精英：经验倍率", h.EliteXp, 1, 50);
                h.BossEveryWaves = I("bo.every", "Boss：每几波一只（0 = 不刷）", h.BossEveryWaves, 0, 100);
                h.BossHp = F("bo.hp", "Boss：生命倍率", h.BossHp, 1f, 200f);
                h.BossHpMultiplier = F("bo.hpExtra", "Boss：额外生命倍率", h.BossHpMultiplier, 0f, 5f);
                h.BossDamage = F("bo.dmg", "Boss：伤害倍率", h.BossDamage, 1f, 10f);
                h.BossScale = F("bo.scale", "Boss：体型倍率", h.BossScale, 1f, 5f);
                h.BossXp = I("bo.xp", "Boss：经验倍率", h.BossXp, 1, 200);
            }

            BuildConfig bc = d.builds;
            if (bc.Builds == null || bc.Builds.Length != BuildIds.Count) bc.Builds = Turn.Weapons.BuildCatalog.Normalize(bc.Builds);
            if (Section("chest", "升级 / 宝箱"))
            {
                bc.OptionCount = I("ch.options", "候选数", bc.OptionCount, 1, 3);
                bc.ChestLevels = I("ch.levels", "宝箱强化一次给几层", bc.ChestLevels, 1, 5);
                bc.CurseBonusLevels = I("ch.bonus", "代价选项：随机给几层", bc.CurseBonusLevels, 1, 10);
                bc.FrailMaxHp = F("ch.frail", "血契：每层最大生命 −", bc.FrailMaxHp, 0f, 100f);
                bc.FuryDamage = F("ch.fury", "狂怒：每层怪物伤害 +", bc.FuryDamage, 0f, 2f);
                bc.HasteSpeed = F("ch.haste", "疾行：每层怪物移速 +", bc.HasteSpeed, 0f, 2f);
            }
            if (bc.Builds != null)
            {
                // 分组：5 把主武器 + 3 把副武器
                for (int w = 0; w < WeaponKinds.Count + SubWeaponKinds.Count - 1; w++)
                {
                    bool isSub = w >= WeaponKinds.Count;
                    WeaponKind weapon = isSub ? WeaponKind.WaterGun : (WeaponKind)w;
                    SubWeaponKind subKind = isSub ? (SubWeaponKind)(w - WeaponKinds.Count + 1) : SubWeaponKind.None;
                    string owner = isSub ? SubWeaponKinds.DisplayName(subKind) : WeaponKinds.DisplayName(weapon);
                    if (!isSub && WeaponKinds.IsRetired(weapon)) continue; // 11/27 已下架的主武器不显示词条表
                    if (isSub && SubWeaponKinds.IsHiddenInPanels(subKind)) continue; // 11/27 已下架的副武器（WASD）不显示词条表
                    if (!Section("bd" + w, "词条表 · " + owner + "（每层数值 / 最大层数）")) continue;
                    for (int i = 0; i < bc.Builds.Length && i < BuildIds.Count; i++)
                    {
                        var id = (BuildId)i;
                        BuildDef def = bc.Builds[i];
                        if (def == null) continue;
                        if (isSub ? BuildIds.SubOf(id) != subKind : (BuildIds.IsSub(id) || BuildIds.WeaponOf(id) != weapon)) continue;
                        string name = Turn.Weapons.BuildCatalog.Name(id);
                        def.Value = F("bd" + i + ".v", name + "：每层", def.Value, 0f, Mathf.Max(2f, def.Value * 4f));
                        def.MaxLevel = I("bd" + i + ".max", name + "：最大层数", def.MaxLevel, 0, 10);
                    }
                }
            }

            if (Section("experience", "经验曲线"))
            {
                ExperienceSettings e = d.experience;
                if (e != null)
                {
                    e.firstLevelXp = I("xp.first", "首级经验（策划 50）", e.firstLevelXp, 1, 500);
                    e.xpGrowthRate = F("xp.rate", "每级递增比例（策划 0.2）", e.xpGrowthRate, 0f, 1f);
                    e.pickupRange = F("xp.pickup", "拾取范围（2.5 = 100 像素）", e.pickupRange, 0.1f, 20f);
                    e.chestOpenRadius = F("xp.chest", "靠近多近自动开宝箱", e.chestOpenRadius, 0.1f, 5f);
                    e.maxHpPerLevel = F("xp.hpLv", "每级生命上限 +（1 级生命在「主角」里）", e.maxHpPerLevel, 0f, 50f);
                    e.maxHpCap = F("xp.hpCap", "生命上限封顶（0 = 不封顶）", e.maxHpCap, 0f, 1000f);
                }
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

        private bool Toggle(string label, bool value)
        {
            bool next = GUILayout.Toggle(value, " " + label, GUILayout.Height(22f));
            if (next != value) _dirty = true;
            return next;
        }

        private bool Section(string key, string title)
        {
            return Section(key, title, true);
        }

        private bool Section(string key, string title, bool defaultOpen)
        {
            bool open;
            if (!_folds.TryGetValue(key, out open)) open = defaultOpen;
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
            _wrap = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _wrap.normal.textColor = new Color(0.82f, 0.84f, 0.88f);

            _panelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _panelTex.SetPixel(0, 0, new Color(0.07f, 0.07f, 0.09f, 0.94f));
            _panelTex.Apply();
            _panelTex.hideFlags = HideFlags.DontSave;
            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = _panelTex;
        }
    }
}