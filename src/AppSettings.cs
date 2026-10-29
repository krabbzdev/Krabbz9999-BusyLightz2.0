using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // User settings, stored under HKCU\Software\Krabbz9999\Blynclight App Control. On/off settings default to on unless noted.
    public static class AppSettings
    {
        const string SettingsKey = @"Software\Krabbz9999\Blynclight App Control";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Krabbz9999 Blynclight App Control";

        public static bool ShowNotifications { get { return Get("ShowNotifications"); } set { Set("ShowNotifications", value); } }
        public static bool HotkeysEnabled { get { return Get("HotkeysEnabled"); } set { Set("HotkeysEnabled", value); } }
        public static bool AutoUpdate { get { return Get("AutoUpdate"); } set { Set("AutoUpdate", value); } }
        public static bool CallsTurnRed { get { return Get("CallsTurnRed"); } set { Set("CallsTurnRed", value); } }
        public static bool OverrideTimesOut { get { return Get("OverrideTimesOut"); } set { Set("OverrideTimesOut", value); } }
        // How long Red, Green and On A Call last before going back to Follow Teams Status.
        public static readonly int[] OverrideMinuteChoices = { 5, 10, 15, 30, 60, 120, 240 };
        public const int DefaultOverrideMinutes = 15;
        public static int OverrideMinutes
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    object value = key == null ? null : key.GetValue("OverrideMinutes");
                    return value is int && (int)value > 0 ? (int)value : DefaultOverrideMinutes;
                }
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    key.SetValue("OverrideMinutes", value, RegistryValueKind.DWord);
            }
        }
        public static bool FlashOnCalls { get { return Get("FlashOnCalls"); } set { Set("FlashOnCalls", value); } }
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

        // Teams status message shown while the lights are red. Off by default, since it changes what coworkers see.
        public static readonly string[] TeamsStatusMessageChoices =
            { "On a video call", "In a meeting", "On a phone call", "Heads down, please message me", "Back in a few minutes" };
        public const int TeamsStatusMessageMaxLength = 280;
        public static bool TeamsStatusMessageOn { get { return Get("TeamsStatusMessageOn", false); } set { Set("TeamsStatusMessageOn", value); } }
        public static string TeamsStatusMessage
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    string value = key == null ? null : key.GetValue("TeamsStatusMessage") as string;
                    return string.IsNullOrWhiteSpace(value) ? TeamsStatusMessageChoices[0] : value;
                }
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                    key.SetValue("TeamsStatusMessage", value ?? "", RegistryValueKind.String);
            }
        }

        static bool Get(string name, bool defaultValue = true)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object value = key == null ? null : key.GetValue(name);
                return value is int ? (int)value != 0 : defaultValue;
            }
        }

        static void Set(string name, bool value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
