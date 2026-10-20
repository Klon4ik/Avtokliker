using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AvtoClicker
{
    public sealed class NativeInput : IActionSink
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            public int dx, dy;
            public uint mouseData, flags, time;
            public UIntPtr extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput
        {
            public ushort vk, scan;
            public uint flags, time;
            public UIntPtr extra;
        }
        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mouse;
            [FieldOffset(0)] public KeyboardInput keyboard;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Input { public uint type; public InputUnion data; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        public static void EnableDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }
        internal static int Normalize(int value, int origin, int extent)
        {
            return (int)Math.Max(0, Math.Min(65535, Math.Round(((double)value - origin) * 65535 / Math.Max(1, extent - 1))));
        }
        private static Input Mouse(int x, int y, uint flags)
        {
            return new Input { type = 0, data = new InputUnion { mouse = new MouseInput { dx = x, dy = y, flags = flags } } };
        }
        internal static Input KeyEvent(Keys key, bool up)
        {
            bool extended = key == Keys.Delete || key == Keys.Insert || key == Keys.Home || key == Keys.End ||
                key == Keys.PageUp || key == Keys.PageDown || key == Keys.Up || key == Keys.Down ||
                key == Keys.Left || key == Keys.Right || key == Keys.Divide;
            return new Input { type = 1, data = new InputUnion { keyboard = new KeyboardInput {
                vk = (ushort)key, flags = (up ? 2u : 0u) | (extended ? 1u : 0u) } } };
        }
        internal static Input[] MouseEvents(AutomationAction action, Rectangle desktop)
        {
            uint down = action.Button == "Right" ? 8u : action.Button == "Middle" ? 32u : 2u;
            int x = Normalize(action.X, desktop.Left, desktop.Width);
            int y = Normalize(action.Y, desktop.Top, desktop.Height);
            return new Input[] { Mouse(x, y, 0xC001), Mouse(0, 0, down), Mouse(0, 0, down * 2) };
        }
        public bool Execute(AutomationAction action)
        {
            Input[] events;
            if (action.Kind == "Mouse")
            {
                bool visible = false;
                foreach (Screen screen in Screen.AllScreens) if (screen.Bounds.Contains(action.X, action.Y)) visible = true;
                if (!visible) throw new InvalidOperationException(L10n.T("Точка «") + action.Name + L10n.T("» поза межами підключених екранів."));
                // Do not combine automation with a physically held button or modifier.
                if (AnyHeld()) return false;
                events = MouseEvents(action, SystemInformation.VirtualScreen);
            }
            else
            {
                if (AnyHeld() || IsHeld((Keys)action.Key)) return false;
                List<Input> keys = new List<Input>();
                if (action.Ctrl) keys.Add(KeyEvent(Keys.ControlKey, false));
                if (action.Alt) keys.Add(KeyEvent(Keys.Menu, false));
                if (action.Shift) keys.Add(KeyEvent(Keys.ShiftKey, false));
                keys.Add(KeyEvent((Keys)action.Key, false));
                keys.Add(KeyEvent((Keys)action.Key, true));
                if (action.Shift) keys.Add(KeyEvent(Keys.ShiftKey, true));
                if (action.Alt) keys.Add(KeyEvent(Keys.Menu, true));
                if (action.Ctrl) keys.Add(KeyEvent(Keys.ControlKey, true));
                events = keys.ToArray();
            }
            uint sent = SendInput((uint)events.Length, events, Marshal.SizeOf(typeof(Input)));
            if (sent != events.Length)
            {
                // A partial batch must not leave an injected button or modifier held.
                List<Input> releases = new List<Input>();
                for (int i = (int)sent - 1; i >= 0; i--)
                {
                    Input input = events[i];
                    if (input.type == 1 && (input.data.keyboard.flags & 2) == 0)
                        releases.Add(KeyEvent((Keys)input.data.keyboard.vk, true));
                    else if (input.type == 0 && (input.data.mouse.flags & 0x2A) != 0)
                        releases.Add(Mouse(0, 0, (input.data.mouse.flags & 0x2A) * 2));
                }
                if (releases.Count > 0) SendInput((uint)releases.Count, releases.ToArray(), Marshal.SizeOf(typeof(Input)));
                throw new InvalidOperationException(L10n.T("Windows не прийняла введення. Якщо цільова програма працює від адміністратора, запустіть автоклікер із такими самими правами."));
            }
            return true;
        }
        private static bool IsHeld(Keys key) { return (GetAsyncKeyState((int)key) & 0x8000) != 0; }
        private static bool AnyHeld()
        {
            return IsHeld(Keys.ControlKey) || IsHeld(Keys.Menu) || IsHeld(Keys.ShiftKey) ||
                IsHeld(Keys.LWin) || IsHeld(Keys.RWin) || IsHeld(Keys.LButton) || IsHeld(Keys.RButton) || IsHeld(Keys.MButton);
        }
    }
}
