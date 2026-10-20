using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AvtoClicker
{
    public sealed class MainForm : Form
    {
        private Profile profile = new Profile();
        private readonly string settingsPath;
        private readonly AutomationEngine engine = new AutomationEngine(new NativeInput());
        private readonly PointMarkerManager markers = new PointMarkerManager();
        private readonly Timer timer = new Timer { Interval = 100 };
        private DataGridView grid;
        private Label empty, status, detail, count, shortcutHint;
        private FlowLayoutPanel toolbar, options;
        private NumericUpDown countdown;
        private CheckBox minimize, showMarkers;
        private Button start, stop;
        private ComboBox language;
        private bool refreshing, session, pending, saved = true, hotkeysReady;
        private DateTime startAt;
        private List<int> activeRows = new List<int>();
        private string startupWarning;
        private int markerSuspensions;
        private bool markersReady, closing;

        public MainForm(string profilePath = null)
        {
            settingsPath = profilePath == null ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json") : Path.GetFullPath(profilePath);
            Text = L10n.T("Автоклікер");
            Font = new Font("Segoe UI", 10);
            ForeColor = Theme.Text;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1160, 750);
            MinimumSize = new Size(1040, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Theme.CreateIcon();
            LoadSettings();
            L10n.SetLanguage(profile.Language);
            Text = L10n.T("Автоклікер");
            BuildInterface();
            markers.PositionChanged += MarkerPositionChanged;
            markers.Selected += SelectMarkerAction;
            markers.EditRequested += delegate(AutomationAction action)
            { if (!session) { SelectMarkerAction(action); RestoreWindow(); EditSelected(); } };
            RefreshRows(-1);
            timer.Tick += delegate { UpdateSession(); markers.Tick(); };
            timer.Start();
            Shown += delegate
            {
                markersReady = true; UpdateMarkers();
                if (startupWarning != null) ShowError(startupWarning);
            };
        }
        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                Margin = new Padding(0), Padding = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Navy, Margin = new Padding(0) };
            Label title = Theme.Label(L10n.T("АВТОКЛІКЕР"), 23, Color.White);
            title.Font = new Font("Segoe UI", 23, FontStyle.Bold); title.Location = new Point(26, 16);
            Label subtitle = Theme.Label(L10n.T("Ваші точки. Ваші інтервали. Один запуск."), 10, Color.FromArgb(183, 194, 218));
            subtitle.Location = new Point(29, 65);
            Label badge = Theme.Label(L10n.T("МИША  +  КЛАВІАТУРА"), 9, Color.FromArgb(201, 192, 255));
            FlowLayoutPanel headerOptions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 300,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 16, 26, 0) };
            badge.Margin = new Padding(0, 0, 0, 10);
            language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240,
                AccessibleName = "Мова / Language", Font = new Font("Segoe UI", 10) };
            language.Items.AddRange(new object[] { "Українська / Ukrainian", "English" });
            language.SelectedIndex = L10n.Language == "en" ? 1 : 0;
            language.SelectedIndexChanged += delegate
            {
                string selectedLanguage = language.SelectedIndex == 1 ? "en" : "uk";
                if (selectedLanguage == L10n.Language || session) return;
                profile.Language = selectedLanguage;
                RebuildInterface(); SaveSettings();
            };
            headerOptions.Controls.Add(badge); headerOptions.Controls.Add(language);
            header.Controls.AddRange(new Control[] { title, subtitle, headerOptions }); root.Controls.Add(header, 0, 0);

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 16, 0),
                Margin = new Padding(0), WrapContents = false };
            AddButton(toolbar, L10n.T("+ Точка кліку"), true, delegate { AddAction("Mouse"); });
            AddButton(toolbar, L10n.T("+ Клавіша"), false, delegate { AddAction("Key"); });
            AddButton(toolbar, L10n.T("Змінити"), false, EditSelected);
            AddButton(toolbar, L10n.T("Копія"), false, DuplicateSelected);
            AddButton(toolbar, L10n.T("Видалити"), false, RemoveSelected);
            AddButton(toolbar, L10n.T("Відкрити профіль"), false, ImportProfile);
            AddButton(toolbar, L10n.T("Зберегти профіль"), false, ExportProfile);
            root.Controls.Add(toolbar, 0, 1);

            Panel tablePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(24, 0, 24, 0), BackColor = Color.White };
            grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, AllowUserToOrderColumns = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 43, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(232, 236, 243),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AccessibleName = L10n.T("Список дій") };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(235, 239, 247),
                ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(6, 0, 0, 0) };
            grid.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 10), ForeColor = Theme.Text,
                BackColor = Color.White, SelectionBackColor = Color.FromArgb(235, 230, 255), SelectionForeColor = Theme.Text,
                Padding = new Padding(6, 0, 0, 0) };
            grid.RowTemplate.Height = 48;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = L10n.T("Увімк."), Name = "Enabled", FillWeight = 55, MinimumWidth = 58 });
            AddColumn(L10n.T("Назва"), "Name", 170, 110);
            AddColumn(L10n.T("Дія"), "Kind", 115, 100);
            AddColumn(L10n.T("Точка / клавіша"), "Target", 145, 125);
            AddColumn(L10n.T("Інтервал, с"), "Interval", 90, 90);
            AddColumn(L10n.T("Затримка, с"), "Delay", 90, 92);
            AddColumn(L10n.T("Повторення"), "Repeats", 95, 94);
            AddColumn(L10n.T("Виконано"), "Count", 82, 80);
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (refreshing || session || e.RowIndex < 0 || e.ColumnIndex != 0) return;
                profile.Actions[e.RowIndex].Enabled = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells[0].Value);
                SaveSettings(); UpdateSummary(); UpdateMarkers();
            };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            { if (!session && e.RowIndex >= 0 && e.ColumnIndex != 0) EditSelected(); };
            grid.SelectionChanged += delegate { if (!refreshing) UpdateMarkers(); };
            tablePanel.Controls.Add(grid);
            empty = Theme.Label(L10n.T("Додайте першу дію\n\nТочку кліку або клавішу можна налаштувати\nз окремим інтервалом і кількістю повторень."), 12, Theme.Muted);
            empty.AutoSize = false; empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Dock = DockStyle.Fill; empty.BackColor = Color.White; tablePanel.Controls.Add(empty);
            root.Controls.Add(tablePanel, 0, 2);

            options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 14, 0, 0), Margin = new Padding(0), WrapContents = true };
            Label countdownTitle = Theme.Label(L10n.T("Відлік перед запуском:"), 10, Theme.Muted); countdownTitle.Margin = new Padding(0, 3, 8, 0);
            countdown = new NumericUpDown { Minimum = 0, Maximum = 60, Value = profile.CountdownSeconds,
                Width = 63, AccessibleName = L10n.T("Відлік перед запуском у секундах") };
            Label seconds = Theme.Label(L10n.T("с"), 10, Theme.Muted); seconds.Margin = new Padding(4, 3, 28, 0);
            minimize = new CheckBox { Text = L10n.T("Згортати вікно під час запуску"), AutoSize = true, Checked = profile.MinimizeOnStart,
                Margin = new Padding(0, 2, 20, 0) };
            Label autosave = Theme.Label(L10n.T("Налаштування зберігаються автоматично"), 9, Theme.Muted); autosave.Margin = new Padding(0, 4, 0, 0);
            options.Controls.AddRange(new Control[] { countdownTitle, countdown, seconds, minimize, autosave });
            options.SetFlowBreak(autosave, true);
            showMarkers = new CheckBox { Text = L10n.T("Показати точки на екрані"), AutoSize = true, Checked = profile.ShowMarkers,
                Margin = new Padding(0, 13, 18, 0) };
            Button arrange = Theme.Button(L10n.T("Розставити точки"), false); arrange.Margin = new Padding(0, 6, 14, 0);
            Label markerHint = Theme.Label(L10n.T("Тягніть кружки · центр — місце кліку · подвійний клік — змінити"), 9, Theme.Muted);
            markerHint.Margin = new Padding(0, 16, 0, 0);
            options.Controls.AddRange(new Control[] { showMarkers, arrange, markerHint });
            showMarkers.CheckedChanged += delegate { profile.ShowMarkers = showMarkers.Checked; UpdateMarkers(); SaveSettings(); };
            arrange.Click += delegate
            {
                bool any = false; foreach (AutomationAction action in profile.Actions) if (action.Kind == "Mouse") any = true;
                if (!any) { ShowError(L10n.T("Спочатку додайте хоча б одну точку кліку. Для кількох кружків додайте точки або натисніть «Копія».")); return; }
                showMarkers.Checked = true; WindowState = FormWindowState.Minimized;
            };
            countdown.ValueChanged += delegate { profile.CountdownSeconds = (int)countdown.Value; SaveSettings(); };
            minimize.CheckedChanged += delegate { profile.MinimizeOnStart = minimize.Checked; SaveSettings(); };
            root.Controls.Add(options, 0, 3);

            TableLayoutPanel run = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Margin = new Padding(24, 5, 24, 10), BackColor = Color.White, Padding = new Padding(18, 14, 18, 10) };
            run.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 345));
            run.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            run.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            FlowLayoutPanel runButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            start = Theme.Button(L10n.T("▶ Запустити · F8"), true); start.Height = 44; start.MinimumSize = new Size(178, 44);
            stop = Theme.Button(L10n.T("■ Стоп · F9"), false); stop.Height = 44; stop.MinimumSize = new Size(130, 44);
            stop.ForeColor = Color.FromArgb(178, 51, 72); stop.Enabled = false;
            start.Click += delegate { Toggle(); }; stop.Click += delegate { StopSession(L10n.T("Зупинено")); };
            runButtons.Controls.Add(start); runButtons.Controls.Add(stop); run.Controls.Add(runButtons, 0, 0);
            Panel statePanel = new Panel { Dock = DockStyle.Fill };
            status = Theme.Label(L10n.T("Готово до запуску"), 12, Theme.Text); status.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            detail = Theme.Label(L10n.T("Додайте дії у список"), 9, Theme.Muted); detail.Top = 29;
            statePanel.Controls.AddRange(new Control[] { status, detail }); run.Controls.Add(statePanel, 1, 0);
            count = Theme.Label(L10n.T("0 дій"), 14, Theme.Accent); count.Dock = DockStyle.Fill; count.AutoSize = false; count.TextAlign = ContentAlignment.MiddleRight;
            run.Controls.Add(count, 2, 0); root.Controls.Add(run, 0, 4);

            shortcutHint = Theme.Label(L10n.T("F6 — координати курсора для вибраної точки     •     F8 — запуск / зупинка     •     F9 — негайна зупинка"), 9, Theme.Muted);
            shortcutHint.Dock = DockStyle.Fill; shortcutHint.AutoSize = false; shortcutHint.Padding = new Padding(26, 8, 10, 0);
            root.Controls.Add(shortcutHint, 0, 5);
        }
        private void AddColumn(string title, string name, float weight, int min)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, Name = name, ReadOnly = true,
                FillWeight = weight, MinimumWidth = min, SortMode = DataGridViewColumnSortMode.NotSortable });
        }
        private void RebuildInterface()
        {
            int selected = SelectedIndex;
            List<string> counts = new List<string>();
            foreach (DataGridViewRow row in grid.Rows) counts.Add(Convert.ToString(row.Cells[7].Value));
            long total = 0;
            foreach (string value in counts) { long completed; if (Int64.TryParse(value, out completed)) total += completed; }
            SuspendLayout();
            refreshing = true;
            try
            {
                Control previous = Controls[0];
                Controls.Remove(previous);
                grid = null; showMarkers = null;
                previous.Dispose();
                L10n.SetLanguage(profile.Language);
                Text = L10n.T("Автоклікер");
                BuildInterface(); RefreshRows(selected);
                for (int i = 0; i < counts.Count && i < grid.Rows.Count; i++) grid.Rows[i].Cells[7].Value = counts[i];
                count.Text = total.ToString("N0") + L10n.T(" дій");
            }
            finally { refreshing = false; ResumeLayout(true); }
        }
        private static void AddButton(FlowLayoutPanel parent, string text, bool primary, Action action)
        {
            Button button = Theme.Button(text, primary); button.Click += delegate { action(); }; parent.Controls.Add(button);
        }
        private int SelectedIndex { get { return grid.CurrentRow == null ? -1 : grid.CurrentRow.Index; } }
        private void AddAction(string kind)
        {
            if (profile.Actions.Count >= 500) { ShowError(L10n.T("Можна додати не більше 500 дій.")); return; }
            AutomationAction action = new AutomationAction { Kind = kind,
                Name = (kind == "Mouse" ? L10n.T("Точка ") : L10n.T("Клавіша ")) + (profile.Actions.Count + 1),
                X = Cursor.Position.X, Y = Cursor.Position.Y };
            using (ActionDialog dialog = new ActionDialog(action))
                if (ShowActionDialog(dialog) == DialogResult.OK)
                { profile.Actions.Add(dialog.Result); RefreshRows(profile.Actions.Count - 1); SaveSettings(); }
        }
        private void EditSelected()
        {
            int index = SelectedIndex; if (index < 0) return;
            using (ActionDialog dialog = new ActionDialog(profile.Actions[index]))
                if (ShowActionDialog(dialog) == DialogResult.OK)
                { profile.Actions[index] = dialog.Result; RefreshRows(index); SaveSettings(); }
        }
        private void DuplicateSelected()
        {
            int index = SelectedIndex; if (index < 0 || profile.Actions.Count >= 500) return;
            AutomationAction copy = profile.Actions[index].Copy();
            copy.Name = copy.Name.Substring(0, Math.Min(72, copy.Name.Length)) + L10n.T(" (копія)");
            profile.Actions.Insert(index + 1, copy); RefreshRows(index + 1); SaveSettings();
        }
        private void RemoveSelected()
        {
            int index = SelectedIndex; if (index < 0) return;
            profile.Actions.RemoveAt(index); RefreshRows(Math.Min(index, profile.Actions.Count - 1)); SaveSettings();
        }
        private void RefreshRows(int selected)
        {
            refreshing = true;
            grid.Rows.Clear();
            int number = 0;
            foreach (AutomationAction action in profile.Actions)
                grid.Rows.Add(action.Enabled, action.Kind == "Mouse" ? "№" + (++number) + " · " + action.Name : action.Name, action.ActionText, action.TargetText,
                    Seconds(action.IntervalMs), Seconds(action.DelayMs), action.RepeatCount == 0 ? "∞" : action.RepeatCount.ToString(), "0");
            empty.Visible = profile.Actions.Count == 0;
            if (empty.Visible) empty.BringToFront();
            if (selected >= 0 && selected < grid.Rows.Count) grid.CurrentCell = grid.Rows[selected].Cells[1];
            refreshing = false; UpdateSummary(); UpdateMarkers();
        }
        private void UpdateMarkers()
        {
            if (closing || showMarkers == null || grid == null) return;
            AutomationAction selected = SelectedIndex >= 0 && SelectedIndex < profile.Actions.Count ? profile.Actions[SelectedIndex] : null;
            markers.Sync(profile.Actions, selected, markersReady && profile.ShowMarkers && markerSuspensions == 0, session);
        }
        private void SelectMarkerAction(AutomationAction action)
        {
            int index = profile.Actions.IndexOf(action);
            if (index >= 0 && !session) grid.CurrentCell = grid.Rows[index].Cells[1];
        }
        private void MarkerPositionChanged(AutomationAction action, Point point, bool committed)
        {
            if (session || closing) return;
            int index = profile.Actions.IndexOf(action); if (index < 0) return;
            action.X = point.X; action.Y = point.Y;
            grid.Rows[index].Cells[3].Value = action.TargetText;
            status.Text = L10n.T("Точка: ") + point.X + ", " + point.Y;
            if (committed) SaveSettings();
        }
        private DialogResult ShowActionDialog(ActionDialog dialog)
        {
            markerSuspensions++; UpdateMarkers();
            try { return dialog.ShowDialog(this); }
            finally { markerSuspensions--; UpdateMarkers(); }
        }
        private static string Seconds(int ms) { return (ms / 1000m).ToString("0.##"); }
        private void UpdateSummary()
        {
            int enabled = 0; foreach (AutomationAction action in profile.Actions) if (action.Enabled) enabled++;
            if (!session) detail.Text = enabled + L10n.T(" увімкн. / ") + profile.Actions.Count + L10n.T(" у списку") + (saved ? "" : L10n.T(" · не збережено"));
        }
        private void Toggle()
        {
            if (OwnedForms.Length > 0) return;
            if (session) { StopSession(L10n.T("Зупинено")); return; }
            if (!hotkeysReady) { ShowError(L10n.T("Не вдалося зареєструвати гарячі клавіші. Закрийте іншу програму, що використовує F6, F8 або F9, і перезапустіть автоклікер.")); return; }
            grid.EndEdit();
            activeRows.Clear();
            for (int i = 0; i < profile.Actions.Count; i++)
            {
                AutomationAction action = profile.Actions[i];
                if (!action.Enabled) continue;
                if (action.Kind == "Mouse")
                {
                    bool visible = false;
                    foreach (Screen screen in Screen.AllScreens) if (screen.Bounds.Contains(action.X, action.Y)) visible = true;
                    if (!visible) { ShowError(L10n.T("Точка «") + action.Name + L10n.T("» поза екраном. Оновіть її координати клавішею F6.")); return; }
                    // The editor must not become the destination of its own automation.
                    if (!minimize.Checked && Bounds.Contains(action.X, action.Y) && WindowState != FormWindowState.Minimized)
                    { ShowError(L10n.T("Точка «") + action.Name + L10n.T("» розташована над вікном автоклікера. Увімкніть згортання або перемістіть вікно.")); return; }
                }
                activeRows.Add(i);
            }
            if (activeRows.Count == 0) { ShowError(L10n.T("Додайте й увімкніть хоча б одну дію.")); return; }
            SaveSettings();
            foreach (DataGridViewRow row in grid.Rows) row.Cells[7].Value = "0";
            count.Text = L10n.T("0 дій");
            session = true; pending = true;
            UpdateMarkers();
            startAt = DateTime.UtcNow.AddSeconds((int)countdown.Value);
            toolbar.Enabled = options.Enabled = false; grid.ReadOnly = true;
            language.Enabled = false;
            start.Text = L10n.T("Зупинити · F8"); stop.Enabled = true;
            status.ForeColor = Theme.Accent;
            if (minimize.Checked) WindowState = FormWindowState.Minimized;
            UpdateSession();
        }
        private void UpdateSession()
        {
            if (!session) return;
            if (pending)
            {
                double seconds = (startAt - DateTime.UtcNow).TotalSeconds;
                if (seconds > 0)
                { status.Text = L10n.T("Запуск через ") + Math.Ceiling(seconds) + L10n.T(" с"); detail.Text = L10n.T("Перейдіть у потрібне вікно · F9 — скасувати"); return; }
                pending = false;
                try { engine.Start(profile.Actions); }
                catch (Exception ex) { StopSession(L10n.T("Не вдалося запустити")); ShowError(ex.Message); return; }
                status.Text = L10n.T("Виконується");
                detail.Text = activeRows.Count + L10n.T(" дій із незалежними інтервалами · F9 — стоп");
            }
            UpdateCounts();
            if (!engine.Running)
            {
                string error = engine.Error;
                StopSession(error == null ? L10n.T("Усі повторення виконано") : L10n.T("Виконання зупинено"));
                if (error != null) { RestoreWindow(); ShowError(error); }
            }
        }
        private void UpdateCounts()
        {
            long[] counts = engine.Counts; long total = 0;
            for (int i = 0; i < counts.Length && i < activeRows.Count; i++)
            {
                total += counts[i]; grid.Rows[activeRows[i]].Cells[7].Value = counts[i].ToString();
                markers.UpdateCount(profile.Actions[activeRows[i]], counts[i]);
            }
            count.Text = total.ToString("N0") + L10n.T(" дій");
        }
        private void StopSession(string message)
        {
            if (!session) return;
            bool wasPending = pending;
            pending = false; engine.Stop();
            if (!wasPending) UpdateCounts();
            session = false;
            toolbar.Enabled = options.Enabled = true; grid.ReadOnly = false;
            language.Enabled = true;
            for (int i = 1; i < grid.Columns.Count; i++) grid.Columns[i].ReadOnly = true;
            start.Text = L10n.T("▶ Запустити · F8"); stop.Enabled = false;
            status.Text = message; status.ForeColor = Theme.Text;
            UpdateSummary(); UpdateMarkers();
        }
        private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            bool a = NativeInput.RegisterHotKey(Handle, 6, 0x4000, (uint)Keys.F6);
            bool b = NativeInput.RegisterHotKey(Handle, 8, 0x4000, (uint)Keys.F8);
            bool c = NativeInput.RegisterHotKey(Handle, 9, 0x4000, (uint)Keys.F9);
            hotkeysReady = a && b && c;
            if (!hotkeysReady) startupWarning = L10n.T("Одна з клавіш F6, F8, F9 уже використовується іншою програмою. Закрийте її та перезапустіть автоклікер.");
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            foreach (int id in new int[] { 6, 8, 9 }) NativeInput.UnregisterHotKey(Handle, id);
            base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                int id = m.WParam.ToInt32();
                if (id == 9) StopSession(L10n.T("Зупинено клавішею F9"));
                if (id == 8) Toggle();
                if (id == 6 && !session && OwnedForms.Length == 0 && SelectedIndex >= 0)
                {
                    int index = SelectedIndex;
                    if (profile.Actions[index].Kind == "Mouse")
                    {
                        Point point = Cursor.Position;
                        profile.Actions[index].X = point.X; profile.Actions[index].Y = point.Y;
                        RefreshRows(index); SaveSettings();
                        status.Text = L10n.T("Точка: ") + point.X + ", " + point.Y;
                    }
                }
                return;
            }
            base.WndProc(ref m);
        }
        private void LoadSettings()
        {
            if (!File.Exists(settingsPath)) return;
            try { profile = ProfileStore.Load(settingsPath); }
            catch (Exception ex)
            {
                try
                {
                    profile = ProfileStore.Load(settingsPath + ".bak");
                    startupWarning = L10n.T("Відновлено останню резервну копію налаштувань. ") + ex.Message;
                }
                catch
                {
                    // Preserve the invalid source before any autosave can replace it.
                    try { File.Copy(settingsPath, settingsPath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false); }
                    catch { saved = false; }
                    startupWarning = L10n.T("Не вдалося прочитати налаштування. Відкрито порожній профіль. ") + ex.Message;
                }
            }
        }
        private void SaveSettings()
        {
            try { ProfileStore.Save(settingsPath, profile); saved = true; }
            catch (Exception ex)
            {
                saved = false;
                if (status != null) { status.Text = L10n.T("Не вдалося зберегти налаштування"); detail.Text = ex.Message; }
            }
        }
        private void ImportProfile()
        {
            markerSuspensions++; UpdateMarkers();
            try
            {
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = L10n.T("Профіль автоклікера (*.json)|*.json"), Title = L10n.T("Відкрити профіль") })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Profile loaded = ProfileStore.Load(dialog.FileName);
                    profile = loaded;
                    if (L10n.Normalize(profile.Language) != L10n.Language) RebuildInterface();
                    else
                    {
                        countdown.Value = profile.CountdownSeconds; minimize.Checked = profile.MinimizeOnStart;
                        showMarkers.Checked = profile.ShowMarkers;
                    }
                    RefreshRows(0); SaveSettings(); status.Text = L10n.T("Профіль відкрито");
                }
                catch (Exception ex) { ShowError(L10n.T("Не вдалося відкрити профіль. ") + ex.Message); }
            }
            }
            finally { markerSuspensions--; UpdateMarkers(); }
        }
        private void ExportProfile()
        {
            markerSuspensions++; UpdateMarkers();
            try
            {
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = L10n.T("Профіль автоклікера (*.json)|*.json"),
                FileName = L10n.T("Мій профіль.json"), Title = L10n.T("Зберегти профіль"), AddExtension = true, DefaultExt = "json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { ProfileStore.Save(dialog.FileName, profile); status.Text = L10n.T("Профіль збережено"); }
                catch (Exception ex) { ShowError(L10n.T("Не вдалося зберегти профіль. ") + ex.Message); }
            }
            }
            finally { markerSuspensions--; UpdateMarkers(); }
        }
        private void ShowError(string message)
        {
            markerSuspensions++; UpdateMarkers();
            try { MessageBox.Show(this, message, L10n.T("Автоклікер"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { markerSuspensions--; UpdateMarkers(); }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            closing = true;
            StopSession(L10n.T("Зупинено")); markers.Dispose(); engine.Dispose(); timer.Stop(); timer.Dispose(); SaveSettings();
            base.OnFormClosing(e);
        }
    }
}
