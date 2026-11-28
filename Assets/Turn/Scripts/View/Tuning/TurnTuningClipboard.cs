using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Turn.View
{
    /// <summary>
    /// 数值面板的剪贴板导入 / 导出（11/28）。
    ///   · 复制全部：{"format":"TurnTuning","kind":"full",...,"values":{ 全部分组 }}；
    ///   · 复制改动项：只含和「默认值」不同的字段（kind = "changes"）。数组里只改了几项时写成 {"下标": 值}，例如 "Builds":{"12":{"Value":0.2}}；
    ///   · 读取：只覆盖剪贴板里有的字段，其余保持面板当前值。也认整份 TurnTuning.json（取 values）、或者直接贴的分组（如 {"player":{...}}）。
    ///     剪贴板前后有聊天里的说明文字 / ```json 代码块也没关系：取第一个「{」到最后一个「}」。
    /// 剪贴板用 GUIUtility.systemCopyBuffer（编辑器和 Windows 打包后都能用）。
    /// 这里自带一个小 JSON 读写器（只做读写和合并，不依赖第三方库）；合并完再交给 JsonUtility 转回 TurnTuningData。
    /// </summary>
    public static class TurnTuningClipboard
    {
        public const string FormatName = "TurnTuning";

        /// <summary>读取结果：Data = 合并后的完整数值；Error 非空 = 读不了；Ignored = 不认识 / 类型不对、被跳过的字段。</summary>
        public sealed class ReadResult
        {
            public TurnTuningData Data;
            public string Error;
            public string Source;
            public readonly List<string> Ignored = new List<string>();
        }

        // ================================================================
        // 导出
        // ================================================================

        /// <summary>复制全部数值。返回写进剪贴板的字数。</summary>
        public static int CopyAll(TurnTuningData values)
        {
            Node tree = Parse(JsonUtility.ToJson(values ?? new TurnTuningData()));
            string text = Wrap("full", "全部数值；贴回时会覆盖这里列出的全部字段", tree);
            GUIUtility.systemCopyBuffer = text;
            return text.Length;
        }

        /// <summary>复制和默认值不同的字段。返回改动的字段数（0 = 没有改动，剪贴板不动）。</summary>
        public static int CopyChanges(TurnTuningData values, TurnTuningData defaults)
        {
            Node cur = Parse(JsonUtility.ToJson(values ?? new TurnTuningData()));
            Node def = Parse(JsonUtility.ToJson(defaults ?? new TurnTuningData()));
            int count = 0;
            Node diff = SparseDiff(cur, def, ref count);
            if (diff == null || count == 0) return 0;
            GUIUtility.systemCopyBuffer = Wrap("changes", "只含和默认值不同的 " + count + " 项；贴回时只覆盖这些项", diff);
            return count;
        }

        private static string Wrap(string kind, string note, Node values)
        {
            var root = new Node(NodeType.Object);
            root.Set("format", Node.Str(FormatName));
            root.Set("kind", Node.Str(kind));
            root.Set("time", Node.Str(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            root.Set("note", Node.Str(note));
            root.Set("values", values);
            var sb = new StringBuilder();
            Write(root, sb, 0);
            return sb.ToString();
        }

        /// <summary>只保留 cur 里和 def 不同的部分；没有差别返回 null。</summary>
        private static Node SparseDiff(Node cur, Node def, ref int count)
        {
            if (cur == null) return null;
            if (def == null || def.Type != cur.Type)
            {
                count += Leaves(cur);
                return cur;
            }
            switch (cur.Type)
            {
                case NodeType.Object:
                {
                    Node result = null;
                    for (int i = 0; i < cur.Keys.Count; i++)
                    {
                        string key = cur.Keys[i];
                        Node d = SparseDiff(cur.Values[i], def.Get(key), ref count);
                        if (d == null) continue;
                        if (result == null) result = new Node(NodeType.Object);
                        result.Set(key, d);
                    }
                    return result;
                }
                case NodeType.Array:
                {
                    if (cur.Items.Count != def.Items.Count)
                    {
                        count += Leaves(cur);
                        return cur;
                    }
                    Node result = null;
                    for (int i = 0; i < cur.Items.Count; i++)
                    {
                        Node d = SparseDiff(cur.Items[i], def.Items[i], ref count);
                        if (d == null) continue;
                        if (result == null) result = new Node(NodeType.Object); // 稀疏数组：{"下标": 值}
                        result.Set(i.ToString(CultureInfo.InvariantCulture), d);
                    }
                    return result;
                }
                default:
                    if (cur.Text == def.Text) return null;
                    count++;
                    return cur;
            }
        }

        private static int Leaves(Node n)
        {
            if (n == null) return 0;
            if (n.Type == NodeType.Object) { int c = 0; foreach (Node v in n.Values) c += Leaves(v); return c; }
            if (n.Type == NodeType.Array) { int c = 0; foreach (Node v in n.Items) c += Leaves(v); return c; }
            return 1;
        }

        // ================================================================
        // 读取
        // ================================================================

        /// <summary>读剪贴板并合并到 current 上（不改 current）。</summary>
        public static ReadResult Read(TurnTuningData current)
        {
            var result = new ReadResult();
            string text = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                result.Error = "剪贴板是空的";
                return result;
            }
            int first = text.IndexOf('{');
            int last = text.LastIndexOf('}');
            if (first < 0 || last <= first)
            {
                result.Error = "剪贴板里不是数值（没有找到 { … }）";
                return result;
            }
            text = text.Substring(first, last - first + 1);

            Node root;
            try
            {
                root = Parse(text);
            }
            catch (FormatException e)
            {
                result.Error = "剪贴板里的内容格式不对：" + e.Message;
                return result;
            }
            if (root.Type != NodeType.Object)
            {
                result.Error = "剪贴板里不是数值（最外层不是 { }）";
                return result;
            }

            Node target = Parse(JsonUtility.ToJson(current ?? new TurnTuningData()));
            Node patch;
            Node format = root.Get("format");
            Node values = root.Get("values");
            if (format != null && format.Type == NodeType.String && format.Text == FormatName && values != null && values.Type == NodeType.Object)
            {
                Node kind = root.Get("kind");
                Node time = root.Get("time");
                result.Source = (kind != null && kind.Text == "changes" ? "面板导出的改动项" : "面板导出的全部数值")
                                + (time != null && time.Type == NodeType.String && time.Text.Length > 0 ? "（" + time.Text + "）" : "");
                patch = values;
            }
            else if (values != null && values.Type == NodeType.Object)
            {
                result.Source = "TurnTuning.json / 历史记录里的 values";
                patch = values;
            }
            else
            {
                bool known = false;
                for (int i = 0; i < root.Keys.Count && !known; i++) known = target.Get(root.Keys[i]) != null;
                if (!known)
                {
                    result.Error = "剪贴板里没有认得出的数值分组（例如 player / roll / arsenal / horde …）";
                    return result;
                }
                result.Source = "直接贴的分组";
                patch = root;
            }

            Merge(target, patch, "", result.Ignored);
            var sb = new StringBuilder();
            Write(target, sb, -1);
            try
            {
                result.Data = JsonUtility.FromJson<TurnTuningData>(sb.ToString());
            }
            catch (Exception e)
            {
                result.Error = "合并后转换失败：" + e.Message;
                return result;
            }
            if (result.Data == null) result.Error = "合并后转换失败";
            return result;
        }

        /// <summary>把 patch 里有的字段写进 target（就地修改）。不认识的字段 / 类型对不上的记进 ignored。</summary>
        private static void Merge(Node target, Node patch, string path, List<string> ignored)
        {
            for (int i = 0; i < patch.Keys.Count; i++)
            {
                string key = patch.Keys[i];
                string p = path.Length > 0 ? path + "." + key : key;
                Node src = patch.Values[i];
                if (target.Type == NodeType.Array)
                {
                    int index;
                    if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || index < 0 || index >= target.Items.Count)
                    {
                        ignored.Add(path + "[" + key + "]（下标超出范围）");
                        continue;
                    }
                    Node slot = target.Items[index];
                    Node merged = MergeValue(slot, src, path + "[" + index + "]", ignored);
                    if (merged != null) target.Items[index] = merged;
                    continue;
                }
                Node dst = target.Get(key);
                if (dst == null)
                {
                    ignored.Add(p + "（不认识的字段）");
                    continue;
                }
                Node value = MergeValue(dst, src, p, ignored);
                if (value != null) target.Set(key, value);
            }
        }

        /// <summary>返回要放进 dst 位置的新值；对象 / 稀疏数组就地合并后返回 dst；类型不对返回 null。</summary>
        private static Node MergeValue(Node dst, Node src, string path, List<string> ignored)
        {
            if (dst.Type == NodeType.Object)
            {
                if (src.Type != NodeType.Object) { ignored.Add(path + "（应该是一组 { }）"); return null; }
                Merge(dst, src, path, ignored);
                return dst;
            }
            if (dst.Type == NodeType.Array)
            {
                if (src.Type == NodeType.Array) return src; // 完整数组：整体替换（长度不对时面板会按默认值补齐）
                if (src.Type == NodeType.Object) { Merge(dst, src, path, ignored); return dst; } // 稀疏：{"下标": 值}
                ignored.Add(path + "（应该是列表 [ ] 或 {\"下标\": 值}）");
                return null;
            }
            bool ok = dst.Type == src.Type || (dst.Type == NodeType.Number && src.Type == NodeType.Bool) || (dst.Type == NodeType.Bool && src.Type == NodeType.Number);
            if (!ok) { ignored.Add(path + "（类型不对：" + src.Text + "）"); return null; }
            if (dst.Type == NodeType.Number && src.Type == NodeType.Bool) return Node.Raw(NodeType.Number, src.Text == "true" ? "1" : "0");
            if (dst.Type == NodeType.Bool && src.Type == NodeType.Number) return Node.Raw(NodeType.Bool, src.Text == "0" ? "false" : "true");
            return src;
        }

        // ================================================================
        // 小 JSON 读写器
        // ================================================================

        private enum NodeType { Object, Array, String, Number, Bool, Null }

        private sealed class Node
        {
            public readonly NodeType Type;
            public string Text = "";
            public readonly List<string> Keys = new List<string>();
            public readonly List<Node> Values = new List<Node>();
            public readonly List<Node> Items = new List<Node>();

            public Node(NodeType type) { Type = type; }

            public static Node Str(string s) { return new Node(NodeType.String) { Text = s ?? "" }; }
            public static Node Raw(NodeType type, string text) { return new Node(type) { Text = text }; }

            public Node Get(string key)
            {
                int i = Keys.IndexOf(key);
                return i >= 0 ? Values[i] : null;
            }

            public void Set(string key, Node value)
            {
                int i = Keys.IndexOf(key);
                if (i >= 0) Values[i] = value;
                else { Keys.Add(key); Values.Add(value); }
            }
        }

        private static Node Parse(string s)
        {
            int i = 0;
            Node n = ParseValue(s, ref i);
            SkipSpace(s, ref i);
            if (i < s.Length) throw Error(s, i, "结尾多了内容");
            return n;
        }

        private static Node ParseValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "内容不完整");
            char c = s[i];
            if (c == '{')
            {
                var obj = new Node(NodeType.Object);
                i++;
                while (true)
                {
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; return obj; }
                    if (i >= s.Length || s[i] != '"') throw Error(s, i, "这里应该是带引号的字段名");
                    string key = ParseString(s, ref i);
                    SkipSpace(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw Error(s, i, "字段名后面缺少冒号 :");
                    i++;
                    obj.Set(key, ParseValue(s, ref i));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; } // 允许最后一项后面多一个逗号
                    if (i < s.Length && s[i] == '}') { i++; return obj; }
                    throw Error(s, i, "缺少逗号 , 或右括号 }");
                }
            }
            if (c == '[')
            {
                var arr = new Node(NodeType.Array);
                i++;
                while (true)
                {
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; return arr; }
                    arr.Items.Add(ParseValue(s, ref i));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return arr; }
                    throw Error(s, i, "缺少逗号 , 或右括号 ]");
                }
            }
            if (c == '"') return Node.Str(ParseString(s, ref i));
            if (Match(s, i, "true")) { i += 4; return Node.Raw(NodeType.Bool, "true"); }
            if (Match(s, i, "false")) { i += 5; return Node.Raw(NodeType.Bool, "false"); }
            if (Match(s, i, "null")) { i += 4; return Node.Raw(NodeType.Null, "null"); }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            string num = s.Substring(start, i - start);
            double d;
            if (num.Length == 0 || !double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw Error(s, start, "看不懂的值");
            return Node.Raw(NodeType.Number, NormalizeNumber(num, d));
        }

        /// <summary>小数按 float 精度输出（4.199999809265137 → 4.2），整数原样。</summary>
        private static string NormalizeNumber(string num, double d)
        {
            if (num.IndexOf('.') < 0 && num.IndexOf('e') < 0 && num.IndexOf('E') < 0) return num;
            return ((float)d).ToString("R", CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // 开头的引号
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return sb.ToString(); }
                if (c == '\\' && i + 1 < s.Length)
                {
                    char e = s[i + 1];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            int code;
                            if (i + 5 < s.Length && int.TryParse(s.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                sb.Append((char)code);
                                i += 6;
                                continue;
                            }
                            throw Error(s, i, "\\u 后面应该是 4 位十六进制");
                        default: sb.Append(e); break;
                    }
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            throw Error(s, i, "字符串缺少结尾的引号");
        }

        private static bool Match(string s, int i, string word)
        {
            return string.CompareOrdinal(s, i, word, 0, word.Length) == 0;
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static FormatException Error(string s, int i, string what)
        {
            int line = 1, col = 1;
            for (int k = 0; k < i && k < s.Length; k++)
            {
                if (s[k] == '\n') { line++; col = 1; }
                else col++;
            }
            return new FormatException("第 " + line + " 行第 " + col + " 列：" + what);
        }

        /// <summary>indent &lt; 0 = 压缩成一行（内部用）；≥ 0 = 两格缩进、方便人看和改。</summary>
        private static void Write(Node n, StringBuilder sb, int indent)
        {
            bool pretty = indent >= 0;
            switch (n.Type)
            {
                case NodeType.Object:
                    if (n.Keys.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    for (int i = 0; i < n.Keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (pretty) NewLine(sb, indent + 1);
                        WriteString(n.Keys[i], sb);
                        sb.Append(pretty ? ": " : ":");
                        Write(n.Values[i], sb, pretty ? indent + 1 : -1);
                    }
                    if (pretty) NewLine(sb, indent);
                    sb.Append('}');
                    return;
                case NodeType.Array:
                    if (n.Items.Count == 0) { sb.Append("[]"); return; }
                    bool flat = true; // 纯数字 / 布尔列表写成一行
                    foreach (Node item in n.Items) if (item.Type == NodeType.Object || item.Type == NodeType.Array || item.Type == NodeType.String) flat = false;
                    sb.Append('[');
                    for (int i = 0; i < n.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(flat && pretty ? ", " : ",");
                        if (pretty && !flat) NewLine(sb, indent + 1);
                        Write(n.Items[i], sb, pretty ? indent + 1 : -1);
                    }
                    if (pretty && !flat) NewLine(sb, indent);
                    sb.Append(']');
                    return;
                case NodeType.String:
                    WriteString(n.Text, sb);
                    return;
                default:
                    sb.Append(n.Text);
                    return;
            }
        }

        private static void NewLine(StringBuilder sb, int indent)
        {
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        private static void WriteString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
