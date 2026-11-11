using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CacheSweep
{
    class Entry
    {
        public string Path;
        public bool IsFolder;
    }

    // ========================================================================
    //  SWEEPER - walks and removes files. Measuring and deleting share
    //  EntriesOf, so the number on screen is the set that will really go.
    // ========================================================================
    static class Sweeper
    {
        /// <summary>What sits directly under a target folder and may be removed. Throws when
        /// the folder cannot be listed, so the caller can report why.</summary>
        public static List<Entry> EntriesOf(Target t, string root)
        {
            var list = new List<Entry>();
            DateTime cutoff = DateTime.UtcNow.AddDays(-t.MinAgeDays);
            foreach (var d in Directory.EnumerateDirectories(root))
                if (Allowed(t, d, cutoff)) list.Add(new Entry { Path = d, IsFolder = true });
            foreach (var f in Directory.EnumerateFiles(root))
                if (Allowed(t, f, cutoff)) list.Add(new Entry { Path = f, IsFolder = false });
            return list;
        }

        static bool Allowed(Target t, string path, DateTime cutoff)
        {
            if (t.Keeps(Path.GetFileName(path))) return false;
            if (t.MinAgeDays <= 0) return true;
            try { return File.GetLastWriteTimeUtc(path) < cutoff; }
            catch { return false; }
        }

        /// <summary>Bytes a delete of this target folder would remove. -1 when cancelled.</summary>
        public static long Size(Target t, string root, CancellationToken token)
        {
            long total = 0;
            foreach (var e in EntriesOf(t, root))
            {
                long part = Measure.EntrySize(e.Path, e.IsFolder, token);
                if (part < 0) return -1;
                total += part;
            }
            return total;
        }

        /// <summary>Bytes a delete of this whole target would remove. -1 when cancelled or unreadable.</summary>
        public static long SizeOf(Target t, CancellationToken token)
        {
            long size = 0;
            try
            {
                if (token.IsCancellationRequested) return -1;
                if (t.UnityLog)
                {
                    foreach (var dir in t.Found)
                        foreach (var f in Directory.EnumerateFiles(dir, "*.log.*"))
                            try { size += new FileInfo(f).Length; } catch { }
                    return size;
                }
                foreach (var path in t.Found)
                {
                    long part = Size(t, path, token);
                    if (part < 0) return -1;
                    size += part;
                }
                return size;
            }
            catch { return -1; }
        }

        /// <summary>Removes one file; a read-only flag does not stop it. Adds what it freed to
        /// <paramref name="bytes"/>. Returns false when the file is still there.</summary>
        public static bool DeleteFile(string file, ref long bytes)
        {
            try
            {
                var fi = new FileInfo(file);
                // A link's flag is left alone: writing through a link could reach its target.
                bool link = (fi.Attributes & FileAttributes.ReparsePoint) != 0;
                long size = link ? 0 : fi.Length;
                if (!link && fi.IsReadOnly) fi.IsReadOnly = false;
                fi.Delete();
                bytes += size;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Removes a folder tree without ever following a junction or symlink, stopping
        /// between files when cancelled. Returns true only when the folder is gone.</summary>
        public static bool DeleteTree(string dir, CancellationToken token, ref long bytes)
        {
            bool whole = true;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    if (token.IsCancellationRequested) return false;
                    if (!DeleteFile(f, ref bytes)) whole = false;
                }
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    if (token.IsCancellationRequested) return false;
                    bool link = (File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0;
                    if (link)
                    {
                        try { Directory.Delete(d, false); } catch { whole = false; }
                    }
                    else if (!DeleteTree(d, token, ref bytes)) whole = false;
                }
                if (!whole) return false;
                var info = new DirectoryInfo(dir);
                if ((info.Attributes & FileAttributes.ReadOnly) != 0) info.Attributes &= ~FileAttributes.ReadOnly;
                info.Delete(false);
                return true;
            }
            catch { return false; }
        }
    }
}
