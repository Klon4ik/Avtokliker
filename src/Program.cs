using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AvtoClicker
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            NativeInput.EnableDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            L10n.SetLanguage("uk");
            if (args.Length > 0 && args[0] == "--self-test")
                return SelfTests.Run(args.Length > 1 ? args[1] : "test-results.txt");
            if (args.Length > 0 && args[0] == "--make-icon")
            {
                using (System.Drawing.Icon icon = Theme.CreateIcon())
                using (FileStream file = File.Create(args[1])) icon.Save(file);
                return 0;
            }
            string profilePath = args.Length == 2 && args[0] == "--profile" ? args[1] : null;
            string settingsPath = profilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            try { if (File.Exists(settingsPath)) L10n.SetLanguage(ProfileStore.Load(settingsPath).Language); }
            catch { /* MainForm handles invalid profiles and backup recovery. */ }
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\AvtoClicker.Desktop.UA", out first))
            {
                if (!first)
                {
                    MessageBox.Show(L10n.T("Автоклікер уже запущено. Відкрийте його на панелі завдань."), L10n.T("Автоклікер"));
                    return 0;
                }
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    // Exit disposes the form and stops its worker before showing any further UI.
                    Application.Exit();
                    MessageBox.Show(e.Exception.Message, L10n.T("Автоклікер: помилка"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                Application.Run(new MainForm(profilePath));
            }
            return 0;
        }
    }
}
