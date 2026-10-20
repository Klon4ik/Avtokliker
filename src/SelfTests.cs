using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AvtoClicker
{
    internal static class SelfTests
    {
        private static int passed;
        public static int Run(string resultPath)
        {
            List<string> log = new List<string>();
            Action<string, Action> test = delegate(string name, Action body)
            {
                try { body(); passed++; log.Add("PASS " + name); }
                catch (Exception ex) { log.Add("FAIL " + name + ": " + ex); }
            };
            test("Independent intervals, initial delays and per-action limits", delegate
            {
                AutomationAction a = new AutomationAction { Name = "A", IntervalMs = 100, RepeatCount = 3 };
                AutomationAction b = new AutomationAction { Name = "B", IntervalMs = 250, DelayMs = 50, RepeatCount = 2 };
                AutomationAction c = new AutomationAction { Name = "C", Kind = "Key", IntervalMs = 70, DelayMs = 20, RepeatCount = 4 };
                Schedule schedule = new Schedule(new[] { a, b, c });
                RecordingSink sink = new RecordingSink();
                for (long now = 0; now <= 500; now += 10)
                { sink.Now = now; long time = now; schedule.Dispatch(time, () => time, () => false, sink); }
                Equal("A@0,C@20,B@50,C@90,A@100,C@160,A@200,C@230,B@300", String.Join(",", sink.Events));
                Assert(schedule.IsComplete, "Finite schedule did not complete");
            });
            test("Disabled actions and immutable run snapshot", delegate
            {
                AutomationAction a = new AutomationAction { Name = "Original" };
                Schedule schedule = new Schedule(new[] { a, new AutomationAction { Enabled = false } });
                a.Name = "Changed"; a.IntervalMs = 20;
                Equal(1, schedule.Actions.Length); Equal("Original", schedule.Actions[0].Name); Equal(1000, schedule.Actions[0].IntervalMs);
            });
            test("Missed intervals are skipped without catch-up bursts", delegate
            {
                Schedule schedule = new Schedule(new[] { new AutomationAction { IntervalMs = 100 } });
                RecordingSink sink = new RecordingSink();
                schedule.Dispatch(0, () => 0, () => false, sink);
                schedule.Dispatch(10000, () => 10000, () => false, sink);
                schedule.Dispatch(10000, () => 10000, () => false, sink);
                Equal(2L, schedule.Counts[0]);
            });
            test("Cancellation is checked between due actions", delegate
            {
                Schedule schedule = new Schedule(new[] { new AutomationAction(), new AutomationAction() });
                RecordingSink sink = new RecordingSink();
                schedule.Dispatch(0, () => 0, () => sink.Events.Count > 0, sink);
                Equal(1, sink.Events.Count); Equal(0L, schedule.Counts[1]);
            });
            test("Skipped physical input is not counted", delegate
            {
                Schedule schedule = new Schedule(new[] { new AutomationAction { RepeatCount = 1 } });
                RecordingSink sink = new RecordingSink { Accept = false };
                schedule.Dispatch(0, () => 0, () => false, sink);
                Equal(0L, schedule.Counts[0]); Assert(!schedule.IsComplete, "Skipped action must remain pending");
            });
            test("Validation rejects invalid intervals and reserved keys", delegate
            {
                Reject(() => new AutomationAction { IntervalMs = 0 }.Validate());
                Reject(() => new AutomationAction { RepeatCount = -1 }.Validate());
                Reject(() => new AutomationAction { Kind = "Key", Key = (int)Keys.F9 }.Validate());
                Reject(() => new AutomationAction { Kind = "Key", Key = (int)Keys.ControlKey }.Validate());
                new AutomationAction { Kind = "Key", Key = (int)Keys.F10, Ctrl = true }.Validate();
                new AutomationAction { IntervalMs = 10, RepeatCount = 1000000 }.Validate();
            });
            test("JSON roundtrip, Unicode, modifiers and atomic backup", delegate
            {
                string file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultPath)), "profile-roundtrip.json");
                Profile profile = new Profile { ShowMarkers = true, Language = "en" };
                profile.Actions.Add(new AutomationAction { Name = "Точка Київ", X = -1920, Y = 123 });
                profile.Actions.Add(new AutomationAction { Name = "Клавіша", Kind = "Key", Ctrl = true, Key = (int)Keys.A });
                try
                {
                    ProfileStore.Save(file, profile);
                    Profile restored = ProfileStore.Load(file);
                    Assert(restored.ShowMarkers, "Marker visibility was not saved");
                    Equal("en", restored.Language);
                    Equal("Точка Київ", restored.Actions[0].Name); Equal(-1920, restored.Actions[0].X);
                    Assert(restored.Actions[1].Ctrl, "Modifier lost");
                    profile.Actions[0].Name = "Оновлена точка"; ProfileStore.Save(file, profile);
                    Equal("Точка Київ", ProfileStore.Load(file + ".bak").Actions[0].Name);
                    Equal("Оновлена точка", ProfileStore.Load(file).Actions[0].Name);
                    File.WriteAllText(file, "{invalid"); Reject(() => ProfileStore.Load(file));
                }
                finally { foreach (string item in new[] { file, file + ".bak", file + ".tmp" }) if (File.Exists(item)) File.Delete(item); }
            });
            test("Native INPUT layout and virtual desktop normalization", delegate
            {
                Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf(typeof(NativeInput.Input)));
                Equal(0, NativeInput.Normalize(-1920, -1920, 3840));
                Equal(65535, NativeInput.Normalize(1919, -1920, 3840));
                NativeInput.Input[] left = NativeInput.MouseEvents(new AutomationAction { X = -1920, Y = 0 }, new Rectangle(-1920, 0, 3840, 1080));
                Equal(0xC001u, left[0].data.mouse.flags); Equal(2u, left[1].data.mouse.flags); Equal(4u, left[2].data.mouse.flags);
                NativeInput.Input[] right = NativeInput.MouseEvents(new AutomationAction { Button = "Right" }, new Rectangle(0, 0, 1920, 1080));
                Equal(8u, right[1].data.mouse.flags); Equal(16u, right[2].data.mouse.flags);
                Equal(3u, NativeInput.KeyEvent(Keys.Left, true).data.keyboard.flags);
            });
            test("Worker stops cleanly and finite runs complete", delegate
            {
                RecordingSink sink = new RecordingSink();
                using (AutomationEngine engine = new AutomationEngine(sink))
                {
                    engine.Start(new[] { new AutomationAction { IntervalMs = 20, RepeatCount = 3 } });
                    for (int i = 0; i < 100 && engine.Running; i++) Thread.Sleep(10);
                    Assert(!engine.Running, "Finite worker did not finish"); Equal(3L, engine.Counts[0]);
                    engine.Start(new[] { new AutomationAction { IntervalMs = 10 } });
                    Thread.Sleep(35); engine.Stop();
                    long stopped = engine.Counts[0]; Thread.Sleep(35); Equal(stopped, engine.Counts[0]);
                    Assert(!engine.Running, "Worker is still running");
                }
            });
            test("Input failures stop the worker and surface the error", delegate
            {
                using (AutomationEngine engine = new AutomationEngine(new FailingSink()))
                {
                    engine.Start(new[] { new AutomationAction() });
                    for (int i = 0; i < 100 && engine.Running; i++) Thread.Sleep(10);
                    Assert(!engine.Running, "Failed worker did not stop"); Equal("Expected failure", engine.Error); Equal(0L, engine.Counts[0]);
                }
            });
            test("Marker drag preserves the cursor offset and exact click center", delegate
            {
                Point moved = MarkerGeometry.Drag(new Point(-1200, 400), new Point(-1180, 410), new Point(-950, 180));
                Equal(new Point(-970, 170), moved);
                Equal(moved, MarkerGeometry.ClickPoint(MarkerGeometry.TopLeft(moved)));
                Equal(new Point(-1920, 1079), MarkerGeometry.Clamp(new Point(-2000, 1200), new Rectangle(-1920, 0, 1920, 1080)));
                Equal(new Point(-1, 0), MarkerGeometry.Clamp(new Point(50, -100), new Rectangle(-1920, 0, 1920, 1080)));
            });
            test("Existing profiles without marker preference remain compatible", delegate
            {
                string file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultPath)), "profile-legacy.json");
                try
                {
                    File.WriteAllText(file, "{\"Version\":1,\"CountdownSeconds\":3,\"MinimizeOnStart\":true,\"Actions\":[]}");
                    Profile loaded = ProfileStore.Load(file);
                    Assert(!loaded.ShowMarkers, "Old profiles should default to hidden markers");
                    Equal(0, loaded.Actions.Count);
                }
                finally { if (File.Exists(file)) File.Delete(file); }
            });
            test("Language switching translates controls, actions and validation without changing names", delegate
            {
                try
                {
                    AutomationAction action = new AutomationAction { Name = "Моя точка" };
                    L10n.SetLanguage("en");
                    Equal("Show click points on screen", L10n.T("Показати точки на екрані"));
                    Equal("Left click", action.ActionText);
                    Equal("Space", KeyNames.Display(Keys.Space));
                    Equal("Моя точка", action.Copy().Name);
                    Equal("1.25", 1.25m.ToString());
                    try { new AutomationAction { IntervalMs = 0 }.Validate(); throw new Exception("Invalid interval accepted"); }
                    catch (InvalidDataException ex) { Assert(ex.Message.StartsWith("Interval:"), "Validation message was not translated"); }
                    Reject(() => new Profile { Language = "de" }.Validate());
                    L10n.SetLanguage("uk");
                    Equal("Лівий клік", action.ActionText);
                    Equal("1,25", 1.25m.ToString());
                    Equal("uk", L10n.Normalize(null));
                }
                finally { L10n.SetLanguage("uk"); }
            });
            test("Live UI language switching rebuilds controls and persists the existing profile", delegate
            {
                string file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultPath)), "profile-ui-language.json");
                try
                {
                    Profile profile = new Profile();
                    profile.Actions.Add(new AutomationAction { Name = "Моя точка", X = 120, Y = 240, IntervalMs = 750 });
                    ProfileStore.Save(file, profile);
                    using (MainForm form = new MainForm(file))
                    {
                        IntPtr handle = form.Handle; // Create an invisible native window; never activate it.
                        ComboBox language = FindControl<ComboBox>(form, c => c.AccessibleName == "Мова / Language");
                        Assert(language != null, "Language selector is missing");
                        language.SelectedIndex = 1;
                        Equal("Auto Clicker", form.Text);
                        Assert(FindControl<Button>(form, c => c.Text == "+ Click point") != null, "English toolbar was not rebuilt");
                        DataGridView grid = FindControl<DataGridView>(form, c => true);
                        Equal("Left click", Convert.ToString(grid.Rows[0].Cells[2].Value));
                        Profile saved = ProfileStore.Load(file);
                        Equal("en", saved.Language); Equal("Моя точка", saved.Actions[0].Name);
                        Equal(120, saved.Actions[0].X); Equal(240, saved.Actions[0].Y); Equal(750, saved.Actions[0].IntervalMs);
                        language = FindControl<ComboBox>(form, c => c.AccessibleName == "Мова / Language");
                        language.SelectedIndex = 0;
                        Equal("Автоклікер", form.Text);
                        Equal("uk", ProfileStore.Load(file).Language);
                        form.Close();
                    }
                }
                finally
                {
                    foreach (string item in new[] { file, file + ".bak", file + ".tmp" }) if (File.Exists(item)) File.Delete(item);
                    L10n.SetLanguage("uk");
                }
            });
            log.Add(passed + "/14 tests passed. No native clicks or keystrokes were sent.");
            File.WriteAllLines(resultPath, log.ToArray(), System.Text.Encoding.UTF8);
            return passed == 14 ? 0 : 1;
        }
        private static T FindControl<T>(Control parent, Func<Control, bool> matches) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                T found = control as T;
                if (found != null && matches(control)) return found;
                found = FindControl<T>(control, matches);
                if (found != null) return found;
            }
            return null;
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Equal<T>(T expected, T actual)
        { if (!Object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
        private static void Reject(Action action)
        {
            bool failed = false;
            try { action(); } catch { failed = true; }
            Assert(failed, "Invalid data was accepted");
        }
        private sealed class RecordingSink : IActionSink
        {
            public readonly List<string> Events = new List<string>();
            public long Now;
            public bool Accept = true;
            public bool Execute(AutomationAction action) { Events.Add(action.Name + "@" + Now); return Accept; }
        }
        private sealed class FailingSink : IActionSink
        { public bool Execute(AutomationAction action) { throw new Exception("Expected failure"); } }
    }
}
