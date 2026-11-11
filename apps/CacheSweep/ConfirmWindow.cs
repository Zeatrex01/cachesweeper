using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevSuite;

namespace CacheSweep
{
    // ========================================================================
    //  CONFIRM WINDOW - scrolls, so a long list can never push the buttons off
    //  the screen. Enter and Esc both mean "keep everything"; deleting takes a
    //  deliberate click.
    // ========================================================================
    class ConfirmWindow : Form
    {
        public ConfirmWindow(List<Target> go, Dictionary<Target, List<string>> held, bool recycle, bool dark, Theme skin)
        {
            Text = Loc.S("confirm_title");
            ClientSize = new Size(Dpi.Px(680), Dpi.Px(520));
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = skin.Bg; ForeColor = skin.Fg;
            Font = new Font("Segoe UI", 9f);

            // WinForms docks the last-added control first, so the Fill control goes in first.
            var body = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
                BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
                BackColor = skin.Bg, ForeColor = skin.Fg, Font = new Font("Segoe UI", 9.5f),
                Text = Compose(go, held, recycle).Replace("\r\n", "\n").Replace("\n", "\r\n"),
                TabStop = false
            };
            var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Dpi.Px(22), Dpi.Px(18), Dpi.Px(10), Dpi.Px(10)) };
            pad.Controls.Add(body);
            Controls.Add(pad);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = Dpi.Px(70), BackColor = skin.Panel };
            Controls.Add(footer);

            var keep = new FlatButton { Text = Loc.S("confirm_keep"), Tint = Color.FromArgb(70, 76, 86), Size = new Size(Dpi.Px(170), Dpi.Px(40)) };
            keep.Location = new Point(ClientSize.Width - Dpi.Px(22) - keep.Width, Dpi.Px(15));
            keep.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(keep);
            AcceptButton = keep;
            CancelButton = keep;

            if (go.Count > 0)
            {
                var delete = new FlatButton { Text = Loc.S("confirm_go", go.Count), Tint = skin.Danger, Size = new Size(Dpi.Px(170), Dpi.Px(40)) };
                delete.Location = new Point(keep.Left - Dpi.Px(12) - delete.Width, Dpi.Px(15));
                delete.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
                footer.Controls.Add(delete);
            }

            Load += (s, e) => NativeChrome.Apply(this, dark, skin.Bg, skin.Fg, skin.Border);
            Shown += (s, e) => { keep.Focus(); body.SelectionLength = 0; };
        }

        static string Compose(List<Target> go, Dictionary<Target, List<string>> held, bool recycle)
        {
            var sb = new StringBuilder();
            long total = 0;
            if (go.Count > 0)
            {
                sb.AppendLine(Loc.S("confirm_head"));
                sb.AppendLine();
                foreach (var t in go)
                {
                    sb.AppendLine(t.Name + "   (" + Measure.Fmt(t.Bytes) + ")");
                    sb.AppendLine("    " + t.Description);
                    foreach (var path in t.Found) sb.AppendLine("    " + Guard.Describe(path));
                    if (t.Exclude.Length > 0) sb.AppendLine("    " + Loc.S("confirm_kept", string.Join(", ", t.Exclude)));
                    sb.AppendLine();
                    if (t.Bytes > 0) total += t.Bytes;
                }
                sb.AppendLine(Loc.S("confirm_gain", Measure.Fmt(total)));
                sb.AppendLine(recycle ? Loc.S("confirm_recycle") : Loc.S("confirm_perm"));
            }
            if (held.Count > 0)
            {
                if (go.Count > 0) sb.AppendLine();
                sb.AppendLine(Loc.S("confirm_held"));
                sb.AppendLine();
                foreach (var kv in held.OrderBy(k => k.Key.Name))
                    sb.AppendLine(kv.Key.Name + ":  " + string.Join(", ", kv.Value));
            }
            return sb.ToString();
        }
    }
}
