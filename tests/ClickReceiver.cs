// Manual smoke-test target. Receives input; it never sends any input itself.
using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class ClickReceiver : Form
{
    private int left, right;
    private Label summary;
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new ClickReceiver());
    }
    private ClickReceiver()
    {
        Text = "Перевірка кліків — тестове вікно";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 450);
        Font = new Font("Segoe UI", 14);
        BackColor = Color.White;
        Label title = new Label { Text = "Тестова область автоклікера", AutoSize = true, Location = new Point(30, 25) };
        summary = new Label { Text = "Ліві: 0 · Праві: 0", AutoSize = true, Location = new Point(30, 380) };
        Controls.AddRange(new Control[] { title, summary });
        AddTarget("Ліва точка", new Rectangle(40, 100, 280, 230), Color.FromArgb(226, 219, 255));
        AddTarget("Права точка", new Rectangle(400, 100, 280, 230), Color.FromArgb(207, 240, 246));
    }
    private void AddTarget(string name, Rectangle bounds, Color color)
    {
        Label target = new Label { Text = name, TextAlign = ContentAlignment.MiddleCenter, Bounds = bounds, BackColor = color };
        Shown += delegate
        {
            Point center = target.PointToScreen(new Point(target.Width / 2, target.Height / 2));
            target.Text = name + "\nX=" + center.X + " Y=" + center.Y;
        };
        target.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) left++;
            if (e.Button == MouseButtons.Right) right++;
            summary.Text = "Ліві: " + left + " · Праві: " + right;
        };
        Controls.Add(target);
    }
}
