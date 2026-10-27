using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // User settings, stored under HKCU\Software\Krabbz9999\Blynclight App Control. All default to on.
    public static class AppSettings
    {
        const string SettingsKey = @"Software\Krabbz9999\Blynclight App Control";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Krabbz9999 Blynclight App Control";

        public static bool ShowNotifications { get { return Get("ShowNotifications"); } set { Set("ShowNotifications", value); } }
        public static bool HotkeysEnabled { get { return Get("HotkeysEnabled"); } set { Set("HotkeysEnabled", value); } }
        public static bool AutoUpdate { get { return Get("AutoUpdate"); } set { Set("AutoUpdate", value); } }
        public static bool CallsTurnRed { get { return Get("CallsTurnRed"); } set { Set("CallsTurnRed", value); } }
        public static bool TeamsBusyOnCalls { get { return Get("TeamsBusyOnCalls"); } set { Set("TeamsBusyOnCalls", value); } }
        public static bool StartWithWindows { get { return Get("StartWithWindows"); } set { Set("StartWithWindows", value); ApplyStartup(); } }
        public static bool OpenWindowAtSignIn { get { return Get("OpenWindowAtSignIn"); } set { Set("OpenWindowAtSignIn", value); ApplyStartup(); } }

        // Makes the sign-in startup entry match the settings. Called at every launch too, because each install or
        // update rewrites the entry the installer owns.
        public static void ApplyStartup()
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (!StartWithWindows) run.DeleteValue(RunValue, false);
                else run.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"" + (OpenWindowAtSignIn ? "" : " --tray"));
            }
        }

        static bool Get(string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object value = key == null ? null : key.GetValue(name);
                return !(value is int) || (int)value != 0;
            }
        }

        static void Set(string name, bool value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
