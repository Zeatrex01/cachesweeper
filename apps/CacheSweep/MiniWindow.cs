using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DevSuite;

namespace CacheSweep
{
    // ========================================================================
    //  FLOATING MODES - a small draggable panel and a smaller round icon.
    //  They exist only after the user switches to them. The one live value is
    //  free disk space, which costs a single system call. Folders are measured
    //  only when Quick scan is pressed, and deleting needs the full window.
    // ========================================================================
    abstract class FloatingWindow : Form
    {
        protected readonly MainWindow Main;
        protected long Free, Total;
        readonly System.Windows.Forms.Timer _clock = new System.Windows.Forms.Timer { Interval = 5000 };
        Point _grab, _start;
        bool _down, _moved;

        protected FloatingWindow(MainWindow main)
        {
            Main = main;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            Text = "CacheSweep";
            Icon = main.Icon;

            var menu = new ContextMenuStrip();
            menu.Items.Add(Loc.S("menu_full"), null, (s, e) => Main.ShowFull());
            menu.Items.Add(Loc.S("menu_close"), null, (s, e) => Main.Close());
            ContextMenuStrip = menu;

            _clock.Tick += (s, e) => Reload();
            VisibleChanged += (s, e) => { _clock.Enabled = Visible; if (Visible) Reload(); };
            Main.StateChanged += OnStateChanged;
            FormClosed += (s, e) => { Main.StateChanged -= OnStateChanged; _clock.Dispose(); };
        }

        void OnStateChanged(object sender, EventArgs e) { if (Visible) Reload(); }

        // Windows gives top-level windows a minimum tracking size (about 136 x 39 logical) and
        // applies it while the window is created, which made the round icon wider than tall.
        // These windows have no frame to resize by, so their own size is the only limit.
        const int WM_GETMINMAXINFO = 0x0024;
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_GETMINMAXINFO) return;
            // MINMAXINFO: ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize (offset 24), ...
            System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 24, 1);
            System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 28, 1);
        }

        protected virtual void Reload()
        {
            Free = Measure.FreeSpace();
            Total = Measure.TotalSpace();
            BackColor = Main.Skin.Panel;
            Invalidate();
        }

        protected double UsedRatio { get { return Total > 0 && Free >= 0 ? Math.Min(1.0, (Total - Free) / (double)Total) : 0; } }

        protected Color UsedColor { get { return UsedRatio >= 0.9 ? Main.Skin.Danger : UsedRatio >= 0.75 ? Main.Skin.Warning : Main.Skin.Accent; } }

        /// <summary>Called when the window is clicked rather than dragged.</summary>
        protected virtual void OnTap() { }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _down = true; _moved = false;
            _grab = Cursor.Position;
            _start = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_down) return;
            int dx = Cursor.Position.X - _grab.X, dy = Cursor.Position.Y - _grab.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > Dpi.Px(4)) _moved = true;
            if (_moved) Location = new Point(_start.X + dx, _start.Y + dy);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_down) return;
            _down = false;
            if (!_moved) OnTap();
        }
    }

    class MiniWindow : FloatingWindow
    {
        readonly FlatButton _scan, _small, _full;
        string _line = "";

        public MiniWindow(MainWindow main) : base(main)
        {
            ClientSize = new Size(Dpi.Px(260), Dpi.Px(172));

            _small = Corner("", Dpi.Px(260 - 62));
            _small.Click += (s, e) => Main.ShowIcon();
            _full = Corner("", Dpi.Px(260 - 34));
            _full.Click += (s, e) => Main.ShowFull();

            _scan = new FlatButton { Location = new Point(Dpi.Px(14), Dpi.Px(124)), Size = new Size(Dpi.Px(232), Dpi.Px(36)) };
            _scan.Click += (s, e) => Main.RunScan();
            Controls.Add(_scan);

            using (var path = Draw.Rounded(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), Dpi.Px(12)))
                Region = new Region(path);
        }

        FlatButton Corner(string glyph, int x)
        {
            var b = new FlatButton
            {
                Text = glyph,
                Font = new Font("Segoe MDL2 Assets", 10f, GraphicsUnit.Pixel),
                Location = new Point(x, Dpi.Px(10)),
                Size = new Size(Dpi.Px(24), Dpi.Px(24))
            };
            Controls.Add(b);
            return b;
        }

        protected override void Reload()
        {
            base.Reload();
            var skin = Main.Skin;
            _small.Tint = _full.Tint = skin.PanelRaised;
            _small.Ink = _full.Ink = skin.Muted;
            _scan.Tint = skin.Accent;
            _scan.Text = Main.Working ? Loc.S("scanning") : Loc.S(Main.HasMemory ? "quick_scan" : "rescan");
            _scan.Enabled = !Main.Working;
            foreach (Control c in Controls) c.Invalidate();
            _line = Main.CacheLine();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var sz = Draw.Quality(g, this);
            var skin = Main.Skin;

            using (var pen = new Pen(skin.Border, 1f))
            using (var path = Draw.Rounded(new Rectangle(0, 0, (int)sz.Width - 1, (int)sz.Height - 1), 12))
                g.DrawPath(pen, path);

            using (var f = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(skin.Fg))
                g.DrawString("CacheSweep", f, b, 14, 12);

            string value = Measure.Fmt(Free);
            using (var f = new Font("Segoe UI Semibold", 24f, GraphicsUnit.Pixel))
            using (var small = new Font("Segoe UI", 12f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(skin.Fg))
            using (var m = new SolidBrush(skin.Muted))
            {
                g.DrawString(value, f, b, 12, 40);
                g.DrawString(Loc.S("mini_free"), small, m, 14 + g.MeasureString(value, f).Width, 53);
            }

            var track = new Rectangle(14, 84, (int)sz.Width - 28, 6);
            using (var b = new SolidBrush(skin.PanelRaised))
            using (var path = Draw.Rounded(track, 3))
                g.FillPath(b, path);
            using (var b = new SolidBrush(UsedColor))
            using (var path = Draw.Rounded(new Rectangle(track.X, track.Y, Math.Max(6, (int)(track.Width * UsedRatio)), track.Height), 3))
                g.FillPath(b, path);

            using (var f = new Font("Segoe UI", 11.3f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(skin.Muted))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(_line, f, b, new RectangleF(14, 98, sz.Width - 28, 20), sf);
        }
    }

    class IconWindow : FloatingWindow
    {
        // The circle is drawn from this fixed side, never from the window size, so a window
        // that Windows sizes differently still shows a whole ring.
        const int Side = 72;

        public IconWindow(MainWindow main) : base(main)
        {
            ClientSize = new Size(Dpi.Px(Side), Dpi.Px(Side));
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, Dpi.Px(Side), Dpi.Px(Side));
                Region = new Region(path);
            }
        }

        protected override void OnTap() { Main.ShowMini(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Draw.Quality(g, this);
            var sz = new SizeF(Side, Side);
            var skin = Main.Skin;

            var ring = new RectangleF(5, 5, sz.Width - 10, sz.Height - 10);
            using (var p = new Pen(skin.PanelRaised, 5f)) g.DrawEllipse(p, ring);
            using (var p = new Pen(UsedColor, 5f))
                g.DrawArc(p, ring, -90, (float)(360 * UsedRatio));

            string gb = Free < 0 ? "?" : (Free / 1073741824L).ToString();
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            using (var big = new Font("Segoe UI Semibold", 18f, GraphicsUnit.Pixel))
            using (var small = new Font("Segoe UI", 11f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(skin.Fg))
            using (var m = new SolidBrush(skin.Muted))
            {
                g.DrawString(gb, big, b, new RectangleF(0, 16, sz.Width, 26), sf);
                g.DrawString("GB", small, m, new RectangleF(0, 39, sz.Width, 16), sf);
            }
        }
    }
}
