// ============================================================================
//  McpSetup - wizard for adding a new MCP server
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DevSuite;

[assembly: AssemblyTitle("McpSetup")]
[assembly: AssemblyDescription("Add a new MCP server to your AI clients")]
[assembly: AssemblyProduct("DevSuite")]
[assembly: AssemblyCompany("Enes Aksu")]
[assembly: AssemblyCopyright("Copyright © 2026 Enes Aksu")]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]
[assembly: ComVisible(false)]

namespace McpSetup
{
    class Template
    {
        public string Name, Description, Command, Arguments, Environment;
        public override string ToString() { return Name; }
    }

    class MainWindow : Form
    {
        Shell _ui;
        TextBox _nameBox, _cmdBox, _argsBox, _envBox;
        CheckedListBox _clientList;
        ComboBox _templateBox;
        TextBox _previewBox;
        Label _stateBar;
        List<Client> _clients;

        static readonly Template[] Templates =
        {
            new Template { Name = "— blank —", Command = "", Arguments = "" },
            new Template { Name = "filesystem", Description = "Filesystem access",
                        Command = "npx", Arguments = "-y @modelcontextprotocol/server-filesystem C:\\Users" },
            new Template { Name = "github", Description = "GitHub repositories",
                        Command = "npx", Arguments = "-y @modelcontextprotocol/server-github",
                        Environment = "GITHUB_PERSONAL_ACCESS_TOKEN=" },
            new Template { Name = "memory", Description = "Persistent memory",
                        Command = "npx", Arguments = "-y @modelcontextprotocol/server-memory" },
            new Template { Name = "postgres", Description = "PostgreSQL queries",
                        Command = "npx", Arguments = "-y @modelcontextprotocol/server-postgres postgresql://localhost/db" },
            new Template { Name = "puppeteer", Description = "Browser automation",
                        Command = "npx", Arguments = "-y @modelcontextprotocol/server-puppeteer" },
            new Template { Name = "python (uvx)", Description = "Python-based server",
                        Command = "uvx", Arguments = "package-name" },
        };

        public MainWindow()
        {
            Loc.Add(new Dictionary<string, string>
            {
                { "app", "MCP Setup" },
                { "sub", "Add a new MCP server" },
                { "tpl", "Template" },
                { "name", "Server name" },
                { "cmd", "Command" },
                { "args", "Arguments" },
                { "env", "Environment variables" },
                { "env_hint", "one per line:  KEY=value" },
                { "targets", "Add to which clients?" },
                { "preview", "Preview" },
                { "test", "Test command" },
                { "add", "Add" },
                { "need_name", "Name and command cannot be empty." },
                { "need_tgt", "Select at least one client." },
                { "ok", "\"{0}\" was added to:\n\n{1}\n\nFully restart the client for this to take effect." },
                { "exists", "A server with this name already exists in {0}. Overwrite?" },
                { "bad_name", "Invalid server name: {0}" },
                { "read_fail", "This config file could not be parsed, so it was left untouched:\n{0}\n\n{1}" },
                { "testing", "Running..." },
                { "test_ok", "Command ran (exit code {0}). MCP servers normally stay open; we stopped it after a few seconds." },
                { "test_fail", "Could not run the command:\n{0}" },
            });

            Palette.Load();
            _ui = new Shell(this, Loc.S("app"), Loc.S("sub"), 960, 760);
            _ui.Toolbar.Height = 0;
            _ui.ThemeButton();
            Palette.Changed += (s, e) => { BackColor = Palette.Bg; Invalidate(true); };

            var g = _ui.List;
            g.Padding = new Padding(Dpi.Px(Space.Edge + 6), Dpi.Px(Space.Sm), Dpi.Px(Space.Edge), Dpi.Px(Space.Lg));

            int y = 6;

            g.Controls.Add(MakeLabel(Loc.S("tpl"), 0, y));
            _templateBox = new ComboBox
            {
                Location = new Point(0, Dpi.Px(y + 22)), Width = Dpi.Px(400), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat, BackColor = Palette.Surface, ForeColor = Palette.Text
            };
            foreach (var h in Templates) _templateBox.Items.Add(h);
            _templateBox.SelectedIndex = 0;
            _templateBox.SelectedIndexChanged += (s, e) => ApplyTemplate();
            g.Controls.Add(_templateBox);
            y += 62;

            g.Controls.Add(MakeLabel(Loc.S("name"), 0, y));
            _nameBox = MakeBox(0, y + 22, 400); g.Controls.Add(_nameBox);
            y += 62;

            g.Controls.Add(MakeLabel(Loc.S("cmd"), 0, y));
            _cmdBox = MakeBox(0, y + 22, 220); g.Controls.Add(_cmdBox);
            g.Controls.Add(MakeLabel(Loc.S("args"), 236, y));
            _argsBox = MakeBox(236, y + 22, 620); g.Controls.Add(_argsBox);
            y += 62;

            g.Controls.Add(MakeLabel(Loc.S("env"), 0, y));
            g.Controls.Add(MakeHint(Loc.S("env_hint"), 90, y + 2));
            _envBox = MakeBox(0, y + 22, 856); _envBox.Multiline = true; _envBox.Height = Dpi.Px(62);
            g.Controls.Add(_envBox);
            y += 100;

            g.Controls.Add(MakeLabel(Loc.S("targets"), 0, y));
            _clients = Client.All().Where(c => c.Exists).ToList();
            if (_clients.Count == 0) _clients = Client.All();
            _clientList = new CheckedListBox
            {
                Location = new Point(0, Dpi.Px(y + 22)), Size = new Size(Dpi.Px(400), Dpi.Px(76)),
                BackColor = Palette.Surface, ForeColor = Palette.Text,
                BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true, IntegralHeight = false
            };
            foreach (var c in _clients) _clientList.Items.Add(c.Name + (c.Exists ? "" : "  (no file)"), c.Exists);
            g.Controls.Add(_clientList);

            g.Controls.Add(MakeLabel(Loc.S("preview"), 424, y));
            _previewBox = new TextBox
            {
                Location = new Point(Dpi.Px(424), Dpi.Px(y + 22)), Size = new Size(Dpi.Px(432), Dpi.Px(76)), Multiline = true,
                ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Palette.Surface, ForeColor = Palette.TextMuted,
                Font = Fonts.Mono
            };
            g.Controls.Add(_previewBox);
            y += 116;

            var bAdd = new UiButton(Loc.S("add"), ButtonKind.Primary) { Location = new Point(0, Dpi.Px(y)) };
            bAdd.Click += (s, e) => Add();
            g.Controls.Add(bAdd);

            var bTest = new UiButton(Loc.S("test"), ButtonKind.Secondary) { Location = new Point(bAdd.Width + Dpi.Px(Space.Md), Dpi.Px(y)) };
            bTest.Click += (s, e) => TestRun();
            g.Controls.Add(bTest);

            _stateBar = new Label
            {
                Location = new Point(Dpi.Px(340), Dpi.Px(y + 12)), AutoSize = true,
                ForeColor = Palette.TextMuted, MaximumSize = new Size(Dpi.Px(520), 0)
            };
            g.Controls.Add(_stateBar);

            _nameBox.TextChanged += (s, e) => Preview();
            _cmdBox.TextChanged += (s, e) => Preview();
            _argsBox.TextChanged += (s, e) => Preview();
            _envBox.TextChanged += (s, e) => Preview();
            Preview();
        }

        Label MakeLabel(string s, int x, int y)
        {
            return new Label
            {
                Text = s, Location = new Point(Dpi.Px(x), Dpi.Px(y)), AutoSize = true,
                ForeColor = Palette.Text, Font = Fonts.Label
            };
        }
        Label MakeHint(string s, int x, int y)
        {
            return new Label { Text = s, Location = new Point(Dpi.Px(x), Dpi.Px(y)), AutoSize = true, ForeColor = Palette.TextMuted, Font = Fonts.Small };
        }
        TextBox MakeBox(int x, int y, int w)
        {
            return new TextBox
            {
                Location = new Point(Dpi.Px(x), Dpi.Px(y)), Width = Dpi.Px(w), BorderStyle = BorderStyle.FixedSingle,
                BackColor = Palette.Surface, ForeColor = Palette.Text, Font = Fonts.Mono
            };
        }

        void ApplyTemplate()
        {
            var h = _templateBox.SelectedItem as Template;
            if (h == null || h.Command == null) return;
            if (h.Name != "— blank —") _nameBox.Text = h.Name.Split(' ')[0];
            _cmdBox.Text = h.Command;
            _argsBox.Text = h.Arguments ?? "";
            _envBox.Text = h.Environment ?? "";
            Preview();
        }

        string[] ArgList()
        {
            // simple split: quoted sections stay together
            var s = _argsBox.Text.Trim();
            if (s.Length == 0) return new string[0];
            var items = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (var c in s)
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (c == ' ' && !quoted) { if (sb.Length > 0) { items.Add(sb.ToString()); sb.Clear(); } continue; }
                sb.Append(c);
            }
            if (sb.Length > 0) items.Add(sb.ToString());
            return items.ToArray();
        }

        Dictionary<string, string> EnvList()
        {
            var d = new Dictionary<string, string>();
            foreach (var line in _envBox.Lines)
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var k = line.Substring(0, i).Trim();
                var v = line.Substring(i + 1).Trim();
                if (k.Length > 0) d[k] = v;
            }
            return d;
        }

        void Preview()
        {
            var name = string.IsNullOrWhiteSpace(_nameBox.Text) ? "server-name" : _nameBox.Text.Trim();
            var args = ArgList();
            var env = EnvList();

            var sb = new StringBuilder();
            sb.AppendLine("// JSON (Claude)");
            sb.AppendLine("\"" + name + "\": {");
            sb.AppendLine("  \"command\": \"" + _cmdBox.Text.Trim() + "\",");
            if (args.Length > 0)
                sb.AppendLine("  \"args\": [" + string.Join(", ", args.Select(a => "\"" + a + "\"")) + "]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# TOML (Codex / ChatGPT)");
            sb.AppendLine("[mcp_servers." + name + "]");
            sb.AppendLine("command = \"" + _cmdBox.Text.Trim() + "\"");
            if (args.Length > 0)
                sb.AppendLine("args = [" + string.Join(", ", args.Select(a => "\"" + a + "\"")) + "]");
            sb.AppendLine("enabled = true");
            foreach (var kv in env) sb.AppendLine("# env " + kv.Key);
            _previewBox.Text = sb.ToString();
        }

        void TestRun()
        {
            if (string.IsNullOrWhiteSpace(_cmdBox.Text)) { _stateBar.Text = Loc.S("need_name"); return; }
            _stateBar.ForeColor = Palette.TextMuted;
            _stateBar.Text = Loc.S("testing");
            Application.DoEvents();
            try
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c " + _cmdBox.Text.Trim() + " " + _argsBox.Text.Trim())
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                foreach (var kv in EnvList()) psi.EnvironmentVariables[kv.Key] = kv.Value;

                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(6000)) { try { p.Kill(); } catch { } }
                    _stateBar.ForeColor = Palette.Success;
                    _stateBar.Text = Loc.S("test_ok", p.HasExited ? p.ExitCode.ToString() : "?");
                }
            }
            catch (Exception ex)
            {
                _stateBar.ForeColor = Palette.Danger;
                _stateBar.Text = Loc.S("test_fail", ex.Message);
            }
        }

        void Add()
        {
            var name = _nameBox.Text.Trim();
            if (name.Length == 0 || _cmdBox.Text.Trim().Length == 0)
            { _stateBar.ForeColor = Palette.Danger; _stateBar.Text = Loc.S("need_name"); return; }

            string bad = Mcp.ValidateName(name);
            if (bad != null)
            { _stateBar.ForeColor = Palette.Danger; _stateBar.Text = Loc.S("bad_name", bad); return; }

            var chosen = new List<Client>();
            for (int i = 0; i < _clientList.Items.Count; i++)
                if (_clientList.GetItemChecked(i)) chosen.Add(_clients[i]);

            if (chosen.Count == 0)
            { _stateBar.ForeColor = Palette.Danger; _stateBar.Text = Loc.S("need_tgt"); return; }

            var args = ArgList();
            var env = EnvList();
            var added = new List<string>();

            foreach (var c in chosen)
            {
                // A file that cannot be parsed is never written to.
                List<Server> existing;
                try { existing = Mcp.Read(c); }
                catch (Exception ex)
                {
                    MessageBox.Show(Loc.S("read_fail", c.ConfigPath, ex.Message), Loc.S("error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                if (existing.Any(x => x.Name == name))
                {
                    if (MessageBox.Show(Loc.S("exists", c.Name), Loc.S("warn"),
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) continue;
                }
                try { Mcp.Add(c, name, _cmdBox.Text.Trim(), args, env); added.Add("  - " + c.Name); }
                catch (Exception ex)
                {
                    MessageBox.Show(Loc.S("write_fail", ex.Message), Loc.S("error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (added.Count > 0)
            {
                MessageBox.Show(Loc.S("ok", name, string.Join("\n", added)), Loc.S("app"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _stateBar.ForeColor = Palette.Success;
                _stateBar.Text = "\u2713 " + name;
            }
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
