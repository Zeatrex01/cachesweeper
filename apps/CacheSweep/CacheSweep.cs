// ============================================================================
//  CacheSweep - developer cache cleaner
//  Single-file C# WinForms app, .NET Framework 4.x, compiled with csc.exe.
//  Part of DevSuite. Nothing is deleted unless the user ticks it and confirms.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;
using DevSuite;                 // Dpi and NativeChrome, shared with the rest of the suite

// ---------------------------------------------------------------------------
//  EXE METADATA  (shown in File > Properties > Details)
// ---------------------------------------------------------------------------
[assembly: AssemblyTitle("CacheSweep")]
[assembly: AssemblyDescription("Developer cache cleaner for Windows")]
[assembly: AssemblyProduct("DevSuite")]
[assembly: AssemblyCompany(CacheSweep.Info.Developer)]
[assembly: AssemblyCopyright("Copyright " + CacheSweep.Info.Year + " " + CacheSweep.Info.Developer)]
[assembly: AssemblyVersion(CacheSweep.Info.Version + ".0")]
[assembly: AssemblyFileVersion(CacheSweep.Info.Version + ".0")]
[assembly: ComVisible(false)]

namespace CacheSweep
{
    // ====================================================================
    //  AUTHOR INFO - edit in one place
    // ====================================================================
    public static class Info
    {
        public const string Version   = "0.9.0";
        public const string Developer = "Enes Aksu";
        public const string Handle    = "";                       // optional, e.g. "@name"
        public const string GitHub    = "https://github.com/Zeatrex01/cachesweeper";
        public const string License   = "MIT";
        public const string Year      = "2026";
    }

    // ========================================================================
    //  LOCALISATION - English is built in; lang.json next to the exe overrides
    // ========================================================================
    static class Loc
    {
        static readonly Dictionary<string, string> S_ = new Dictionary<string, string>
        {
            { "app", "CacheSweep" },
            { "tagline", "Developer cache cleaner" },
            { "rescan", "Scan" },
            { "quick_scan", "Quick scan" },
            { "full_scan", "Full scan" },
            { "remembered", "Showing the scan from {0}. Sizes marked ~ may be out of date." },
            { "quick_done", "{0} measured again, {1} reused. Sizes marked ~ come from an earlier scan." },
            { "measure_first", "Measuring the selected folders before you confirm..." },
            { "age_now", "just now" },
            { "age_min", "{0} min ago" },
            { "age_hour", "{0} h ago" },
            { "age_day", "{0} d ago" },
            { "scan_hint", "Nothing has been scanned yet. Press Scan to measure your caches." },
            { "settings", "Settings" },
            { "export", "Export" },
            { "scanning", "Scanning..." },
            { "scanning_n", "Scanning... {0} left" },
            { "measuring", "measuring..." },
            { "protected_hint", "purple row = protected, never deleted" },
            { "selected_none", "Selected: 0" },
            { "selected_n", "Selected: {0} items - {1}" },
            { "clear_sel", "Clear selection" },
            { "delete_sel", "Delete selected" },
            { "confirm_title", "Confirm" },
            { "confirm_head", "The CONTENTS of these will be removed:" },
            { "confirm_gain", "Expected gain: up to {0}" },
            { "confirm_go", "Delete ({0})" },
            { "confirm_keep", "Keep everything" },
            { "confirm_held", "Skipped, because a program using them is open. Close it and scan again:" },
            { "confirm_kept", "Kept: {0}" },
            { "confirm_recycle", "Will be sent to the Recycle Bin (items too large for the bin are deleted permanently)." },
            { "confirm_perm", "Will be deleted PERMANENTLY." },
            { "deleting", "Deleting: {0}" },
            { "done", "Done" },
            { "gained", "Reclaimed: {0}" },
            { "gained_none", "Nothing was removed." },
            { "gained_recycle", "Moved to the Recycle Bin: {0}. The space returns when the bin is emptied." },
            { "partial_head", "Only partly cleaned. Clean them again, or use the tool's own clean command:" },
            { "held_late", "skipped, now in use by {0}" },
            { "stopped", "Stopped before finishing." },
            { "stopping", "Stopping..." },
            { "busy_close", "A delete is running. Press Cancel and wait for it to stop before closing." },
            { "settings_failed", "Your settings could not be read, so the defaults are in use. Check Settings before deleting." },
            { "prot_title", "Protected" },
            { "prot_body", "\"{0}\" is protected.\n\nThis app never touches it." },

            { "set_title", "Settings" },
            { "set_theme", "Theme" },
            { "set_dark", "Dark" },
            { "set_light", "Light" },
            { "set_min", "Hide items smaller than" },
            { "set_autoscan", "Scan as soon as CacheSweep opens" },
            { "set_recycle", "Send to Recycle Bin when possible (items too large for the bin are deleted permanently)" },
            { "set_advanced", "Show advanced items: installed SDK components and shared stores that can break projects" },
            { "set_custom", "Your own folders" },
            { "set_add", "Add folder" },
            { "set_remove", "Remove" },
            { "set_save", "Save" },
            { "set_cancel", "Cancel" },
            { "set_custom_hint", "Contents of folders added here get deleted if you select them. Cache folders only - project, personal and system folders are refused." },
            { "grp_custom", "Custom" },
            { "tile_total", "In caches" },
            { "tile_delta", "Since last scan" },
            { "last_scan", "Last scan: {0}" },
            { "mini_mode", "Switch to the small panel" },
            { "mini_free", "free" },
            { "mini_none", "Nothing scanned yet" },
            { "mini_delta", "Caches {0}, {1} since last scan" },
            { "mini_age", "Caches {0}, measured {1}" },
            { "menu_full", "Full window" },
            { "menu_close", "Close" },
            { "tile_free", "Free now" },
            { "grp_advanced", "advanced" },

            { "guard_title", "Folder not allowed" },
            { "guard_root", "Drive roots cannot be added." },
            { "guard_network", "Network paths are not supported." },
            { "guard_missing", "Folder not found:\n{0}" },
            { "guard_system", "System folders cannot be added:\n{0}" },
            { "guard_personal", "Personal folders, and folders that contain them, cannot be added:\n{0}" },
            { "guard_project", "This looks like a project folder ({0} found). Only cache folders can be added." },
            { "guard_protected", "This folder overlaps a protected location:\n{0}" },
            { "guard_unresolved", "Could not tell where this folder really leads, so it was refused:\n{0}" },
            { "blocked_head", "{0} item(s) were skipped by the safety rules:" },

            { "exp_title", "Save scan report" },
            { "exp_ok", "Report saved:\n{0}" },
            { "cancel", "Cancel" },
            { "cancelled", "Cancelled." },
            { "skipped_head", "{0} item(s) could not be fully deleted (in use or no permission)." },
            { "locked_by", "Likely culprit: {0}" },
            { "about", "About" },
            { "about_by", "Built by" },
            { "about_src", "Source code" },
            { "about_lic", "License" },
            { "about_desc", "Finds, measures and clears the cache folders piled up by Node, Python, Unity, Android and .NET toolchains. Never touches anything you didn't tick." },
            { "about_close", "Close" },
        };

        public static string S(string k)
        {
            string v;
            return S_.TryGetValue(k, out v) ? v : k;
        }

        public static string S(string k, params object[] a) { return string.Format(S(k), a); }

        /// <summary>Language-pack override for <paramref name="k"/> if present, otherwise the built-in text.</summary>
        public static string S2(string k, string fallback)
        {
            string v;
            return S_.TryGetValue(k, out v) ? v : fallback;
        }

        /// <summary>JSON string escapes, left to right, so "\\n" stays a backslash and an n.</summary>
        static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, null, out code))
                            { sb.Append((char)code); i += 4; break; }
                        }
                        sb.Append('u'); break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Loads lang.json sitting next to the executable, if present.
        /// The English build ships without it; a localised package includes one.</summary>
        public static void LoadPack()
        {
            try
            {
                var file = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "lang.json");
                if (!File.Exists(file)) return;
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             File.ReadAllText(file),
                             "\"(?<k>[^\"]+)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\""))
                {
                    S_[m.Groups["k"].Value] = Unescape(m.Groups["v"].Value);
                }
            }
            catch { }
        }
    }

    // ========================================================================
    //  THEME
    // ========================================================================
    class Theme
    {
        public Color Bg, Panel, PanelRaised, Border, Fg, Muted, Accent, Danger, Warning, Amber, Info, Badge, SelectedBg;

        public static Theme Dark()
        {
            return new Theme
            {
                Bg          = Color.FromArgb(20, 22, 25),
                Panel       = Color.FromArgb(29, 32, 37),
                PanelRaised = Color.FromArgb(38, 42, 49),
                Border      = Color.FromArgb(46, 51, 58),
                Fg          = Color.FromArgb(233, 235, 238),
                Muted       = Color.FromArgb(140, 147, 157),
                Accent      = Color.FromArgb(59, 130, 246),
                Danger      = Color.FromArgb(239, 68, 68),
                Warning     = Color.FromArgb(249, 146, 48),
                Amber       = Color.FromArgb(226, 186, 72),
                Info        = Color.FromArgb(167, 111, 232),
                Badge       = Color.FromArgb(45, 50, 58),
                SelectedBg  = Color.FromArgb(35, 47, 68)
            };
        }

        public static Theme Light()
        {
            return new Theme
            {
                Bg          = Color.FromArgb(245, 246, 248),
                Panel       = Color.White,
                PanelRaised = Color.FromArgb(240, 242, 245),
                Border      = Color.FromArgb(225, 228, 233),
                Fg          = Color.FromArgb(24, 27, 32),
                Muted       = Color.FromArgb(112, 119, 130),
                Accent      = Color.FromArgb(37, 99, 235),
                Danger      = Color.FromArgb(220, 38, 38),
                Warning     = Color.FromArgb(217, 119, 6),
                Amber       = Color.FromArgb(180, 140, 20),
                Info        = Color.FromArgb(139, 92, 246),
                Badge       = Color.FromArgb(235, 238, 242),
                SelectedBg  = Color.FromArgb(225, 236, 253)
            };
        }
    }

    // ========================================================================
    //  SETTINGS  (%APPDATA%\CacheSweep\settings.ini)
    // ========================================================================
    class Settings
    {
        public string ThemeName = "dark";      // dark | light
        public int    MinMB = 20;
        public bool   AutoScan = false;
        public bool   UseRecycleBin = false;
        public bool   ShowAdvanced = false;
        public bool   LoadFailed;
        public List<string> Custom = new List<string>();

        // Folders refused today (an unplugged drive, say) stay in the file for when they come back.
        readonly List<string> _dormant = new List<string>();

        static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CacheSweep"); }
        }
        static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        public static Settings Load()
        {
            var a = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return a;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var s = raw.Trim();
                    var i = s.IndexOf('=');
                    if (i <= 0) continue;
                    string k = s.Substring(0, i).Trim(), v = s.Substring(i + 1).Trim();
                    switch (k)
                    {
                        case "theme":    a.ThemeName = v == "light" ? "light" : "dark"; break;
                        case "minmb":    { int n; if (int.TryParse(v, out n) && n >= 0 && n <= 5000) a.MinMB = n; break; }
                        case "scanonopen": a.AutoScan = (v == "1"); break;
                        case "recycle":  a.UseRecycleBin = (v == "1"); break;
                        case "advanced": a.ShowAdvanced = (v == "1"); break;
                        case "custom":
                        {
                            // A folder that was allowed when it was added may have become a
                            // project or been deleted since; re-check on every load.
                            if (v.Length == 0) break;
                            if (Guard.Reject(v) != null) { a._dormant.Add(v); break; }
                            string norm = Guard.Normalize(v);
                            if (!a.Custom.Contains(norm, StringComparer.OrdinalIgnoreCase)) a.Custom.Add(norm);
                            break;
                        }
                    }
                }
            }
            catch { a.LoadFailed = true; }
            return a;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var sb = new StringBuilder();
                sb.AppendLine("theme=" + ThemeName);
                sb.AppendLine("minmb=" + MinMB);
                sb.AppendLine("scanonopen=" + (AutoScan ? "1" : "0"));
                sb.AppendLine("recycle=" + (UseRecycleBin ? "1" : "0"));
                sb.AppendLine("advanced=" + (ShowAdvanced ? "1" : "0"));
                foreach (var o in Custom) sb.AppendLine("custom=" + o);
                foreach (var o in _dormant) if (!Custom.Contains(o, StringComparer.OrdinalIgnoreCase)) sb.AppendLine("custom=" + o);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(true));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { }
        }
    }

    // ========================================================================
    //  GUARD - the safety rules. Every custom folder passes through Reject()
    //  when it is added, when settings are loaded and again right before its
    //  contents are deleted. Protected roots (Asset Store) are checked for
    //  every single item that is about to be deleted.
    // ========================================================================
    static class Guard
    {
        /// <summary>Unity Asset Store downloads: assets the user paid for. Never touched.</summary>
        public static readonly string AssetStore = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unity", "Asset Store-5.x");

        /// <summary>Chrome profiles, cache and on-device model data, for every release channel.
        /// Never touched, even if pointed at directly as a custom folder.</summary>
        static string ChromeUserData(string channel)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", channel, "User Data");
        }

        // Known from the moment the process starts, not only after the first scan.
        public static readonly List<string> ProtectedRoots = new List<string>
        {
            AssetStore,
            ChromeUserData("Chrome"), ChromeUserData("Chrome Beta"), ChromeUserData("Chrome Dev"), ChromeUserData("Chrome SxS"),
        };

        static readonly string[] ProjectMarkers =
        {
            ".git", ".svn", ".hg", "package.json", "pyproject.toml", "setup.py", "requirements.txt",
            "Cargo.toml", "go.mod", "pom.xml", "build.gradle", "settings.gradle", "CMakeLists.txt",
            "Makefile", "Gemfile", "composer.json", "pubspec.yaml", "Package.swift", "mix.exs",
        };

        static readonly string[] ProjectPatterns = { "*.sln", "*.csproj", "*.vbproj", "*.fsproj", "*.uproject", "*.xcodeproj" };

        // Strong signals only: a child folder with one of these is a checked-out project.
        static readonly string[] ChildProjectMarkers = { ".git", ".svn", ".hg" };
        static readonly string[] ChildProjectPatterns = { "*.sln", "*.uproject" };

        static readonly string[] ConfigFolders =
        {
            ".ssh", ".gnupg", ".aws", ".azure", ".kube", ".docker", ".config",
            ".claude", ".codex", ".cursor", ".vscode",
        };

        public static string Normalize(string path)
        {
            var full = Path.GetFullPath(path.Trim());
            var root = Path.GetPathRoot(full);
            if (full.Length > root.Length) full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="parent"/>.</summary>
        public static bool IsUnder(string path, string parent)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(parent)) return false;
            string p, q;
            try { p = Normalize(path); q = Normalize(parent); } catch { return false; }
            if (q.Length == 0 || p.Length <= q.Length) return false;
            if (!q.EndsWith(Path.DirectorySeparatorChar.ToString())) q += Path.DirectorySeparatorChar;
            return p.StartsWith(q, StringComparison.OrdinalIgnoreCase);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(Microsoft.Win32.SafeHandles.SafeFileHandle file,
            StringBuilder buffer, uint size, uint flags);

        const uint ShareAll = 7, OpenExisting = 3, BackupSemantics = 0x02000000;

        // What Windows itself says this existing file or folder is, or null when it cannot tell.
        static string FinalPath(string existing)
        {
            using (var h = CreateFileW(existing, 0, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                var sb = new StringBuilder(32768);
                uint n = GetFinalPathNameByHandleW(h, sb, (uint)sb.Capacity, 0);
                if (n == 0 || n >= sb.Capacity) return null;
                var s = sb.ToString();
                if (s.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + s.Substring(8);
                if (s.StartsWith(@"\\?\", StringComparison.Ordinal)) return s.Substring(4);
                return s;
            }
        }

        /// <summary>Where a path really leads once junctions, symlinks and substituted drives are
        /// followed. A path that does not exist yet is resolved through its nearest existing
        /// ancestor. Null when it exists but cannot be resolved; callers must treat that as a refusal.</summary>
        public static string RealPath(string path)
        {
            try
            {
                string probe = Normalize(path), tail = "";
                while (!Directory.Exists(probe) && !File.Exists(probe))
                {
                    string up = Path.GetDirectoryName(probe);
                    if (string.IsNullOrEmpty(up)) return Normalize(path);
                    tail = Path.Combine(Path.GetFileName(probe), tail);
                    probe = up;
                }
                string real = FinalPath(probe);
                if (real == null) return null;
                return tail.Length == 0 ? real : Normalize(Path.Combine(real, tail));
            }
            catch { return null; }
        }

        static bool Redirected(string path)
        {
            try
            {
                string real = RealPath(path);
                return real != null && !Same(real, Normalize(path));
            }
            catch { return false; }
        }

        // Each path as written, plus where it really leads when that differs.
        static List<string> WithReal(IEnumerable<string> paths)
        {
            var l = new List<string>();
            foreach (var p in paths.ToList())
            {
                l.Add(p);
                if (Redirected(p)) l.Add(RealPath(p));
            }
            return l;
        }

        /// <summary>A path for the confirmation dialog: when it is redirected, the real location
        /// is shown too, because that is what will actually be emptied.</summary>
        public static string Describe(string path)
        {
            return Redirected(path) ? path + "  ->  " + RealPath(path) : path;
        }

        /// <summary>Equal to, inside, or containing a protected root, as written or where it really leads.</summary>
        public static bool TouchesProtected(string path)
        {
            try
            {
                string real = RealPath(path);
                if (real == null) return true;
                foreach (var root in WithReal(ProtectedRoots))
                    foreach (var n in new[] { Normalize(path), real })
                        if (Same(n, Normalize(root)) || IsUnder(n, root) || IsUnder(root, n)) return true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>Path is exactly a protected root or lives inside one, as written or where it really leads.</summary>
        public static bool IsProtected(string path)
        {
            try
            {
                string real = RealPath(path);
                if (real == null) return true;
                foreach (var root in WithReal(ProtectedRoots))
                    foreach (var n in new[] { Normalize(path), real })
                        if (Same(n, Normalize(root)) || IsUnder(n, root)) return true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>The last check on a target folder before its contents go; null means go ahead.
        /// A built-in target only takes the full guard when something has redirected it, since
        /// only then can it lead somewhere the rules would refuse.</summary>
        public static string CheckBeforeDelete(string path, bool custom)
        {
            if (custom || Redirected(path))
            {
                string reason = Reject(path);
                if (reason != null) return reason;
            }
            return TouchesProtected(path) ? Loc.S("guard_protected", path) : null;
        }

        static string Special(Environment.SpecialFolder f)
        {
            try { return Environment.GetFolderPath(f); } catch { return ""; }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        static string KnownFolder(string guid)
        {
            IntPtr p = IntPtr.Zero;
            try
            {
                if (SHGetKnownFolderPath(new Guid(guid), 0, IntPtr.Zero, out p) == 0 && p != IntPtr.Zero)
                    return Marshal.PtrToStringUni(p);
            }
            catch { }
            finally { if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p); }
            return "";
        }

        static List<string> SystemFolders()
        {
            var l = new List<string>
            {
                Special(Environment.SpecialFolder.Windows),
                Special(Environment.SpecialFolder.System),
                Special(Environment.SpecialFolder.SystemX86),
                Special(Environment.SpecialFolder.ProgramFiles),
                Special(Environment.SpecialFolder.ProgramFilesX86),
                Special(Environment.SpecialFolder.CommonProgramFiles),
                Special(Environment.SpecialFolder.CommonProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432") ?? "",
                Special(Environment.SpecialFolder.CommonApplicationData),
            };
            return l.Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        // Folders whose contents are personal data. Adding a subfolder of these is fine
        // (e.g. a Unity Library folder inside Documents), the folder itself is not.
        static List<string> PersonalFolders()
        {
            string user = Special(Environment.SpecialFolder.UserProfile);
            string appData = Special(Environment.SpecialFolder.ApplicationData);
            string local = Special(Environment.SpecialFolder.LocalApplicationData);
            var l = new List<string>
            {
                user,
                Special(Environment.SpecialFolder.Desktop),
                Special(Environment.SpecialFolder.DesktopDirectory),
                Special(Environment.SpecialFolder.MyDocuments),
                Special(Environment.SpecialFolder.MyPictures),
                Special(Environment.SpecialFolder.MyMusic),
                Special(Environment.SpecialFolder.MyVideos),
                Special(Environment.SpecialFolder.Favorites),
                Special(Environment.SpecialFolder.CommonDocuments),
                Special(Environment.SpecialFolder.CommonDesktopDirectory),
                Special(Environment.SpecialFolder.UserProfile),
                appData, local,
                KnownFolder("{374DE290-123F-4565-9164-39C4925E467B}"),   // Downloads
                KnownFolder("{A520A1A4-1780-4FF6-BD18-167343C5AF16}"),   // AppData\LocalLow
                Environment.GetEnvironmentVariable("OneDrive") ?? "",
                Environment.GetEnvironmentVariable("OneDriveConsumer") ?? "",
                Environment.GetEnvironmentVariable("OneDriveCommercial") ?? "",
            };
            if (!string.IsNullOrEmpty(user))
            {
                l.Add(Path.Combine(user, "Downloads"));
                l.Add(Path.Combine(user, "AppData"));
                l.Add(Path.Combine(user, "OneDrive"));
                foreach (var c in ConfigFolders) l.Add(Path.Combine(user, c));
            }
            if (!string.IsNullOrEmpty(appData)) l.Add(Path.GetDirectoryName(appData) ?? "");
            return l.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Returns null when the folder may be cleaned, otherwise the reason it was refused.</summary>
        public static string Reject(string path)
        {
            string full;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return Loc.S("guard_missing", path);
                if (path.Trim().StartsWith(@"\\") || path.Trim().StartsWith("//")) return Loc.S("guard_network");
                full = Normalize(path);
            }
            catch { return Loc.S("guard_missing", path); }

            // Judged twice: as written, and where it really leads. A junction to Documents is Documents.
            string reason = RejectAt(full);
            if (reason != null) return reason;
            string real = RealPath(full);
            if (real == null) return Loc.S("guard_unresolved", full);
            return Same(real, full) ? null : RejectAt(real);
        }

        static string RejectAt(string full)
        {
            if (!Path.IsPathRooted(full) || full.StartsWith(@"\\")) return Loc.S("guard_network");

            string root = Path.GetPathRoot(full);
            if (Same(full, root) || Same(full, root.TrimEnd('\\'))) return Loc.S("guard_root");

            if (!Directory.Exists(full)) return Loc.S("guard_missing", full);

            // System folders: refuse the folder itself, anything inside it, and anything containing it.
            foreach (var s in WithReal(SystemFolders()))
                if (Same(full, Normalize(s)) || IsUnder(full, s) || IsUnder(s, full))
                    return Loc.S("guard_system", s);

            // Personal folders: refuse the folder itself and anything containing it.
            foreach (var p in WithReal(PersonalFolders()))
                if (Same(full, Normalize(p)) || IsUnder(p, full))
                    return Loc.S("guard_personal", p);

            // Credential and AI-client state folders: nothing inside them either.
            string home = Special(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
                foreach (var dir in WithReal(ConfigFolders.Select(c => Path.Combine(home, c))))
                    if (IsUnder(full, dir)) return Loc.S("guard_personal", dir);

            // Protected targets (Asset Store, Chrome): no overlap in either direction.
            foreach (var r in WithReal(ProtectedRoots))
            {
                try
                {
                    string pr = Normalize(r);
                    if (Same(full, pr) || IsUnder(full, pr) || IsUnder(pr, full)) return Loc.S("guard_protected", r);
                }
                catch { return Loc.S("guard_protected", r); }
            }

            // Project roots: a source tree is never a cache.
            try
            {
                if (IsProjectRoot(full, ProjectMarkers, ProjectPatterns))
                    return Loc.S("guard_project", ProjectHint(full, ProjectMarkers, ProjectPatterns));

                // A folder full of checked-out projects (a "repos" folder) is not a cache either.
                int seen = 0;
                foreach (var child in Directory.EnumerateDirectories(full))
                {
                    if (++seen > 300) break;
                    if (IsProjectRoot(child, ChildProjectMarkers, ChildProjectPatterns))
                        return Loc.S("guard_project", Path.GetFileName(child) + "\\" + ProjectHint(child, ChildProjectMarkers, ChildProjectPatterns));
                }
            }
            catch { }

            return null;
        }

        static bool IsProjectRoot(string dir, string[] markers, string[] patterns)
        {
            return ProjectHint(dir, markers, patterns) != null;
        }

        /// <summary>Name of the first project marker found directly inside <paramref name="dir"/>, or null.</summary>
        static string ProjectHint(string dir, string[] markers, string[] patterns)
        {
            try
            {
                foreach (var m in markers)
                    if (File.Exists(Path.Combine(dir, m)) || Directory.Exists(Path.Combine(dir, m))) return m;
                foreach (var pat in patterns)
                    if (Directory.EnumerateFileSystemEntries(dir, pat).Any()) return pat;
                if (Directory.Exists(Path.Combine(dir, "Assets")) && Directory.Exists(Path.Combine(dir, "ProjectSettings")))
                    return "Assets + ProjectSettings";
            }
            catch { }
            return null;
        }
    }

    // ========================================================================
    //  MODEL + MEASURING
    // ========================================================================
    static class Measure
    {
        /// <summary>Total size of a folder tree. Returns -1 when cancelled so a partial
        /// number is never shown as if it were complete.</summary>
        public static long FolderSize(string root, CancellationToken token)
        {
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (token.IsCancellationRequested) return -1;
                string d = stack.Pop();
                try { foreach (var f in Directory.EnumerateFiles(d)) { try { total += new FileInfo(f).Length; } catch { } } }
                catch { }
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(d))
                    {
                        try
                        {
                            var di = new DirectoryInfo(sub);
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;   // junctions / symlinks
                        }
                        catch { continue; }
                        stack.Push(sub);
                    }
                }
                catch { }
            }
            return token.IsCancellationRequested ? -1 : total;
        }

        /// <summary>Size of one file or folder. A link counts as nothing: it is unlinked, not followed.</summary>
        public static long EntrySize(string path, bool isFolder, CancellationToken token)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return 0;
                return isFolder ? FolderSize(path, token) : new FileInfo(path).Length;
            }
            catch { return 0; }
        }

        public static string Fmt(long b)
        {
            var ci = CultureInfo.CurrentCulture;
            if (b < 0) return "?";
            if (b >= 1073741824L) return (b / 1073741824.0).ToString("0.00", ci) + " GB";
            if (b >= 1048576L)    return (b / 1048576.0).ToString("0", ci) + " MB";
            if (b == 0)           return "0";
            return (b / 1024.0).ToString("0", ci) + " KB";
        }

        public static long TotalSpace()
        {
            try { return new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))).TotalSize; }
            catch { return 0; }
        }

        public static long FreeSpace()
        {
            try { return new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))).AvailableFreeSpace; }
            catch { return 0; }
        }
    }

    // ========================================================================
    //  UI PARTS
    // ========================================================================
    static class Draw
    {
        /// <summary>Turns on the good rendering modes and puts the canvas into logical
        /// pixels, so the paint code below keeps the coordinates it was written with
        /// whatever the display scale is. Returns the control's size in those same
        /// logical pixels - Width and Height are device pixels and must not be mixed in.</summary>
        public static SizeF Quality(Graphics g, Control c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            if (Dpi.Scale != 1f) g.ScaleTransform(Dpi.Scale, Dpi.Scale);
            return new SizeF(c.Width / Dpi.Scale, c.Height / Dpi.Scale);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class FlatButton : Button
    {
        public Color Tint = Color.Gray;       // fill
        public Color Ink = Color.White;       // label
        bool _hover;

        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI Semibold", 12.7f, GraphicsUnit.Pixel);   // logical px: this label is painted, not drawn by Windows
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // The fill below is rounded, so the four corners fall outside it. Without
            // clearing them first they keep whatever the buffer held, which shows up
            // as stale fragments once the button is moved by a re-layout.
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            var sz = Draw.Quality(g, this);
            int w = (int)sz.Width, h = (int)sz.Height;
            Color c = Tint;
            if (!Enabled) c = Color.FromArgb(Math.Max(0, Tint.R / 3 + 30), Math.Max(0, Tint.G / 3 + 30), Math.Max(0, Tint.B / 3 + 30));
            else if (_hover) c = ControlPaint.Light(Tint, 0.12f);
            using (var b = new SolidBrush(c))
            using (var path = Draw.Rounded(new Rectangle(0, 0, w - 1, h - 1), 7))
                g.FillPath(b, path);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(Enabled ? Ink : Color.FromArgb(150, Ink)))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, sz.Width, sz.Height), sf);
        }
    }

    class ProgressLine : Control
    {
        public double Value;                  // 0..1
        public bool Indeterminate;
        public Color Tint = Color.DodgerBlue, Track = Color.Gray;
        int _offset;
        readonly System.Windows.Forms.Timer _timer;

        public ProgressLine()
        {
            Height = Dpi.Px(4);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _timer = new System.Windows.Forms.Timer { Interval = 30 };
            // The sweep is painted in logical pixels, so it travels a logical width.
            _timer.Tick += (s, e) => { _offset = (_offset + 7) % Math.Max(1, (int)(Width / Dpi.Scale) + 160); Invalidate(); };
        }

        public void StartIndeterminate() { Indeterminate = true; _timer.Start(); }
        public void Stop() { Indeterminate = false; _timer.Stop(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var sz = Draw.Quality(g, this);
            int full = (int)sz.Width, h = Math.Max(1, (int)sz.Height);
            using (var b = new SolidBrush(Track)) g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
            if (Indeterminate)
            {
                int w = 140;
                using (var b = new SolidBrush(Tint))
                using (var path = Draw.Rounded(new Rectangle(_offset - w, 0, w, h), h / 2))
                    g.FillPath(b, path);
            }
            else if (Value > 0)
            {
                int w = Math.Max(2, (int)(full * Math.Min(1.0, Value)));
                using (var b = new SolidBrush(Tint))
                using (var path = Draw.Rounded(new Rectangle(0, 0, w, h), h / 2))
                    g.FillPath(b, path);
            }
        }
    }

    class Row : Panel
    {
        public Target Item;
        public bool Selected;
        public double Ratio;
        public Theme Skin;
        public event EventHandler SelectionChanged;
        public const int RowHeight = 58;
        bool _hover;

        public Row(Target item, Theme skin)
        {
            Item = item; Skin = skin;
            Height = Dpi.Px(RowHeight); Dock = DockStyle.Top;
            DoubleBuffered = true;
            Cursor = item.Protected ? Cursors.No : Cursors.Hand;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            Click += (s, e) => Toggle();
        }

        public void Toggle()
        {
            if (Item.Protected)
            {
                MessageBox.Show(Loc.S("prot_body", Item.Name), Loc.S("prot_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (Item.Bytes <= 0) return;
            Selected = !Selected;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        Color SizeColor()
        {
            if (Item.Protected) return Skin.Info;
            if (Item.Bytes >= 5L * 1073741824L) return Skin.Danger;
            if (Item.Bytes >= 1073741824L)      return Skin.Warning;
            if (Item.Bytes >= 200L * 1048576L)  return Skin.Amber;
            return Skin.Muted;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var sz = Draw.Quality(g, this);
            int w = (int)sz.Width, h = (int)sz.Height;

            var bg = Selected ? Skin.SelectedBg : (_hover ? Skin.Panel : Skin.Bg);
            using (var b = new SolidBrush(Skin.Bg)) g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
            if (Selected || _hover)
                using (var card = Draw.Rounded(new Rectangle(0, 1, w - 1, h - 3), 8))
                using (var b = new SolidBrush(bg))
                    g.FillPath(b, card);
            using (var p = new Pen(Color.FromArgb(120, Skin.Border), 1f)) g.DrawLine(p, 0, h - 1, w, h - 1);

            var box = new Rectangle(10, 20, 18, 18);
            if (Item.Protected)
            {
                using (var p = new Pen(Skin.Info, 1.6f)) g.DrawPath(p, Draw.Rounded(box, 4));
                using (var f = new Font("Segoe UI", 13.3f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Skin.Info)) g.DrawString("!", f, b, box.X + 5, box.Y + 1);
            }
            else if (Selected)
            {
                using (var b = new SolidBrush(Skin.Accent)) g.FillPath(b, Draw.Rounded(box, 4));
                using (var p = new Pen(Color.White, 2f))
                {
                    g.DrawLine(p, box.X + 4, box.Y + 9, box.X + 7, box.Y + 13);
                    g.DrawLine(p, box.X + 7, box.Y + 13, box.X + 14, box.Y + 5);
                }
            }
            else
            {
                using (var p = new Pen(Item.Bytes > 0 ? Skin.Muted : Color.FromArgb(90, Skin.Border), 1.4f))
                    g.DrawPath(p, Draw.Rounded(box, 4));
            }

            const int textX = 42, barW = 120, sizeW = 96;
            float tagX = textX;
            using (var f = new Font("Segoe UI Semibold", 13.3f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Item.Bytes > 0 || Item.Protected ? Skin.Fg : Skin.Muted))
            {
                g.DrawString(Item.Name, f, b, textX, 8);
                tagX += g.MeasureString(Item.Name, f).Width + 4;
            }

            using (var f = new Font("Segoe UI", 10.5f, GraphicsUnit.Pixel))
            {
                if (Item.Advanced)
                {
                    string gr = Loc.S("grp_advanced");
                    int badgeW = (int)g.MeasureString(gr, f).Width + 12;
                    var r = new Rectangle((int)tagX, 9, badgeW, 18);
                    using (var b = new SolidBrush(Skin.Badge)) g.FillPath(b, Draw.Rounded(r, 9));
                    using (var b = new SolidBrush(Skin.Warning)) g.DrawString(gr, f, b, r.X + 6, r.Y + 2);
                    tagX += badgeW + 6;
                }
                if (Math.Abs(Item.Delta) >= 1048576L)
                    using (var b = new SolidBrush(Item.Delta > 0 ? Skin.Warning : Skin.Muted))
                        g.DrawString((Item.Delta > 0 ? "+" : "-") + Measure.Fmt(Math.Abs(Item.Delta)), f, b, tagX, 11);
            }

            using (var f = new Font("Segoe UI", 11.3f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Skin.Muted))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Item.Description, f, b, new RectangleF(textX, 32, Math.Max(40, w - textX - barW - sizeW - 36), 18), sf);

            string label = Item.Bytes == -2 ? Loc.S("measuring") : (Item.Estimated && Item.Bytes >= 0 ? "~ " : "") + Measure.Fmt(Item.Bytes);
            using (var f = new Font("Segoe UI Semibold", 13.3f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Item.Bytes == -2 ? Skin.Muted : Item.Bytes > 0 || Item.Protected ? Skin.Fg : Skin.Muted))
            {
                var m = g.MeasureString(label, f);
                g.DrawString(label, f, b, w - 10 - m.Width, (h - m.Height) / 2);
            }

            if (Ratio > 0)
            {
                var r = new Rectangle(w - 10 - sizeW - barW, (h - 6) / 2, barW, 6);
                using (var b = new SolidBrush(Skin.Border)) g.FillPath(b, Draw.Rounded(r, 3));
                var r2 = new Rectangle(r.X, r.Y, Math.Max(4, (int)(barW * Ratio)), 6);
                using (var b = new SolidBrush(SizeColor())) g.FillPath(b, Draw.Rounded(r2, 3));
            }
        }
    }

    // ========================================================================
    //  SETTINGS WINDOW
    // ========================================================================
    class SettingsWindow : Form
    {
        public Settings Result;
        readonly Theme _skin;
        ComboBox _cbTheme;
        NumericUpDown _nudMin;
        CheckBox _chkAutoScan, _chkRecycle, _chkAdvanced;
        ListBox _lbCustom;

        public SettingsWindow(Settings current, Theme skin)
        {
            Result = current; _skin = skin;
            Text = Loc.S("set_title");
            ClientSize = new Size(Dpi.Px(560), Dpi.Px(500));
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = skin.Bg; ForeColor = skin.Fg;
            Font = new Font("Segoe UI", 9f);
            Build();
        }

        Label MakeLabel(string s, int x, int y, bool strong)
        {
            return new Label
            {
                Text = s,
                Location = new Point(Dpi.Px(x), Dpi.Px(y)),
                AutoSize = true,
                ForeColor = strong ? _skin.Fg : _skin.Muted,
                Font = new Font("Segoe UI" + (strong ? " Semibold" : ""), strong ? 10f : 8.5f)
            };
        }

        void Build()
        {
            int y = 22;

            Controls.Add(MakeLabel(Loc.S("set_theme"), 26, y, true));
            _cbTheme = new ComboBox { Location = new Point(Dpi.Px(240), Dpi.Px(y - 3)), Width = Dpi.Px(290), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = _skin.Panel, ForeColor = _skin.Fg };
            _cbTheme.Items.AddRange(new object[] { Loc.S("set_dark"), Loc.S("set_light") });
            _cbTheme.SelectedIndex = Result.ThemeName == "light" ? 1 : 0;
            Controls.Add(_cbTheme);
            y += 42;

            Controls.Add(MakeLabel(Loc.S("set_min"), 26, y, true));
            _nudMin = new NumericUpDown { Location = new Point(Dpi.Px(240), Dpi.Px(y - 3)), Width = Dpi.Px(100), Minimum = 0, Maximum = 5000, Value = Math.Min(5000, Math.Max(0, Result.MinMB)), BackColor = _skin.Panel, ForeColor = _skin.Fg, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(_nudMin);
            Controls.Add(MakeLabel("MB", 348, y + 2, false));
            y += 46;

            _chkAutoScan = new CheckBox { Text = Loc.S("set_autoscan"), Location = new Point(Dpi.Px(24), Dpi.Px(y)), AutoSize = true, Checked = Result.AutoScan, ForeColor = _skin.Fg, Font = new Font("Segoe UI", 9.5f) };
            Controls.Add(_chkAutoScan);
            y += 30;

            _chkRecycle = new CheckBox { Text = Loc.S("set_recycle"), Location = new Point(Dpi.Px(24), Dpi.Px(y)), AutoSize = true, Checked = Result.UseRecycleBin, ForeColor = _skin.Fg, Font = new Font("Segoe UI", 9.5f), MaximumSize = new Size(Dpi.Px(510), 0) };
            Controls.Add(_chkRecycle);
            y += 48;

            _chkAdvanced = new CheckBox { Text = Loc.S("set_advanced"), Location = new Point(Dpi.Px(24), Dpi.Px(y - 8)), AutoSize = true, Checked = Result.ShowAdvanced, ForeColor = _skin.Fg, Font = new Font("Segoe UI", 9.5f), MaximumSize = new Size(Dpi.Px(510), 0) };
            Controls.Add(_chkAdvanced);
            y += 48;

            Controls.Add(MakeLabel(Loc.S("set_custom"), 26, y, true));
            y += 24;
            var hint = MakeLabel(Loc.S("set_custom_hint"), 26, y, false);
            hint.MaximumSize = new Size(Dpi.Px(504), 0);
            Controls.Add(hint);
            y += 40;

            _lbCustom = new ListBox { Location = new Point(Dpi.Px(26), Dpi.Px(y)), Size = new Size(Dpi.Px(504), Dpi.Px(130)), BackColor = _skin.Panel, ForeColor = _skin.Fg, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
            foreach (var o in Result.Custom) _lbCustom.Items.Add(o);
            Controls.Add(_lbCustom);
            y += 140;

            var bAdd = new FlatButton { Text = Loc.S("set_add"), Tint = _skin.Accent, Size = new Size(Dpi.Px(130), Dpi.Px(34)), Location = new Point(Dpi.Px(26), Dpi.Px(y)) };
            bAdd.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = Loc.S("set_add"), ShowNewFolderButton = false })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    string reason = Guard.Reject(d.SelectedPath);
                    if (reason != null)
                    {
                        MessageBox.Show(this, reason, Loc.S("guard_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    string norm = Guard.Normalize(d.SelectedPath);
                    if (!_lbCustom.Items.Cast<string>().Any(x => string.Equals(x, norm, StringComparison.OrdinalIgnoreCase)))
                        _lbCustom.Items.Add(norm);
                }
            };
            Controls.Add(bAdd);

            var bRemove = new FlatButton { Text = Loc.S("set_remove"), Tint = Color.FromArgb(70, 76, 86), Size = new Size(Dpi.Px(110), Dpi.Px(34)), Location = new Point(Dpi.Px(166), Dpi.Px(y)) };
            bRemove.Click += (s, e) => { if (_lbCustom.SelectedIndex >= 0) _lbCustom.Items.RemoveAt(_lbCustom.SelectedIndex); };
            Controls.Add(bRemove);

            var bCancel = new FlatButton { Text = Loc.S("set_cancel"), Tint = Color.FromArgb(70, 76, 86), Size = new Size(Dpi.Px(100), Dpi.Px(34)), Location = new Point(Dpi.Px(300), Dpi.Px(y)) };
            bCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(bCancel);

            var bSave = new FlatButton { Text = Loc.S("set_save"), Tint = _skin.Accent, Size = new Size(Dpi.Px(120), Dpi.Px(34)), Location = new Point(Dpi.Px(410), Dpi.Px(y)) };
            bSave.Click += (s, e) =>
            {
                // Re-check every folder on save; the list may be stale.
                var kept = new List<string>();
                foreach (var o in _lbCustom.Items.Cast<string>())
                {
                    string reason = Guard.Reject(o);
                    if (reason != null)
                    {
                        MessageBox.Show(this, o + "\n\n" + reason, Loc.S("guard_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }
                    kept.Add(Guard.Normalize(o));
                }
                Result.ThemeName = _cbTheme.SelectedIndex == 1 ? "light" : "dark";
                Result.MinMB = (int)_nudMin.Value;
                Result.AutoScan = _chkAutoScan.Checked;
                Result.UseRecycleBin = _chkRecycle.Checked;
                Result.ShowAdvanced = _chkAdvanced.Checked;
                Result.Custom = kept;
                Result.Save();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(bSave);
        }
    }

    // ========================================================================
    //  MAIN WINDOW
    // ========================================================================
    class MainWindow : Form
    {
        Settings _settings;
        Theme _skin;
        List<Target> _targets;
        readonly List<Row> _rows = new List<Row>();

        Panel _header, _footer, _list;
        Label _lblTitle, _lblTagline, _lblSelection, _lblStatus, _lblVersion;
        Panel _tiles;
        Label _scanHint;
        LogoMark _logo;
        StatTile _tileFree, _tileTotal, _tileDelta;
        readonly List<Label> _groupHeads = new List<Label>();
        FlatButton _btnMini, _btnScan, _btnFull, _btnSettings, _btnExport, _btnDelete, _btnClear, _btnCancel;
        ProgressLine _bar;
        CancellationTokenSource _cancel;
        bool _busy, _deleting;
        int _scanGen;                                   // ignores callbacks from a superseded scan
        Dictionary<string, Remembered> _memory;
        Panel _actions;
        bool _haveDelta;                                // the last scan had an earlier one to compare with
        DateTime _lastScan;
        MiniWindow _mini;
        IconWindow _icon;
        Point _floatSpot;
        readonly List<string> _skipped = new List<string>();
        readonly List<string> _blocked = new List<string>();
        readonly List<string> _partial = new List<string>();
        int _skippedTotal;
        long _removed, _recycled;
        CancellationToken _deleteToken;
        int _totalJobs, _doneJobs;

        public MainWindow()
        {
            _settings = Settings.Load();
            _memory = ScanMemory.Load();
            _skin = _settings.ThemeName == "light" ? Theme.Light() : Theme.Dark();

            ClientSize = new Size(Dpi.Px(980), Dpi.Px(740));
            MinimumSize = new Size(Dpi.Px(780), Dpi.Px(560));
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            if (Icon == null) Icon = MakeIcon();

            Build();
            ApplyTheme();
            Load += (s, e) => ApplyTheme();          // the window handle exists by now
            Shown += (s, e) =>
            {
                if (_settings.LoadFailed)
                    MessageBox.Show(this, Loc.S("settings_failed"), Loc.S("settings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (_settings.AutoScan) { Scan(_memory.Count > 0); return; }
                if (_memory.Count > 0) { ShowRemembered(); return; }
                _lblStatus.Text = "";
                _tileTotal.Show(Loc.S("tile_total"), "-");
                _tileDelta.Show(Loc.S("tile_delta"), "-");
                _scanHint = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = Loc.S("scan_hint"),
                    Font = new Font("Segoe UI", 10.5f),
                    BackColor = _skin.Bg,
                    ForeColor = _skin.Muted
                };
                _list.Controls.Add(_scanHint);
            };
            // Closing mid-delete would kill the worker with no report of what is left.
            FormClosing += (s, e) =>
            {
                if (!_deleting || e.CloseReason == CloseReason.WindowsShutDown) return;
                e.Cancel = true;
                MessageBox.Show(this, Loc.S("busy_close"), Loc.S("app"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            FormClosed += (s, e) =>
            {
                if (_mini != null) _mini.Close();
                if (_icon != null) _icon.Close();
            };
        }

        static Icon MakeIcon()
        {
            try
            {
                var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp)) Logo.Paint(g, new RectangleF(0, 0, 32, 32));
                return Icon.FromHandle(bmp.GetHicon());
            }
            catch { return null; }
        }

        // ---------------------------------------------------------------
        void Build()
        {
            // WinForms docks the last-added control first, so the Fill panel goes in first
            // and the header, which must hug the top edge, goes in last.
            _list = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(Dpi.Px(24), 0, Dpi.Px(14), Dpi.Px(16)) };
            Controls.Add(_list);

            _footer = new Panel { Dock = DockStyle.Bottom, Height = Dpi.Px(64) };
            Controls.Add(_footer);

            _lblSelection = new Label { Font = new Font("Segoe UI Semibold", 11f), Location = new Point(Dpi.Px(24), Dpi.Px(21)), AutoSize = true };
            _footer.Controls.Add(_lblSelection);

            _btnDelete = new FlatButton { Size = new Size(Dpi.Px(150), Dpi.Px(38)), Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnDelete.Click += (s, e) => Delete();
            _footer.Controls.Add(_btnDelete);

            _btnClear = new FlatButton { Size = new Size(Dpi.Px(124), Dpi.Px(38)), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnClear.Click += (s, e) => { foreach (var r in _rows) { r.Selected = false; r.Invalidate(); } UpdateSelection(); };
            _footer.Controls.Add(_btnClear);

            _actions = new Panel { Dock = DockStyle.Top, Height = Dpi.Px(56) };
            Controls.Add(_actions);

            _btnScan = new FlatButton { Size = new Size(Dpi.Px(122), Dpi.Px(36)) };
            _btnScan.Click += (s, e) => Scan(_memory.Count > 0);
            _actions.Controls.Add(_btnScan);

            _btnCancel = new FlatButton { Size = new Size(Dpi.Px(122), Dpi.Px(36)), Visible = false };
            _btnCancel.Click += (s, e) =>
            {
                if (_cancel != null) _cancel.Cancel();
                _lblStatus.Text = _deleting ? Loc.S("stopping") : Loc.S("cancelled");
                _btnCancel.Enabled = false;
            };
            _actions.Controls.Add(_btnCancel);

            _btnFull = new FlatButton { Size = new Size(Dpi.Px(104), Dpi.Px(36)), Visible = false };
            _btnFull.Click += (s, e) => Scan(false);
            _actions.Controls.Add(_btnFull);

            _lblStatus = new Label { Font = new Font("Segoe UI", 9f), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _actions.Controls.Add(_lblStatus);

            _bar = new ProgressLine { Dock = DockStyle.Bottom };
            _actions.Controls.Add(_bar);

            _tiles = new Panel { Dock = DockStyle.Top, Height = Dpi.Px(80) };
            Controls.Add(_tiles);
            _tileFree = new StatTile();
            _tileTotal = new StatTile();
            _tileDelta = new StatTile();
            _tiles.Controls.Add(_tileFree);
            _tiles.Controls.Add(_tileTotal);
            _tiles.Controls.Add(_tileDelta);

            _header = new Panel { Dock = DockStyle.Top, Height = Dpi.Px(68) };
            Controls.Add(_header);

            _logo = new LogoMark { Location = new Point(Dpi.Px(24), Dpi.Px(16)), Size = new Size(Dpi.Px(38), Dpi.Px(38)) };
            _header.Controls.Add(_logo);

            _lblTitle = new Label { Text = "CacheSweep", Font = new Font("Segoe UI Semibold", 13f), Location = new Point(Dpi.Px(70), Dpi.Px(11)), AutoSize = true };
            _header.Controls.Add(_lblTitle);

            _lblTagline = new Label { Font = new Font("Segoe UI", 8.5f), Location = new Point(Dpi.Px(72), Dpi.Px(37)), AutoSize = true };
            _header.Controls.Add(_lblTagline);

            _lblVersion = new Label
            {
                Text = "v" + Info.Version + "  -  " + Info.Developer,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, Dpi.Px(16)),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _lblVersion.Click += (s, e) => ShowAbout();
            _header.Controls.Add(_lblVersion);

            _btnMini = new FlatButton { Size = new Size(Dpi.Px(36), Dpi.Px(32)), Font = new Font("Segoe MDL2 Assets", 12f, GraphicsUnit.Pixel), Text = "\uE73F", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnMini.Click += (s, e) => ShowMini();
            _header.Controls.Add(_btnMini);

            _btnSettings = new FlatButton { Size = new Size(Dpi.Px(88), Dpi.Px(32)), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnSettings.Click += (s, e) => ShowSettings();
            _header.Controls.Add(_btnSettings);

            _btnExport = new FlatButton { Size = new Size(Dpi.Px(80), Dpi.Px(32)), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnExport.Click += (s, e) => Export();
            _header.Controls.Add(_btnExport);

            Resize += (s, e) => LayoutControls();
            LayoutControls();
            ApplyTexts();
        }

        void LayoutControls()
        {
            // Header and footer actions flow in from the right edge, each placed
            // against the one before it. Fixed offsets used to be tuned to one set
            // of label widths, so a longer number or a translated caption landed on
            // top of its neighbour.
            int edge = Dpi.Px(24), gap = Dpi.Px(8);

            _lblVersion.Location = new Point(_lblTitle.Right + Dpi.Px(8), Dpi.Px(16));

            int x = ClientSize.Width - edge - _btnMini.Width;
            _btnMini.Location = new Point(x, Dpi.Px(18));
            x -= gap + _btnSettings.Width;
            _btnSettings.Location = new Point(x, Dpi.Px(18));
            x -= gap + _btnExport.Width;
            _btnExport.Location = new Point(x, Dpi.Px(18));

            // Three tiles share the width, whatever the window size.
            int tileGap = Dpi.Px(12), tileW = (ClientSize.Width - edge * 2 - tileGap * 2) / 3, tileH = Dpi.Px(72);
            _tileFree.SetBounds(edge, 0, tileW, tileH);
            _tileTotal.SetBounds(edge + tileW + tileGap, 0, tileW, tileH);
            _tileDelta.SetBounds(edge + (tileW + tileGap) * 2, 0, tileW, tileH);

            // Cancel takes Scan's place while a scan runs. Full scan only exists once there is
            // something to compare with, and the status line takes what is left.
            int ax = edge;
            _btnScan.Location = new Point(ax, Dpi.Px(8));
            _btnCancel.Location = new Point(ax, Dpi.Px(8));
            ax += _btnScan.Width + gap;
            if (_memory.Count > 0)
            {
                _btnFull.Location = new Point(ax, Dpi.Px(8));
                ax += _btnFull.Width + gap;
            }
            _lblStatus.SetBounds(ax + Dpi.Px(4), Dpi.Px(8), Math.Max(Dpi.Px(40), ClientSize.Width - edge - ax - Dpi.Px(4)), Dpi.Px(36));

            int fx = ClientSize.Width - edge - _btnDelete.Width;
            _btnDelete.Location = new Point(fx, Dpi.Px(13));
            fx -= gap + _btnClear.Width;
            _btnClear.Location = new Point(fx, Dpi.Px(13));

            foreach (var r in _rows) r.Width = _list.ClientSize.Width - Dpi.Px(38);
        }

        void ApplyTexts()
        {
            Text              = "CacheSweep - " + Loc.S("tagline");
            _lblTagline.Text  = _lastScan > DateTime.MinValue ? Loc.S("last_scan", ScanMemory.Age(_lastScan)) : Loc.S("tagline");
            _btnScan.Text     = Loc.S(_memory.Count > 0 ? "quick_scan" : "rescan");
            _btnFull.Text     = Loc.S("full_scan");
            _btnFull.Visible  = _memory.Count > 0 && !_busy;
            _btnCancel.Text   = Loc.S("cancel");
            _btnSettings.Text = Loc.S("settings");
            _btnExport.Text   = Loc.S("export");
            _btnDelete.Text   = Loc.S("delete_sel");
            _btnClear.Text    = Loc.S("clear_sel");
            ShowFree();
            UpdateSelection();
            LayoutControls();
        }

        void ShowFree() { _tileFree.Show(Loc.S("tile_free"), Measure.Fmt(Measure.FreeSpace())); }

        void ApplyTheme()
        {
            // Title bar and scrollbars belong to Windows; without this a dark app
            // wears a white caption strip. Needs a handle, so it also runs on Load.
            NativeChrome.Apply(this, _settings.ThemeName != "light", _skin.Bg, _skin.Fg, _skin.Border);
            BackColor = _skin.Bg;
            _header.BackColor = _skin.Bg;
            _actions.BackColor = _skin.Bg;
            _footer.BackColor = _skin.Panel;
            _list.BackColor = _skin.Bg;
            _lblTitle.ForeColor = _skin.Fg;
            _lblTagline.ForeColor = _skin.Muted;
            _lblVersion.ForeColor = _skin.Accent;
            _tiles.BackColor = _skin.Bg;
            _logo.BackColor = _skin.Bg;
            foreach (var t in new[] { _tileFree, _tileTotal, _tileDelta }) { t.Skin = _skin; t.Invalidate(); }
            foreach (var h in _groupHeads) { h.BackColor = _skin.Bg; h.ForeColor = _skin.Muted; }
            if (_scanHint != null) { _scanHint.BackColor = _skin.Bg; _scanHint.ForeColor = _skin.Muted; }
            _lblSelection.ForeColor = _skin.Fg;
            _lblStatus.ForeColor = _skin.Muted;
            _btnScan.Tint = _skin.Accent;
            _btnCancel.Tint = _skin.Warning;
            _btnFull.Tint = _skin.PanelRaised;
            _btnFull.Ink = _skin.Fg;
            _btnMini.Tint = _skin.PanelRaised;
            _btnMini.Ink = _skin.Fg;
            _btnSettings.Tint = _skin.PanelRaised;
            _btnSettings.Ink = _skin.Fg;
            _btnExport.Tint = _skin.PanelRaised;
            _btnExport.Ink = _skin.Fg;
            _btnClear.Tint = _skin.PanelRaised;
            _btnClear.Ink = _skin.Fg;
            _btnDelete.Tint = _skin.Danger;
            _bar.Tint = _skin.Accent;
            _bar.Track = _skin.Bg;
            foreach (var r in _rows) { r.Skin = _skin; r.Invalidate(); }
            Invalidate(true);
        }

        /// <summary>Buttons that must not be used while a scan or delete is running.</summary>
        void SetBusy(bool busy)
        {
            _busy = busy;
            _btnSettings.Enabled = !busy;
            _btnExport.Enabled = !busy;
            _btnScan.Visible = !busy;
            _btnFull.Visible = !busy && _memory.Count > 0;
            _btnCancel.Visible = busy;
            _btnCancel.Enabled = busy;
            _btnMini.Enabled = !busy;
            if (busy) _btnDelete.Enabled = false;
            if (StateChanged != null) StateChanged(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------------
        //  Floating modes. The full window stays alive behind them, hidden.
        public event EventHandler StateChanged;
        public Theme Skin { get { return _skin; } }
        public bool Working { get { return _busy; } }
        public bool HasMemory { get { return _memory.Count > 0; } }

        public void RunScan() { Scan(_memory.Count > 0); }

        public string CacheLine()
        {
            if (_busy) return Loc.S("scanning");
            if (_rows.Count == 0) return Loc.S("mini_none");
            string caches = Sum(_rows.Where(r => !r.Item.Protected).Select(r => r.Item));
            long delta = _targets.Sum(t => t.Delta);
            if (_haveDelta && Math.Abs(delta) >= 1048576L) return Loc.S("mini_delta", caches, SignedSize(delta));
            return _lastScan > DateTime.MinValue ? Loc.S("mini_age", caches, ScanMemory.Age(_lastScan)) : caches;
        }

        void Float(FloatingWindow show, FloatingWindow other)
        {
            if (_floatSpot.IsEmpty)
            {
                var area = Screen.FromControl(this).WorkingArea;
                _floatSpot = new Point(area.Right - Dpi.Px(284), area.Top + Dpi.Px(24));
            }
            if (other != null && other.Visible) { _floatSpot = other.Location; other.Hide(); }

            // A monitor may have gone since the window was last moved, so keep it reachable.
            var work = Screen.FromPoint(_floatSpot).WorkingArea;
            show.Location = new Point(
                Math.Max(work.Left, Math.Min(_floatSpot.X, work.Right - show.Width)),
                Math.Max(work.Top, Math.Min(_floatSpot.Y, work.Bottom - show.Height)));
            Hide();
            show.Show();
        }

        public void ShowMini()
        {
            if (_mini == null) _mini = new MiniWindow(this);
            Float(_mini, _icon);
        }

        public void ShowIcon()
        {
            if (_icon == null) _icon = new IconWindow(this);
            Float(_icon, _mini);
        }

        public void ShowFull()
        {
            foreach (FloatingWindow w in new FloatingWindow[] { _mini, _icon })
                if (w != null && w.Visible) { _floatSpot = w.Location; w.Hide(); }
            ShowFree();
            ApplyTexts();
            Show();
            Activate();
        }

        void ShowSettings()
        {
            if (_busy) return;
            using (var d = new SettingsWindow(_settings, _skin))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                _settings = d.Result;
                _skin = _settings.ThemeName == "light" ? Theme.Light() : Theme.Dark();
                ApplyTheme();
                ApplyTexts();
                if (_rows.Count > 0) Scan(_memory.Count > 0);
            }
        }

        // ---------------------------------------------------------------
        /// <summary>Shows what the last scan found without measuring anything.</summary>
        void ShowRemembered()
        {
            ScanMemory.Apply(BuildRows(), _memory);
            _haveDelta = false;
            var known = _targets.Where(t => t.Estimated).ToList();
            if (known.Count > 0) _lastScan = known.Max(t => t.MeasuredAt);
            ShowResults();
            ApplyTexts();
            if (known.Count > 0)
                _lblStatus.Text = Loc.S("remembered", ScanMemory.Age(known.Min(t => t.MeasuredAt)));
        }

        /// <summary>Lays out one row for each target that exists on disk, and returns those targets.</summary>
        List<Target> BuildRows()
        {
            if (_scanHint != null) { _scanHint.Dispose(); _scanHint = null; }

            foreach (var old in _rows) old.Dispose();
            foreach (var old in _groupHeads) old.Dispose();
            _list.Controls.Clear();
            _rows.Clear();
            _groupHeads.Clear();

            _targets = Catalog.Build(_settings);
            foreach (var t in _targets)
            {
                t.Bytes = -2;
                Catalog.Locate(t);
            }

            var shown = _targets.Where(t => t.Found.Count > 0).ToList();

            // Dock Top stacks the last-added control highest, so groups and rows go in backwards.
            foreach (var grp in shown.GroupBy(t => t.Group).Reverse())
            {
                foreach (var t in grp.Reverse())
                {
                    var r = new Row(t, _skin) { Width = _list.ClientSize.Width - Dpi.Px(36) };
                    r.SelectionChanged += (s, e) => UpdateSelection();
                    _list.Controls.Add(r);
                    _rows.Add(r);
                }
                var head = new Label
                {
                    Tag = grp.Key,
                    Text = Loc.S2("grp_" + grp.Key.ToLowerInvariant(), grp.Key),
                    Dock = DockStyle.Top,
                    Height = Dpi.Px(34),
                    TextAlign = ContentAlignment.BottomLeft,
                    Padding = new Padding(Dpi.Px(4), 0, 0, Dpi.Px(5)),
                    Font = new Font("Segoe UI Semibold", 9f),
                    BackColor = _skin.Bg,
                    ForeColor = _skin.Muted
                };
                _list.Controls.Add(head);
                _groupHeads.Add(head);
            }
            return shown;
        }

        // ---------------------------------------------------------------
        void Scan(bool quick)
        {
            if (_busy) return;
            SetBusy(true);
            int gen = ++_scanGen;

            var shown = BuildRows();
            var memory = _memory;
            _haveDelta = memory.Count > 0;
            _tileTotal.Show(Loc.S("tile_total"), Loc.S("measuring"));
            _tileDelta.Show(Loc.S("tile_delta"), "-");
            _totalJobs = shown.Count;
            _doneJobs = 0;
            _bar.Indeterminate = false;
            _bar.Value = 0;
            _bar.Invalidate();
            _lblStatus.Text = Loc.S("scanning");

            if (_totalJobs == 0) { OnScanDone(quick); return; }

            _cancel = new CancellationTokenSource();
            var token = _cancel.Token;

            Task.Run(() =>
            {
                try
                {
                Parallel.ForEach(shown,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) },
                    t =>
                    {
                        // Taken before measuring, so a change made during the measurement still shows next time.
                        string print = t.UnityLog || t.MinAgeDays > 0 ? "" : ScanMemory.Fingerprint(t);
                        Remembered known;
                        memory.TryGetValue(ScanMemory.KeyOf(t), out known);
                        t.Print = print;
                        if (quick && !ScanMemory.MustMeasure(t, known, print))
                        {
                            t.MeasuredAt = known.At;
                            t.Estimated = true;
                            t.Bytes = known.Bytes;
                        }
                        else
                        {
                            t.MeasuredAt = DateTime.UtcNow;
                            t.Bytes = Sweeper.SizeOf(t, token);
                            if (known != null && t.Bytes >= 0) t.Delta = t.Bytes - known.Bytes;
                        }

                        try
                        {
                            BeginInvoke((Action)(() =>
                            {
                                if (gen != _scanGen) return;
                                var row = _rows.FirstOrDefault(x => x.Item == t);
                                if (row != null) row.Invalidate();
                                _doneJobs++;
                                _bar.Value = _totalJobs == 0 ? 1 : (double)_doneJobs / _totalJobs;
                                _bar.Invalidate();
                                _lblStatus.Text = Loc.S("scanning_n", _totalJobs - _doneJobs);
                                if (_doneJobs >= _totalJobs) OnScanDone(quick);
                            }));
                        }
                        catch { }
                    });
                }
                catch
                {
                    // Whatever went wrong, the window must not stay in the busy state.
                    try { BeginInvoke((Action)(() => { if (gen == _scanGen && _busy) OnScanDone(quick); })); } catch { }
                }
            });
        }

        void OnScanDone(bool quick)
        {
            ScanMemory.Update(_memory, _targets);
            ScanMemory.Save(_memory);
            _lastScan = DateTime.UtcNow;

            SetBusy(false);
            _btnScan.Enabled = true;
            _bar.Value = 0; _bar.Invalidate();
            ShowResults();
            ApplyTexts();

            if (quick)
            {
                var measured = _targets.Where(t => t.Found.Count > 0).ToList();
                int reused = measured.Count(t => t.Estimated);
                _lblStatus.Text = Loc.S("quick_done", measured.Count - reused, reused);
            }
        }

        /// <summary>Sorts out the rows once every size is known: drops the small ones, fills in the bars and totals.</summary>
        void ShowResults()
        {
            long largest = 1;
            foreach (var r in _rows) if (!r.Item.Protected && r.Item.Bytes > largest) largest = r.Item.Bytes;

            long threshold = (long)_settings.MinMB * 1048576L;
            foreach (var r in _rows.ToList())
            {
                if (!r.Item.Protected && r.Item.Bytes >= 0 && r.Item.Bytes < threshold)
                {
                    _list.Controls.Remove(r);
                    _rows.Remove(r);
                    r.Dispose();
                    continue;
                }
                r.Ratio = r.Item.Bytes > 0 ? (double)r.Item.Bytes / largest : 0;
                r.Invalidate();
            }

            // A group whose rows were all below the size limit loses its heading too.
            foreach (var h in _groupHeads.ToList())
                if (!_rows.Any(r => r.Item.Group == (string)h.Tag))
                {
                    _list.Controls.Remove(h);
                    _groupHeads.Remove(h);
                    h.Dispose();
                }

            _lblStatus.Text = Loc.S("protected_hint");
            ShowFree();
            LayoutControls();
            ShowTotals();
        }

        static string Sum(IEnumerable<Target> items)
        {
            var known = items.Where(t => t.Bytes > 0).ToList();
            return (known.Any(t => t.Estimated) ? "~ " : "") + Measure.Fmt(known.Sum(t => t.Bytes));
        }

        static string SignedSize(long bytes)
        {
            if (Math.Abs(bytes) < 1048576L) return "0";
            return (bytes > 0 ? "+" : "-") + Measure.Fmt(Math.Abs(bytes));
        }

        void ShowTotals()
        {
            _tileTotal.Show(Loc.S("tile_total"), Sum(_rows.Where(r => !r.Item.Protected).Select(r => r.Item)));
            long delta = _targets == null ? 0 : _targets.Sum(t => t.Delta);
            _tileDelta.Ink = delta >= 1048576L && _haveDelta ? _skin.Warning : Color.Empty;
            _tileDelta.Show(Loc.S("tile_delta"), _haveDelta ? SignedSize(delta) : "-");
            UpdateSelection();
            if (StateChanged != null) StateChanged(this, EventArgs.Empty);
        }

        void UpdateSelection()
        {
            var sel = _rows.Where(r => r.Selected && !r.Item.Protected).ToList();
            string t = Sum(sel.Select(r => r.Item));
            _lblSelection.Text = sel.Count == 0 ? Loc.S("selected_none") : Loc.S("selected_n", sel.Count, t);
            _btnDelete.Enabled = sel.Count > 0 && !_busy;
        }

        void ShowAbout()
        {
            using (var f = new Form())
            {
                f.Text = Loc.S("about");
                f.ClientSize = new Size(Dpi.Px(460), Dpi.Px(330));
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false;
                f.BackColor = _skin.Bg; f.ForeColor = _skin.Fg;
                f.Font = new Font("Segoe UI", 9f);
                try { f.Icon = Icon; } catch { }

                f.Controls.Add(new LogoMark { Size = new Size(Dpi.Px(64), Dpi.Px(64)), Location = new Point(Dpi.Px(28), Dpi.Px(26)), BackColor = _skin.Bg });

                f.Controls.Add(new Label { Text = "CacheSweep", Font = new Font("Segoe UI Semibold", 17f), ForeColor = _skin.Fg, Location = new Point(Dpi.Px(106), Dpi.Px(28)), AutoSize = true });
                f.Controls.Add(new Label { Text = "v" + Info.Version, ForeColor = _skin.Muted, Location = new Point(Dpi.Px(108), Dpi.Px(62)), AutoSize = true });
                f.Controls.Add(new Label { Text = Loc.S("about_desc"), ForeColor = _skin.Muted, Location = new Point(Dpi.Px(30), Dpi.Px(106)), Size = new Size(Dpi.Px(400), Dpi.Px(60)) });

                int y = 180;
                Action<string, string> line = (label, value) =>
                {
                    f.Controls.Add(new Label { Text = label, ForeColor = _skin.Muted, Location = new Point(Dpi.Px(30), Dpi.Px(y)), Size = new Size(Dpi.Px(110), Dpi.Px(20)) });
                    f.Controls.Add(new Label { Text = value, ForeColor = _skin.Fg, Font = new Font("Segoe UI Semibold", 9f), Location = new Point(Dpi.Px(146), Dpi.Px(y)), AutoSize = true });
                    y += 26;
                };

                line(Loc.S("about_by"), Info.Developer + (Info.Handle.Length > 0 ? "  " + Info.Handle : ""));
                line(Loc.S("about_lic"), Info.License);

                f.Controls.Add(new Label { Text = Loc.S("about_src"), ForeColor = _skin.Muted, Location = new Point(Dpi.Px(30), Dpi.Px(y)), Size = new Size(Dpi.Px(110), Dpi.Px(20)) });
                var link = new LinkLabel
                {
                    Text = Info.GitHub,
                    Location = new Point(Dpi.Px(146), Dpi.Px(y)),
                    AutoSize = true,
                    LinkColor = _skin.Accent,
                    ActiveLinkColor = _skin.Accent,
                    VisitedLinkColor = _skin.Info,
                    BackColor = Color.Transparent
                };
                link.Click += (s2, e2) => { try { Process.Start(Info.GitHub); } catch { } };
                f.Controls.Add(link);

                var close = new FlatButton { Text = Loc.S("about_close"), Tint = Color.FromArgb(70, 76, 86), Size = new Size(Dpi.Px(110), Dpi.Px(34)), Location = new Point(Dpi.Px(322), Dpi.Px(272)) };
                close.Click += (s2, e2) => f.Close();
                f.Controls.Add(close);

                f.ShowDialog(this);
            }
        }

        // ---------------------------------------------------------------
        void Export()
        {
            if (_rows.Count == 0 || _busy) return;
            using (var d = new SaveFileDialog
            {
                Title = Loc.S("exp_title"),
                Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json",
                FileName = "cachesweep-" + DateTime.Now.ToString("yyyyMMdd-HHmm")
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                bool json = d.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

                if (json)
                {
                    sb.AppendLine("{");
                    sb.AppendLine("  \"generated\": \"" + DateTime.Now.ToString("s") + "\",");
                    sb.AppendLine("  \"freeBytes\": " + Measure.FreeSpace() + ",");
                    sb.AppendLine("  \"items\": [");
                    var rows = _rows.OrderByDescending(r => r.Item.Bytes).ToList();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var t = rows[i].Item;
                        sb.Append("    { \"group\": \"" + Esc(t.Group) + "\", \"name\": \"" + Esc(t.Name) +
                                  "\", \"bytes\": " + t.Bytes + ", \"estimated\": " + (t.Estimated ? "true" : "false") + ", \"protected\": " + (t.Protected ? "true" : "false") +
                                  ", \"paths\": [" + string.Join(", ", t.Found.Select(p => "\"" + Esc(p) + "\"")) + "] }");
                        sb.AppendLine(i < rows.Count - 1 ? "," : "");
                    }
                    sb.AppendLine("  ]");
                    sb.AppendLine("}");
                }
                else
                {
                    sb.AppendLine("Group;Name;Bytes;GB;Estimated;Protected;Paths");
                    foreach (var r in _rows.OrderByDescending(x => x.Item.Bytes))
                    {
                        var t = r.Item;
                        sb.AppendLine(string.Join(";", new[]
                        {
                            t.Group, t.Name, t.Bytes.ToString(),
                            (t.Bytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture),
                            t.Estimated ? "yes" : "no",
                            t.Protected ? "yes" : "no",
                            string.Join(" | ", t.Found)
                        }));
                    }
                }

                try
                {
                    File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show(Loc.S("exp_ok", d.FileName), "CacheSweep", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "CacheSweep", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }

        // ---------------------------------------------------------------
        void Delete()
        {
            if (_busy) return;
            var sel = Enumerable.Reverse(_rows).Where(r => r.Selected && !r.Item.Protected).Select(r => r.Item).ToList();
            if (sel.Count == 0) return;
            if (sel.Any(t => t.Estimated)) { MeasureThenConfirm(sel); return; }

            sel = Catalog.WithoutNested(sel);

            // The setting is read once, here, and never again while deleting.
            bool recycle = _settings.UseRecycleBin;

            var held = new Dictionary<Target, List<string>>();
            foreach (var t in sel)
            {
                var tools = RunningTools.Find(t);
                if (tools.Count > 0) held[t] = tools;
            }
            var go = sel.Where(t => !held.ContainsKey(t)).ToList();

            using (var d = new ConfirmWindow(go, held, recycle, _settings.ThemeName != "light", _skin))
                if (d.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true);
            _deleting = true;
            _bar.Value = 0; _bar.Invalidate();
            _skipped.Clear();
            _blocked.Clear();
            _partial.Clear();
            _skippedTotal = 0;
            _removed = 0;
            _recycled = 0;
            _cancel = new CancellationTokenSource();
            _deleteToken = _cancel.Token;
            var token = _deleteToken;
            int n = 0;

            // The Recycle Bin path goes through the shell (SHFileOperation), which wants an STA thread.
            var worker = new Thread(() =>
            {
                try
                {
                foreach (var t in go)
                {
                    if (token.IsCancellationRequested) break;
                    try { BeginInvoke((Action)(() => _lblStatus.Text = Loc.S("deleting", t.Name))); } catch { }

                    // The tool may have started while the confirmation was open.
                    var late = RunningTools.Find(t);
                    if (late.Count > 0) { Block(t.Name, Loc.S("held_late", string.Join(", ", late))); continue; }

                    int skippedBefore = _skippedTotal;
                    if (t.UnityLog)
                    {
                        foreach (var dir in t.Found)
                        {
                            try
                            {
                                foreach (var f in Directory.EnumerateFiles(dir, "*.log.*"))
                                {
                                    if (token.IsCancellationRequested) break;
                                    if (!DeleteEntry(f, false, recycle, dir)) Skip(f);
                                }
                            }
                            catch (Exception ex) { Block(dir, ex.Message); }
                        }
                    }
                    else
                    {
                        foreach (var path in t.Found)
                        {
                            if (!Directory.Exists(path)) continue;
                            string reason = Guard.CheckBeforeDelete(path, t.Custom);
                            if (reason != null) { Block(path, reason); continue; }

                            List<Entry> entries;
                            try { entries = Sweeper.EntriesOf(t, path); }
                            catch (Exception ex) { Block(path, ex.Message); continue; }

                            foreach (var e in entries)
                            {
                                if (token.IsCancellationRequested) break;
                                if (!DeleteEntry(e.Path, e.IsFolder, recycle, path) && !token.IsCancellationRequested) Skip(e.Path);
                            }
                        }
                    }
                    if (_skippedTotal > skippedBefore || token.IsCancellationRequested) _partial.Add(t.Name);

                    n++;
                    int done = n;
                    try { BeginInvoke((Action)(() => { _bar.Value = (double)done / go.Count; _bar.Invalidate(); })); } catch { }
                }

                bool stopped = token.IsCancellationRequested;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _deleting = false;
                        SetBusy(false);
                        _bar.Value = 0; _bar.Invalidate();
                        ShowFree();

                        var msg = new StringBuilder();
                        if (stopped) msg.AppendLine(Loc.S("stopped"));
                        if (_removed > 0) msg.AppendLine(Loc.S("gained", Measure.Fmt(_removed)));
                        if (_recycled > 0) msg.AppendLine(Loc.S("gained_recycle", Measure.Fmt(_recycled)));
                        if (_removed <= 0 && _recycled <= 0) msg.AppendLine(Loc.S("gained_none"));
                        if (_partial.Count > 0)
                        {
                            msg.AppendLine();
                            msg.AppendLine(Loc.S("partial_head"));
                            foreach (var x in _partial.Take(6)) msg.AppendLine("   " + x);
                        }
                        if (_blocked.Count > 0)
                        {
                            msg.AppendLine();
                            msg.AppendLine(Loc.S("blocked_head", _blocked.Count));
                            foreach (var x in _blocked.Take(6)) msg.AppendLine("   " + x);
                        }
                        if (_skippedTotal > 0)
                        {
                            msg.AppendLine();
                            msg.AppendLine(Loc.S("skipped_head", _skippedTotal));
                            msg.AppendLine();
                            foreach (var x in _skipped.Take(6)) msg.AppendLine("   " + x);
                            var culprit = GuessCulprit(_skipped);
                            if (culprit != null) { msg.AppendLine(); msg.AppendLine(Loc.S("locked_by", culprit)); }
                        }
                        MessageBox.Show(this, msg.ToString(), Loc.S("done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Scan(true);
                    }));
                }
                catch { }
                }
                catch (Exception ex)
                {
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            _deleting = false;
                            SetBusy(false);
                            UpdateSelection();
                            MessageBox.Show(this, ex.Message, Loc.S("done"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                    catch { }
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        /// <summary>A remembered size must never reach the confirmation, so the selected folders
        /// are measured first. Cancelling here ends the delete before it was offered.</summary>
        void MeasureThenConfirm(List<Target> sel)
        {
            SetBusy(true);
            _lblStatus.Text = Loc.S("measure_first");
            _cancel = new CancellationTokenSource();
            var token = _cancel.Token;

            Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(sel, t =>
                    {
                        Catalog.Locate(t);
                        long size = Sweeper.SizeOf(t, token);
                        if (token.IsCancellationRequested) return;
                        t.Bytes = size;
                        t.Estimated = false;
                    });
                }
                catch { }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        SetBusy(false);
                        foreach (var r in _rows) r.Invalidate();
                        ShowTotals();
                        if (sel.Any(t => t.Estimated)) return;
                        _lblStatus.Text = Loc.S("protected_hint");
                        Delete();
                    }));
                }
                catch { }
            });
        }

        void Skip(string path) { _skippedTotal++; lock (_skipped) { if (_skipped.Count < 40) _skipped.Add(path); } }
        void Block(string path, string reason) { lock (_blocked) { if (_blocked.Count < 40) _blocked.Add(path + "  -  " + reason); } }

        /// <summary>Deletes one file or folder. Refuses anything that is not directly inside
        /// <paramref name="parent"/>, anything protected, and anything the guard rejects.
        /// Returns true only when the entry is really gone or was refused and reported.</summary>
        bool DeleteEntry(string path, bool isFolder, bool recycle, string parent)
        {
            try
            {
                if (!Guard.IsUnder(path, parent)) return false;
                if (Guard.IsProtected(path) || Guard.TouchesProtected(path)) { Block(path, Loc.S("guard_protected", path)); return true; }

                if (isFolder)
                {
                    var di = new DirectoryInfo(path);
                    // Never follow a junction or symlink out of the cache folder: unlink only.
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) { Directory.Delete(path, false); return !Directory.Exists(path); }
                    if (recycle)
                    {
                        long size = Measure.EntrySize(path, true, _deleteToken);
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                            Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
                        bool gone = !Directory.Exists(path);
                        if (gone && size > 0) _recycled += size;
                        return gone;
                    }
                    return Sweeper.DeleteTree(path, _deleteToken, ref _removed);
                }
                else
                {
                    if (recycle)
                    {
                        long size = Measure.EntrySize(path, false, _deleteToken);
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                            Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
                        bool gone = !File.Exists(path);
                        if (gone && size > 0) _recycled += size;
                        return gone;
                    }
                    return Sweeper.DeleteFile(path, ref _removed);
                }
            }
            catch { return false; }
        }

        // Looks at the locked paths and guesses which tool is holding them.
        static string GuessCulprit(List<string> paths)
        {
            try
            {
                var all = string.Join(" ", paths).ToLowerInvariant();
                var hints = new Dictionary<string, string[]>
                {
                    { "uv / Python",             new[] { "\\uv\\" } },
                    { "Node / npm",              new[] { "npm-cache", "node-gyp", "pnpm" } },
                    { "Unity",                   new[] { "\\unity" } },
                    { "Android Studio / Gradle", new[] { ".gradle", "\\android\\" } },
                    { "Visual Studio / .NET",    new[] { ".nuget" } },
                };
                foreach (var h in hints)
                    foreach (var needle in h.Value)
                        if (all.Contains(needle)) return h.Key;
            }
            catch { }
            return null;
        }

        [STAThread]
        static void Main()
        {
            Dpi.Init();                  // must happen before any window exists
            Loc.LoadPack();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using (new Mutex(true, "DevSuite.CacheSweep", out first))
            {
                if (first) { Application.Run(new MainWindow()); return; }
                try
                {
                    int self = Process.GetCurrentProcess().Id;
                    var other = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).First(p => p.Id != self);
                    Microsoft.VisualBasic.Interaction.AppActivate(other.Id);
                }
                catch { }
            }
        }
    }
}
