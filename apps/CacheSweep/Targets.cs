using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CacheSweep
{
    // ========================================================================
    //  TARGET MODEL - one row of the list, and the rules for emptying it
    // ========================================================================
    class Target
    {
        public string Group, Name, Description;
        public string[] Paths = new string[0];
        public bool Protected, UnityLog, Custom;

        /// <summary>Not a plain cache: hidden unless the user turns on advanced items.</summary>
        public bool Advanced;

        /// <summary>Process names that must not be running while this target is emptied.</summary>
        public string[] Tools = new string[0];

        /// <summary>A Gradle daemon is a java process, so it needs its own check.</summary>
        public bool GradleDaemon;

        /// <summary>Names directly under the target that are kept. A trailing * matches a prefix.</summary>
        public string[] Exclude = new string[0];

        /// <summary>Entries changed more recently than this are kept. 0 keeps nothing.</summary>
        public int MinAgeDays;

        public long Bytes = -2;                 // -2 measuring, -1 unknown/cancelled
        public List<string> Found = new List<string>();

        /// <summary>The size shown comes from an earlier scan and is measured again before a delete.</summary>
        public bool Estimated;
        public DateTime MeasuredAt;
        public long Delta;                      // change since the remembered size, when measured again
        public string Print;

        public Target Needs(params string[] tools) { Tools = tools; return this; }
        public Target Gradle() { GradleDaemon = true; return this; }
        public Target Keep(params string[] names) { Exclude = names; return this; }
        public Target OlderThan(int days) { MinAgeDays = days; return this; }
        public Target AdvancedOnly() { Advanced = true; return this; }

        public bool Keeps(string name)
        {
            foreach (var x in Exclude)
            {
                if (x.EndsWith("*"))
                {
                    if (name.StartsWith(x.Substring(0, x.Length - 1), StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (string.Equals(name, x, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>True when emptying this target removes the given path too.</summary>
        public bool Empties(string path)
        {
            if (UnityLog || MinAgeDays > 0) return false;
            foreach (var root in Found)
            {
                if (!Guard.IsUnder(path, root)) continue;
                string first = Guard.Normalize(path).Substring(Guard.Normalize(root).Length).TrimStart('\\').Split('\\')[0];
                if (!Keeps(first)) return true;
            }
            return false;
        }
    }

    // ========================================================================
    //  CATALOG - what the app knows how to clean, and what each one costs.
    //  Every description says what the user has to do to get it back.
    // ========================================================================
    static class Catalog
    {
        /// <summary>
        /// Drops a target that another selected one already empties, so its bytes are not counted
        /// twice. A nested target the parent keeps, such as _npx under npm, stays on its own.
        /// </summary>
        public static List<Target> WithoutNested(List<Target> selected)
        {
            return selected.Where(t => t.UnityLog || !t.Found.All(p => selected.Any(o => o != t && o.Empties(p)))).ToList();
        }

        /// <summary>Finds which of a target's own folders exist right now.</summary>
        public static void Locate(Target t)
        {
            t.Found.Clear();
            if (t.UnityLog)
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unity");
                if (Directory.Exists(p)) t.Found.Add(p);
                if (Directory.Exists(Path.Combine(p, "Editor"))) t.Found.Add(Path.Combine(p, "Editor"));
            }
            else foreach (var path in t.Paths) if (Directory.Exists(path)) t.Found.Add(path);
        }

        const string AndroidTools = "studio64";
        static readonly string[] AndroidRunning = { AndroidTools, "adb", "qemu-system-x86_64", "emulator" };

        // key -> lang.json entries ts_<key> (name) and td_<key> (description)
        static Target T(string group, string key, string name, string[] paths, string desc)
        {
            return new Target
            {
                Group = group,
                Name = Loc.S2("ts_" + key, name),
                Paths = paths,
                Description = Loc.S2("td_" + key, desc)
            };
        }

        /// <summary>The user's temp folder, unless TEMP points somewhere that does not look like one.</summary>
        static string TempFolder(string localAppData)
        {
            string fallback = Path.Combine(localAppData, "Temp");
            try
            {
                string temp = Guard.Normalize(Path.GetTempPath());
                string leaf = Path.GetFileName(temp);
                if (leaf.Equals("Temp", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Tmp", StringComparison.OrdinalIgnoreCase))
                    return temp;
            }
            catch { }
            return fallback;
        }

        public static List<Target> Build(Settings a)
        {
            string L = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string R = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string U = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string W = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            var l = new List<Target>
            {
                T("Web", "npm_cache", "npm cache", new[] { Path.Combine(L, "npm-cache") }, "npm download cache. Refills on next install; needs internet.").Keep("_npx"),
                T("Web", "npx_temp_packages", "npx temp packages", new[] { Path.Combine(L, "npm-cache", "_npx") }, "npx packages. Close running npx tools, including MCP servers.").Needs("node"),
                T("Web", "pnpm_store", "pnpm store", new[] { Path.Combine(L, "pnpm-cache"), Path.Combine(L, "pnpm", "store") }, "Package store. Size freed may be far smaller. Breaks projects using the global virtual store.").Needs("pnpm").AdvancedOnly(),
                T("Web", "yarn_cache", "Yarn cache", new[] { Path.Combine(L, "Yarn", "Cache") }, "Yarn package cache."),
                T("Web", "node_gyp_headers", "node-gyp headers", new[] { Path.Combine(L, "node-gyp") }, "Node headers for building native modules."),
                // mcp-* folders hold the browser logins of Playwright MCP, so they are not a cache.
                T("Web", "playwright_browsers", "Playwright browsers", new[] { Path.Combine(L, "ms-playwright"), Path.Combine(L, "ms-playwright-go") }, "Test browsers. Restore with: npx playwright install").Keep("mcp-*"),
                T("Web", "vercel_cli", "Vercel CLI", new[] { Path.Combine(L, "com.vercel.cli") }, "Vercel CLI cache."),
                T("Web", "electron_squirrel", "Electron / Squirrel", new[] { Path.Combine(L, "SquirrelTemp"), Path.Combine(L, "electron-builder") }, "Installer temp files and electron-builder downloads. Not while an app updates or builds."),

                T("Python", "uv_cache", "uv cache", new[] { Path.Combine(L, "uv", "cache") }, "uv cache. Close uv first. Breaks symlink-mode environments.").Needs("uv"),
                T("Python", "pip_cache", "pip cache", new[] { Path.Combine(L, "pip", "cache") }, "pip wheel cache."),
                T("Python", "conda_packages", "conda packages", new[] { Path.Combine(L, "conda", "conda", "pkgs") }, "Conda package cache. Breaks symlink-linked environments.").Needs("conda").AdvancedOnly(),

                T("Unity", "unity_package_cache", "Unity package cache", new[] { Path.Combine(L, "Unity", "cache") }, "Package Manager store. Refetched on project open; needs internet. Close Unity first.").Needs("Unity"),
                T("Unity", "unity_editor_cache", "Unity editor cache", new[] { Path.Combine(L, "Unity", "Caches") }, "Editor caches. Close Unity first.").Needs("Unity"),
                T("Unity", "unity_hub_cache", "Unity Hub cache", new[] { Path.Combine(L, "UnityHub", "Cache"), Path.Combine(R, "UnityHub", "Cache") }, "Hub web cache.").Needs("Unity Hub"),
                T("Unity", "unity_accelerator", "Unity Accelerator", new[] { Path.Combine(L, "UnityAccelerator", "cachedb") }, "Close Accelerator first. Imports rebuild slowly.").Needs("unity-accelerator").AdvancedOnly(),

                T("Mobile", "gradle_cache", "Gradle cache", new[] { Path.Combine(U, ".gradle", "caches") }, "Run gradlew --stop first. Next build re-downloads; offline builds fail.").Needs(AndroidTools).Gradle(),
                T("Mobile", "android_ndk", "Android NDK", new[] { Path.Combine(L, "Android", "Sdk", "ndk") }, "Installed SDK component. Reinstall from SDK Manager.").Needs(AndroidRunning).AdvancedOnly(),
                T("Mobile", "android_emulator", "Android emulator", new[] { Path.Combine(L, "Android", "Sdk", "emulator") }, "Installed SDK component. Reinstall from SDK Manager.").Needs(AndroidRunning).AdvancedOnly(),
                T("Mobile", "android_sources", "Android sources", new[] { Path.Combine(L, "Android", "Sdk", "sources") }, "IDE source navigation only. Reinstall from SDK Manager.").Needs(AndroidTools).AdvancedOnly(),
                T("Mobile", "android_build_tools", "Android build-tools", new[] { Path.Combine(L, "Android", "Sdk", "build-tools") }, "Installed SDK component. Reinstall from SDK Manager.").Needs(AndroidRunning).AdvancedOnly(),

                T("Other", "nuget_packages", "NuGet packages", new[] { Path.Combine(U, ".nuget", "packages") }, "Restored on build if feeds are reachable. Close Visual Studio first.").Needs("devenv", "VBCSCompiler"),
                T("Other", "cargo_rust", "Cargo (Rust)", new[] { Path.Combine(U, ".cargo", "registry") }, "Re-downloaded on next build; offline builds fail.").Needs("cargo", "rustc"),
                T("Other", "go_modules", "Go modules", new[] { Path.Combine(U, "go", "pkg", "mod") }, "Go module cache. Re-downloaded on next build.").Needs("go"),

                T("System", "user_temp", "User Temp", new[] { TempFolder(L) }, "Temp files older than 7 days. Close installers first.").OlderThan(7),
                T("System", "crash_dumps", "Crash dumps", new[] { Path.Combine(L, "CrashDumps") }, "Crash evidence. Not recoverable."),
                T("System", "directx_shader", "DirectX shader", new[] { Path.Combine(L, "D3DSCache") }, "Game shader cache. Regenerated."),
                T("System", "nvidia_shader", "NVIDIA shader", new[] { Path.Combine(L, "NVIDIA", "DXCache"), Path.Combine(L, "NVIDIA", "GLCache"), Path.Combine(L, "NVIDIA", "OptixCache") }, "GPU shader cache."),
            };

            // Standard users cannot even list this folder, so it would only ever read as empty.
            if (RunningTools.IsAdmin())
                l.Add(T("System", "windows_temp", "Windows Temp", new[] { Path.Combine(W, "Temp") }, "System temp files older than 7 days. Close installers first.").OlderThan(7).AdvancedOnly());

            l.Add(new Target
            {
                Group = "Unity",
                Name = Loc.S2("x_unity_log_files", "Unity log files"),
                UnityLog = true,
                Tools = new[] { "Unity" },
                Description = Loc.S2("x_unity_log_desc", "Old rotated logs. Not recoverable.")
            });

            l.Add(new Target
            {
                Group = "Unity",
                Name = Loc.S2("x_asset_store_downloads", "Asset Store downloads"),
                Paths = new[] { Guard.AssetStore },
                Protected = true,
                Description = Loc.S2("x_asset_store_desc", "ASSETS YOU OWN. This app never touches them - size shown for reference only.")
            });

            // Protected roots are enforced by Guard for every delete, not just by the row flag.
            foreach (var t in l)
                if (t.Protected)
                    foreach (var pp in t.Paths)
                        if (!Guard.ProtectedRoots.Contains(pp, StringComparer.OrdinalIgnoreCase)) Guard.ProtectedRoots.Add(pp);

            foreach (var o in a.Custom)
            {
                if (Guard.Reject(o) != null) continue;
                string name;
                try { name = new DirectoryInfo(o).Name; } catch { continue; }
                if (string.IsNullOrEmpty(name)) name = o;
                l.Add(new Target
                {
                    Group = "Custom",
                    Custom = true,
                    Name = name,
                    Paths = new[] { o },
                    Description = o
                });
            }

            return a.ShowAdvanced ? l : l.Where(t => !t.Advanced).ToList();
        }
    }
}
