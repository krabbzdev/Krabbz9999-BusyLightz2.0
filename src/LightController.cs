using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Krabbz.Blynclight
{
    // Owns the light: remembers its state, cycles colors in Party Mode, and re-applies the state when a light is plugged in.
    public class LightController : IDisposable
    {
        // Party Mode moves this far round the color wheel every PartyStepMs: a full cycle about every 2.4 seconds.
        public const int PartyStepMs = 100;
        public const int PartyHueStep = 15;
        // During a call flash, switch between red and blue this often.
        const int CallFlashMs = 300;
        // Look for newly plugged-in or removed lights, and re-send the current color, this often.
        const int DeviceCheckMs = 2000;

        readonly object sync = new object();
        readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krabbz9999 Blynclight");
        readonly Thread lightThread;
        string state;
        // While set (e.g. red during a phone call), shown instead of the saved state; not saved.
        string callOverride;
        // Color for the user's Teams status, shown in FollowTeams when the app reads presence itself.
        // Null means Embrava Connect is driving the light instead.
        string teamsColor;
        // When a color picked by hand (Red, Green, On A Call) goes back to following Teams, in UTC; null for never.
        // Saved, so the timer keeps running across restarts.
        DateTime? returnToTeamsAt;
        List<LightDevice> knownLights = new List<LightDevice>();
        string lastLogged;

        // Raised on a background thread when a light is plugged in or removed.
        public event Action LightsChanged;

        public LightController()
        {
            Directory.CreateDirectory(dataDir);
            state = LoadState();
            returnToTeamsAt = LoadReturnToTeams();
            lightThread = new Thread(LightLoop);
            lightThread.IsBackground = true;
            lightThread.Start();
        }

        // The state the user picked (saved across restarts).
        public string State { get { lock (sync) return state; } }
        // What the light is actually showing: the call override if one is active, then the Teams color
        // when following Teams, otherwise State.
        public string ShownState
        {
            get
            {
                lock (sync)
                {
                    if (callOverride != null) return callOverride;
                    if (state == LightState.FollowTeams && teamsColor != null) return teamsColor;
                    return state;
                }
            }
        }
        public bool OverrideActive { get { lock (sync) return callOverride != null; } }
        public int LightCount { get { lock (sync) return knownLights.Count; } }

        // e.g. "Blynclight + 2 Luxafor", or "none".
        public string LightSummary
        {
            get
            {
                int blync, lux;
                lock (sync)
                {
                    blync = knownLights.FindAll(l => l.Kind == LightKind.Blynclight).Count;
                    lux = knownLights.Count - blync;
                }
                var parts = new List<string>();
                if (blync > 0) parts.Add((blync > 1 ? blync + " " : "") + "Blynclight");
                if (lux > 0) parts.Add((lux > 1 ? lux + " " : "") + "Luxafor");
                return parts.Count == 0 ? "none" : string.Join(" + ", parts.ToArray());
            }
        }

        // Colors picked by hand that override Teams status, so they go back to following Teams after a while.
        public static bool OverridesTeams(string state)
        {
            return state == LightState.Red || state == LightState.Green || state == LightState.CallFlash;
        }

        public DateTime? ReturnToTeamsAt { get { lock (sync) return returnToTeamsAt; } }

        // Picking a state by hand also cancels any call override, so a manual choice always wins.
        // It also cancels any return-to-Teams timer; the caller starts a new one if needed.
        public void Set(string newState)
        {
            lock (sync)
            {
                state = newState;
                callOverride = null;
            }
            SaveState(newState);
            SetReturnToTeams(null);
            Apply(newState);
        }

        public void SetReturnToTeams(DateTime? utc)
        {
            lock (sync) returnToTeamsAt = utc;
            try
            {
                if (utc == null) File.Delete(ReturnToTeamsPath);
                else File.WriteAllText(ReturnToTeamsPath, utc.Value.Ticks.ToString());
            }
            catch (Exception ex) { Log("Could not save the return-to-Teams time: " + ex.Message); }
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

        // Sets the color for the user's Teams status (e.g. "green", "yellow"); null hands the light back to
        // Embrava Connect. Only shown while the saved state is FollowTeams and no call override is active.
        public void SetTeamsColor(string color)
        {
            lock (sync)
            {
                if (teamsColor == color) return;
                teamsColor = color;
            }
            Apply(ShownState);
        }

        // Stops the background loop. Party Mode and call flashing can't continue without it, so the light is turned
        // off in those cases; the saved state is kept so it resumes next time the app starts.
        public void Dispose()
        {
            stopEvent.Set();
            lightThread.Join(2000);
            if (Animated(ShownState)) WriteAll(LightColor.Off);
        }

        static bool Animated(string shown)
        {
            return shown == LightState.Party || shown == LightState.CallFlash;
        }

        void LightLoop()
        {
            int hue = 0;
            int flashMs = 0;
            int sinceDeviceCheck = DeviceCheckMs;
            do
            {
                string current = ShownState;

                if (sinceDeviceCheck >= DeviceCheckMs)
                {
                    sinceDeviceCheck = 0;
                    var found = BlyncDevice.FindLights();
                    bool newLight = false, countChanged;
                    lock (sync)
                    {
                        foreach (var light in found)
                            if (!knownLights.Exists(known => known.Path == light.Path)) newLight = true;
                        countChanged = found.Count != knownLights.Count;
                        knownLights = found;
                    }
                    // Re-send the solid color every check, not just when a light is plugged in: another program
                    // (or a write that raced the first device scan) can leave a light showing the wrong color.
                    current = ShownState;
                    Apply(current);
                    if ((newLight || countChanged) && LightsChanged != null) LightsChanged();
                }

                if (current == LightState.Party)
                {
                    byte r, g, b;
                    BlyncDevice.HueToRgb(hue, out r, out g, out b);
                    WriteAll(LightColor.Rgb(r, g, b));
                    hue = (hue + PartyHueStep) % 360;
                }
                if (current == LightState.CallFlash)
                {
                    // Write every tick so a light plugged in mid-call joins in; the color flips every CallFlashMs.
                    bool red = (flashMs / CallFlashMs) % 2 == 0;
                    WriteAll(BlyncDevice.ColorFor(red ? LightState.Red : "blue"));
                    flashMs += PartyStepMs;
                }
                else flashMs = 0;
                sinceDeviceCheck += PartyStepMs;
            } while (!stopEvent.WaitOne(PartyStepMs));
        }

        void Apply(string newState)
        {
            // Party Mode and call flashing are driven by LightLoop; solid states are written once. In FollowTeams
            // without a Teams color the light belongs to Embrava Connect, so nothing is written.
            if (!Animated(newState) && newState != LightState.FollowTeams)
                WriteAll(BlyncDevice.ColorFor(newState));
        }

        // Sends the color to every connected light, each in its own report format.
        void WriteAll(LightColor color)
        {
            List<LightDevice> lights;
            lock (sync) lights = knownLights;
            foreach (var light in lights)
            {
                try { BlyncDevice.Write(light.Path, BlyncDevice.ReportFor(light.Kind, color)); }
                catch (Exception ex) { Log("Could not update " + light.Kind + ": " + ex.Message); }
            }
        }

        string StatePath { get { return Path.Combine(dataDir, "state.txt"); } }
        string ReturnToTeamsPath { get { return Path.Combine(dataDir, "return-to-teams.txt"); } }

        DateTime? LoadReturnToTeams()
        {
            try
            {
                long ticks;
                if (long.TryParse(File.ReadAllText(ReturnToTeamsPath).Trim(), out ticks)) return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (IOException) { }
            return null;
        }

        string LoadState()
        {
            try
            {
                string saved = File.ReadAllText(StatePath).Trim();
                if (saved == LightState.LegacyFlashRedBlue) return LightState.Party;
                if (LightState.IsValid(saved)) return saved;
            }
            catch (IOException) { }
            // New installs follow Teams status until the user picks something else.
            return LightState.FollowTeams;
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
