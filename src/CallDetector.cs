using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // An app in Windows' microphone usage record.
    public class MicApp
    {
        // Registry key name: a "#"-separated path for desktop apps, or a package family name for Store apps.
        public string Id;
        // Short display name, e.g. "Zoom", "Teams", "Cytracom".
        public string Name;
        public bool InUse;
        public DateTime LastUsed;
        public bool IsTeams { get { return Name == "Teams"; } }
    }

    // Treats any app with the microphone open as being on a call, except apps on the user's ignore list.
    // Windows sets LastUsedTimeStop to 0 while an app has the microphone open.
    public class CallDetector : IDisposable
    {
        const int PollMs = 1000;
        const string MicUsageKey = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        const string SettingsKey = @"Software\Krabbz9999\Blynclight App Control";
        const string IgnoredValue = "IgnoredMicApps";

        // Friendlier names for apps people will recognize, keyed by exe file or package name.
        static readonly Dictionary<string, string> KnownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Cytracom Desktop", "Cytracom" },
            { "Teams", "Teams" },        // Classic Teams
            { "ms-teams", "Teams" },
            { "MSTeams", "Teams" },      // New Teams (Store package)
            { "msedge", "Edge" },
            { "chrome", "Chrome" },
            { "firefox", "Firefox" },
        };

        readonly Timer timer;
        readonly Action<MicApp> callChanged;
        string currentId;

        // callChanged runs on a background thread with the app when a call starts or switches apps, and null when
        // it ends. Taken here rather than as an event so a call already in progress at startup isn't missed.
        public CallDetector(Action<MicApp> callChanged)
        {
            this.callChanged = callChanged;
            timer = new Timer(delegate { Poll(); }, null, 0, PollMs);
        }

        void Poll()
        {
            MicApp app = null;
            try
            {
                var ignored = new HashSet<string>(IgnoredIds(), StringComparer.OrdinalIgnoreCase);
                foreach (var candidate in AppsInRecord())
                {
                    if (!candidate.InUse || ignored.Contains(candidate.Id)) continue;
                    // Prefer a calling app if several have the mic, so the status names the call.
                    if (app == null || candidate.IsTeams) app = candidate;
                }
            }
            catch (Exception) { return; } // Registry briefly unreadable; try again next poll.

            string id = app == null ? null : app.Id;
            if (id == currentId) return;
            currentId = id;
            callChanged(app);
        }

        // Every app Windows has recorded using the microphone, most recently used first.
        public static List<MicApp> AppsInRecord()
        {
            var apps = new List<MicApp>();
            using (var mic = Registry.CurrentUser.OpenSubKey(MicUsageKey))
            {
                if (mic == null) return apps;
                foreach (string name in mic.GetSubKeyNames())
                {
                    if (name == "NonPackaged") continue;
                    using (var key = mic.OpenSubKey(name)) AddIfUsed(apps, key, name, PackageName(name));
                }
                using (var desktop = mic.OpenSubKey("NonPackaged"))
                {
                    if (desktop != null)
                        foreach (string name in desktop.GetSubKeyNames())
                            using (var key = desktop.OpenSubKey(name)) AddIfUsed(apps, key, name, ExeName(name));
                }
            }
            apps.Sort((a, b) => b.LastUsed.CompareTo(a.LastUsed));
            return apps;
        }

        // True once Windows has recorded an app with this display name (e.g. "Cytracom") using the microphone.
        public static bool HasMicRecord(string appName)
        {
            foreach (var app in AppsInRecord()) if (app.Name == appName) return true;
            return false;
        }

        static void AddIfUsed(List<MicApp> apps, RegistryKey key, string id, string rawName)
        {
            if (key == null) return;
            object start = key.GetValue("LastUsedTimeStart");
            object stop = key.GetValue("LastUsedTimeStop");
            if (!(start is long) || (long)start == 0) return;
            string name;
            apps.Add(new MicApp
            {
                Id = id,
                Name = KnownNames.TryGetValue(rawName, out name) ? name : rawName,
                InUse = stop is long && (long)stop == 0,
                LastUsed = DateTime.FromFileTime((long)start),
            });
        }

        // "C:#Program Files#Zoom#bin#Zoom.exe" -> "Zoom"
        static string ExeName(string keyName)
        {
            return Path.GetFileNameWithoutExtension(keyName.Substring(keyName.LastIndexOf('#') + 1));
        }

        // "Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe" -> "WindowsSoundRecorder"
        static string PackageName(string keyName)
        {
            string name = keyName.Split('_')[0];
            return KnownNames.ContainsKey(name) ? name : name.Substring(name.LastIndexOf('.') + 1);
        }

        public static List<string> IgnoredIds()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                var ids = key == null ? null : key.GetValue(IgnoredValue) as string[];
                return ids == null ? new List<string>() : new List<string>(ids);
            }
        }

        public static void SetIgnoredIds(IEnumerable<string> ids)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue(IgnoredValue, new List<string>(ids).ToArray(), RegistryValueKind.MultiString);
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
