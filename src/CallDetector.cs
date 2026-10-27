using System;
using System.Threading;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // Detects calls by watching Windows' microphone usage record for calling apps.
    // Windows sets LastUsedTimeStop to 0 while an app has the microphone open, which for these apps means a call.
    public class CallDetector : IDisposable
    {
        const int PollMs = 1000;
        const string MicUsageKey = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        // Desktop apps are recorded under NonPackaged by path, e.g. "C:#Program Files#Cytracom Desktop#Cytracom Desktop.exe".
        static readonly string[][] DesktopApps =
        {
            new[] { "#Cytracom Desktop.exe", "Cytracom" },
            new[] { "#Teams.exe", "Teams" }, // Classic Teams
        };
        // Store apps are recorded by package family name.
        static readonly string[][] PackagedApps =
        {
            new[] { "MSTeams_8wekyb3d8bbwe", "Teams" }, // New Teams
        };

        readonly Timer timer;
        readonly Action<string> callChanged;
        string currentApp;

        // callChanged runs on a background thread with the app name ("Cytracom" or "Teams") when a call starts,
        // and null when it ends. Taken here rather than as an event so a call already in progress at startup isn't missed.
        public CallDetector(Action<string> callChanged)
        {
            this.callChanged = callChanged;
            timer = new Timer(delegate { Poll(); }, null, 0, PollMs);
        }

        void Poll()
        {
            string app;
            try { app = AppUsingMic(); }
            catch (Exception) { return; } // Registry briefly unreadable; try again next poll.
            if (app == currentApp) return;
            currentApp = app;
            callChanged(app);
        }

        static string AppUsingMic()
        {
            using (var mic = Registry.CurrentUser.OpenSubKey(MicUsageKey))
            {
                if (mic == null) return null;

                using (var desktop = mic.OpenSubKey("NonPackaged"))
                {
                    if (desktop != null)
                    {
                        foreach (string name in desktop.GetSubKeyNames())
                            foreach (var app in DesktopApps)
                                if (name.EndsWith(app[0], StringComparison.OrdinalIgnoreCase) && InUse(desktop, name)) return app[1];
                    }
                }
                foreach (var app in PackagedApps)
                    if (InUse(mic, app[0])) return app[1];
            }
            return null;
        }

        static bool InUse(RegistryKey parent, string name)
        {
            using (var app = parent.OpenSubKey(name))
            {
                if (app == null) return false;
                object start = app.GetValue("LastUsedTimeStart");
                object stop = app.GetValue("LastUsedTimeStop");
                return start is long && (long)start != 0 && stop is long && (long)stop == 0;
            }
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
