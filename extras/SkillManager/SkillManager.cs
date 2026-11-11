// ============================================================================
//  SkillManager - who provides each skill, how big it is, is it on
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DevSuite;

[assembly: AssemblyTitle("SkillManager")]
[assembly: AssemblyDescription("Inspect, enable and disable Claude skills")]
[assembly: AssemblyProduct("DevSuite")]
[assembly: AssemblyCompany("Enes Aksu")]
[assembly: AssemblyCopyright("Copyright © 2026 Enes Aksu")]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]
[assembly: ComVisible(false)]

namespace SkillManager
{
    class MainWindow : Form
    {
        Shell _ui;
        FilterBar _stateBar, _sortBar;
        SearchBox _search;
        List<Skill> _all = new List<Skill>();

        public MainWindow()
        {
            Palette.Load();
            Loc.Add(new Dictionary<string, string>
            {
                { "app", "Skill Manager" },
                { "sub", "Installed skills — who provides them, what they cost in context" },
                { "f_all", "All" },
                { "f_on", "On" },
                { "f_off", "Off" },
                { "f_warn", "Flagged" },
                { "s_name", "Name" },
                { "s_owner", "Provider" },
                { "s_size", "Size" },
                { "s_date", "Date" },
                { "own_personal", "personal" },
                { "own_desktop", "desktop" },
                { "untouched", "{0}d ago" },
                { "summary", "{0} skills · {1} on · {2} off · {3} warnings · {4} providers" },
                { "edit", "SKILL.md — {0}" },
                { "empty", "No matching skills" },
                { "empty_all", "No skills found" },
                { "empty_hint", "Scanned ~/.claude/skills and plugin folders." },
                { "w_dup", "{0} more skills share this name" },
                { "w_desc", "description is {0} chars — loaded in every conversation" },
                { "w_big", "SKILL.md is {0} KB" },
                { "w_broad", "trigger is very broad" },
                { "w_nodesc", "no description" },
                { "nowarn", "looks fine" },
                { "disable_title", "Disable this skill?" },
                { "disable_q", "\"{0}\" will stop loading in your clients.\n\nIts folder moves from:\n{1}\n\nto:\n{2}\n\nNothing is deleted. You can enable it again here." },
            });

            _ui = new Shell(this, Loc.S("app"), Loc.S("sub"), 1100, 720);

            _ui.TopRight(new UiButton(Loc.S("refresh"), ButtonKind.Secondary)).Click += (s, e) => Reload();
            _ui.ThemeButton();

            _stateBar = new FilterBar { Location = new Point(Dpi.Px(Space.Edge + 4), Dpi.Px(11)) };
            _stateBar.Build(
                new FilterBar.Chip { Key = "all",  Label = Loc.S("f_all") },
                new FilterBar.Chip { Key = "on",   Label = Loc.S("f_on") },
                new FilterBar.Chip { Key = "off",  Label = Loc.S("f_off") },
                new FilterBar.Chip { Key = "warn", Label = Loc.S("f_warn"), Tint = Palette.Warning });
            _stateBar.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_stateBar);

            _sortBar = new FilterBar { Location = new Point(Dpi.Px(Space.Edge + 4) + _stateBar.Width + Dpi.Px(Space.Xxl), Dpi.Px(11)) };
            _sortBar.Build(
                new FilterBar.Chip { Key = "warn",  Label = "⚠" },
                new FilterBar.Chip { Key = "name",  Label = Loc.S("s_name") },
                new FilterBar.Chip { Key = "owner", Label = Loc.S("s_owner") },
                new FilterBar.Chip { Key = "size",  Label = Loc.S("s_size") },
                new FilterBar.Chip { Key = "date",  Label = Loc.S("s_date") });
            _sortBar.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_sortBar);

            _search = new SearchBox(240) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _search.Location = new Point(ClientSize.Width - _search.Width - Dpi.Px(Space.Edge), Dpi.Px(11));
            _search.Changed += (s, e) => Populate();
            _ui.Toolbar.Controls.Add(_search);
            Resize += (s, e) => _search.Location = new Point(ClientSize.Width - _search.Width - Dpi.Px(Space.Edge), Dpi.Px(11));

            Palette.Changed += (s, e) => Populate();
            Shown += (s, e) => Reload();
        }

        /// <summary>Rescans the skill folders; filtering and search only re-populate.</summary>
        void Reload()
        {
            _all = Skills.Scan();
            Populate();
        }

        static Color OwnerColor(string kind)
        {
            switch (kind)
            {
                case "plugin":  return Palette.Info;
                case "personal": return Palette.Accent;
                case "desktop": return Palette.Success;
                default:        return Palette.TextSecondary;
            }
        }

        void Populate()
        {
            var q = _search.Text.ToLowerInvariant();

            IEnumerable<Skill> items = _all;
            switch (_stateBar.Selected)
            {
                case "on":   items = _all.Where(x => x.Enabled); break;
                case "off":  items = _all.Where(x => !x.Enabled); break;
                case "warn": items = _all.Where(x => x.Warnings.Count > 0); break;
            }
            if (q.Length > 0)
                items = items.Where(x =>
                    (x.Name ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Owner ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Description ?? "").ToLowerInvariant().Contains(q));

            switch (_sortBar.Selected)
            {
                case "name":  items = items.OrderBy(x => x.Name); break;
                case "owner": items = items.OrderBy(x => x.Owner).ThenBy(x => x.Name); break;
                case "size":  items = items.OrderByDescending(x => x.Bytes); break;
                case "date":  items = items.OrderByDescending(x => x.Date); break;
                default:      items = items.OrderByDescending(x => x.Warnings.Count).ThenBy(x => x.Name); break;
            }

            _stateBar.SetCounts(new Dictionary<string, int>
            {
                { "all",  _all.Count },
                { "on",   _all.Count(x => x.Enabled) },
                { "off",  _all.Count(x => !x.Enabled) },
                { "warn", _all.Count(x => x.Warnings.Count > 0) },
            });

            // Counts change chip width; the bar built from the placeholder (count-less) width
            // would otherwise overlap once real numbers come in.
            _sortBar.Location = new Point(_stateBar.Right + Dpi.Px(Space.Xxl), Dpi.Px(11));

            var rows = new List<Row>();
            foreach (var skill in items)
            {
                var s = skill;
                var r = new Row
                {
                    Title = s.Name,
                    Enabled = s.Enabled,
                    Dim = !s.Enabled,
                    Warning = s.Warnings.Count,
                    RightValue = s.Bytes >= 1024 ? (s.Bytes / 1024.0).ToString("0") + " KB" : s.Bytes + " B",
                    RightColor = s.Bytes > 50 * 1024 ? Palette.Warning : Palette.TextMuted,
                    Subtitle = s.Warnings.Count > 0
                              ? string.Join("  ·  ", s.Warnings)
                              : (string.IsNullOrEmpty(s.Description) ? Loc.S("nowarn") : s.Description)
                };
                r.Badges.Add(s.Owner ?? "?");
                r.BadgeColors.Add(OwnerColor(s.OwnerKind));
                r.Badges.Add(Loc.S("untouched", s.DaysAgo));
                r.BadgeColors.Add(Palette.TextMuted);

                r.AddAction(s.Enabled ? Loc.S("disable") : Loc.S("enable"),
                            s.Enabled ? ButtonKind.Danger : ButtonKind.Success,
                            () =>
                            {
                                if (s.Enabled && MessageBox.Show(this,
                                        Loc.S("disable_q", s.Name, s.Path, Skills.DisabledPath(s)), Loc.S("disable_title"),
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                                try { Skills.Toggle(s, !s.Enabled); }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(Loc.S("write_fail", ex.Message), Loc.S("error"),
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                                Reload();
                            });
                r.AddAction(Loc.S("view"), ButtonKind.Secondary, () => Edit(s));
                r.AddAction(Loc.S("reveal"), ButtonKind.Ghost,
                            () => { try { Process.Start("explorer.exe", "\"" + s.Path + "\""); } catch { } });

                rows.Add(r);
            }

            _ui.Populate(rows, new EmptyState
            {
                Glyph = "*",
                Title = _all.Count == 0 ? Loc.S("empty_all") : Loc.S("empty"),
                Hint = _all.Count == 0 ? Loc.S("empty_hint") : ""
            });

            int providers = _all.Select(x => x.Owner).Distinct().Count();
            _ui.FooterLeft.Text = Loc.S("summary", _all.Count, _all.Count(x => x.Enabled),
                                  _all.Count(x => !x.Enabled), _all.Sum(x => x.Warnings.Count), providers);
            _ui.FooterRight.Text = string.Join("   ", Skills.Roots().Select(Path.GetFileName));
        }

        void Edit(Skill s)
        {
            var mdPath = Path.Combine(s.Path, "SKILL.md");
            if (!File.Exists(mdPath)) return;

            using (var f = new Form())
            {
                f.Text = Loc.S("edit", s.Name);
                f.ClientSize = new Size(Dpi.Px(880), Dpi.Px(660));
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Palette.Bg;
                f.ForeColor = Palette.Text;
                f.Font = Fonts.Body;
                try { f.Icon = Icon; } catch { }

                var box = new TextBox
                {
                    Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                    BackColor = Palette.Surface, ForeColor = Palette.Text, BorderStyle = BorderStyle.None,
                    AcceptsTab = true, Font = Fonts.Mono, MaxLength = 0, Text = File.ReadAllText(mdPath)
                };
                var pad = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Bg,
                    Padding = new Padding(Dpi.Px(Space.Edge), Dpi.Px(Space.Md), Dpi.Px(Space.Edge), 0) };
                pad.Controls.Add(box);

                var bottom = new Panel { Dock = DockStyle.Bottom, Height = Dpi.Px(62), BackColor = Palette.Bg,
                    Padding = new Padding(Dpi.Px(Space.Edge), Dpi.Px(Space.Md), Dpi.Px(Space.Edge), Dpi.Px(Space.Md)) };

                var bSave = new UiButton(Loc.S("save"), ButtonKind.Primary) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
                bSave.Location = new Point(f.ClientSize.Width - bSave.Width - Dpi.Px(Space.Edge), Dpi.Px(Space.Md));
                bSave.Click += (s2, e2) =>
                {
                    try
                    {
                        File.Copy(mdPath, mdPath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak", true);
                        File.WriteAllText(mdPath, box.Text, new UTF8Encoding(false));
                        f.Close(); Reload();
                    }
                    catch (Exception ex) { MessageBox.Show(Loc.S("write_fail", ex.Message), Loc.S("error")); }
                };
                var bClose = new UiButton(Loc.S("close"), ButtonKind.Ghost) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
                bClose.Location = new Point(bSave.Left - bClose.Width - Dpi.Px(Space.Sm), Dpi.Px(Space.Md));
                bClose.Click += (s2, e2) => f.Close();

                var pathLabel = new Label
                {
                    Text = s.Path, ForeColor = Palette.TextMuted, Font = Fonts.Small,
                    Location = new Point(Dpi.Px(Space.Edge), Dpi.Px(Space.Xl)), AutoSize = true
                };

                bottom.Controls.Add(pathLabel);
                bottom.Controls.Add(bSave);
                bottom.Controls.Add(bClose);
                f.Controls.Add(pad);
                f.Controls.Add(bottom);
                f.Load += (s2, e2) => Shell.Dress(f);
                f.ShowDialog(this);
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
