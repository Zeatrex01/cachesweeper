using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;

namespace CacheSweep
{
    // ========================================================================
    //  RUNNING TOOLS - emptying a cache under a live tool can corrupt it, so a
    //  target is skipped while the program that uses it is open.
    // ========================================================================
    static class RunningTools
    {
        public static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        /// <summary>The programs currently holding this target; empty when it is free to clean.</summary>
        public static List<string> Find(Target t)
        {
            var found = new List<string>();
            foreach (var name in t.Tools)
                if (IsRunning(name)) found.Add(name);
            if (t.GradleDaemon && GradleDaemonRunning()) found.Add("Gradle daemon");
            return found;
        }

        static bool IsRunning(string name)
        {
            Process[] all;
            try { all = Process.GetProcessesByName(name); }
            catch { return false; }
            foreach (var p in all) p.Dispose();
            return all.Length > 0;
        }

        // Any java process could be anything; only its command line says it is Gradle.
        static bool GradleDaemonRunning()
        {
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name = 'java.exe'"))
                using (var results = q.Get())
                    foreach (ManagementBaseObject p in results)
                        using (p)
                        {
                            var line = p["CommandLine"] as string;
                            if (line != null && line.IndexOf("org.gradle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        }
            }
            catch { }
            return false;
        }
    }
}
