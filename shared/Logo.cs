using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DevSuite
{
    // ========================================================================
    //  LOGO - assets/logo.svg as code, so it stays sharp at any display scale
    // ========================================================================
    public static class Logo
    {
        static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Draws the mark inside a square. Coordinates match the SVG's 256 unit grid.</summary>
        public static void Paint(Graphics g, RectangleF box)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = box.Width / 256f;
            Func<float, float, float, float, RectangleF> at =
                (x, y, w, h) => new RectangleF(box.X + x * s, box.Y + y * s, w * s, h * s);

            var plate = at(8, 8, 240, 240);
            using (var path = Round(plate, 56 * s))
            using (var fill = new LinearGradientBrush(plate, Color.FromArgb(59, 130, 246), Color.FromArgb(14, 165, 164), 45f))
                g.FillPath(fill, path);

            int[] alpha = { 255, 209, 153 };
            float[] widths = { 152, 104, 56 };
            float[] tops = { 66, 112, 158 };
            for (int i = 0; i < 3; i++)
                using (var path = Round(at(52, tops[i], widths[i], 32), 16 * s))
                using (var b = new SolidBrush(Color.FromArgb(alpha[i], Color.White)))
                    g.FillPath(b, path);

            using (var pen = new Pen(Color.White, 10 * s))
                g.DrawEllipse(pen, at(168, 156, 36, 36));
            g.SmoothingMode = old;
        }
    }

    public class LogoMark : Control
    {
        public LogoMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Logo.Paint(e.Graphics, new RectangleF(0, 0, Width, Height));
        }
    }
}
