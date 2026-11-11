// ============================================================================
//  DevSuite - shared UI primitives
//  Localisation, drawing helpers and the custom controls every app is built
//  from. Measurements and colours come from Tokens.cs; native window chrome
//  from Native.cs. No magic numbers: everything comes from Space/Fonts/Palette.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DevSuite
{
    // ====================================================================
    //  NATIVE-DRAWN TEXT
    //  Controls Windows draws itself never see the scaled canvas, so their
    //  font has to carry the real on-screen size instead of the design size.
    // ====================================================================
    public static class NativeText
    {
        /// <summary>Gives every Windows-drawn control in the tree its true-size font.
        /// Run once per window, after its controls exist. UiButton is the exception:
        /// it draws its own label through the scaled canvas, so it keeps the painting
        /// font. Every other control here is drawn by Windows, including the ones that
        /// simply inherit the form's font.</summary>
        public static void Apply(Control root)
        {
            root.Font = Fonts.Native(root.Font);
            Walk(root);
        }

        static void Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                // A control with no font of its own hands back its parent's font object,
                // and that one has just been converted - converting it again would scale
                // the same text twice.
                if (!(c is UiButton) && !ReferenceEquals(c.Font, parent.Font))
                    c.Font = Fonts.Native(c.Font);
                if (c.Controls.Count > 0) Walk(c);
            }
        }
    }

    // ====================================================================
    //  LOCALISATION
    // ====================================================================
    public static class Loc
    {
        // Built-in English strings. Each app adds its own with Add().
        static readonly Dictionary<string, string> S_ = new Dictionary<string, string>();
        // Strings from lang.json. Kept apart so an app's Add() can never overwrite a translation.
        static readonly Dictionary<string, string> Pack_ = new Dictionary<string, string>();

        public static void Add(Dictionary<string, string> m)
        {
            foreach (var kv in m) S_[kv.Key] = kv.Value;
        }
        // Name of the running executable without extension: lang.json may carry
        // "<App>.<key>" entries that win over plain "<key>" entries for that app only.
        static readonly string App_ = AppName();

        static string AppName()
        {
            try { return System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath); }
            catch { return ""; }
        }

        public static string S(string k)
        {
            string v;
            if (Pack_.TryGetValue(App_ + "." + k, out v)) return v;
            if (Pack_.TryGetValue(k, out v)) return v;
            return S_.TryGetValue(k, out v) ? v : k;
        }

        /// <summary>Formats a string; a translation with the wrong number of placeholders
        /// falls back to the unformatted text instead of crashing the window.</summary>
        public static string S(string k, params object[] a)
        {
            string f = S(k);
            try { return string.Format(f, a); } catch (FormatException) { return f; }
        }

        /// <summary>Returns the language-pack override for <paramref name="k"/> if one
        /// exists, otherwise the given English text.</summary>
        public static string S2(string k, string fallback)
        {
            string v;
            return Pack_.TryGetValue(k, out v) ? v : fallback;
        }

        /// <summary>Loads lang.json sitting next to the executable, if present.
        /// The English build ships without it; a localised package includes one.</summary>
        public static void LoadPack()
        {
            try
            {
                var file = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.ExecutablePath), "lang.json");
                if (!System.IO.File.Exists(file)) return;
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             System.IO.File.ReadAllText(file),
                             "\"(?<k>[^\"]+)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\""))
                {
                    Pack_[m.Groups["k"].Value] = Unescape(m.Groups["v"].Value);
                }
            }
            catch { }
        }


        /// <summary>JSON string escapes, left to right, so "\\n" stays a backslash and an n.</summary>
        static string Unescape(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
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
                            if (int.TryParse(s.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out code))
                            { sb.Append((char)code); i += 4; break; }
                        }
                        sb.Append('u'); break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        static Loc()
        {
            Add(new Dictionary<string, string>
            {
                { "on", "on" },
                { "off", "off" },
                { "enable", "Enable" },
                { "disable", "Disable" },
                { "refresh", "Refresh" },
                { "close", "Close" },
                { "save", "Save" },
                { "cancel", "Cancel" },
                { "view", "Open" },
                { "reveal", "Folder" },
                { "search", "Search…" },
                { "all", "All" },
                { "error", "Error" },
                { "warn", "Warning" },
                { "write_fail", "Write failed: {0}" },
                { "restart_hint", "You need to fully restart the client." },
                { "empty_title", "Nothing here" },
            });
        }
    }

    // ====================================================================
    //  DRAWING HELPERS
    // ====================================================================
    public static class Draw
    {
        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Fill(Graphics g, RectangleF r, Color c, float radius)
        {
            using (var b = new SolidBrush(c)) using (var y = Rounded(r, radius)) g.FillPath(b, y);
        }

        public static void Stroke(Graphics g, RectangleF r, Color c, float radius, float thickness)
        {
            using (var p = new Pen(c, thickness))
            using (var y = Rounded(new RectangleF(r.X + thickness / 2, r.Y + thickness / 2,
                                                   r.Width - thickness, r.Height - thickness), radius))
                g.DrawPath(p, y);
        }

        /// <summary>Draws a small badge and returns its width.</summary>
        public static int Badge(Graphics g, string label, int x, int y, Color bg2, Color fg, bool border = false)
        {
            int w = (int)g.MeasureString(label, Fonts.Micro).Width + 16;
            var r = new RectangleF(x, y, w, 19);
            Fill(g, r, bg2, 9.5f);
            if (border) Stroke(g, r, Color.FromArgb(80, fg), 9.5f, 1f);
            using (var b = new SolidBrush(fg))
                g.DrawString(label, Fonts.Micro, b, x + 8, y + 3);
            return w;
        }

        public static void Dot(Graphics g, int x, int y, int cap, Color c, bool halo)
        {
            if (halo)
                using (var b = new SolidBrush(Color.FromArgb(45, c)))
                    g.FillEllipse(b, x - cap, y - cap, cap * 3, cap * 3);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, cap, cap);
        }

        public static void Clip(Graphics g, string label, Font f, Color c, RectangleF r)
        {
            var sf = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
                LineAlignment = StringAlignment.Center
            };
            using (var b = new SolidBrush(c)) g.DrawString(label, f, b, r, sf);
        }

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Dpi.Scale != 1f) g.ScaleTransform(Dpi.Scale, Dpi.Scale);
        }

        /// <summary>Same as Quality, and returns the control's size in the logical
        /// pixels the paint code works in - Width and Height are device pixels and
        /// would be too large once the canvas is scaled.</summary>
        public static SizeF Quality(Graphics g, Control c)
        {
            Quality(g);
            return new SizeF(c.Width / Dpi.Scale, c.Height / Dpi.Scale);
        }
    }

    // ====================================================================
    //  BUTTON - variants with real interaction states
    // ====================================================================
    public enum ButtonKind { Primary, Secondary, Ghost, Danger, Success }

    public class UiButton : Control
    {
        public ButtonKind Kind = ButtonKind.Secondary;
        public bool Small;
        bool _hot, _pressed;

        public UiButton(string label = "", ButtonKind kind = ButtonKind.Secondary, bool small = false)
        {
            Kind = kind; Small = small; Text = label;
            Cursor = Cursors.Hand;
            Height = Dpi.Px(small ? 30 : 38);
            Font = small ? Fonts.Small : Fonts.Label;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            MouseEnter += (s, e) => { _hot = true; Invalidate(); };
            MouseLeave += (s, e) => { _hot = false; _pressed = false; Invalidate(); };
            MouseDown += (s, e) => { _pressed = true; Invalidate(); };
            MouseUp += (s, e) => { _pressed = false; Invalidate(); };
            FitToText();
        }

        public void FitToText()
        {
            // MeasureString runs on an unscaled canvas with a painting font, so the
            // result is in logical pixels; the control's own width is not.
            using (var g = CreateGraphics())
                Width = Dpi.Px(Math.Max(Small ? 64 : 92,
                        (int)g.MeasureString(Text, Font).Width + (Small ? 24 : 34)));
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitToText(); Invalidate(); }

        void Resolve(out Color bg2, out Color label, out Color border)
        {
            border = Color.Empty;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    bg2 = _pressed ? Palette.AccentPressed : _hot ? Palette.AccentHover : Palette.Accent;
                    label = Color.White; break;
                case ButtonKind.Danger:
                    bg2 = _pressed ? Palette.Hex("#C22F2F") : _hot ? Palette.Hex("#DC3B3B") : Palette.Danger;
                    label = Color.White; break;
                case ButtonKind.Success:
                    bg2 = _pressed ? Palette.Hex("#22A463") : _hot ? Palette.Hex("#2BB672") : Palette.Success;
                    label = Palette.Hex("#0A1A12"); break;
                case ButtonKind.Ghost:
                    bg2 = _pressed ? Palette.SurfaceHover : _hot ? Palette.SurfaceRaised : Color.Transparent;
                    label = _hot ? Palette.Text : Palette.TextSecondary; break;
                default:
                    bg2 = _pressed ? Palette.SurfaceHover : _hot ? Palette.SurfaceRaised : Palette.Surface;
                    label = Palette.Text;
                    border = _hot ? Palette.BorderStrong : Palette.Border; break;
            }
            if (!Enabled)
            {
                bg2 = Kind == ButtonKind.Ghost ? Color.Transparent : Palette.Surface;
                label = Palette.TextMuted;
                border = Palette.Border;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz = Draw.Quality(g, this);
            Color z, m, c; Resolve(out z, out m, out c);
            var r = new RectangleF(0, 0, sz.Width, sz.Height);

            if (z != Color.Transparent) Draw.Fill(g, r, z, Space.Radius);
            if (c != Color.Empty) Draw.Stroke(g, r, c, Space.Radius, 1f);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(m)) g.DrawString(Text, Font, b, r, sf);
        }
    }

    // ====================================================================
    //  SEARCH BOX
    // ====================================================================
    public class SearchBox : Panel
    {
        public TextBox Box;
        public event EventHandler Changed;

        public SearchBox(int width = 280)
        {
            Size = new Size(Dpi.Px(width), Dpi.Px(34));
            BackColor = Palette.Surface;
            DoubleBuffered = true;

            Box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                Font = Fonts.Body,
                Location = new Point(Dpi.Px(34), Dpi.Px(9)),
                Width = Dpi.Px(width - 44)
            };
            Box.TextChanged += (s, e) => { Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); };
            Controls.Add(Box);
        }

        public new string Text { get { return Box.Text.Trim(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz = Draw.Quality(g, this);
            var r = new RectangleF(0, 0, sz.Width - 1, sz.Height - 1);
            Draw.Fill(g, r, Palette.Surface, Space.Radius);
            Draw.Stroke(g, r, Box.Focused ? Palette.Accent : Palette.Border, Space.Radius, 1f);

            // magnifier
            using (var p = new Pen(Palette.TextMuted, 1.6f))
            {
                g.DrawEllipse(p, 13, 11, 9, 9);
                g.DrawLine(p, 21, 19, 25, 23);
            }
            if (Box.Text.Length == 0)
                using (var b = new SolidBrush(Palette.TextMuted))
                    g.DrawString(Loc.S("search"), Fonts.Body, b, 33, 8);
        }
    }

    // ====================================================================
    //  FILTER CHIPS  (segmented control)
    // ====================================================================
    public class FilterBar : Panel
    {
        public class Chip { public string Key, Label; public int Count; public Color Tint = Color.Empty; }

        const int ChipHeight = 34;               // logical

        readonly List<Chip> _chips = new List<Chip>();
        // Logical coordinates, like everything else the paint code touches.
        readonly List<RectangleF> _bounds = new List<RectangleF>();
        int _selected, _hoverIndex = -1;

        public event EventHandler Changed;
        public string Selected { get { return _chips.Count > 0 ? _chips[_selected].Key : null; } }

        /// <summary>Mouse positions arrive in device pixels; the chips live in logical ones.</summary>
        static PointF Logical(Point p) { return new PointF(p.X / Dpi.Scale, p.Y / Dpi.Scale); }

        public FilterBar()
        {
            Height = Dpi.Px(ChipHeight);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            MouseMove += (s, e) =>
            {
                var p = Logical(e.Location);
                int y = -1;
                for (int i = 0; i < _bounds.Count; i++) if (_bounds[i].Contains(p)) { y = i; break; }
                if (y != _hoverIndex) { _hoverIndex = y; Cursor = y >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            };
            MouseLeave += (s, e) => { _hoverIndex = -1; Invalidate(); };
            MouseDown += (s, e) =>
            {
                var p = Logical(e.Location);
                for (int i = 0; i < _bounds.Count; i++)
                    if (_bounds[i].Contains(p) && i != _selected)
                    { _selected = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); return; }
            };
        }

        public void Build(params Chip[] chips)
        {
            _chips.Clear(); _chips.AddRange(chips);
            if (_selected >= _chips.Count) _selected = 0;
            Measure();
            Invalidate();
        }

        public void SetCounts(Dictionary<string, int> counts)
        {
            foreach (var c in _chips) { int v; if (counts.TryGetValue(c.Key, out v)) c.Count = v; }
            Measure(); Invalidate();
        }

        void Measure()
        {
            _bounds.Clear();
            using (var g = CreateGraphics())
            {
                int x = 0;
                foreach (var c in _chips)
                {
                    string m = c.Label + (c.Count > 0 ? "  " + c.Count : "");
                    int w = (int)g.MeasureString(m, Fonts.Label).Width + 26;
                    _bounds.Add(new RectangleF(x, 0, w, ChipHeight));
                    x += w + Space.Sm;
                }
                Width = Dpi.Px(Math.Max(10, x - Space.Sm));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Draw.Quality(g);
            for (int i = 0; i < _chips.Count && i < _bounds.Count; i++)
            {
                var c = _chips[i];
                var r = _bounds[i];
                bool selected = i == _selected;
                var bg2 = selected ? Palette.AccentSubtle : (i == _hoverIndex ? Palette.SurfaceRaised : Palette.Surface);
                Draw.Fill(g, r, bg2, 17);
                Draw.Stroke(g, r, selected ? Palette.Accent : Palette.Border, 17, 1f);

                var fg = selected ? Palette.Hex("#9CC3FF") : (c.Tint != Color.Empty ? c.Tint : Palette.TextSecondary);
                using (var b = new SolidBrush(fg))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    string m = c.Label;
                    var w = g.MeasureString(m, Fonts.Label).Width;
                    g.DrawString(m, Fonts.Label, b, r.X + 13, r.Y + (r.Height - 15) / 2f);
                    if (c.Count > 0)
                        using (var b2 = new SolidBrush(selected ? Palette.Accent : Palette.TextMuted))
                            g.DrawString(c.Count.ToString(), Fonts.Label, b2, r.X + 13 + w + 6, r.Y + (r.Height - 15) / 2f);
                }
            }
        }
    }

    // ====================================================================
    //  LIST ROW - three-column layout, actions revealed on hover
    // ====================================================================
    public class Row : Panel
    {
        public string Title = "";
        public string Subtitle = "";
        public string RightValue = "";          // large value on the right (size etc.)
        public Color RightColor = Color.Empty;
        public double Ratio;                   // 0..1 -> thin bar under the value
        public new bool Enabled = true;
        public bool Dim;                    // dimmed rendering
        public Color StatusColor = Color.Empty;
        public List<string> Badges = new List<string>();
        public List<Color> BadgeColors = new List<Color>();
        public int Warning;
        public string WarningText;

        readonly List<UiButton> _actions = new List<UiButton>();
        bool _hot;

        public Row()
        {
            Height = Dpi.Px(Space.RowHeight);
            Dock = DockStyle.Top;
            DoubleBuffered = true;
            BackColor = Palette.Surface;
            MouseEnter += (s, e) => { _hot = true; ShowActions(true); Invalidate(); };
            MouseLeave += (s, e) =>
            {
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                { _hot = false; ShowActions(false); Invalidate(); }
            };
        }

        public UiButton AddAction(string label, ButtonKind kind, Action onClick, bool alwaysVisible = false)
        {
            var b = new UiButton(label, kind, true)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = alwaysVisible,
                Tag = alwaysVisible
            };
            b.Click += (s, e) => onClick();
            b.MouseEnter += (s, e) => { _hot = true; ShowActions(true); Invalidate(); };
            _actions.Add(b);
            Controls.Add(b);
            LayoutActions();
            return b;
        }

        void ShowActions(bool g)
        {
            foreach (var b in _actions)
                b.Visible = g || (b.Tag is bool && (bool)b.Tag);
        }

        // Buttons are real controls, so this runs in device pixels.
        void LayoutActions()
        {
            int x = Width - Dpi.Px(Space.Lg);
            foreach (var b in _actions)
            {
                x -= b.Width;
                b.Location = new Point(x, (Height - b.Height) / 2);
                x -= Dpi.Px(Space.Sm);
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutActions(); }

        /// <summary>Room the action buttons take, in the logical pixels the paint code uses.</summary>
        int ActionsWidth()
        {
            int w = Space.Lg;
            foreach (var b in _actions) w += (int)(b.Width / Dpi.Scale) + Space.Sm;
            return w;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz = Draw.Quality(g, this);
            int w = (int)sz.Width, h = (int)sz.Height;
            var full = new RectangleF(0, 0, w, h);

            using (var b = new SolidBrush(_hot ? Palette.SurfaceRaised : Palette.Surface)) g.FillRectangle(b, full);
            using (var p = new Pen(Palette.Border)) g.DrawLine(p, Space.Edge, h - 1, w - Space.Edge, h - 1);

            // status dot
            var state = StatusColor != Color.Empty ? StatusColor
                       : (Enabled ? Palette.Success : Palette.TextMuted);
            Draw.Dot(g, Space.Edge, h / 2 - 4, 8, Dim ? Palette.TextMuted : state, _hot && !Dim);

            int leftX = Space.Edge + 22;
            int rightEdge = w - (_hot ? ActionsWidth() : Space.Edge) - 12;

            // title
            var titleColor = Dim ? Palette.TextMuted : Palette.Text;
            var titleW = (int)g.MeasureString(Title, Fonts.Title).Width;
            Draw.Clip(g, Title, Fonts.Title, titleColor,
                     new RectangleF(leftX, 10, Math.Min(titleW + 4, Math.Max(80, rightEdge - leftX - 140)), 20));

            // badges to the right of the title
            int rx = leftX + Math.Min(titleW + 12, Math.Max(90, rightEdge - leftX - 140));
            for (int i = 0; i < Badges.Count && rx < rightEdge - 60; i++)
            {
                var rc = i < BadgeColors.Count ? BadgeColors[i] : Palette.TextSecondary;
                rx += Draw.Badge(g, Badges[i], rx, 11, Color.FromArgb(40, rc), rc, true) + Space.Xs;
            }
            if (Warning > 0 && rx < rightEdge - 50)
                Draw.Badge(g, "⚠ " + Warning, rx, 11, Palette.WarningBg, Palette.Warning, true);

            // subtitle
            if (!string.IsNullOrEmpty(Subtitle))
                Draw.Clip(g, Subtitle, Fonts.Small,
                         Warning > 0 ? Palette.Warning : Palette.TextMuted,
                         new RectangleF(leftX, 34, Math.Max(80, rightEdge - leftX), 18));

            // right-hand value
            if (!_hot && !string.IsNullOrEmpty(RightValue))
            {
                var c = RightColor != Color.Empty ? RightColor : Palette.TextSecondary;
                var vs = g.MeasureString(RightValue, Fonts.Numeric);
                using (var b = new SolidBrush(c))
                    g.DrawString(RightValue, Fonts.Numeric, b, w - Space.Edge - vs.Width, 10);

                if (Ratio > 0)
                {
                    int barW = 150;
                    var r1 = new RectangleF(w - Space.Edge - barW, 40, barW, 4);
                    Draw.Fill(g, r1, Palette.Border, 2);
                    var r2 = new RectangleF(r1.X, r1.Y, (float)Math.Max(3, barW * Ratio), 4);
                    Draw.Fill(g, r2, c, 2);
                }
            }
        }
    }

    // ====================================================================
    //  EMPTY STATE
    // ====================================================================
    public class EmptyState : Panel
    {
        public string Title = Loc.S("empty_title");
        public string Hint = "";
        public string Glyph = "◍";

        public EmptyState() { Dock = DockStyle.Fill; BackColor = Palette.Bg; DoubleBuffered = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz = Draw.Quality(g, this);
            var sf = new StringFormat { Alignment = StringAlignment.Center };
            int y = (int)sz.Height / 2 - 70;

            using (var f = new Font("Segoe UI", 45f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Palette.Border))
                g.DrawString(Glyph, f, b, new RectangleF(0, y, sz.Width, 60), sf);

            using (var b = new SolidBrush(Palette.TextSecondary))
                g.DrawString(Title, Fonts.H2, b, new RectangleF(0, y + 66, sz.Width, 30), sf);

            if (!string.IsNullOrEmpty(Hint))
                using (var b = new SolidBrush(Palette.TextMuted))
                    g.DrawString(Hint, Fonts.Body, b,
                        new RectangleF(sz.Width / 2f - 260, y + 96, 520, 60), sf);
        }
    }

    // ====================================================================
    //  PROGRESS LINE
    // ====================================================================
    public class ProgressLine : Control
    {
        public double Value;
        public Color Tint = Palette.Accent;

        public ProgressLine()
        {
            Height = Dpi.Px(3);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz = Draw.Quality(g, this);
            using (var b = new SolidBrush(Palette.Border))
                g.FillRectangle(b, new RectangleF(0, 0, sz.Width, sz.Height));
            if (Value > 0)
                Draw.Fill(g, new RectangleF(0, 0, (float)(sz.Width * Math.Min(1, Value)), sz.Height), Tint, 1.5f);
        }
    }

    // ====================================================================
    //  WINDOW SHELL
    // ====================================================================
    public class Shell
    {
        public Form Form;
        public Panel Header, Toolbar, List;
        public Panel Footer;
        public Label FooterLeft, FooterRight;
        public ProgressLine Bar;
        Label _titleLabel, _subLabel;

        /// <summary>Title bar and scrollbars in the current theme's colours.</summary>
        public static void Chrome(Form f)
        {
            NativeChrome.Apply(f, Palette.IsDark, Palette.Bg, Palette.Text, Palette.Border);
        }

        /// <summary>Everything a window needs once its controls exist: true-size fonts
        /// for the controls Windows draws, and themed chrome around them. Forms built
        /// outside a Shell call this from their own Load handler.</summary>
        public static void Dress(Form f)
        {
            NativeText.Apply(f);
            Chrome(f);
        }

        public Shell(Form f, string title, string subtitle, int w = 1080, int h = 720)
        {
            Form = f;
            f.Text = title;
            f.ClientSize = new Size(Dpi.Px(w), Dpi.Px(h));
            f.MinimumSize = new Size(Dpi.Px(880), Dpi.Px(560));
            f.StartPosition = FormStartPosition.CenterScreen;
            f.BackColor = Palette.Bg;
            f.ForeColor = Palette.Text;
            f.Font = Fonts.Body;          // Dress() turns this into its true size at Load
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // WinForms docks children from the LAST added to the FIRST: the last-added
            // control sits closest to its edge and Controls[0] gets what is left.
            // So: List(Fill) first, Footer(Bottom), Toolbar(Top), Header(Top) last.

            List = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true, BackColor = Palette.Bg,
                Padding = new Padding(Dpi.Px(Space.Lg), Dpi.Px(Space.Sm), Dpi.Px(Space.Lg), Dpi.Px(Space.Lg))
            };
            f.Controls.Add(List);

            Footer = new Panel { Dock = DockStyle.Bottom, Height = Dpi.Px(44), BackColor = Palette.Surface };
            f.Controls.Add(Footer);
            FooterLeft = new Label { Location = new Point(Dpi.Px(Space.Edge + 4), Dpi.Px(14)), AutoSize = true,
                                 ForeColor = Palette.TextSecondary, Font = Fonts.Body };
            FooterRight = new Label { AutoSize = true, ForeColor = Palette.TextMuted, Font = Fonts.Small,
                                 Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Footer.Controls.Add(FooterLeft);
            Footer.Controls.Add(FooterRight);
            // An AutoSize label anchored right keeps its left edge and grows off the
            // window, so it is placed again whenever its width can have changed -
            // a resize, a new caption, or the font growing to its true size at Load.
            Footer.Resize += (s, e) => PlaceFooterRight();
            FooterRight.TextChanged += (s, e) => PlaceFooterRight();
            FooterRight.FontChanged += (s, e) => PlaceFooterRight();

            Toolbar = new Panel { Dock = DockStyle.Top, Height = Dpi.Px(54), BackColor = Palette.Bg };
            f.Controls.Add(Toolbar);

            Header = new Panel { Dock = DockStyle.Top, Height = Dpi.Px(76), BackColor = Palette.Bg };
            f.Controls.Add(Header);

            _titleLabel = new Label { Text = title, Font = Fonts.H1, ForeColor = Palette.Text,
                                  Location = new Point(Dpi.Px(Space.Edge + 4), Dpi.Px(14)), AutoSize = true };
            _subLabel = new Label { Text = subtitle, Font = Fonts.Body, ForeColor = Palette.TextMuted,
                                   Location = new Point(Dpi.Px(Space.Edge + 6), Dpi.Px(43)), AutoSize = true };
            Header.Controls.Add(_titleLabel);
            Header.Controls.Add(_subLabel);

            Bar = new ProgressLine { Dock = DockStyle.Bottom };
            Header.Controls.Add(Bar);

            f.Resize += (s, e) => LayoutHeader();
            // Dress enlarges the fonts, which grows every AutoSize label, so the
            // right-aligned pieces have to be placed again from their new widths.
            f.Load += (s, e) => { Dress(f); LayoutHeader(); };
            Palette.Changed += (s, e) => { ApplyTheme(); };
        }

        /// <summary>Puts a mark to the left of the title and moves the title over for it.</summary>
        public void Mark(Control mark, int size)
        {
            mark.Location = new Point(Dpi.Px(Space.Edge + 4), Dpi.Px(14));
            mark.Size = new Size(Dpi.Px(size), Dpi.Px(size));
            Header.Controls.Add(mark);
            int shift = Dpi.Px(size + Space.Md);
            _titleLabel.Left += shift;
            _subLabel.Left += shift;
        }

        /// <summary>Repaints the whole control tree when the theme changes.</summary>
        public void ApplyTheme()
        {
            Form.BackColor = Palette.Bg;
            Form.ForeColor = Palette.Text;
            Header.BackColor = Palette.Bg;
            Toolbar.BackColor = Palette.Bg;
            List.BackColor = Palette.Bg;
            Footer.BackColor = Palette.Surface;
            _titleLabel.ForeColor = Palette.Text;
            _subLabel.ForeColor = Palette.TextMuted;
            FooterLeft.ForeColor = Palette.TextSecondary;
            FooterRight.ForeColor = Palette.TextMuted;
            Bar.Tint = Palette.Accent;
            Chrome(Form);
            Repaint(Form);
            Form.Invalidate(true);
        }

        static void Repaint(Control k)
        {
            foreach (Control c in k.Controls)
            {
                if (c is Row) c.BackColor = Palette.Surface;
                else if (c is TextBox || c is ComboBox || c is ListControl) { c.BackColor = Palette.Surface; c.ForeColor = Palette.Text; }
                else if (c is Label && c.ForeColor != Palette.Accent) { /* label colour is owned by its creator */ }
                c.Invalidate();
                if (c.Controls.Count > 0) Repaint(c);
            }
        }

        /// <summary>Dark/light toggle, added to the top right.</summary>
        public UiButton ThemeButton()
        {
            var b = new UiButton(Palette.IsDark ? "☾" : "☀", ButtonKind.Ghost, false);
            b.Width = Dpi.Px(44);
            b.Click += (s, e) =>
            {
                Palette.Apply(!Palette.IsDark);
                Palette.Save();
                b.Text = Palette.IsDark ? "☾" : "☀";
                b.Width = Dpi.Px(44);
            };
            return TopRight(b);
        }

        readonly List<Control> _topRight = new List<Control>();

        public T TopRight<T>(T c) where T : Control
        {
            c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _topRight.Add(c);
            Header.Controls.Add(c);
            LayoutHeader();
            return c;
        }

        void LayoutHeader()
        {
            int x = Form.ClientSize.Width - Dpi.Px(Space.Edge);
            foreach (var c in _topRight)
            {
                x -= c.Width;
                c.Location = new Point(x, Dpi.Px(20));
                x -= Dpi.Px(Space.Sm);
            }
            PlaceFooterRight();
        }

        void PlaceFooterRight()
        {
            // TextChanged runs before AutoSize has resized the label, so Width is
            // still the previous caption's. PreferredSize is the new one.
            int w = Math.Max(FooterRight.Width, FooterRight.PreferredSize.Width);
            FooterRight.Location = new Point(Footer.Width - w - Dpi.Px(Space.Edge), Dpi.Px(15));
        }

        public void Populate(IEnumerable<Row> rows, EmptyState empty = null)
        {
            List.SuspendLayout();
            foreach (var old in List.Controls.Cast<Control>().ToList()) old.Dispose();
            List.Controls.Clear();
            var arr = rows.ToList();
            if (arr.Count == 0)
            {
                List.Controls.Add(empty ?? new EmptyState());
            }
            else
            {
                // Dock.Top stacking: add in reverse order
                for (int i = arr.Count - 1; i >= 0; i--)
                {
                    arr[i].Width = List.ClientSize.Width - Dpi.Px(Space.Xxxl);
                    List.Controls.Add(arr[i]);
                }
            }
            List.ResumeLayout();
        }
    }
}
