using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Turn.View
{
    /// <summary>数值面板的一条历史记录 = 一次保存时的完整数值快照 + 和上一次保存相比改了什么。</summary>
    [Serializable]
    public sealed class TurnTuningHistoryEntry
    {
        /// <summary>保存时间（本地时间，yyyy-MM-dd HH:mm:ss）。</summary>
        public string time = "";
        /// <summary>保存时填的备注（可以为空）。</summary>
        public string note = "";
        /// <summary>和上一次保存相比改了几项（首次保存 = -1）。</summary>
        public int changeCount;
        /// <summary>改动明细，每行一项：「路径：旧值 → 新值」（最多 MaxDetailLines 行）。</summary>
        public string changes = "";
        public bool useTuning = true;
        public TurnTuningData values = new TurnTuningData();
    }

    [Serializable]
    public sealed class TurnTuningHistoryFile
    {
        public int version = 1;
        public List<TurnTuningHistoryEntry> entries = new List<TurnTuningHistoryEntry>();
    }

    /// <summary>
    /// 数值面板的历史记录（F1 面板「历史记录」）。每次「保存」自动追加一条，最多保留 MaxEntries 条（最旧的先丢）。
    ///   编辑器：工程根目录的 UserSettings/TurnTuningHistory.json（不在 Assets 里：不会被导入、不会打进包、默认不进版本库）；
    ///   打包后：persistentDataPath/TurnTuningHistory.json。
    /// 改动明细：把前后两份 TurnTuningData 转成 JSON 再拍平成「字段路径 → 值」逐项比较，所以新加的参数不用额外登记。
    /// </summary>
    public static class TurnTuningHistory
    {
        public const int MaxEntries = 50;
        public const int MaxDetailLines = 60;

        public static string FilePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "UserSettings", "TurnTuningHistory.json");
#else
                return Path.Combine(Application.persistentDataPath, "TurnTuningHistory.json");
#endif
            }
        }

        public static TurnTuningHistoryFile Load()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    var file = JsonUtility.FromJson<TurnTuningHistoryFile>(File.ReadAllText(path));
                    if (file != null)
                    {
                        if (file.entries == null) file.entries = new List<TurnTuningHistoryEntry>();
                        return file;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 读取数值面板历史记录失败：" + e.Message);
            }
            return new TurnTuningHistoryFile();
        }

        public static bool Save(TurnTuningHistoryFile file)
        {
            if (file == null) return false;
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(file, false));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 保存数值面板历史记录失败：" + e.Message);
                return false;
            }
        }

        /// <summary>保存成功后调用：previous = 上一次保存的数值（没有则 null）。返回新加的那条。</summary>
        public static TurnTuningHistoryEntry Record(TurnTuningHistoryFile history, TurnTuningData previous, TurnTuningData current,
            bool useTuning, bool previousUseTuning, string note)
        {
            if (history == null || current == null) return null;
            var entry = new TurnTuningHistoryEntry
            {
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                note = note ?? "",
                useTuning = useTuning,
                values = current.Clone(),
            };
            if (previous == null)
            {
                entry.changeCount = -1;
                entry.changes = "";
            }
            else
            {
                var lines = new List<string>();
                if (useTuning != previousUseTuning) lines.Add("使用面板数值：" + (previousUseTuning ? "开" : "关") + " → " + (useTuning ? "开" : "关"));
                Diff(previous, current, lines);
                entry.changeCount = lines.Count;
                entry.changes = Join(lines, MaxDetailLines);
            }
            history.entries.Add(entry);
            while (history.entries.Count > MaxEntries) history.entries.RemoveAt(0);
            Save(history);
            return entry;
        }

        /// <summary>a → b 的逐项改动（「路径：旧值 → 新值」）。</summary>
        public static void Diff(TurnTuningData a, TurnTuningData b, List<string> lines)
        {
            Dictionary<string, string> fa = Flatten(a);
            Dictionary<string, string> fb = Flatten(b);
            var keys = new List<string>(fb.Keys);
            foreach (string k in fa.Keys) if (!fb.ContainsKey(k)) keys.Add(k);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                string va, vb;
                fa.TryGetValue(k, out va);
                fb.TryGetValue(k, out vb);
                if (va == vb) continue;
                lines.Add(k + "：" + (va ?? "（无）") + " → " + (vb ?? "（无）"));
            }
        }

        /// <summary>两份数值之间改了几项（不生成明细）。</summary>
        public static int CountDiff(TurnTuningData a, TurnTuningData b)
        {
            var lines = new List<string>();
            Diff(a, b, lines);
            return lines.Count;
        }

        public static string Join(List<string> lines, int max)
        {
            var sb = new StringBuilder();
            int n = Math.Min(lines.Count, max);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }
            if (lines.Count > max) sb.Append("\n…… 还有 ").Append(lines.Count - max).Append(" 项");
            return sb.ToString();
        }

        // ---------------- JSON 拍平（只认 JsonUtility 输出的格式） ----------------

        public static Dictionary<string, string> Flatten(TurnTuningData data)
        {
            var result = new Dictionary<string, string>();
            if (data == null) return result;
            string json = JsonUtility.ToJson(data);
            int i = 0;
            try
            {
                ReadValue(json, ref i, "", result);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Turn] 数值面板历史：解析失败 " + e.Message);
            }
            return result;
        }

        private static void ReadValue(string s, ref int i, string path, Dictionary<string, string> result)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) return;
            char c = s[i];
            if (c == '{')
            {
                i++;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return; }
                while (i < s.Length)
                {
                    SkipSpace(s, ref i);
                    string key = ReadString(s, ref i);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    ReadValue(s, ref i, path.Length > 0 ? path + "." + key : key, result);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return; }
                    return;
                }
            }
            else if (c == '[')
            {
                i++;
                int index = 0;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return; }
                while (i < s.Length)
                {
                    ReadValue(s, ref i, path + "[" + index + "]", result);
                    index++;
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return; }
                    return;
                }
            }
            else if (c == '"')
            {
                result[path] = "\"" + ReadString(s, ref i) + "\"";
            }
            else
            {
                int start = i;
                while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i])) i++;
                result[path] = s.Substring(start, i - start);
            }
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    sb.Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                sb.Append(s[i]);
                i++;
            }
            i++; // 收尾的引号
            return sb.ToString();
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
