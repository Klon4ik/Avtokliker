using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace AvtoClicker
{
    [DataContract]
    public sealed class AutomationAction
    {
        [DataMember] public string Name = L10n.T("Нова дія");
        [DataMember] public bool Enabled = true;
        [DataMember] public string Kind = "Mouse";
        [DataMember] public string Button = "Left";
        [DataMember] public int X;
        [DataMember] public int Y;
        [DataMember] public int Key = (int)Keys.Space;
        [DataMember] public bool Ctrl;
        [DataMember] public bool Alt;
        [DataMember] public bool Shift;
        [DataMember] public int IntervalMs = 1000;
        [DataMember] public int DelayMs;
        [DataMember] public int RepeatCount;

        public AutomationAction Copy() { return (AutomationAction)MemberwiseClone(); }
        public string TargetText
        {
            get
            {
                if (Kind == "Mouse") return X + ", " + Y;
                return (Ctrl ? "Ctrl + " : "") + (Alt ? "Alt + " : "") +
                    (Shift ? "Shift + " : "") + KeyNames.Display((Keys)Key);
            }
        }
        public string ActionText
        {
            get
            {
                if (Kind == "Key") return L10n.T("Клавіатура");
                return Button == "Right" ? L10n.T("Правий клік") : Button == "Middle" ? L10n.T("Середній клік") : L10n.T("Лівий клік");
            }
        }
        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(Name) || Name.Length > 80)
                throw new InvalidDataException(L10n.T("Назва має містити від 1 до 80 символів."));
            if (Kind != "Mouse" && Kind != "Key") throw new InvalidDataException(L10n.T("Невідомий тип дії."));
            if (IntervalMs < 10 || IntervalMs > 86400000)
                throw new InvalidDataException(L10n.T("Інтервал: від 0,01 до 86 400 секунд."));
            if (DelayMs < 0 || DelayMs > 86400000 || RepeatCount < 0 || RepeatCount > 1000000)
                throw new InvalidDataException(L10n.T("Некоректна затримка або кількість повторень."));
            if (Kind == "Mouse" && Button != "Left" && Button != "Right" && Button != "Middle")
                throw new InvalidDataException(L10n.T("Невідома кнопка миші."));
            if (Kind == "Key" && !KeyNames.Supported((Keys)Key))
                throw new InvalidDataException(L10n.T("Непідтримувана клавіша. F6, F8 та F9 зарезервовані для керування."));
        }
    }

    public static class KeyNames
    {
        public static List<Keys> All()
        {
            List<Keys> result = new List<Keys>();
            for (int k = (int)Keys.A; k <= (int)Keys.Z; k++) result.Add((Keys)k);
            for (int k = (int)Keys.D0; k <= (int)Keys.D9; k++) result.Add((Keys)k);
            for (int k = (int)Keys.F1; k <= (int)Keys.F24; k++)
                if (k != (int)Keys.F6 && k != (int)Keys.F8 && k != (int)Keys.F9) result.Add((Keys)k);
            result.AddRange(new Keys[] { Keys.Space, Keys.Enter, Keys.Tab, Keys.Escape, Keys.Back,
                Keys.Delete, Keys.Insert, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
                Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.OemMinus, Keys.Oemplus,
                Keys.Oemcomma, Keys.OemPeriod, Keys.OemQuestion, Keys.OemSemicolon,
                Keys.OemQuotes, Keys.OemOpenBrackets, Keys.OemCloseBrackets, Keys.OemPipe, Keys.Oemtilde });
            for (int k = (int)Keys.NumPad0; k <= (int)Keys.Divide; k++) result.Add((Keys)k);
            return result;
        }
        public static bool Supported(Keys key) { return All().Contains(key); }
        public static string Display(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)key - (int)Keys.D0).ToString();
            switch (key)
            {
                case Keys.Space: return L10n.T("Space (пробіл)");
                case Keys.Enter: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                default: return key.ToString();
            }
        }
    }

    [DataContract]
    public sealed class Profile
    {
        [DataMember] public int Version = 1;
        [DataMember] public int CountdownSeconds = 3;
        [DataMember] public bool MinimizeOnStart = true;
        [DataMember] public bool ShowMarkers;
        [DataMember] public string Language = "uk";
        [DataMember] public List<AutomationAction> Actions = new List<AutomationAction>();
        public void Validate()
        {
            if (Language != null && Language != "uk" && Language != "en")
                throw new InvalidDataException(L10n.T("Непідтримувана мова інтерфейсу."));
            if (Version != 1) throw new InvalidDataException(L10n.T("Непідтримувана версія профілю."));
            if (CountdownSeconds < 0 || CountdownSeconds > 60)
                throw new InvalidDataException(L10n.T("Відлік перед запуском: від 0 до 60 секунд."));
            if (Actions == null || Actions.Count > 500)
                throw new InvalidDataException(L10n.T("Профіль може містити до 500 дій."));
            foreach (AutomationAction action in Actions)
            {
                if (action == null) throw new InvalidDataException(L10n.T("Профіль містить порожню дію."));
                action.Validate();
            }
        }
    }

    public static class ProfileStore
    {
        public static Profile Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > 2 * 1024 * 1024) throw new InvalidDataException(L10n.T("Файл профілю завеликий."));
                Profile profile = (Profile)new DataContractJsonSerializer(typeof(Profile)).ReadObject(stream);
                if (profile == null) throw new InvalidDataException(L10n.T("Порожній профіль."));
                profile.Validate();
                return profile;
            }
        }
        public static void Save(string path, Profile profile)
        {
            profile.Validate();
            string fullPath = Path.GetFullPath(path);
            string temporary = fullPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                {
                    new DataContractJsonSerializer(typeof(Profile)).WriteObject(stream, profile);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, fullPath + ".bak");
                else File.Move(temporary, fullPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
