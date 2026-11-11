// ============================================================================
//  DevSuite - design tokens
//  Spacing, typography and colour, resolved for the display's scale factor.
//  Nothing here draws or owns a control; Ui.cs builds on top of it.
// ============================================================================

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DevSuite
{
    // ====================================================================
    //  DPI
    //  A window that does not claim to understand DPI is enlarged by Windows
    //  as a finished bitmap, which is why text and hairlines look soft on a
    //  scaled display. These apps claim it instead, and pay for it by doing
    //  the arithmetic themselves: every measurement is written in logical
    //  pixels (as if the screen were 96 dpi). Px() turns a logical
    //  measurement into a real one for anything Windows positions (control
    //  bounds), and the custom painting draws onto a scaled canvas instead
    //  (see Draw.Quality), so paint code keeps its logical numbers.
    // ====================================================================
    public static class Dpi
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        public static float Scale = 1f;

        /// <summary>Call once at the top of Main(), before any window exists.</summary>
        public static void Init()
        {
            try { SetProcessDPIAware(); } catch { }
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f; }
            catch { Scale = 1f; }
            if (Scale < 1f) Scale = 1f;          // never shrink below the design size
        }

        /// <summary>Logical pixels -> device pixels.</summary>
        public static int Px(int logical) { return (int)Math.Round(logical * Scale); }
    }

    // ====================================================================
    //  TOKEN: SPACING  (8pt grid, logical pixels)
    // ====================================================================
    public static class Space
    {
        public const int Xs = 4, Sm = 8, Md = 12, Lg = 16, Xl = 20, Xxl = 24, Xxxl = 32;
        public const int RowHeight = 64;      // list row height
        public const int Edge = 20;       // window inner padding
        public const int Radius = 8;      // default corner radius
    }

    // ====================================================================
    //  TOKEN: TYPOGRAPHY
    // ====================================================================
    public static class Fonts
    {
        const string Family = "Segoe UI";
        const string MonoFamily = "Consolas";

        // Design sizes in logical pixels, the same scale everything else is
        // written in. (A 17pt font at 96 dpi is 23 logical pixels tall.)
        const float H1px = 23, H2px = 17, TitlePx = 14, BodyPx = 13,
                    LabelPx = 12, SmallPx = 11, MicroPx = 10, NumericPx = 16, MonoPx = 12;

        // Held in logical pixels, like every other measurement. The custom painting
        // draws onto a canvas the DPI factor has already been applied to, so these
        // land at the right physical size there without any arithmetic.
        public static readonly Font
            H1      = Make(Family, H1px, FontStyle.Bold),
            H2      = Make(Family, H2px, FontStyle.Bold),
            Title   = Make(Family, TitlePx, FontStyle.Bold),
            Body    = Make(Family, BodyPx, FontStyle.Regular),
            Label   = Make(Family, LabelPx, FontStyle.Bold),
            Small   = Make(Family, SmallPx, FontStyle.Regular),
            Micro   = Make(Family, MicroPx, FontStyle.Bold),
            Numeric = Make(Family, NumericPx, FontStyle.Bold),
            Mono    = Make(MonoFamily, MonoPx, FontStyle.Regular);

        static Font Make(string family, float px, FontStyle style)
        {
            return new Font(family, px, style, GraphicsUnit.Pixel);
        }

        /// <summary>The same face at its true on-screen size. Controls that Windows
        /// draws itself - Label, TextBox, ComboBox - never see the scaled canvas, so
        /// the scale factor has to be baked into their font instead. Applying it to a
        /// whole window is NativeText.Apply.</summary>
        public static Font Native(Font f)
        {
            if (f == null || f.Unit != GraphicsUnit.Pixel) return f;
            return new Font(f.FontFamily, f.Size * Dpi.Scale, f.Style, GraphicsUnit.Pixel);
        }
    }

    // ====================================================================
    //  TOKEN: COLOUR  (semantic names - "Accent", not "blue")
    // ====================================================================
    public static class Palette
    {
        // layers
        public static Color Bg, Surface, SurfaceRaised, SurfaceHover, Border, BorderStrong;
        // label
        public static Color Text, TextSecondary, TextMuted;
        // emphasis
        public static Color Accent, AccentHover, AccentPressed, AccentSubtle;
        // state
        public static Color Success, SuccessBg, Warning, WarningBg,
                            Danger, DangerBg, Info, InfoBg;

        public static bool IsDark { get; private set; }
        public static event EventHandler Changed;

        static Palette() { Apply(true); }

        public static Color Hex(string s) { return ColorTranslator.FromHtml(s); }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                Bg = Hex("#111317");  Surface = Hex("#181B20");  SurfaceRaised = Hex("#1F232A");
                SurfaceHover = Hex("#252A32"); Border = Hex("#2A3038"); BorderStrong = Hex("#39414B");
                Text = Hex("#E8EAED"); TextSecondary = Hex("#A7AEB8"); TextMuted = Hex("#6E7681");
                Accent = Hex("#3B82F6"); AccentHover = Hex("#5B96F8"); AccentPressed = Hex("#2563EB");
                AccentSubtle = Hex("#1B2C48");
                Success = Hex("#34C77B"); SuccessBg = Hex("#132A20");
                Warning = Hex("#E0A030");  WarningBg  = Hex("#302518");
                Danger = Hex("#EF4444");DangerBg= Hex("#31191A");
                Info = Hex("#8B5CF6");  InfoBg  = Hex("#241B3A");
            }
            else
            {
                Bg = Hex("#F6F7F9");  Surface = Hex("#FFFFFF");  SurfaceRaised = Hex("#F0F2F5");
                SurfaceHover = Hex("#E8EBF0"); Border = Hex("#E2E6EC"); BorderStrong = Hex("#C9D0DA");
                Text = Hex("#15181D"); TextSecondary = Hex("#4A515C"); TextMuted = Hex("#79818D");
                Accent = Hex("#2563EB"); AccentHover = Hex("#1D4ED8"); AccentPressed = Hex("#1E40AF");
                AccentSubtle = Hex("#E3EDFD");
                Success = Hex("#16A34A"); SuccessBg = Hex("#E6F6EC");
                Warning = Hex("#B4780C");  WarningBg  = Hex("#FBF0DC");
                Danger = Hex("#DC2626");DangerBg= Hex("#FCE9E9");
                Info = Hex("#7C3AED");  InfoBg  = Hex("#F0E9FE");
            }
            if (Changed != null) Changed(null, EventArgs.Empty);
        }

        /// <summary>Severity colour by size - louder as the number grows.</summary>
        public static Color Severity(long bytes)
        {
            if (bytes >= 5L * 1073741824L) return Danger;
            if (bytes >= 1073741824L)      return Warning;
            if (bytes >= 200L * 1048576L)  return IsDark ? Hex("#D4C066") : Hex("#8A7318");
            return TextSecondary;
        }

        /// <summary>Theme preference is kept in %APPDATA%\DevSuite\theme.txt.</summary>
        public static void Load()
        {
            try
            {
                var p = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DevSuite", "theme.txt");
                if (System.IO.File.Exists(p))
                    Apply(System.IO.File.ReadAllText(p).Trim() != "light");
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var k = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevSuite");
                System.IO.Directory.CreateDirectory(k);
                System.IO.File.WriteAllText(System.IO.Path.Combine(k, "theme.txt"), IsDark ? "dark" : "light");
            }
            catch { }
        }
    }
}
