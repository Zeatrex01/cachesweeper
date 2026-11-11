// ============================================================================
//  DevSuite - skill scanning and management
//
//  A skill is a folder with a SKILL.md inside. Disabling one moves the folder
//  into a sibling "_disabled" folder; enabling moves it back. Nothing is ever
//  deleted here.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevSuite
{
    public class Skill
    {
        public string Name, Path, Source, Description;
        public string Owner;        // plugin name, "personal", "desktop" ...
        public string OwnerKind;    // plugin | personal | desktop | project
        public long Bytes;
        public DateTime Date;
        public bool Enabled;
        public List<string> Warnings = new List<string>();
        public int DaysAgo { get { return (int)(DateTime.Now - Date).TotalDays; } }
    }

    public static class Skills
    {
        const int MaxDepth = 7;      // plugins/cache/<marketplace>/<plugin>/<version>/skills/<skill>
        static readonly string[] SkipDirs = { "node_modules", ".git", ".hg", ".svn", "__pycache__" };
        static readonly Regex VersionRx = new Regex(@"^v?\d+(\.\d+)*$", RegexOptions.Compiled);

        public static List<string> Roots()
        {
            string U = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string R = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var candidates = new List<string>
            {
                System.IO.Path.Combine(U, ".claude", "skills"),
                System.IO.Path.Combine(U, ".claude", "plugins"),
                System.IO.Path.Combine(R, "Claude", "skills"),
            };
            // NTFS is case-insensitive: "skills" and "Skills" are the same folder, list it once.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = new List<string>();
            foreach (var c in candidates)
            {
                if (!Directory.Exists(c)) continue;
                string full;
                try { full = System.IO.Path.GetFullPath(c).TrimEnd('\\'); } catch { continue; }
                if (seen.Add(full)) roots.Add(full);
            }
            return roots;
        }

        public static List<Skill> Scan()
        {
            var items = new List<Skill>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in Roots())
            {
                foreach (var mdPath in Find(root, MaxDepth))
                {
                    var folder = System.IO.Path.GetDirectoryName(mdPath);
                    if (!seenPaths.Add(folder)) continue;
                    var s = new Skill
                    {
                        Path = folder,
                        Name = new DirectoryInfo(folder).Name,
                        Source = new DirectoryInfo(root).Name,
                        Enabled = !IsDisabled(folder, root)
                    };
                    ResolveOwner(s, root);
                    try
                    {
                        var fi = new FileInfo(mdPath);
                        s.Bytes = fi.Length;
                        s.Date = fi.LastWriteTime;
                        s.Description = ReadDescription(mdPath);
                    }
                    catch { }
                    items.Add(s);
                }
            }

            // Only enabled skills compete for a name; a disabled copy is not a conflict.
            var dupes = items.Where(x => x.Enabled)
                             .GroupBy(x => x.Name.ToLowerInvariant())
                             .Where(g => g.Count() > 1)
                             .ToDictionary(g => g.Key, g => g.Count());

            foreach (var s in items)
            {
                int count;
                if (s.Enabled && dupes.TryGetValue(s.Name.ToLowerInvariant(), out count))
                    s.Warnings.Add(Loc.S("w_dup", count - 1));

                if (string.IsNullOrWhiteSpace(s.Description))
                    s.Warnings.Add(Loc.S("w_nodesc"));
                else
                {
                    if (s.Description.Length > 500) s.Warnings.Add(Loc.S("w_desc", s.Description.Length));
                    var d = s.Description.ToLowerInvariant();
                    if (d.Contains("always use") || d.Contains("every message") || d.Contains("any task")
                        || d.Contains("for everything") || d.Contains("before any response"))
                        s.Warnings.Add(Loc.S("w_broad"));
                }
                if (s.Bytes > 50 * 1024) s.Warnings.Add(Loc.S("w_big", s.Bytes / 1024));
            }

            return items.OrderByDescending(x => x.Warnings.Count).ThenBy(x => x.Name).ToList();
        }

        /// <summary>Derives the provider from the path: plugin name, personal, desktop.</summary>
        static void ResolveOwner(Skill s, string root)
        {
            try
            {
                var parts = s.Path.Split(System.IO.Path.DirectorySeparatorChar)
                                  .Where(x => x.Length > 0 && !x.Equals("_disabled", StringComparison.OrdinalIgnoreCase))
                                  .ToList();
                int i = parts.FindLastIndex(x => x.Equals("plugins", StringComparison.OrdinalIgnoreCase));
                if (i >= 0)
                {
                    // .../plugins/[marketplaces/<mp>/ | cache/<mp>/]<plugin>[/<version>]/skills/<name>
                    int j = parts.FindLastIndex(x => x.Equals("skills", StringComparison.OrdinalIgnoreCase));
                    if (j > i + 1)
                    {
                        int k = j - 1;
                        if (k - 1 > i && VersionRx.IsMatch(parts[k])) k--;      // skip a version folder
                        s.Owner = parts[k]; s.OwnerKind = "plugin"; return;
                    }
                    if (i + 1 < parts.Count) { s.Owner = parts[i + 1]; s.OwnerKind = "plugin"; return; }
                }
                if (root.IndexOf(System.IO.Path.Combine(".claude", "skills"), StringComparison.OrdinalIgnoreCase) >= 0)
                { s.Owner = Loc.S("own_personal"); s.OwnerKind = "personal"; return; }
                if (root.IndexOf(System.IO.Path.Combine("Claude", "skills"), StringComparison.OrdinalIgnoreCase) >= 0)
                { s.Owner = Loc.S("own_desktop"); s.OwnerKind = "desktop"; return; }
            }
            catch { }
            s.Owner = s.Source;
            s.OwnerKind = "project";
        }

        static IEnumerable<string> Find(string root, int maxDepth)
        {
            var found = new List<string>();
            var stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(root, 0));
            while (stack.Count > 0)
            {
                var it = stack.Pop();
                if (it.Value > maxDepth) continue;
                try
                {
                    var mdPath = System.IO.Path.Combine(it.Key, "SKILL.md");
                    if (File.Exists(mdPath)) { found.Add(mdPath); continue; }
                    foreach (var d in Directory.EnumerateDirectories(it.Key))
                    {
                        var name = System.IO.Path.GetFileName(d);
                        if (SkipDirs.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                        try
                        {
                            if ((new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch { continue; }
                        stack.Push(new KeyValuePair<string, int>(d, it.Value + 1));
                    }
                }
                catch { }
            }
            return found;
        }

        /// <summary>True when any folder between the root and the skill is named _disabled.</summary>
        static bool IsDisabled(string folder, string root)
        {
            try
            {
                var di = new DirectoryInfo(folder).Parent;
                while (di != null && di.FullName.Length > root.Length)
                {
                    if (di.Name.Equals("_disabled", StringComparison.OrdinalIgnoreCase)) return true;
                    di = di.Parent;
                }
            }
            catch { }
            return false;
        }

        static string ReadDescription(string mdPath)
        {
            try
            {
                var text = File.ReadAllText(mdPath);
                var m = Regex.Match(text, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
                if (!m.Success) return "";
                var d = Regex.Match(m.Groups[1].Value,
                    @"^description\s*:\s*(?<v>.+?)(?=\n[A-Za-z_-]+\s*:|\z)",
                    RegexOptions.Multiline | RegexOptions.Singleline);
                return d.Success ? Regex.Replace(d.Groups["v"].Value.Trim().Trim('"', '\''), @"\s+", " ") : "";
            }
            catch { return ""; }
        }

        /// <summary>Where a skill's folder goes when it is disabled.</summary>
        public static string DisabledPath(Skill s)
        {
            var di = new DirectoryInfo(s.Path);
            return System.IO.Path.Combine(di.Parent.FullName, "_disabled", di.Name);
        }

        /// <summary>Enable or disable a skill by moving its folder. Returns the new path.
        /// Throws with a plain-language reason when the move cannot be done.</summary>
        public static string Toggle(Skill s, bool on)
        {
            var di = new DirectoryInfo(s.Path);
            if (di.Parent == null) throw new InvalidOperationException("The skill folder has no parent folder.");

            if (on)
            {
                // Move out of the nearest _disabled ancestor, keeping the path below it.
                var off = di.Parent;
                var rel = di.Name;
                while (off != null && !off.Name.Equals("_disabled", StringComparison.OrdinalIgnoreCase))
                {
                    rel = System.IO.Path.Combine(off.Name, rel);
                    off = off.Parent;
                }
                if (off == null || off.Parent == null)
                    throw new InvalidOperationException("\"" + s.Name + "\" is not inside a _disabled folder; move it back by hand.");
                var dest = System.IO.Path.Combine(off.Parent.FullName, rel);
                if (Directory.Exists(dest))
                    throw new InvalidOperationException("A folder named \"" + di.Name + "\" already exists at:\n" + dest);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                Directory.Move(s.Path, dest);
                return dest;
            }
            else
            {
                if (di.Parent.Name.Equals("_disabled", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("\"" + s.Name + "\" is already disabled.");
                var dest = DisabledPath(s);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                if (Directory.Exists(dest))
                    throw new InvalidOperationException("A disabled copy of \"" + di.Name + "\" already exists at:\n" + dest);
                Directory.Move(s.Path, dest);
                return dest;
            }
        }
    }
}
