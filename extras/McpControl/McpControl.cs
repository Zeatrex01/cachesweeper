// ============================================================================
//  McpControl - enable and disable MCP servers
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

[assembly: AssemblyTitle("McpControl")]
[assembly: AssemblyDescription("Enable and disable MCP servers across AI clients")]
[assembly: AssemblyProduct("DevSuite")]
[assembly: AssemblyCompany("Enes Aksu")]
[assembly: AssemblyCopyright("Copyright © 2026 Enes Aksu")]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]
[assembly: ComVisible(false)]

namespace McpControl
{
    class MainWindow : Form
    {
        Shell _ui;
        FilterBar _stateBar, _clientBar;
        SearchBox _search;
        List<Server> _all = new List<Server>();

        public MainWindow()
        {
            Palette.Load();
            Loc.Add(new Dictionary<string, string>
            {
                { "app", "MCP Control" },
                { "sub", "Which MCP servers start with your clients?" },
                { "f_all", "All" },
                { "f_on", "On" },
                { "f_off", "Off" },
                { "setup", "Add new" },
                { "cfg", "File" },
                { "summary", "{0} servers · {1} on — {1} processes start at launch" },
                { "empty", "No matching servers" },
                { "empty_all", "No MCP servers found" },
                { "empty_hint", "Scanned the Claude Desktop, Claude Code and Codex/ChatGPT config files." },
                { "nosetup", "McpSetup.exe not found." },
                { "read_err", "{0} config file(s) could not be parsed and are hidden. They are never written to." },
                { "read_err_title", "Config file problem" },
                { "disable_title", "Disable this MCP server?" },
                { "disable_q", "\"{0}\" will no longer start with {1}.\n\n{2}\n\nFile: {3}\nA timestamped .bak copy of this file is written first. Nothing is deleted. You can enable it again here." },
                { "effect_json", "Its entry moves from {0} to {1}." },
                { "effect_toml", "Its entry gets enabled = false." },
            });

            _ui = new Shell(this, Loc.S("app"), Loc.S("sub"), 1100, 720);

            _ui.TopRight(new UiButton(Loc.S("setup"), ButtonKind.Primary)).Click += (s, e) => OpenSetup();
            _ui.TopRight(new UiButton(Loc.S("refresh"), ButtonKind.Secondary)).Click += (s, e) => Reload();
            _ui.ThemeButton();

            _stateBar = new FilterBar { Location = new Point(Dpi.Px(Space.Edge + 4), Dpi.Px(11)) };
            _stateBar.Build(
                new FilterBar.Chip { Key = "all", Label = Loc.S("f_all") },
                new FilterBar.Chip { Key = "on",  Label = Loc.S("f_on"),  Tint = Palette.Success },
                new FilterBar.Chip { Key = "off", Label = Loc.S("f_off") });
            _stateBar.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_stateBar);

            _clientBar = new FilterBar { Location = new Point(Dpi.Px(Space.Edge + 4) + _stateBar.Width + Dpi.Px(Space.Xxl), Dpi.Px(11)) };
            var chips = new List<FilterBar.Chip> { new FilterBar.Chip { Key = "*", Label = Loc.S("f_all") } };
            foreach (var c in Client.All()) chips.Add(new FilterBar.Chip { Key = c.Name, Label = c.Name });
            _clientBar.Build(chips.ToArray());
            _clientBar.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_clientBar);

            _search = new SearchBox(230) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _search.Location = new Point(ClientSize.Width - _search.Width - Dpi.Px(Space.Edge), Dpi.Px(11));
            _search.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_search);
            Resize += (s, e) => _search.Location = new Point(ClientSize.Width - _search.Width - Dpi.Px(Space.Edge), Dpi.Px(11));

            Palette.Changed += (s, e) => Populate();
            Shown += (s, e) => Reload();
        }

        /// <summary>Re-reads the config files; filtering and search only re-populate.</summary>
        void Reload()
        {
            _all = Mcp.ReadAll();
            Populate();
        }

        void OpenSetup()
        {
            var path = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "McpSetup.exe");
            if (File.Exists(path)) { try { Process.Start(path); } catch { } }
            else MessageBox.Show(Loc.S("nosetup"), Loc.S("app"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static Color ClientColor(string name)
        {
            if (name.StartsWith("Claude Desktop")) return Palette.Info;
            if (name.StartsWith("Claude Code"))    return Palette.Accent;
            return Palette.Success;
        }

        bool _warnedRead;

        void Populate()
        {
            if (Mcp.Errors.Count > 0 && !_warnedRead)
            {
                _warnedRead = true;
                MessageBox.Show(Loc.S("read_err", Mcp.Errors.Count) + "\n\n" + string.Join("\n\n", Mcp.Errors),
                    Loc.S("read_err_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            var q = _search.Text.ToLowerInvariant();

            IEnumerable<Server> items = _all;
            if (_stateBar.Selected == "on")  items = items.Where(x => x.Enabled);
            if (_stateBar.Selected == "off") items = items.Where(x => !x.Enabled);
            if (_clientBar.Selected != "*") items = items.Where(x => x.Client.Name == _clientBar.Selected);
            if (q.Length > 0)
                items = items.Where(x => (x.Name ?? "").ToLowerInvariant().Contains(q) ||
                                         (x.Command ?? "").ToLowerInvariant().Contains(q));

            _stateBar.SetCounts(new Dictionary<string, int>
            {
                { "all", _all.Count },
                { "on",  _all.Count(x => x.Enabled) },
                { "off", _all.Count(x => !x.Enabled) },
            });
            var counts = new Dictionary<string, int> { { "*", _all.Count } };
            foreach (var g in _all.GroupBy(x => x.Client.Name)) counts[g.Key] = g.Count();
            _clientBar.SetCounts(counts);

            // Counts change chip width; the bar built from the placeholder (count-less) width
            // would otherwise overlap once real numbers come in.
            _clientBar.Location = new Point(_stateBar.Right + Dpi.Px(Space.Xxl), Dpi.Px(11));

            var rows = new List<Row>();
            foreach (var srv in items.OrderBy(x => x.Client.Name).ThenBy(x => x.Name))
            {
                var s = srv;
                var r = new Row
                {
                    Title = s.Name,
                    Enabled = s.Enabled,
                    Dim = !s.Enabled,
                    Subtitle = string.IsNullOrEmpty(s.Command) ? "-" : s.Command,
                    RightValue = "",
                };
                r.Badges.Add(s.Client.Name);
                r.BadgeColors.Add(ClientColor(s.Client.Name));
                r.Badges.Add(s.Enabled ? Loc.S("on") : Loc.S("off"));
                r.BadgeColors.Add(s.Enabled ? Palette.Success : Palette.TextMuted);

                r.AddAction(s.Enabled ? Loc.S("disable") : Loc.S("enable"),
                            s.Enabled ? ButtonKind.Danger : ButtonKind.Success,
                            () =>
                            {
                                if (s.Enabled)
                                {
                                    var effect = s.Client.Format == Format.Json
                                        ? Loc.S("effect_json", s.Client.Key, s.Client.DisabledKey) : Loc.S("effect_toml");
                                    if (MessageBox.Show(this, Loc.S("disable_q", s.Name, s.Client.Name, effect, s.Client.ConfigPath),
                                            Loc.S("disable_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                                            MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                                }
                                try { Mcp.Toggle(s.Client, s.Name, !s.Enabled); }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(Loc.S("write_fail", ex.Message), Loc.S("error"),
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                                Reload();
                            });
                r.AddAction(Loc.S("cfg"), ButtonKind.Ghost, () =>
                {
                    try { Process.Start("explorer.exe", "/select,\"" + s.Client.ConfigPath + "\""); } catch { }
                });

                rows.Add(r);
            }

            _ui.Populate(rows, new EmptyState
            {
                Glyph = "⬡",
                Title = _all.Count == 0 ? Loc.S("empty_all") : Loc.S("empty"),
                Hint = _all.Count == 0 ? Loc.S("empty_hint") : ""
            });

            _ui.FooterLeft.Text = Loc.S("summary", _all.Count, _all.Count(x => x.Enabled));
            _ui.FooterRight.Text = Mcp.Errors.Count > 0 ? Loc.S("read_err", Mcp.Errors.Count) : Loc.S("restart_hint");
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
