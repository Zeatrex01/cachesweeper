// ============================================================================
//  DevSuite - native window chrome
//  The title bar and the scrollbars are drawn by Windows, not by us, so a dark
//  app ends up wearing a light title bar and light scrollbars unless it asks
//  for the dark ones. Every call is optional: on a Windows build that does not
//  know an attribute the call fails and the window keeps the system default.
//
//  The colours are passed in rather than read from a palette, so an app that
//  keeps its own theme (CacheSweep) gets the same chrome as the rest.
// ============================================================================

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DevSuite
{
    public static class NativeChrome
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        // Undocumented, but it is the only way to get dark scrollbars: without it
        // SetWindowTheme("DarkMode_Explorer") is ignored. Present since Windows 10 1809.
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        static extern int SetPreferredAppMode(int mode);

        const int DarkMode = 20, DarkModeBefore20H1 = 19;   // DWMWA_USE_IMMERSIVE_DARK_MODE
        const int BorderColour = 34, CaptionColour = 35, CaptionText = 36;   // Windows 11

        static bool _appMode;

        static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        static void Set(IntPtr hwnd, int attr, int value)
        {
            try { DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int)); }
            catch { }
        }

        /// <summary>Gives the window the app's own title bar colours and matching
        /// scrollbars. Safe to call again whenever the theme changes.</summary>
        public static void Apply(Form f, bool dark, Color caption, Color captionText, Color border)
        {
            if (f == null || !f.IsHandleCreated) return;

            if (!_appMode)
            {
                _appMode = true;
                try { SetPreferredAppMode(1); } catch { }      // 1 = follow each window's theme
            }

            Set(f.Handle, DarkMode, dark ? 1 : 0);
            Set(f.Handle, DarkModeBefore20H1, dark ? 1 : 0);

            // Windows 11 can paint the caption itself, so the title bar stops looking
            // like a strip torn off another window.
            Set(f.Handle, CaptionColour, Bgr(caption));
            Set(f.Handle, CaptionText, Bgr(captionText));
            Set(f.Handle, BorderColour, Bgr(border));

            Scrollbars(f, dark);
        }

        /// <summary>Native scrollbars follow the app theme instead of staying light.</summary>
        public static void Scrollbars(Control k, bool dark)
        {
            if (k == null) return;
            try
            {
                if (k.IsHandleCreated)
                    SetWindowTheme(k.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
            foreach (Control c in k.Controls) Scrollbars(c, dark);
        }
    }
}
