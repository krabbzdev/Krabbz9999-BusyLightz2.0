using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Krabbz.Blynclight
{
    static class Program
    {
        // Display name. The exe file, install folder, settings folder and registry key keep their original
        // "Krabbz9999 Blynclight..." names, because installed copies relaunch themselves by exe path after updating.
        public const string AppName = "KrabbzDev-BusyLightz2.0";

        // Stamped by build.ps1 into build\AssemblyInfo.cs.
        public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        public static string VersionText { get { return Version.ToString(3); } }
        public static string BuildDate { get { return Metadata("BuildDate") ?? "unknown"; } }
        // GitHub "owner/repo" whose releases this build updates from; null disables auto-update.
        public static string UpdateRepo { get { return Metadata("UpdateRepo"); } }
        // Entra app registration for setting Teams presence; null hides the feature.
        public static string GraphClientId { get { return Metadata("GraphClientId"); } }
        public static string GraphTenantId { get { return Metadata("GraphTenantId"); } }

        static string Metadata(string key)
        {
            foreach (AssemblyMetadataAttribute meta in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
                if (meta.Key == key && !string.IsNullOrEmpty(meta.Value)) return meta.Value;
            return null;
        }

        // Usage: no args opens the window (also used at sign-in); --tray starts in the tray only;
        // --updated (added after an auto-update) shows an "Updated to version x" notice.
        [STAThread]
        static int Main(string[] args)
        {
            bool trayOnly = Array.IndexOf(args, "--tray") >= 0;
            bool justUpdated = Array.IndexOf(args, "--updated") >= 0;
            bool firstInstance;
            using (var mutex = new Mutex(true, @"Local\Krabbz9999BlynclightTray", out firstInstance))
            using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Krabbz9999BlynclightShow"))
            {
                // A second launch just asks the running copy to show its window.
                if (!firstInstance)
                {
                    if (!trayOnly) showSignal.Set();
                    return 0;
                }
                // Errors nothing else caught go to app.log. On the UI thread the app logs them and keeps running,
                // instead of showing .NET's error dialog; on other threads Windows still closes the app.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (sender, e) => LogError("Unexpected error", e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    LogError("Unexpected error, the app closed", e.ExceptionObject as Exception);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new TrayContext(showSignal, !trayOnly, justUpdated)); }
                catch (Exception ex) { LogError("Unexpected error, the app closed", ex); throw; }
            }
            return 0;
        }

        // Runs background work and logs anything it throws, instead of letting it close the app.
        public static void Guard(string what, Action work)
        {
            try { work(); }
            catch (Exception ex) { LogError(what, ex); }
        }

        // Guard on a thread-pool thread.
        public static void Background(string what, Action work)
        {
            ThreadPool.QueueUserWorkItem(delegate { Guard(what, work); });
        }

        static readonly object logGate = new object();

        // Appends the full error, with the version and where it happened, to app.log. Past 1 MB the log
        // moves to app.old.log first, so crash details can't fill the disk.
        public static void LogError(string what, Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krabbz9999 Blynclight");
                string log = Path.Combine(dir, "app.log");
                lock (logGate)
                {
                    Directory.CreateDirectory(dir);
                    if (File.Exists(log) && new FileInfo(log).Length > 1024 * 1024)
                    {
                        string old = Path.Combine(dir, "app.old.log");
                        File.Delete(old);
                        File.Move(log, old);
                    }
                    File.AppendAllText(log, DateTime.Now.ToString("s") + "  " + what + " (version " + VersionText + ", " + WindowsVersion
                        + "): " + (ex == null ? "unknown error" : ex.ToString()) + Environment.NewLine);
                }
            }
            catch (Exception) { } // Logging must never cause a crash of its own.
        }

        // E.g. "Windows 25H2, build 26200.9711". Environment.OSVersion says 6.2 on every Windows since 8 for apps without
        // a compatibility manifest, so read the registry.
        public static string WindowsVersion
        {
            get
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                        return "Windows " + key.GetValue("DisplayVersion") + ", build " + key.GetValue("CurrentBuild") + "." + key.GetValue("UBR");
                }
                catch (Exception) { return "Windows " + Environment.OSVersion.Version; }
            }
        }
    }

    // Hidden window that receives global hotkeys and lets background threads post work to the UI thread.
    class MessageWindow : Control
    {
        const int WM_HOTKEY = 0x0312;
        public event Action<int> HotkeyPressed;

        public MessageWindow() { CreateHandle(); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && HotkeyPressed != null) HotkeyPressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    class TrayContext : ApplicationContext
    {
        const uint MOD_ALT = 0x1, MOD_NOREPEAT = 0x4000;
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Global hotkeys. The hotkey id is the index into HotkeyStates/HotkeyKeys; HotkeyText is shown in the menu and on buttons.
        public static readonly Dictionary<string, string> HotkeyText = new Dictionary<string, string>
        {
            { LightState.Red, "Alt+R" },
            { LightState.Green, "Alt+G" },
        };
        static readonly string[] HotkeyStates = { LightState.Red, LightState.Green };
        static readonly Keys[] HotkeyKeys = { Keys.R, Keys.G };

        // Check for updates a minute after starting, then this often.
        const int UpdateFirstCheckMs = 60 * 1000;
        const int UpdateCheckMs = 6 * 60 * 60 * 1000;
        // How often to read Teams presence while following Teams status.
        const int TeamsPollMs = 20 * 1000;

        readonly LightController light = new LightController();
        readonly CallDetector calls;
        // App that has the microphone open, or null when not on a call.
        MicApp callApp;
        // Reads Teams status and sets Teams to Busy during non-Teams calls; null when no Teams app is configured.
        TeamsPresence presence;
        ToolStripMenuItem presenceItem;
        readonly System.Threading.Timer teamsPoll;
        // Last Teams availability read while signed in (e.g. "Available"), or null.
        string teamsAvailability;
        bool embravaWarned;
        // What the last notification described, so only real changes notify.
        string lastNotifiedKey;
        readonly System.Threading.Timer updateTimer;
        int updateCheckRunning;
        readonly NotifyIcon tray;
        readonly MessageWindow messages = new MessageWindow();
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
        // Counts down a color override and switches back to Follow Teams Status when it runs out.
        readonly System.Windows.Forms.Timer overrideTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly RegisteredWaitHandle showWait;
        readonly Dictionary<string, Icon> icons = new Dictionary<string, Icon>();
        readonly Dictionary<string, ToolStripMenuItem> stateItems = new Dictionary<string, ToolStripMenuItem>();
        ControlForm form;
        SetupForm setupForm;
        SettingsForm settingsForm;
        // Where the tray icon and swatch are on the color wheel in Party Mode.
        int partyHue;

        // What the light is showing right now (red during a softphone call).
        public string State { get { return light.ShownState; } }
        public Color DisplayColor { get; private set; }
        public event Action DisplayChanged;

        public TrayContext(EventWaitHandle showSignal, bool showWindow, bool justUpdated)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open " + Program.AppName, null, delegate { ShowWindow(); }).Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());
            AddStateItem(menu, "Busy (red)", LightState.Red);
            AddStateItem(menu, "Available (green)", LightState.Green);
            AddStateItem(menu, "On a call (flashing red/blue)", LightState.CallFlash);
            AddStateItem(menu, "Test lights (cycling colors)", LightState.Party);
            AddStateItem(menu, "Off", LightState.Off);
            AddStateItem(menu, "Follow Teams Status", LightState.FollowTeams);
            menu.Items.Add(new ToolStripSeparator());
            presence = CreatePresence();
            // Without a Teams app for this organization, the item opens the setup checklist to set one up.
            presenceItem = new ToolStripMenuItem("Connect to Microsoft Teams", null, delegate
            {
                if (presence == null) ShowSetup();
                else TogglePresence();
            });
            menu.Items.Add(presenceItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings...", null, delegate { ShowSettings(); });
            menu.Items.Add("Setup checklist...", null, delegate { ShowSetup(); });
            menu.Items.Add("Version " + Program.VersionText).Enabled = false;
            if (Program.UpdateRepo != null)
            {
                menu.Items.Add("Check for updates", null, delegate { CheckForUpdates(true); });
                menu.Items.Add("Project page on GitHub", null, delegate { OpenProjectPage(); });
                menu.Items.Add("Help (user guide)", null, delegate { OpenHelp(); });
            }
            menu.Items.Add("Email crash log...", null, delegate { CrashReport.Email(); });
            menu.Items.Add("☕ Buy me a coffee", null, delegate { OpenCoffee(); });
            menu.Items.Add("Exit tray app", null, delegate { Application.Exit(); });

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.Visible = true;

            // Cycle the tray icon round the color wheel at the light's speed, in half as many steps.
            flashTimer.Interval = LightController.PartyStepMs * 2;
            flashTimer.Tick += delegate { partyHue = (partyHue + LightController.PartyHueStep * 2) % 360; UpdateDisplay(); };

            if (AppSettings.HotkeysEnabled) RegisterHotkeys();
            messages.HotkeyPressed += id => SetState(HotkeyStates[id]);
            // Installs and updates rewrite the sign-in startup entry; put back the user's choice.
            try { AppSettings.ApplyStartup(); } catch (Exception) { }
            // An override saved by a version without the timer gets a fresh hour from now.
            StartOverrideTimerIfNeeded();
            overrideTimer.Tick += delegate { CheckOverride(); };
            overrideTimer.Start();
            Refresh();

            light.LightsChanged += delegate
            {
                try { messages.BeginInvoke((Action)Refresh); }
                catch (InvalidOperationException) { } // Window already gone during exit.
            };
            // Red while any (non-ignored) app has the microphone open, then back to the user's color
            // (or back to Teams status when following Teams). Both parts can be turned off in Settings.
            calls = new CallDetector(app =>
            {
                try { messages.BeginInvoke((Action)delegate { OnMicApp(app); }); }
                catch (InvalidOperationException) { } // Window already gone during exit.
            });
            teamsPoll = new System.Threading.Timer(delegate { Program.Guard("Teams status check", PollTeams); }, null, 0, TeamsPollMs);
            if (Program.UpdateRepo != null)
                updateTimer = new System.Threading.Timer(delegate { Program.Guard("Update check", () => CheckForUpdates(false)); },
                    null, UpdateFirstCheckMs, UpdateCheckMs);
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal,
                delegate { Program.Guard("Opening the window", () => messages.BeginInvoke((Action)ShowWindow)); }, null, -1, false);
            if (showWindow) ShowWindow();
            // Walk new users (and users updating from before the checklist existed) through setup once.
            if (!File.Exists(SetupShownPath))
            {
                ShowSetup();
                try { File.WriteAllText(SetupShownPath, Program.VersionText); } catch (IOException) { }
            }
            string lastVersion = AppSettings.LastVersion;
            try { AppSettings.LastVersion = Program.VersionText; } catch (Exception) { }
            if (justUpdated || (lastVersion != null && lastVersion != Program.VersionText)) ShowWhatsNew();
        }

        // Clicking the "what changed" card opens this release page; other cards open nothing.
        string balloonLink;

        // Shows an "Updated to version x" card listing what changed, from the release notes on GitHub.
        void ShowWhatsNew()
        {
            tray.BalloonTipClicked += delegate { if (balloonLink != null) System.Diagnostics.Process.Start(balloonLink); };
            tray.BalloonTipClosed += delegate { balloonLink = null; };
            Program.Background("Showing what changed", delegate
            {
                string repo = Program.UpdateRepo;
                string notes = repo == null ? null : Updater.ReleaseNotes(repo, Program.VersionText);
                // Windows cuts notification text off at about 200 characters.
                const int MaxLength = 200;
                string more = repo == null ? "" : "\nClick to see the full release notes.";
                string text = notes ?? "Click to see what changed.";
                if (notes != null && text.Length + more.Length > MaxLength)
                    text = text.Substring(0, MaxLength - more.Length - 3).TrimEnd() + "...";
                if (notes != null) text += more;
                try
                {
                    messages.BeginInvoke((Action)delegate
                    {
                        balloonLink = repo == null ? null : Updater.ReleasePage(repo, Program.VersionText);
                        tray.ShowBalloonTip(15000, "Updated to version " + Program.VersionText, repo == null ? "You're on the newest version." : text, ToolTipIcon.Info);
                    });
                }
                catch (InvalidOperationException) { } // Window already gone during exit.
            });
        }

        static string SetupShownPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Krabbz9999 Blynclight", "setup-shown.txt");
            }
        }

        // For the setup checklist.
        public LightController Light { get { return light; } }
        public TeamsPresence Presence { get { return presence; } }

        public void ConnectTeams()
        {
            if (presence != null && !presence.SignedIn) TogglePresence();
        }

        public void DisconnectTeams()
        {
            if (presence != null && presence.SignedIn) TogglePresence();
        }

        TeamsPresence CreatePresence()
        {
            if (!TeamsAppConfig.Configured) return null;
            return new TeamsPresence(TeamsAppConfig.ClientId, TeamsAppConfig.TenantId, message => ShowBalloon(message, ToolTipIcon.Warning));
        }

        // Switches to the Teams app now saved in TeamsAppConfig. Any sign-in with the previous app is dropped.
        public void ApplyTeamsApp()
        {
            var old = presence;
            presence = null;
            teamsAvailability = null;
            light.SetTeamsColor(null);
            if (old != null)
            {
                Program.Background("Teams sign-out", delegate
                {
                    old.SignOut();
                    old.Dispose();
                });
            }
            presence = CreatePresence();
            Refresh();
        }

        public void ShowSetup()
        {
            if (setupForm == null || setupForm.IsDisposed) setupForm = new SetupForm(this);
            setupForm.Show();
            if (setupForm.WindowState == FormWindowState.Minimized) setupForm.WindowState = FormWindowState.Normal;
            setupForm.Activate();
        }

        // What the lights show during a call: flashing red/blue, or steady red.
        static string CallState { get { return AppSettings.FlashOnCalls ? LightState.CallFlash : LightState.Red; } }

        // Shows the call color for a few seconds so the user can see the light respond, unless a real call is in progress.
        public void TestLight()
        {
            if (light.OverrideActive) return;
            light.SetOverride(CallState);
            Refresh();
            var timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += delegate
            {
                timer.Dispose();
                if (callApp == null) light.SetOverride(null);
                Refresh();
            };
            timer.Start();
        }

        void OnMicApp(MicApp app)
        {
            bool wasOverriding = light.OverrideActive;
            // Teams already shows its own calls as Busy; other apps need presence set.
            if (presence != null)
            {
                if (app != null && !app.IsTeams && AppSettings.TeamsBusyOnCalls) presence.CallStarted();
                else presence.CallEnded();
            }
            var shown = AppSettings.CallsTurnRed ? app : null;
            callApp = shown;
            light.SetOverride(shown != null ? CallState : null);
            if (shown == null && wasOverriding && light.State == LightState.FollowTeams) HandBackToTeams();
            Refresh();
        }

        // Re-applies call and hotkey settings after they change in the Settings window.
        public void ApplySettings()
        {
            calls.Recheck();
            if (AppSettings.OverrideTimesOut) StartOverrideTimerIfNeeded();
            else light.SetReturnToTeams(null);
            Refresh();
            for (int id = 0; id < HotkeyStates.Length; id++) UnregisterHotKey(messages.Handle, id);
            if (AppSettings.HotkeysEnabled) RegisterHotkeys();
        }

        // Reopens the main and Settings windows in the current Light/Dark colors, where they were. Runs after the
        // caller's event finishes, since that's usually a control in the Settings window being replaced.
        public void ApplyTheme()
        {
            messages.BeginInvoke((Action)delegate
            {
                if (form != null && !form.IsDisposed)
                {
                    bool shown = form.Visible;
                    var at = form.Location;
                    form.Dispose();
                    form = new ControlForm(this) { StartPosition = FormStartPosition.Manual, Location = at };
                    if (shown) form.Show();
                }
                if (settingsForm != null && !settingsForm.IsDisposed)
                {
                    var at = settingsForm.Location;
                    settingsForm.Close(); // Also stops its refresh timer.
                    settingsForm = new SettingsForm(this) { StartPosition = FormStartPosition.Manual, Location = at };
                    settingsForm.Show();
                    settingsForm.Activate();
                }
            });
        }

        public void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) settingsForm = new SettingsForm(this);
            settingsForm.Show();
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        void AddStateItem(ContextMenuStrip menu, string text, string state)
        {
            var item = new ToolStripMenuItem(text, null, delegate { SetState(state); });
            string keys;
            if (HotkeyText.TryGetValue(state, out keys)) item.ShortcutKeyDisplayString = keys;
            stateItems[state] = item;
            menu.Items.Add(item);
        }

        void RegisterHotkeys()
        {
            var failed = new List<string>();
            for (int id = 0; id < HotkeyStates.Length; id++)
            {
                if (!RegisterHotKey(messages.Handle, id, MOD_ALT | MOD_NOREPEAT, (uint)HotkeyKeys[id]))
                    failed.Add(HotkeyText[HotkeyStates[id]]);
            }
            if (failed.Count > 0)
                tray.ShowBalloonTip(5000, Program.AppName,
                    string.Join(", ", failed.ToArray()) + " is already used by another program, so that hotkey won't work.", ToolTipIcon.Warning);
        }

        public void ShowWindow()
        {
            if (form == null || form.IsDisposed) form = new ControlForm(this);
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        // Downloads and installs a newer release in the background. Only a manual check reports "up to date" or errors.
        // The user guide lives in the repo's docs folder.
        public void OpenHelp()
        {
            if (Program.UpdateRepo != null) System.Diagnostics.Process.Start("https://github.com/" + Program.UpdateRepo + "/blob/main/docs/README.md");
        }

        // KrabbzDev's tip jar.
        const string CoffeeUrl = "https://buymeacoffee.com/krabbzdev";

        public void OpenCoffee()
        {
            System.Diagnostics.Process.Start(CoffeeUrl);
        }

        public void OpenProjectPage()
        {
            if (Program.UpdateRepo != null) System.Diagnostics.Process.Start("https://github.com/" + Program.UpdateRepo);
        }

        public void CheckForUpdates(bool manual)
        {
            if (!manual && !AppSettings.AutoUpdate) return;
            if (Interlocked.Exchange(ref updateCheckRunning, 1) == 1) return;
            Program.Background("Update check", delegate
            {
                UpdateInfo update = null;
                string message;
                try
                {
                    update = Updater.DownloadIfNewer(Program.UpdateRepo, Program.Version);
                    message = "You're on the latest version (" + Program.VersionText + ").";
                }
                catch (Exception ex) { message = "Couldn't check for updates: " + ex.Message; }
                finally { Interlocked.Exchange(ref updateCheckRunning, 0); }

                try
                {
                    messages.BeginInvoke((Action)delegate
                    {
                        if (update != null)
                        {
                            // Reopen the way the user had it: window showing, or tray only.
                            Updater.Install(update, Application.ExecutablePath, form != null && form.Visible);
                            Application.Exit();
                        }
                        else if (manual)
                        {
                            tray.ShowBalloonTip(5000, Program.AppName, message, ToolTipIcon.Info);
                        }
                    });
                }
                catch (InvalidOperationException) { } // Window already gone during exit.
            });
        }

        public void SetState(string state)
        {
            light.Set(state);
            StartOverrideTimerIfNeeded();
            // Before reading Teams status back, so a Busy this app set isn't mistaken for the user's own.
            UpdateTeamsMessage();
            if (state == LightState.FollowTeams) HandBackToTeams();
            Refresh();
        }

        // Called when the override length changes: a running timer starts over with the new length.
        public void RestartOverrideTimer()
        {
            if (light.ReturnToTeamsAt == null) return;
            light.SetReturnToTeams(null);
            StartOverrideTimerIfNeeded();
            Refresh();
        }

        void StartOverrideTimerIfNeeded()
        {
            if (AppSettings.OverrideTimesOut && LightController.OverridesTeams(light.State) && light.ReturnToTeamsAt == null)
                light.SetReturnToTeams(DateTime.UtcNow.AddMinutes(AppSettings.OverrideMinutes));
        }

        // True while the user's own Red, Green or On A Call choice is overriding Teams status.
        public bool OverrideOn { get { return LightController.OverridesTeams(light.State); } }

        // e.g. "14:52", or null when no override timer is running.
        public string OverrideTimeLeft
        {
            get
            {
                var at = light.ReturnToTeamsAt;
                if (at == null || !AppSettings.OverrideTimesOut) return null;
                var left = at.Value - DateTime.UtcNow;
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                return (int)left.TotalMinutes + ":" + left.Seconds.ToString("00");
            }
        }

        // e.g. "Back to Teams status in 14:52", or null when no override timer is running.
        public string OverrideCountdown
        {
            get
            {
                string left = OverrideTimeLeft;
                return left == null ? null : "Back to Teams status in " + left;
            }
        }

        void UpdateTooltip()
        {
            string tip = Program.AppName + "\n" + StatusText;
            if (OverrideOn)
            {
                string left = OverrideTimeLeft;
                tip += "\nOverride on" + (left != null ? ", Teams in " + left : "");
            }
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        void CheckOverride()
        {
            var at = light.ReturnToTeamsAt;
            if (at == null || !AppSettings.OverrideTimesOut) return;
            if (DateTime.UtcNow >= at.Value)
            {
                string overridden = LightState.Describe(light.State);
                SetState(LightState.FollowTeams);
                ShowBalloon("The " + overridden + " override ran for " + AppSettings.OverrideMinutes +
                    " minutes, so the lights are following your Teams status again.", ToolTipIcon.Info);
                return;
            }
            UpdateTooltip();
            if (DisplayChanged != null) DisplayChanged();
        }

        // Signs in (browser) to connect to Teams, or signs out to disconnect.
        void TogglePresence()
        {
            if (presence.SignedIn)
            {
                Program.Background("Teams sign-out", delegate
                {
                    presence.SignOut();
                    try
                    {
                        messages.BeginInvoke((Action)delegate
                        {
                            // Back to Embrava Connect for Teams status, if it's installed.
                            teamsAvailability = null;
                            light.SetTeamsColor(null);
                            if (light.State == LightState.FollowTeams) HandBackToTeams();
                            Refresh();
                        });
                    }
                    catch (InvalidOperationException) { } // Window already gone during exit.
                });
                ShowBalloon("Disconnected from Teams. The app won't read or change your Teams status.", ToolTipIcon.Info);
                return;
            }
            presenceItem.Enabled = false;
            presenceItem.Text = "Signing in to Microsoft (check your browser)...";
            Program.Background("Teams sign-in", delegate
            {
                string message;
                ToolTipIcon icon = ToolTipIcon.Info;
                var signingIn = presence;
                try
                {
                    signingIn.SignIn();
                    message = "Connected to Teams as " + signingIn.Account + ". Follow Teams Status now uses your Teams status directly, and Teams shows you as Busy during other calls.";
                    if (callApp != null && !callApp.IsTeams && AppSettings.TeamsBusyOnCalls) signingIn.CallStarted();
                    PollTeams();
                }
                catch (Exception ex)
                {
                    // When the organization has no matching app, Microsoft shows its error in the browser and never
                    // comes back to the app, so the sign-in times out.
                    message = ex.Message + " If Microsoft said the application wasn't found, your organization's Teams app " +
                              "needs setting up: open Setup checklist from the tray menu.";
                    icon = ToolTipIcon.Warning;
                }
                try
                {
                    messages.BeginInvoke((Action)delegate
                    {
                        presenceItem.Enabled = true;
                        presenceItem.Text = "Connect to Microsoft Teams";
                        UpdateDisplay();
                    });
                }
                catch (InvalidOperationException) { } // Window already gone during exit.
                ShowBalloon(message, icon);
            });
        }

        // Safe to call from any thread.
        void ShowBalloon(string message, ToolTipIcon icon)
        {
            try { messages.BeginInvoke((Action)delegate { tray.ShowBalloonTip(5000, Program.AppName, message, icon); }); }
            catch (InvalidOperationException) { } // Window already gone during exit.
        }

        // Reads Teams presence and colors the light from it, when signed in and following Teams status.
        void PollTeams()
        {
            if (presence == null || !presence.SignedIn || light.State != LightState.FollowTeams) return;
            string availability = presence.ReadAvailability();
            if (availability == null) return;

            if (!embravaWarned && System.Diagnostics.Process.GetProcessesByName("EmbravaConnect").Length > 0)
            {
                embravaWarned = true;
                ShowBalloon("Embrava Connect is also running and may change the light. This app now shows your Teams status itself, so you can quit or uninstall Embrava Connect.", ToolTipIcon.Info);
            }
            try
            {
                messages.BeginInvoke((Action)delegate
                {
                    teamsAvailability = availability;
                    light.SetTeamsColor(ColorForAvailability(availability));
                    Refresh();
                });
            }
            catch (InvalidOperationException) { } // Window already gone during exit.
        }

        static string ColorForAvailability(string availability)
        {
            switch (availability)
            {
                case "Available": case "AvailableIdle": return "green";
                case "Busy": case "BusyIdle": return "red";
                case "DoNotDisturb": return "purple";
                case "Away": case "BeRightBack": return "yellow";
                default: return "off"; // Offline, PresenceUnknown
            }
        }

        static string DescribeAvailability(string availability)
        {
            switch (availability)
            {
                case "Available": case "AvailableIdle": return "Available";
                case "Busy": case "BusyIdle": return "Busy";
                case "DoNotDisturb": return "Do not disturb";
                case "Away": return "Away";
                case "BeRightBack": return "Be right back";
                case "Offline": return "Offline";
                default: return "Unknown";
            }
        }

        // Puts the light back on Teams status: read directly when signed in, otherwise through Embrava Connect
        // (restarted so it repaints from current Teams/Calendar presence).
        void HandBackToTeams()
        {
            if (presence != null && presence.SignedIn)
            {
                Program.Background("Teams status check", delegate { PollTeams(); });
                return;
            }
            Program.Background("Embrava Connect restart", delegate
            {
                bool installed;
                try { installed = EmbravaConnect.Restart(); }
                catch (Exception) { installed = false; }
                if (installed) return;
                try
                {
                    messages.BeginInvoke((Action)delegate
                    {
                        tray.ShowBalloonTip(5000, Program.AppName,
                            "Embrava Connect isn't installed, so nothing is showing your Teams status on the light.", ToolTipIcon.Warning);
                    });
                }
                catch (InvalidOperationException) { } // Window already gone during exit.
            });
        }

        void Refresh()
        {
            bool partying = State == LightState.Party && light.LightCount > 0;
            if (partying && !flashTimer.Enabled) partyHue = 0;
            flashTimer.Enabled = partying;
            UpdateDisplay();
            UpdateTeamsMessage();
        }

        // While the lights are red by the user's choice or because of a call, shows their chosen Teams status message.
        // Picking Busy or On a call also shows them as Busy in Teams, and Open as Available (except during a call).
        void UpdateTeamsMessage()
        {
            if (presence == null) return;
            bool picked = light.State == LightState.Red || light.State == LightState.CallFlash;
            bool red = picked || callApp != null;
            presence.ShowStatusMessage(red && AppSettings.TeamsStatusMessageOn ? AppSettings.TeamsStatusMessage : null);
            string status = null;
            if (AppSettings.TeamsBusyWhenPicked)
            {
                if (picked) status = "Busy";
                else if (light.State == LightState.Green && callApp == null) status = "Available";
            }
            presence.ShowPresence(status);
        }

        public string StatusText
        {
            get
            {
                if (light.LightCount == 0) return "Light not found";
                if (light.OverrideActive) return callApp == null ? "Testing the light" : "On a call (" + callApp.Name + ")";
                if (light.State == LightState.FollowTeams && teamsAvailability != null) return "Teams: " + DescribeAvailability(teamsAvailability);
                return LightState.Describe(State);
            }
        }

        void UpdateDisplay()
        {
            if (light.LightCount == 0) DisplayColor = Color.Gray;
            // A flashing call shows as red in the tray and window; flashing an icon would be distracting.
            else if (State == LightState.Red || State == LightState.CallFlash) DisplayColor = Color.Red;
            else if (State == LightState.Green) DisplayColor = Color.Lime;
            else if (State == LightState.Party)
            {
                byte r, g, b;
                BlyncDevice.HueToRgb(partyHue, out r, out g, out b);
                DisplayColor = Color.FromArgb(r, g, b);
            }
            // Teams status colors, when the app reads presence itself.
            else if (State == "yellow") DisplayColor = Color.Gold;
            else if (State == "purple") DisplayColor = Color.MediumPurple;
            // The real color is up to Embrava Connect; show Teams purple to mean "Teams decides".
            else if (State == LightState.FollowTeams) DisplayColor = Color.FromArgb(98, 100, 167);
            else DisplayColor = Color.Black;

            tray.Icon = IconFor(DisplayColor, OverrideOn);
            UpdateTooltip();
            // Check the user's own choice, even while a call is overriding it.
            foreach (var pair in stateItems) pair.Value.Checked = pair.Key == light.State;
            if (presenceItem != null)
            {
                presenceItem.Checked = presence != null && presence.SignedIn;
                presenceItem.ToolTipText = presence == null ? "Your organization's Teams app isn't set up yet. Click to open the setup checklist."
                    : presence.SignedIn ? "Connected as " + presence.Account + ". Click to disconnect."
                    : "Sign in to Microsoft so the light follows your Teams status and Teams shows Busy during other calls.";
            }
            NotifyIfChanged();
            if (DisplayChanged != null) DisplayChanged();
        }

        // Shows a notification when what the light shows changes (not for each Party Mode color, and not at startup).
        void NotifyIfChanged()
        {
            string key = light.LightCount == 0 ? "no light" : State + "|" + StatusText;
            if (key == lastNotifiedKey) return;
            bool first = lastNotifiedKey == null;
            lastNotifiedKey = key;
            if (first || !AppSettings.ShowNotifications) return;

            string message;
            if (light.LightCount == 0) message = "No light found. Plug your Blynclight or Luxafor back in.";
            else if (State == LightState.Party) message = "Testing the lights: cycling through every color.";
            else if (State == LightState.FollowTeams) message = "Light handed to Embrava Connect to show your Teams status.";
            else if (State == LightState.Off) message = "Light turned off.";
            else if (State == LightState.CallFlash) message = "Light flashing red and blue: " + StatusText + ".";
            else
            {
                string color = State; // "red", "green", "purple", "yellow"
                message = string.Equals(StatusText, color, StringComparison.OrdinalIgnoreCase)
                    ? "Light set to " + color + "."
                    : "Light set to " + color + ": " + StatusText + ".";
            }
            tray.ShowBalloonTip(3000, Program.AppName, message, ToolTipIcon.None);
        }

        // A filled circle of the light's color. While an override is on, a white pin badge sits in the corner.
        Icon IconFor(Color color, bool overrideOn)
        {
            string key = color.ToArgb() + (overrideOn ? "+override" : "");
            Icon icon;
            if (icons.TryGetValue(key, out icon)) return icon;
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var fill = new SolidBrush(color))
                using (var outline = new Pen(Color.FromArgb(60, 60, 60), 2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(fill, 3, 3, 26, 26);
                    g.DrawEllipse(outline, 3, 3, 26, 26);
                    if (overrideOn)
                    {
                        using (var badge = new SolidBrush(Color.White))
                        using (var badgeOutline = new Pen(Color.FromArgb(40, 40, 40), 2))
                        {
                            g.FillEllipse(badge, 18, 18, 13, 13);
                            g.DrawEllipse(badgeOutline, 18, 18, 13, 13);
                        }
                    }
                }
                icon = Icon.FromHandle(bmp.GetHicon());
            }
            icons[key] = icon;
            return icon;
        }

        protected override void ExitThreadCore()
        {
            for (int id = 0; id < HotkeyStates.Length; id++) UnregisterHotKey(messages.Handle, id);
            calls.Dispose();
            if (teamsPoll != null) teamsPoll.Dispose();
            if (presence != null) presence.Dispose(); // Clears Busy if a phone call is in progress.
            if (updateTimer != null) updateTimer.Dispose();
            showWait.Unregister(null);
            light.Dispose();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }
    }

}
