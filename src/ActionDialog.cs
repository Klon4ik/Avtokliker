using System;
using System.Drawing;
using System.Windows.Forms;

namespace AvtoClicker
{
    public sealed class ActionDialog : Form
    {
        public AutomationAction Result;
        private readonly AutomationAction original;
        private TextBox name;
        private ComboBox button, key;
        private NumericUpDown x, y, interval, delay, repeats;
        private CheckBox ctrl, alt, shift;
        private readonly Timer pickTimer = new Timer { Interval = 1000 };
        private int remaining;
        private Form ownerForm;
        private FormWindowState ownerState;
        private bool picking;
        private Label pickHint;

        public ActionDialog(AutomationAction action)
        {
            original = action.Copy();
            Text = action.Kind == "Mouse" ? L10n.T("Точка кліку") : L10n.T("Натискання клавіші");
            Font = new Font("Segoe UI", 10);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            ClientSize = new Size(520, 550);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Icon = Theme.CreateIcon();
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24),
                ColumnCount = 2, RowCount = 10 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            Label heading = Theme.Label(Text, 18, Theme.Text);
            heading.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            root.Controls.Add(heading, 0, 0); root.SetColumnSpan(heading, 2);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            name = new TextBox { Text = action.Name, MaxLength = 80, Dock = DockStyle.Top, AccessibleName = L10n.T("Назва дії") };
            AddRow(root, 1, L10n.T("Назва"), name, 45);
            if (action.Kind == "Mouse")
            {
                button = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                    AccessibleName = L10n.T("Кнопка миші") };
                button.Items.AddRange(new object[] { L10n.T("Ліва кнопка"), L10n.T("Права кнопка"), L10n.T("Середня кнопка") });
                button.SelectedIndex = action.Button == "Right" ? 1 : action.Button == "Middle" ? 2 : 0;
                AddRow(root, 2, L10n.T("Кнопка миші"), button, 45);
                FlowLayoutPanel coordinates = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                x = Number(-100000, 100000, action.X, 0); x.Width = 110; x.AccessibleName = L10n.T("Координата X");
                y = Number(-100000, 100000, action.Y, 0); y.Width = 110; y.AccessibleName = L10n.T("Координата Y");
                coordinates.Controls.Add(x); coordinates.Controls.Add(Theme.Label("  /  ", 10, Theme.Muted)); coordinates.Controls.Add(y);
                AddRow(root, 3, L10n.T("Координати X / Y"), coordinates, 45);
                Button pick = Theme.Button(L10n.T("Вибрати на екрані · 3 с"), false);
                pick.Click += delegate { BeginPick(); };
                root.Controls.Add(pick, 1, 4);
                pickHint = Theme.Label(L10n.T("Наведіть курсор на потрібне місце."), 9, Theme.Muted);
                root.Controls.Add(pickHint, 0, 5); root.SetColumnSpan(pickHint, 2);
            }
            else
            {
                key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                    AccessibleName = L10n.T("Клавіша"), DisplayMember = "Label" };
                foreach (Keys k in KeyNames.All())
                {
                    int index = key.Items.Add(new KeyChoice(k));
                    if ((int)k == action.Key) key.SelectedIndex = index;
                }
                AddRow(root, 2, L10n.T("Клавіша"), key, 45);
                FlowLayoutPanel modifiers = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                ctrl = new CheckBox { Text = "Ctrl", Checked = action.Ctrl, AutoSize = true };
                alt = new CheckBox { Text = "Alt", Checked = action.Alt, AutoSize = true };
                shift = new CheckBox { Text = "Shift", Checked = action.Shift, AutoSize = true };
                modifiers.Controls.AddRange(new Control[] { ctrl, alt, shift });
                AddRow(root, 3, L10n.T("Комбінація з"), modifiers, 45);
                Label note = Theme.Label(L10n.T("Натискання надсилаються в активне вікно.\nF6, F8 та F9 зарезервовані для керування."), 9, Theme.Muted);
                root.Controls.Add(note, 0, 4); root.SetColumnSpan(note, 2);
            }
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            interval = Number(0.01m, 86400, action.IntervalMs / 1000m, 2); interval.AccessibleName = L10n.T("Інтервал у секундах");
            delay = Number(0, 86400, action.DelayMs / 1000m, 2); delay.AccessibleName = L10n.T("Затримка у секундах");
            repeats = Number(0, 1000000, action.RepeatCount, 0); repeats.AccessibleName = L10n.T("Кількість повторень");
            AddRow(root, 6, L10n.T("Інтервал, с"), interval, 45);
            AddRow(root, 7, L10n.T("Затримка першої дії, с"), delay, 45);
            AddRow(root, 8, L10n.T("Повторення (0 = ∞)"), repeats, 45);
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            Button save = Theme.Button(L10n.T("Зберегти"), true);
            Button cancel = Theme.Button(L10n.T("Скасувати"), false); cancel.DialogResult = DialogResult.Cancel;
            save.Click += delegate { SaveAction(); };
            footer.Controls.Add(save); footer.Controls.Add(cancel);
            root.Controls.Add(footer, 0, 9); root.SetColumnSpan(footer, 2);
            AcceptButton = save; CancelButton = cancel;
            pickTimer.Tick += delegate
            {
                remaining--;
                if (remaining > 0) return;
                pickTimer.Stop();
                Point position = Cursor.Position;
                x.Value = position.X; y.Value = position.Y;
                RestoreAfterPick();
                pickHint.Text = L10n.T("Точку збережено: ") + position.X + ", " + position.Y;
            };
            FormClosed += delegate { pickTimer.Stop(); if (picking) RestoreAfterPick(); pickTimer.Dispose(); };
        }
        private static NumericUpDown Number(decimal min, decimal max, decimal value, int digits)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = digits,
                Increment = digits == 0 ? 1 : 0.1m, Width = 160, ThousandsSeparator = true };
        }
        private static void AddRow(TableLayoutPanel panel, int row, string label, Control value, int height)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Label title = Theme.Label(label, 10, Theme.Muted); title.Padding = new Padding(0, 4, 0, 0);
            panel.Controls.Add(title, 0, row); panel.Controls.Add(value, 1, row);
        }
        private void BeginPick()
        {
            picking = true;
            ownerForm = Owner;
            if (ownerForm != null) { ownerState = ownerForm.WindowState; ownerForm.Hide(); }
            Hide();
            remaining = 3;
            pickTimer.Start();
        }
        private void RestoreAfterPick()
        {
            picking = false;
            if (ownerForm != null) { ownerForm.Show(); ownerForm.WindowState = ownerState; }
            Show(); Activate();
        }
        private void SaveAction()
        {
            AutomationAction action = original.Copy();
            action.Name = name.Text.Trim();
            action.IntervalMs = (int)(interval.Value * 1000);
            action.DelayMs = (int)(delay.Value * 1000);
            action.RepeatCount = (int)repeats.Value;
            if (action.Kind == "Mouse")
            {
                action.Button = button.SelectedIndex == 1 ? "Right" : button.SelectedIndex == 2 ? "Middle" : "Left";
                action.X = (int)x.Value; action.Y = (int)y.Value;
            }
            else
            {
                if (key.SelectedItem == null) return;
                action.Key = (int)((KeyChoice)key.SelectedItem).Key;
                action.Ctrl = ctrl.Checked; action.Alt = alt.Checked; action.Shift = shift.Checked;
            }
            try { action.Validate(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L10n.T("Перевірте дію"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Result = action; DialogResult = DialogResult.OK; Close();
        }
        private sealed class KeyChoice
        {
            public readonly Keys Key;
            public string Label { get { return KeyNames.Display(Key); } }
            public KeyChoice(Keys key) { Key = key; }
        }
    }
}
