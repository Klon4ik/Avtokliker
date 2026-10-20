using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AvtoClicker
{
    internal static class MarkerGeometry
    {
        public const int Size = 76;
        public const int Center = Size / 2;
        public static Point TopLeft(Point point) { return new Point(point.X - Center, point.Y - Center); }
        public static Point ClickPoint(Point location) { return new Point(location.X + Center, location.Y + Center); }
        public static Point Drag(Point center, Point mouseStart, Point mouseNow)
        { return new Point(center.X + mouseNow.X - mouseStart.X, center.Y + mouseNow.Y - mouseStart.Y); }
        public static Point Clamp(Point point, Rectangle screen)
        {
            return new Point(Math.Max(screen.Left, Math.Min(screen.Right - 1, point.X)),
                Math.Max(screen.Top, Math.Min(screen.Bottom - 1, point.Y)));
        }
    }

    // These are independent tool windows: minimizing the editor must not hide the markers.
    internal sealed class PointMarkerManager : IDisposable
    {
        private readonly Dictionary<AutomationAction, PointMarkerForm> markers = new Dictionary<AutomationAction, PointMarkerForm>();
        public event Action<AutomationAction, Point, bool> PositionChanged;
        public event Action<AutomationAction> Selected;
        public event Action<AutomationAction> EditRequested;

        public void Sync(IList<AutomationAction> actions, AutomationAction selected, bool visible, bool running)
        {
            HashSet<AutomationAction> current = new HashSet<AutomationAction>();
            int number = 0;
            foreach (AutomationAction action in actions)
            {
                if (action.Kind != "Mouse") continue;
                current.Add(action); number++;
                PointMarkerForm marker;
                if (!markers.TryGetValue(action, out marker))
                {
                    marker = new PointMarkerForm(action);
                    marker.PositionChanged += delegate(AutomationAction a, Point point, bool committed)
                    { if (PositionChanged != null) PositionChanged(a, point, committed); };
                    marker.Selected += delegate(AutomationAction a) { if (Selected != null) Selected(a); };
                    marker.EditRequested += delegate(AutomationAction a) { if (EditRequested != null) EditRequested(a); };
                    markers.Add(action, marker);
                }
                marker.UpdateAppearance(number, action == selected);
                marker.PassThrough = running;
                if (!marker.Dragging) marker.Location = MarkerGeometry.TopLeft(new Point(action.X, action.Y));
                bool onScreen = false;
                foreach (Screen screen in Screen.AllScreens) if (screen.Bounds.Contains(action.X, action.Y)) onScreen = true;
                if (visible && onScreen) { if (!marker.Visible) marker.Show(); }
                else if (marker.Visible) marker.Hide();
            }
            List<AutomationAction> removed = new List<AutomationAction>();
            foreach (AutomationAction action in markers.Keys) if (!current.Contains(action)) removed.Add(action);
            foreach (AutomationAction action in removed) { markers[action].Dispose(); markers.Remove(action); }
        }
        public void UpdateCount(AutomationAction action, long count)
        {
            PointMarkerForm marker;
            if (markers.TryGetValue(action, out marker)) marker.UpdateCount(count);
        }
        public void Tick() { foreach (PointMarkerForm marker in markers.Values) marker.Tick(); }
        public void Dispose()
        {
            foreach (PointMarkerForm marker in markers.Values) marker.Dispose();
            markers.Clear();
        }
    }

    internal sealed class PointMarkerForm : Form
    {
        private const int TransparentStyle = 0x20;
        private readonly AutomationAction action;
        private readonly ToolTip hint = new ToolTip();
        private static readonly Color[] Palette = { Theme.Accent, Color.FromArgb(0, 143, 166),
            Color.FromArgb(215, 122, 36), Color.FromArgb(34, 153, 104), Color.FromArgb(197, 66, 126) };
        private bool passThrough, dragging, selected, flashing;
        private int number, flashAt;
        private long count;
        private Point dragMouse, dragCenter;
        public event Action<AutomationAction, Point, bool> PositionChanged;
        public event Action<AutomationAction> Selected;
        public event Action<AutomationAction> EditRequested;
        public bool Dragging { get { return dragging; } }
        public bool PassThrough
        {
            get { return passThrough; }
            set
            {
                if (value == passThrough) return;
                if (value) EndDrag();
                passThrough = value;
                if (IsHandleCreated)
                {
                    int style = GetWindowLong(Handle, -20);
                    SetWindowLong(Handle, -20, value ? style | TransparentStyle : style & ~TransparentStyle);
                    SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x37);
                }
                if (!value) { flashing = false; count = 0; }
                Invalidate();
            }
        }
        public PointMarkerForm(AutomationAction action)
        {
            this.action = action;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(MarkerGeometry.Size, MarkerGeometry.Size);
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Opacity = 0.92;
            DoubleBuffered = true;
            Cursor = Cursors.SizeAll;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x80 | 0x80000; // NOACTIVATE, TOOLWINDOW, LAYERED
                if (passThrough) parameters.ExStyle |= TransparentStyle;
                return parameters;
            }
        }
        public void UpdateAppearance(int number, bool selected)
        {
            this.number = number; this.selected = selected;
            Text = L10n.T("Точка кліку ") + number;
            AccessibleName = Text + ": " + action.Name;
            hint.SetToolTip(this, "№" + number + " · " + action.Name + " · " + action.ActionText +
                L10n.T("\nІнтервал: ") + (action.IntervalMs / 1000m).ToString("0.##") + L10n.T(" с") +
                (action.Enabled ? "" : L10n.T(" · вимкнено")) + L10n.T("\nПеретягніть, щоб перемістити. Подвійний клік — змінити."));
            Invalidate();
        }
        public void UpdateCount(long value)
        {
            if (value > count) { flashing = true; flashAt = Environment.TickCount; Invalidate(); }
            count = value;
        }
        public void Tick()
        {
            if (flashing && unchecked(Environment.TickCount - flashAt) >= 180)
            { flashing = false; Invalidate(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = action.Enabled ? Palette[(Math.Max(1, number) - 1) % Palette.Length] : Theme.Muted;
            if (flashing) color = Color.FromArgb(35, 199, 126);
            Rectangle ring = new Rectangle(8, 8, 60, 60);
            using (Brush fill = new SolidBrush(Theme.Navy)) graphics.FillEllipse(fill, ring);
            using (Pen border = new Pen(color, selected ? 5 : 3))
            {
                if (!action.Enabled) border.DashStyle = DashStyle.Dash;
                graphics.DrawEllipse(border, ring);
            }
            using (Pen cross = new Pen(Color.White, 2))
            {
                graphics.DrawLine(cross, 28, 38, 48, 38);
                graphics.DrawLine(cross, 38, 28, 38, 48);
            }
            using (Brush center = new SolidBrush(color)) graphics.FillEllipse(center, 35, 35, 6, 6);
            using (Brush badge = new SolidBrush(color)) graphics.FillEllipse(badge, 48, 0, 28, 28);
            using (Font font = new Font("Segoe UI", number >= 100 ? 8 : 10, FontStyle.Bold))
                TextRenderer.DrawText(graphics, number.ToString(), font, new Rectangle(48, 0, 28, 28), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (passThrough)
            {
                using (Brush state = new SolidBrush(color)) graphics.FillEllipse(state, 34, 57, 8, 8);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (passThrough || e.Button != MouseButtons.Left) return;
            dragMouse = Cursor.Position;
            dragCenter = MarkerGeometry.ClickPoint(Location);
            dragging = true; Capture = true;
            if (Selected != null) Selected(action);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || passThrough) return;
            Point point = MarkerGeometry.Drag(dragCenter, dragMouse, Cursor.Position);
            point = MarkerGeometry.Clamp(point, Screen.FromPoint(point).Bounds);
            Location = MarkerGeometry.TopLeft(point);
            if (PositionChanged != null) PositionChanged(action, point, false);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) EndDrag(); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { base.OnMouseCaptureChanged(e); if (!Capture) EndDrag(); }
        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false; Capture = false;
            if (PositionChanged != null) PositionChanged(action, MarkerGeometry.ClickPoint(Location), true);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (!passThrough && e.Button == MouseButtons.Left)
            { EndDrag(); if (EditRequested != null) EditRequested(action); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) hint.Dispose(); base.Dispose(disposing); }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
