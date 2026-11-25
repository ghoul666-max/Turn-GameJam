using System;
using System.Collections.Generic;
using UnityEngine;
using Turn.Core;
using Turn.Weapons;

namespace Turn.View
{
    /// <summary>
    /// 强化词条面板（调试 · IMGUI，F7 开关，打开时游戏暂停）：
    /// 直接给这一局加 / 减任意词条的层数，不用等升级 / 宝箱。
    ///   · 默认只列这一局出战装备用得上的词条（= 升级三选一的候选池），按武器 / 副武器分组；
    ///   · 取消「只看出战装备」可以列出全部词条（没带的装备上的词条加了也不生效，会标灰）；
    ///   · 每行：− / + 一层、满级、清零；下面一行是当前层数的效果说明（0 层时显示 1 层的效果）。
    /// 只通过 IProgressionSystem.DebugSetLevel 改层数，数值本身在 F1 数值面板的词条表里调。
    /// </summary>
    public sealed class BuildPickerPanel : MonoBehaviour
    {
        private Func<ITurnContext> _context;
        private Func<bool> _blocked;
        private bool _open;
        private bool _carriedOnly = true;
        private string _filter = "";
        // 勾选 / 搜索框改动会改变控件数量：先记下来，下一次 Layout 再生效（同一帧各事件的控件数量必须一致）
        private bool _carriedOnlyNext = true;
        private string _filterNext = "";
        private Vector2 _scroll;
        private readonly Dictionary<string, bool> _folds = new Dictionary<string, bool>();
        private readonly List<KeyValuePair<string, bool>> _foldRequests = new List<KeyValuePair<string, bool>>();
        private readonly List<BuildId> _rows = new List<BuildId>();

        private GUIStyle _title;
        private GUIStyle _header;
        private GUIStyle _label;
        private GUIStyle _dim;
        private GUIStyle _hint;
        private GUIStyle _panel;
        private Texture2D _panelTex;

        /// <summary>面板是否正在显示（GameManager 据此暂停模拟、屏蔽玩法输入）。</summary>
        public bool IsOpen { get { return _open && !IsBlocked; } }

        private bool IsBlocked { get { return _blocked != null && _blocked(); } }

        /// <summary>GameManager 调用：context = 当前一局；blocked = 别的面板（F1）打开时让位。</summary>
        public void Setup(Func<ITurnContext> context, Func<bool> blocked)
        {
            _context = context;
            _blocked = blocked;
        }

        private void OnDestroy()
        {
            if (_panelTex != null) Destroy(_panelTex);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F7 && !IsBlocked)
            {
                _open = !_open;
                GUIUtility.keyboardControl = 0;
                e.Use();
                return; // 控件数量变了：下一帧 Layout 重新排版
            }
            if (e.type == EventType.Layout)
            {
                for (int i = 0; i < _foldRequests.Count; i++) _folds[_foldRequests[i].Key] = _foldRequests[i].Value;
                _foldRequests.Clear();
                _carriedOnly = _carriedOnlyNext;
                _filter = _filterNext;
            }
            if (IsBlocked) return;

            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 820f, 1f, 3f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            ITurnContext ctx = _context != null ? _context() : null;
            if (!_open || ctx == null)
            {
                GUI.Label(new Rect(w - 150f, h - 48f, 140f, 22f), "F7：强化词条", _hint);
                GUI.matrix = oldMatrix;
                return;
            }

            float pw = Mathf.Min(620f, w - 20f);
            Rect r = new Rect(10f, 10f, pw, h - 20f);
            GUI.Box(r, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f));
            DrawToolbar(ctx);
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawGroups(ctx);
            GUILayout.Space(10f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        private void DrawToolbar(ITurnContext ctx)
        {
            GUILayout.Label("强化词条（调试 · 游戏已暂停 · F7 关闭）", _title);
            GUILayout.Label("出战：" + LoadoutName(ctx.Loadout) + "    等级 " + ctx.Progression.Level, _label);

            GUILayout.BeginHorizontal();
            _carriedOnlyNext = GUILayout.Toggle(_carriedOnlyNext, " 只看出战装备的词条", GUILayout.Width(170f));
            GUILayout.Label("搜索", _label, GUILayout.Width(36f));
            _filterNext = GUILayout.TextField(_filterNext ?? "", GUILayout.Width(160f));
            if (GUILayout.Button("清空", GUILayout.Width(50f)))
            {
                _filterNext = "";
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("出战词条全部满级", GUILayout.Height(24f))) SetAll(ctx, true, true);
            if (GUILayout.Button("出战词条全部清零", GUILayout.Height(24f))) SetAll(ctx, true, false);
            if (GUILayout.Button("全部词条清零", GUILayout.Height(24f))) SetAll(ctx, false, false);
            GUILayout.EndHorizontal();
            GUILayout.Label("层数改动立即生效；每层数值 / 最大层数在 F1 数值面板的词条表里调。", _hint);
            GUILayout.Space(4f);
        }

        /// <summary>分组：5 把主武器 + 3 把副武器；只看出战装备时没带的组整组跳过。</summary>
        private void DrawGroups(ITurnContext ctx)
        {
            IProgressionSystem p = ctx.Progression;
            BuildDef[] defs = ctx.Config.Builds.Builds;
            string filter = (_filter ?? "").Trim();
            int shown = 0;

            for (int g = 0; g < WeaponKinds.Count + SubWeaponKinds.Count - 1; g++)
            {
                bool isSub = g >= WeaponKinds.Count;
                WeaponKind weapon = isSub ? WeaponKind.WaterGun : (WeaponKind)g;
                SubWeaponKind sub = isSub ? (SubWeaponKind)(g - WeaponKinds.Count + 1) : SubWeaponKind.None;

                _rows.Clear();
                int levels = 0;
                bool carried = false;
                for (int i = 0; i < BuildIds.Count; i++)
                {
                    var id = (BuildId)i;
                    if (isSub ? BuildIds.SubOf(id) != sub : (BuildIds.IsSub(id) || BuildIds.WeaponOf(id) != weapon)) continue;
                    carried = p.IsCarried(id);
                    if (filter.Length > 0 && BuildCatalog.Name(id).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _rows.Add(id);
                    levels += p.GetLevel(id);
                }
                if (_rows.Count == 0 || (_carriedOnly && !carried)) continue;

                string owner = isSub ? SubWeaponKinds.DisplayName(sub) : WeaponKinds.DisplayName(weapon);
                string title = owner + (carried ? "" : "（未携带，不生效）") + "    已加 " + levels + " 层";
                if (!Section("g" + g, title, carried)) continue;

                for (int k = 0; k < _rows.Count; k++)
                {
                    DrawRow(p, defs, _rows[k], carried);
                    shown++;
                }
                GUILayout.Space(6f);
            }
            if (shown == 0 && filter.Length > 0) GUILayout.Label("没有名字包含「" + filter + "」的词条", _dim);
        }

        private void DrawRow(IProgressionSystem p, BuildDef[] defs, BuildId id, bool carried)
        {
            int level = p.GetLevel(id);
            int max = p.MaxLevel(id);
            BuildDef def = defs != null && (int)id < defs.Length ? defs[(int)id] : null;
            GUIStyle style = carried ? _label : _dim;

            GUILayout.BeginHorizontal();
            GUILayout.Label((level > 0 ? "● " : "○ ") + BuildCatalog.Name(id), style, GUILayout.Width(170f));
            GUILayout.Label(level + " / " + max, style, GUILayout.Width(56f));
            GUI.enabled = level > 0;
            if (GUILayout.Button("−", GUILayout.Width(30f))) p.DebugSetLevel(id, level - 1);
            GUI.enabled = level < max;
            if (GUILayout.Button("+", GUILayout.Width(30f))) p.DebugSetLevel(id, level + 1);
            if (GUILayout.Button("满级", GUILayout.Width(46f))) p.DebugSetLevel(id, max);
            GUI.enabled = level > 0;
            if (GUILayout.Button("清零", GUILayout.Width(46f))) p.DebugSetLevel(id, 0);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            string desc = BuildCatalog.Describe(id, def, Math.Max(1, level));
            GUILayout.Label("      " + (level > 0 ? desc : "1 层：" + desc), _dim);
        }

        private static void SetAll(ITurnContext ctx, bool carriedOnly, bool max)
        {
            IProgressionSystem p = ctx.Progression;
            for (int i = 0; i < BuildIds.Count; i++)
            {
                var id = (BuildId)i;
                if (carriedOnly && !p.IsCarried(id)) continue;
                p.DebugSetLevel(id, max ? p.MaxLevel(id) : 0);
            }
        }

        private static string LoadoutName(ILoadoutSystem loadout)
        {
            if (loadout.Scheme == LoadoutScheme.DualMain)
                return WeaponKinds.DisplayName(loadout.Equipped) + " / " + WeaponKinds.DisplayName(loadout.Standby);
            return loadout.Sub != SubWeaponKind.None
                ? WeaponKinds.DisplayName(loadout.Equipped) + " + " + SubWeaponKinds.DisplayName(loadout.Sub)
                : WeaponKinds.DisplayName(loadout.Equipped);
        }

        /// <summary>可折叠的分组标题；出战装备的组默认展开，没带的默认折叠。</summary>
        private bool Section(string key, string title, bool defaultOpen)
        {
            bool open;
            if (!_folds.TryGetValue(key, out open)) open = defaultOpen;
            bool clicked = GUILayout.Toggle(open, (open ? "[-] " : "[+] ") + title, _header);
            if (clicked != open) _foldRequests.Add(new KeyValuePair<string, bool>(key, clicked));
            return open;
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
            _dim = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _dim.normal.textColor = new Color(0.62f, 0.64f, 0.68f);
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
