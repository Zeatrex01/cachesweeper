// ============================================================================
//  DevSuite Hub - the screen that opens first.
//  Lists the modules sitting next to it and starts one when asked. It does no
//  scanning, uses no network, and runs nothing until a button is pressed.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DevSuite;

[assembly: AssemblyTitle("DevSuite Hub")]
[assembly: AssemblyDescription("Start screen for the DevSuite modules")]
[assembly: AssemblyProduct("DevSuite")]
[assembly: AssemblyCompany("Enes Aksu")]
[assembly: AssemblyCopyright("Copyright 2026 Enes Aksu")]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]
[assembly: ComVisible(false)]

namespace Hub
{
    // ====================================================================
    class Module
    {
        public string Id, Name, Category, Glyph, ColorHex, En;
        public bool Experimental;

        public string Exe { get { return Id + ".exe"; } }
        public string Description { get { return Loc.S2("pkg_" + Id, En); } }
        public Color Accent { get { return ColorTranslator.FromHtml(ColorHex); } }
    }

    static class Modules
    {
        // The Hub only ever starts these. Any other exe in the folder is none of its business.
        static readonly Module[] Known =
        {
            new Module { Id = "CacheSweep", Name = "CacheSweep", Category = "disk", Glyph = "⌫", ColorHex = "#3B82F6",
                         En = "Finds, measures and clears the caches piled up by Node, Python, Unity and Android toolchains." },
            new Module { Id = "McpControl", Name = "MCP Control", Category = "mcp", Glyph = "⬡", ColorHex = "#22C55E", Experimental = true,
                         En = "Lists MCP servers across clients and switches each on or off." },
            new Module { Id = "McpSetup", Name = "MCP Setup", Category = "mcp", Glyph = "＋", ColorHex = "#A855F7", Experimental = true,
                         En = "Adds a new MCP server to several clients." },
            new Module { Id = "SkillManager", Name = "Skill Manager", Category = "skill", Glyph = "◇", ColorHex = "#F59E0B", Experimental = true,
                         En = "Lists skills with their provider and switches them on or off." },
        };

        public static string Root { get { return Path.GetDirectoryName(Application.ExecutablePath); } }

        public static List<Module> Available(string folder)
        {
            return Known.Where(m => File.Exists(Path.Combine(folder, m.Exe))).ToList();
        }
    }

    // ====================================================================
    class Card : Panel
    {
        readonly Module _m;
        bool _hot;

        public Card(Module m)
        {
            _m = m;
            Size = new Size(Dpi.Px(320), Dpi.Px(172));
            DoubleBuffered = true;
            BackColor = Palette.Bg;
            MouseEnter += (s, e) => { _hot = true; Invalidate(); };
            MouseLeave += (s, e) => { _hot = false; Invalidate(); };

            var open = new UiButton(Loc.S("open"), ButtonKind.Primary) { Location = new Point(Dpi.Px(Space.Lg), Dpi.Px(118)) };
            open.Click += (s, e) => Open();
            Controls.Add(open);
        }

        void Open()
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(Modules.Root, _m.Exe)) { WorkingDirectory = Modules.Root }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Loc.S("app"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var sz2 = Draw.Quality(g, this);
            int w = (int)sz2.Width, h = (int)sz2.Height;
            var r = new RectangleF(0, 0, w - 1, h - 1);

            Draw.Fill(g, r, _hot ? Palette.SurfaceRaised : Palette.Surface, 14);
            Draw.Stroke(g, r, _hot ? Color.FromArgb(140, _m.Accent) : Palette.Border, 14, _hot ? 1.5f : 1f);

            var box = new RectangleF(Space.Lg, Space.Lg, 52, 52);
            Draw.Fill(g, box, _m.Accent, 14);
            using (var f = new Font("Segoe UI", 25f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.White))
            {
                var sz = g.MeasureString(_m.Glyph, f);
                g.DrawString(_m.Glyph, f, b, box.X + (box.Width - sz.Width) / 2, box.Y + (box.Height - sz.Height) / 2);
            }

            using (var b = new SolidBrush(Palette.Text))
                g.DrawString(_m.Name, Fonts.H2, b, Space.Lg + 66, Space.Lg + 3);

            int rx = Space.Lg + 68;
            rx += Draw.Badge(g, _m.Category, rx, Space.Lg + 28, Color.FromArgb(38, _m.Accent), _m.Accent, true) + Space.Xs;
            if (_m.Experimental)
                Draw.Badge(g, Loc.S("experimental"), rx, Space.Lg + 28, Palette.SurfaceHover, Palette.Warning);

            using (var b = new SolidBrush(Palette.TextMuted))
                g.DrawString(_m.Description, Fonts.Small, b, new RectangleF(Space.Lg, 80, w - Space.Lg * 2, 34));
        }
    }

    // ====================================================================
    class MainWindow : Form
    {
        readonly Shell _ui;
        readonly FlowLayoutPanel _flow;

        public MainWindow()
        {
            Palette.Load();
            Loc.Add(new Dictionary<string, string>
            {
                { "app", "DevSuite" },
                { "sub", "Pick a tool. Nothing runs until you open it." },
                { "open", "Open" },
                { "experimental", "experimental" },
                { "count", "Modules: {0}" },
                { "empty_lib", "No modules found" },
                { "empty_lib_h", "Keep CacheSweep.exe in the same folder as this program." },
            });

            _ui = new Shell(this, Loc.S("app"), Loc.S("sub"), 880, 560);
            _ui.ThemeButton();
            _ui.Toolbar.Visible = false;

            var mark = new LogoMark { BackColor = Palette.Bg };
            _ui.Mark(mark, 48);

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = Palette.Bg,
                Padding = new Padding(Dpi.Px(Space.Lg), Dpi.Px(Space.Md), Dpi.Px(Space.Lg), Dpi.Px(Space.Xxl))
            };
            _ui.List.Controls.Add(_flow);
            Palette.Changed += (s, e) => { _flow.BackColor = Palette.Bg; mark.BackColor = Palette.Bg; mark.Invalidate(); Populate(); };

            Shown += (s, e) => Populate();
        }

        void Populate()
        {
            var modules = Modules.Available(Modules.Root);

            _flow.SuspendLayout();
            foreach (var old in _flow.Controls.Cast<Control>().ToList()) old.Dispose();
            _flow.Controls.Clear();
            if (modules.Count == 0)
            {
                _flow.Controls.Add(new EmptyState
                {
                    Dock = DockStyle.None,
                    Size = new Size(_flow.ClientSize.Width - Dpi.Px(40), Dpi.Px(300)),
                    Glyph = "◍",
                    Title = Loc.S("empty_lib"),
                    Hint = Loc.S("empty_lib_h")
                });
            }
            else
            {
                foreach (var m in modules)
                    _flow.Controls.Add(new Card(m) { Margin = new Padding(Dpi.Px(Space.Md)) });
            }
            _flow.ResumeLayout();

            _ui.FooterLeft.Text = Loc.S("count", modules.Count);
        }

        [STAThread]
        static void Main()
        {
            Dpi.Init();
            Loc.LoadPack();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
