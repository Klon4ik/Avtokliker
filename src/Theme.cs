using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AvtoClicker
{
    internal static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(22, 29, 48);
        public static readonly Color Accent = Color.FromArgb(100, 76, 224);
        public static readonly Color Background = Color.FromArgb(243, 245, 250);
        public static readonly Color Text = Color.FromArgb(32, 42, 61);
        public static readonly Color Muted = Color.FromArgb(100, 112, 135);
        public static Button Button(string text, bool primary)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(90, 36),
                Padding = new Padding(12, 2, 12, 2), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Text,
                Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(217, 223, 235);
            return button;
        }
        public static Label Label(string text, float size, Color color)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Segoe UI", size),
                BackColor = Color.Transparent, Margin = new Padding(0) };
        }
        public static Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (Brush bg = new SolidBrush(Accent)) graphics.FillEllipse(bg, 1, 1, 62, 62);
                Point[] cursor = { new Point(21, 13), new Point(21, 45), new Point(29, 38),
                    new Point(36, 52), new Point(43, 48), new Point(36, 35), new Point(47, 34) };
                graphics.FillPolygon(Brushes.White, cursor);
                System.IntPtr handle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(System.IntPtr handle);
    }
}
