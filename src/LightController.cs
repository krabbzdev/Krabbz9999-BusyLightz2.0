using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Krabbz.Blynclight
{
    // Owns the light: remembers its state, alternates red/blue while flashing, and re-applies the state when a light is plugged in.
    public class LightController : IDisposable
    {
        // Swap colors this often while flashing red/blue.
        public const int AlternateMs = 400;
        // Look for newly plugged-in or removed lights this often.
        const int DeviceCheckMs = 2000;

        readonly object sync = new object();
        readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krabbz9999 Blynclight");
        readonly Thread lightThread;
        string state;
        // While set (e.g. red during a phone call), shown instead of the saved state; not saved.
        string callOverride;
        List<string> knownPaths = new List<string>();
        string lastLogged;

        // Raised on a background thread when a light is plugged in or removed.
        public event Action LightsChanged;

        public LightController()
        {
            Directory.CreateDirectory(dataDir);
            state = LoadState();
            lightThread = new Thread(LightLoop);
            lightThread.IsBackground = true;
            lightThread.Start();
        }

        // The state the user picked (saved across restarts).
        public string State { get { lock (sync) return state; } }
        // What the light is actually showing: the call override if one is active, otherwise State.
        public string ShownState { get { lock (sync) return callOverride ?? state; } }
        public bool OverrideActive { get { lock (sync) return callOverride != null; } }
        public int LightCount { get { lock (sync) return knownPaths.Count; } }

        // Picking a state by hand also cancels any call override, so a manual choice always wins.
        public void Set(string newState)
        {
            lock (sync)
            {
                state = newState;
                callOverride = null;
            }
            SaveState(newState);
            Apply(newState);
        }

        // Shows overrideState until cleared with null, then returns to the saved state.
        public void SetOverride(string overrideState)
        {
            lock (sync)
            {
                if (callOverride == overrideState) return;
                callOverride = overrideState;
            }
            Apply(ShownState);
        }

        // Stops the background loop. Flashing can't continue without it, so the light is turned off in that case;
        // the saved state is kept so flashing resumes next time the app starts.
        public void Dispose()
        {
            stopEvent.Set();
            lightThread.Join(2000);
            if (ShownState == LightState.FlashRedBlue) WriteAll(BlyncDevice.ReportFor(LightState.Off));
        }

        void LightLoop()
        {
            bool showRed = true;
            int sinceDeviceCheck = DeviceCheckMs;
            do
            {
                string current = ShownState;

                if (sinceDeviceCheck >= DeviceCheckMs)
                {
                    sinceDeviceCheck = 0;
                    var paths = BlyncDevice.FindPaths();
                    bool newLight = false, countChanged;
                    lock (sync)
                    {
                        foreach (var path in paths) if (!knownPaths.Contains(path)) newLight = true;
                        countChanged = paths.Count != knownPaths.Count;
                        knownPaths = paths;
                    }
                    if (newLight) Apply(current);
                    if ((newLight || countChanged) && LightsChanged != null) LightsChanged();
                }

                if (current == LightState.FlashRedBlue)
                {
                    WriteAll(BlyncDevice.ReportFor(showRed ? "red" : "blue"));
                    showRed = !showRed;
                }
                sinceDeviceCheck += AlternateMs;
            } while (!stopEvent.WaitOne(AlternateMs));
        }

        void Apply(string newState)
        {
            // Flashing is driven by LightLoop; solid states are written once. In FollowTeams the light
            // belongs to Embrava Connect, so nothing is written.
            if (newState != LightState.FlashRedBlue && newState != LightState.FollowTeams)
                WriteAll(BlyncDevice.ReportFor(newState));
        }

        void WriteAll(byte[] report)
        {
            List<string> paths;
            lock (sync) paths = knownPaths;
            foreach (var path in paths)
            {
                try { BlyncDevice.Write(path, report); }
                catch (Exception ex) { Log("Could not update light: " + ex.Message); }
            }
        }

        string StatePath { get { return Path.Combine(dataDir, "state.txt"); } }

        string LoadState()
        {
            try
            {
                string saved = File.ReadAllText(StatePath).Trim();
                if (LightState.IsValid(saved)) return saved;
            }
            catch (IOException) { }
            return LightState.Off;
        }

        void SaveState(string newState)
        {
            try { File.WriteAllText(StatePath, newState); }
            catch (Exception ex) { Log("Could not save state: " + ex.Message); }
        }

        // Appends to app.log, skipping repeats so a missing light doesn't flood the file.
        void Log(string message)
        {
            if (message == lastLogged) return;
            lastLogged = message;
            try { File.AppendAllText(Path.Combine(dataDir, "app.log"), DateTime.Now.ToString("s") + "  " + message + Environment.NewLine); }
            catch (IOException) { }
        }
    }
}
