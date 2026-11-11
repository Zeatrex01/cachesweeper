using System.Drawing;
using System.Windows.Forms;
using DevSuite;

namespace CacheSweep
{
    // ========================================================================
    //  STAT TILE - one number at the top of the window
    // ========================================================================
    class StatTile : Control
    {
        public Theme Skin;
        public string Caption = "", Value = "";
        public Color Ink = Color.Empty;

        public StatTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void Show(string caption, string value)
        {
            Caption = caption; Value = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Skin == null) return;
            var g = e.Graphics;
            var sz = Draw.Quality(g, this);
            using (var b = new SolidBrush(Skin.Bg)) g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
            using (var path = Draw.Rounded(new Rectangle(0, 0, (int)sz.Width - 1, (int)sz.Height - 1), 12))
            using (var b = new SolidBrush(Skin.Panel))
                g.FillPath(b, path);

            using (var f = new Font("Segoe UI", 11.5f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Skin.Muted))
                g.DrawString(Caption, f, b, 16, 10);
            using (var f = new Font("Segoe UI Semibold", 22f, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Ink != Color.Empty ? Ink : Skin.Fg))
                g.DrawString(Value, f, b, 15, 28);
        }
    }
}
