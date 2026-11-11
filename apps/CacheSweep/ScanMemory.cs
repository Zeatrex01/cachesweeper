using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CacheSweep
{
    class Remembered
    {
        public long Bytes;
        public string Print;
        public DateTime At;
    }

    // ========================================================================
    //  SCAN MEMORY  (%APPDATA%\CacheSweep\last-scan.txt)
    //  What the last scan measured, so the window can show it at once and a
    //  quick scan can re-measure only what looks changed. It holds sizes only:
    //  the folders to act on always come from the catalog, never from this file.
    // ========================================================================
    static class ScanMemory
    {
        const string Header = "cachesweep-scan 1";
        const long MaxFileBytes = 1048576;

        /// <summary>How far below a target folder a quick scan looks for changes.</summary>
        public const int PrintDepth = 3;

        /// <summary>How many entries a quick scan looks at per target, however large it is.</summary>
        public const int PrintBudget = 20000;

        static string DefaultFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "CacheSweep", "last-scan.txt");
            }
        }

        public static string KeyOf(Target t) { return t.UnityLog ? "unity-logs" : string.Join("|", t.Paths); }

        public static Dictionary<string, Remembered> Load() { return Load(DefaultFile); }

        /// <summary>Never throws: a missing or damaged file only means nothing is remembered.</summary>
        public static Dictionary<string, Remembered> Load(string file)
        {
            var map = new Dictionary<string, Remembered>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.Length > MaxFileBytes) return map;
                var lines = File.ReadAllLines(file, Encoding.UTF8);
                if (lines.Length == 0 || lines[0] != Header) return map;
                foreach (var line in lines.Skip(1))
                {
                    var p = line.Split('\t');
                    long ticks, bytes;
                    if (p.Length != 4 || p[3].Length == 0) continue;
                    if (!long.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) continue;
                    if (!long.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out bytes)) continue;
                    if (ticks > DateTime.MaxValue.Ticks) continue;
                    map[p[3]] = new Remembered { At = new DateTime(ticks, DateTimeKind.Utc), Bytes = bytes, Print = p[2] };
                }
            }
            catch { map.Clear(); }
            return map;
        }

        public static void Save(Dictionary<string, Remembered> map) { Save(map, DefaultFile); }

        /// <summary>Goes through a temp file, so a crash cannot leave half a file. A failure only
        /// costs the memory, so it is not worth interrupting anyone over.</summary>
        public static void Save(Dictionary<string, Remembered> map, string file)
        {
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var sb = new StringBuilder();
                sb.Append(Header).Append('\n');
                foreach (var kv in map)
                    sb.Append(kv.Value.At.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(kv.Value.Bytes.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(kv.Value.Print ?? "").Append('\t')
                      .Append(kv.Key).Append('\n');
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(file)) File.Replace(tmp, file, null);
                else File.Move(tmp, file);
            }
            catch { }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        /// <summary>Takes in what a finished scan measured. A target that could not be measured
        /// keeps its older entry; a target whose folders are gone loses it.</summary>
        public static void Update(Dictionary<string, Remembered> map, IEnumerable<Target> targets)
        {
            foreach (var t in targets)
            {
                string key = KeyOf(t);
                if (t.Found.Count == 0) map.Remove(key);
                else if (t.Bytes >= 0) map[key] = new Remembered { At = t.MeasuredAt, Bytes = t.Bytes, Print = t.Print };
            }
        }

        /// <summary>Puts remembered sizes on the rows without touching the disk. Only looks entries
        /// up by the target's own key, so the file can never add a folder to the list.</summary>
        public static void Apply(IEnumerable<Target> targets, Dictionary<string, Remembered> map)
        {
            foreach (var t in targets)
            {
                Remembered known;
                if (map.TryGetValue(KeyOf(t), out known))
                {
                    t.Bytes = known.Bytes;
                    t.Print = known.Print;
                    t.MeasuredAt = known.At;
                    t.Estimated = true;
                }
                else t.Bytes = -1;
            }
        }

        /// <summary>The rule for a quick scan. Age rules depend on the clock and Unity logs are
        /// loose files, so neither can be judged by a fingerprint.</summary>
        public static bool MustMeasure(Target t, Remembered known, string print)
        {
            if (known == null || t.UnityLog || t.MinAgeDays > 0) return true;
            return string.IsNullOrEmpty(print) || print != known.Print;
        }

        /// <summary>
        /// A cheap summary of a target: names, dates and sizes down to PrintDepth, up to
        /// PrintBudget entries. A folder's date only reflects its direct children, so a change
        /// below that depth can be missed. Sizes reused because of it are shown as estimates and
        /// measured again before any delete. Links are noted but never entered.
        /// </summary>
        public static string Fingerprint(Target t)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var sink = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
                using (var w = new StreamWriter(sink, new UTF8Encoding(false)))
                {
                    int budget = PrintBudget;
                    foreach (var root in t.Found)
                    {
                        var level = new List<DirectoryInfo> { new DirectoryInfo(root) };
                        w.Write(root + "|" + level[0].LastWriteTimeUtc.Ticks + "\n");
                        for (int depth = 0; depth < PrintDepth && level.Count > 0 && budget > 0; depth++)
                        {
                            var next = new List<DirectoryInfo>();
                            foreach (var dir in level)
                            {
                                if (budget <= 0) break;
                                try
                                {
                                    foreach (var e in dir.EnumerateFileSystemInfos())
                                    {
                                        if (--budget < 0) break;
                                        var file = e as FileInfo;
                                        w.Write(e.FullName + "|" + e.LastWriteTimeUtc.Ticks + "|" + (file != null ? file.Length : -1) + "\n");
                                        if (file == null && (e.Attributes & FileAttributes.ReparsePoint) == 0)
                                            next.Add((DirectoryInfo)e);
                                    }
                                }
                                catch (UnauthorizedAccessException) { w.Write(dir.FullName + "|denied\n"); }
                                catch (IOException) { w.Write(dir.FullName + "|unreadable\n"); }
                            }
                            level = next;
                        }
                    }
                    w.Flush();
                    sink.FlushFinalBlock();
                    return Convert.ToBase64String(sha.Hash);
                }
            }
            catch { return ""; }
        }

        public static string Age(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1) return Loc.S("age_now");
            if (span.TotalHours < 1) return Loc.S("age_min", (int)span.TotalMinutes);
            if (span.TotalDays < 1) return Loc.S("age_hour", (int)span.TotalHours);
            return Loc.S("age_day", (int)span.TotalDays);
        }
    }
}
