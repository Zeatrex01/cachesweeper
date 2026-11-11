// ============================================================================
//  DevSuite - MCP configuration read/write
//  Shared by McpControl and McpSetup.
//
//  Safety rules:
//    * a timestamped .bak copy is written before every change
//    * files are written to a temp file and swapped in (never half-written)
//    * newline style and BOM of the original file are preserved
//    * a file that cannot be parsed is reported, never rewritten
//    * server names are validated before they reach a config file
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace DevSuite
{
    public enum Format { Json, Toml }

    public class Client
    {
        public string Name;
        public string ConfigPath;
        public Format Format;
        public string Key;            // json: "mcpServers"
        public string DisabledKey;    // json: "mcpServersDisabled"
        public bool Exists { get { return File.Exists(ConfigPath); } }

        public static List<Client> All()
        {
            string U = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string R = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return new List<Client>
            {
                new Client { Name = "Claude Desktop",  Format = Format.Json,
                              ConfigPath = System.IO.Path.Combine(R, "Claude", "claude_desktop_config.json"),
                              Key = "mcpServers", DisabledKey = "mcpServersDisabled" },
                new Client { Name = "Claude Code",     Format = Format.Json,
                              ConfigPath = System.IO.Path.Combine(U, ".claude.json"),
                              Key = "mcpServers", DisabledKey = "mcpServersDisabled" },
                new Client { Name = "Codex / ChatGPT", Format = Format.Toml,
                              ConfigPath = System.IO.Path.Combine(U, ".codex", "config.toml") },
            };
        }
    }

    public class Server
    {
        public Client Client;
        public string Name;
        public string Command;
        public bool Enabled;
    }

    public static class Mcp
    {
        /// <summary>Config files that could not be parsed during the last ReadAll().</summary>
        public static readonly List<string> Errors = new List<string>();

        static readonly Regex NameRx = new Regex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

        /// <summary>Null when the name is safe for both JSON keys and TOML table names, otherwise the rule.</summary>
        public static string ValidateName(string name)
        {
            if (name == null || !NameRx.IsMatch(name))
                return "Use 1-64 letters, digits, '-' or '_' (no spaces or dots).";
            return null;
        }

        // ---------------- files ----------------
        public static string Backup(string path)
        {
            if (!File.Exists(path)) return null;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string dest = path + "." + stamp + ".bak";
            for (int i = 1; File.Exists(dest); i++) dest = path + "." + stamp + "-" + i + ".bak";
            File.Copy(path, dest, false);
            return dest;
        }

        class TextFile
        {
            public string Body = "";
            public string NewLine = "\n";
            public bool Bom;
        }

        static TextFile ReadText(string path)
        {
            var t = new TextFile();
            if (!File.Exists(path)) return t;
            var bytes = File.ReadAllBytes(path);
            t.Bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            t.Body = new UTF8Encoding(false).GetString(bytes, t.Bom ? 3 : 0, bytes.Length - (t.Bom ? 3 : 0));
            t.NewLine = t.Body.Contains("\r\n") ? "\r\n" : "\n";
            return t;
        }

        /// <summary>Backs the file up, writes the new text to a temp file and swaps it in.</summary>
        static void WriteText(string path, string body, TextFile original)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Backup(path);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, body, new UTF8Encoding(original.Bom));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // ---------------- api ----------------
        /// <summary>Servers in one client. Throws when the file exists but cannot be parsed.</summary>
        public static List<Server> Read(Client c)
        {
            if (!c.Exists) return new List<Server>();
            return c.Format == Format.Json ? JsonRead(c) : TomlRead(c);
        }

        /// <summary>Servers across all clients. Unparseable files are listed in Errors and skipped.</summary>
        public static List<Server> ReadAll()
        {
            Errors.Clear();
            var l = new List<Server>();
            foreach (var c in Client.All())
            {
                try { l.AddRange(Read(c)); }
                catch (Exception ex) { Errors.Add(c.Name + ": " + c.ConfigPath + " - " + ex.Message); }
            }
            return l;
        }

        public static void Toggle(Client c, string name, bool on)
        {
            if (c.Format == Format.Json) JsonToggle(c, name, on);
            else TomlToggle(c, name, on);
        }

        public static void Add(Client c, string name, string command, string[] args, Dictionary<string, string> env)
        {
            string bad = ValidateName(name);
            if (bad != null) throw new ArgumentException(bad);
            if (c.Format == Format.Json) JsonAdd(c, name, command, args, env);
            else TomlAdd(c, name, command, args, env);
        }

        // ======================= JSON =======================
        static JavaScriptSerializer Ser()
        {
            return new JavaScriptSerializer { MaxJsonLength = 200 * 1024 * 1024 };
        }

        static Dictionary<string, object> JsonRoot(TextFile t)
        {
            if (string.IsNullOrWhiteSpace(t.Body)) return new Dictionary<string, object>();
            var root = Ser().Deserialize<Dictionary<string, object>>(t.Body);
            if (root == null) throw new FormatException("top level is not a JSON object");
            return root;
        }

        static List<Server> JsonRead(Client c)
        {
            var items = new List<Server>();
            var root = JsonRoot(ReadText(c.ConfigPath));
            Action<string, bool> collect = (key, enabled) =>
            {
                object o;
                if (string.IsNullOrEmpty(key) || !root.TryGetValue(key, out o)) return;
                var d = o as Dictionary<string, object>;
                if (d == null) return;
                foreach (var kv in d)
                    items.Add(new Server { Client = c, Name = kv.Key, Enabled = enabled, Command = CommandSummary(kv.Value) });
            };
            collect(c.Key, true);
            collect(c.DisabledKey, false);
            return items;
        }

        static string CommandSummary(object v)
        {
            var d = v as Dictionary<string, object>;
            if (d == null) return "";
            var sb = new StringBuilder();
            object c, a, u;
            if (d.TryGetValue("command", out c) && c != null) sb.Append(c);
            if (d.TryGetValue("args", out a))
            {
                var al = a as System.Collections.ArrayList;
                if (al != null) foreach (var x in al) sb.Append(' ').Append(x);
                else { var oa = a as object[]; if (oa != null) foreach (var x in oa) sb.Append(' ').Append(x); }
            }
            if (sb.Length == 0 && d.TryGetValue("url", out u) && u != null) sb.Append(u);
            return sb.ToString().Trim();
        }

        static void JsonToggle(Client c, string name, bool on)
        {
            var file = ReadText(c.ConfigPath);
            var root = JsonRoot(file);
            string src = on ? c.DisabledKey : c.Key;
            string dest = on ? c.Key : c.DisabledKey;

            object so, dobj;
            var sd = root.TryGetValue(src, out so) ? so as Dictionary<string, object> : null;
            if (sd == null || !sd.ContainsKey(name)) throw new InvalidOperationException("\"" + name + "\" was not found in \"" + src + "\"");

            var dd = root.TryGetValue(dest, out dobj) ? dobj as Dictionary<string, object> : null;
            if (dd == null) { dd = new Dictionary<string, object>(); root[dest] = dd; }
            if (dd.ContainsKey(name))
                throw new InvalidOperationException("\"" + name + "\" exists in both \"" + c.Key + "\" and \"" + c.DisabledKey +
                                                    "\". Remove one of them in the file first; nothing was changed.");

            dd[name] = sd[name];
            sd.Remove(name);
            if (sd.Count == 0 && src == c.DisabledKey) root.Remove(src);

            WriteText(c.ConfigPath, JsonWrite(root, 0, file.NewLine) + file.NewLine, file);
        }

        static void JsonAdd(Client c, string name, string command, string[] args, Dictionary<string, string> env)
        {
            var file = ReadText(c.ConfigPath);
            var root = JsonRoot(file);
            object o;
            var d = root.TryGetValue(c.Key, out o) ? o as Dictionary<string, object> : null;
            if (d == null) { d = new Dictionary<string, object>(); root[c.Key] = d; }

            // A disabled copy with the same name would shadow the new entry; drop it.
            object dis;
            var dd = root.TryGetValue(c.DisabledKey, out dis) ? dis as Dictionary<string, object> : null;
            if (dd != null && dd.ContainsKey(name))
            {
                dd.Remove(name);
                if (dd.Count == 0) root.Remove(c.DisabledKey);
            }

            var entry = new Dictionary<string, object> { { "command", command } };
            if (args != null && args.Length > 0) entry["args"] = args.Cast<object>().ToArray();
            if (env != null && env.Count > 0)
            {
                var e = new Dictionary<string, object>();
                foreach (var kv in env) e[kv.Key] = kv.Value;
                entry["env"] = e;
            }
            d[name] = entry;

            WriteText(c.ConfigPath, JsonWrite(root, 0, file.NewLine) + file.NewLine, file);
        }

        // --- readable JSON writer, lossless for everything JavaScriptSerializer produces ---
        static string JsonWrite(object o, int level, string nl)
        {
            string inner = new string(' ', (level + 1) * 2), outer = new string(' ', level * 2);
            var d = o as Dictionary<string, object>;
            if (d != null)
            {
                if (d.Count == 0) return "{}";
                var sb = new StringBuilder("{" + nl);
                int i = 0;
                foreach (var kv in d)
                {
                    sb.Append(inner).Append(Quote(kv.Key)).Append(": ").Append(JsonWrite(kv.Value, level + 1, nl));
                    sb.Append(++i < d.Count ? "," + nl : nl);
                }
                return sb.Append(outer).Append('}').ToString();
            }
            var al = o as System.Collections.ArrayList;
            var ar = al != null ? al.ToArray() : o as object[];
            if (ar != null)
            {
                if (ar.Length == 0) return "[]";
                var sb = new StringBuilder("[" + nl);
                for (int i = 0; i < ar.Length; i++)
                {
                    sb.Append(inner).Append(JsonWrite(ar[i], level + 1, nl));
                    sb.Append(i < ar.Length - 1 ? "," + nl : nl);
                }
                return sb.Append(outer).Append(']').ToString();
            }
            if (o == null) return "null";
            if (o is bool) return ((bool)o) ? "true" : "false";
            if (o is double) return ((double)o).ToString("R", CultureInfo.InvariantCulture);
            if (o is float) return ((float)o).ToString("R", CultureInfo.InvariantCulture);
            if (o is int || o is long || o is decimal || o is short || o is byte)
                return Convert.ToString(o, CultureInfo.InvariantCulture);
            if (o is DateTime) return Quote(((DateTime)o).ToString("o", CultureInfo.InvariantCulture));
            return Quote(o.ToString());
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ======================= TOML =======================
        // Only the subset Codex writes is understood: [mcp_servers.<name>] tables,
        // optional [mcp_servers.<name>.env] sub-tables, "key = value" lines,
        // comments and multi-line arrays. Everything else is copied through untouched.
        static readonly Regex TableRx    = new Regex(@"^\s*\[mcp_servers\.(?<name>""[^""]+""|[^\].\s]+)\]\s*(#.*)?$", RegexOptions.Compiled);
        static readonly Regex SubTableRx = new Regex(@"^\s*\[mcp_servers\.(?<name>""[^""]+""|[^\].\s]+)\.[^\]]+\]\s*(#.*)?$", RegexOptions.Compiled);
        static readonly Regex EnabledRx  = new Regex(@"^(?<lead>\s*)enabled\s*=\s*(?<v>true|false)\s*(?<tail>#.*)?$", RegexOptions.Compiled);
        static readonly Regex CommandRx  = new Regex(@"^\s*command\s*=\s*(?:""(?<v>(?:[^""\\]|\\.)*)""|'(?<v>[^']*)')", RegexOptions.Compiled);
        static readonly Regex UrlRx      = new Regex(@"^\s*url\s*=\s*(?:""(?<v>(?:[^""\\]|\\.)*)""|'(?<v>[^']*)')", RegexOptions.Compiled);

        static string TableName(Match m) { return m.Groups["name"].Value.Trim('"'); }

        /// <summary>Net change in '[' / ']' depth on this line, ignoring strings and comments.</summary>
        static int BracketDelta(string line)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quote != '\0')
                {
                    if (ch == '\\' && quote == '"') { i++; continue; }
                    if (ch == quote) quote = '\0';
                    continue;
                }
                if (ch == '"' || ch == '\'') { quote = ch; continue; }
                if (ch == '#') break;
                if (ch == '[') depth++;
                else if (ch == ']') depth--;
            }
            return depth;
        }

        static bool IsHeader(string line, int depth)
        {
            return depth <= 0 && line.TrimStart().StartsWith("[");
        }

        /// <summary>Lines [start, end) that belong to the server, including its sub-tables. start is -1 when absent.</summary>
        static void FindBlock(List<string> lines, string name, out int start, out int end)
        {
            start = -1; end = lines.Count;
            int depth = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                bool header = IsHeader(lines[i], depth);
                if (header)
                {
                    var m = TableRx.Match(lines[i]);
                    if (start < 0)
                    {
                        if (m.Success && TableName(m) == name) start = i;
                    }
                    else
                    {
                        var sub = SubTableRx.Match(lines[i]);
                        if (!(sub.Success && TableName(sub) == name)) { end = i; return; }
                    }
                }
                depth = Math.Max(0, depth + BracketDelta(lines[i]));
            }
        }

        static string Unescape(string s)
        {
            return s.Replace("\\\\", "\\").Replace("\\\"", "\"");
        }

        static List<Server> TomlRead(Client c)
        {
            var items = new List<Server>();
            Server current = null;
            bool inSub = false;
            int depth = 0;
            foreach (var raw in ReadText(c.ConfigPath).Body.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (IsHeader(line, depth))
                {
                    var m = TableRx.Match(line);
                    if (m.Success)
                    {
                        current = new Server { Client = c, Name = TableName(m), Enabled = true };
                        items.Add(current);
                        inSub = false;
                    }
                    else if (SubTableRx.IsMatch(line)) inSub = true;
                    else { current = null; inSub = false; }
                }
                else if (current != null && !inSub && depth == 0)
                {
                    var e = EnabledRx.Match(line);
                    if (e.Success) current.Enabled = e.Groups["v"].Value == "true";
                    else
                    {
                        var k = CommandRx.Match(line);
                        if (k.Success) current.Command = Unescape(k.Groups["v"].Value);
                        else
                        {
                            var u = UrlRx.Match(line);
                            if (u.Success && string.IsNullOrEmpty(current.Command)) current.Command = Unescape(u.Groups["v"].Value);
                        }
                    }
                }
                depth = Math.Max(0, depth + BracketDelta(line));
            }
            return items;
        }

        static void TomlToggle(Client c, string name, bool on)
        {
            var file = ReadText(c.ConfigPath);
            var lines = file.Body.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
            int start, end;
            FindBlock(lines, name, out start, out end);
            if (start < 0) throw new InvalidOperationException("[mcp_servers." + name + "] was not found");

            // Only the server's own table counts, not its .env sub-table.
            int stop = end;
            for (int i = start + 1; i < end; i++) if (SubTableRx.IsMatch(lines[i])) { stop = i; break; }

            int at = -1;
            int depth = 0;
            for (int i = start + 1; i < stop; i++)
            {
                if (depth == 0 && EnabledRx.IsMatch(lines[i])) { at = i; break; }
                depth = Math.Max(0, depth + BracketDelta(lines[i]));
            }

            if (at >= 0)
            {
                var m = EnabledRx.Match(lines[at]);
                string tail = m.Groups["tail"].Success ? "  " + m.Groups["tail"].Value : "";
                lines[at] = m.Groups["lead"].Value + "enabled = " + (on ? "true" : "false") + tail;
            }
            else lines.Insert(start + 1, "enabled = " + (on ? "true" : "false"));

            WriteText(c.ConfigPath, string.Join(file.NewLine, lines), file);
        }

        static string TomlString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static void TomlAdd(Client c, string name, string command, string[] args, Dictionary<string, string> env)
        {
            var file = ReadText(c.ConfigPath);
            var lines = file.Body.Length == 0
                ? new List<string>()
                : file.Body.Split('\n').Select(x => x.TrimEnd('\r')).ToList();

            var block = new List<string>();
            block.Add("[mcp_servers." + name + "]");
            block.Add("command = " + TomlString(command));
            if (args != null && args.Length > 0)
                block.Add("args = [" + string.Join(", ", args.Select(TomlString)) + "]");
            block.Add("enabled = true");
            if (env != null && env.Count > 0)
            {
                block.Add("");
                block.Add("[mcp_servers." + name + ".env]");
                foreach (var kv in env)
                {
                    string key = kv.Key.Trim();
                    if (!NameRx.IsMatch(key)) key = TomlString(key);
                    block.Add(key + " = " + TomlString(kv.Value));
                }
            }

            int start, end;
            FindBlock(lines, name, out start, out end);
            if (start >= 0)
            {
                // Replace the existing table in place instead of appending a duplicate.
                lines.RemoveRange(start, end - start);
                if (start < lines.Count && lines[start].Trim().Length > 0) block.Add("");
                lines.InsertRange(start, block);
            }
            else
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
                if (lines.Count > 0) lines.Add("");
                lines.AddRange(block);
                lines.Add("");
            }

            WriteText(c.ConfigPath, string.Join(file.NewLine, lines), file);
        }
    }
}
