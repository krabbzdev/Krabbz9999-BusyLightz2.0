using System;
using System.Threading;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // Detects softphone calls by watching Windows' microphone usage record for Cytracom Desktop.
    // Windows sets LastUsedTimeStop to 0 while an app has the microphone open, which for the softphone means a call.
    public class CallDetector : IDisposable
    {
        const int PollMs = 1000;
        const string MicUsageKey = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged";
        // Matches the softphone's entry, e.g. "C:#Program Files#Cytracom Desktop#Cytracom Desktop.exe".
        const string SoftphoneExe = "#Cytracom Desktop.exe";

        readonly Timer timer;
        readonly Action<bool> callChanged;
        bool onCall;

        // callChanged runs on a background thread with true when a call starts and false when it ends.
        // Taken here rather than as an event so a call already in progress at startup isn't missed.
        public CallDetector(Action<bool> callChanged)
        {
            this.callChanged = callChanged;
            timer = new Timer(delegate { Poll(); }, null, 0, PollMs);
        }

        void Poll()
        {
            bool nowOnCall;
            try { nowOnCall = SoftphoneUsingMic(); }
            catch (Exception) { return; } // Registry briefly unreadable; try again next poll.
            if (nowOnCall == onCall) return;
            onCall = nowOnCall;
            callChanged(nowOnCall);
        }

        static bool SoftphoneUsingMic()
        {
            using (var apps = Registry.CurrentUser.OpenSubKey(MicUsageKey))
            {
                if (apps == null) return false;
                foreach (string name in apps.GetSubKeyNames())
                {
                    if (!name.EndsWith(SoftphoneExe, StringComparison.OrdinalIgnoreCase)) continue;
                    using (var app = apps.OpenSubKey(name))
                    {
                        object start = app.GetValue("LastUsedTimeStart");
                        object stop = app.GetValue("LastUsedTimeStop");
                        if (start is long && (long)start != 0 && stop is long && (long)stop == 0) return true;
                    }
                }
            }
            return false;
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
