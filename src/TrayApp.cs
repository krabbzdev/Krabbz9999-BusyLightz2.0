using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Krabbz.Blynclight
{
    static class Program
    {
        public const string AppName = "Krabbz9999 Blynclight App Control";

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
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext(showSignal, !trayOnly, justUpdated));
            }
            return 0;
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
        // App the current call is in ("Cytracom" or "Teams"), or null when not on a call.
        string callApp;
        // Sets Teams to Busy during Cytracom calls; null when this build has no Entra app registration.
        readonly TeamsPresence presence;
        ToolStripMenuItem presenceItem;
        readonly System.Threading.Timer teamsPoll;
        // Last Teams availability read while signed in (e.g. "Available"), or null.
        string teamsAvailability;
        bool embravaWarned;
        readonly System.Threading.Timer updateTimer;
        int updateCheckRunning;
        readonly NotifyIcon tray;
        readonly MessageWindow messages = new MessageWindow();
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
        readonly RegisteredWaitHandle showWait;
        readonly Dictionary<Color, Icon> icons = new Dictionary<Color, Icon>();
        readonly Dictionary<string, ToolStripMenuItem> stateItems = new Dictionary<string, ToolStripMenuItem>();
        ControlForm form;
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
            AddStateItem(menu, "Red (Busy Mode)", LightState.Red);
            AddStateItem(menu, "Green (Open Mode)", LightState.Green);
            AddStateItem(menu, "Cycling Colors (PARTY MODE)", LightState.Party);
            AddStateItem(menu, "Off", LightState.Off);
            AddStateItem(menu, "Follow Teams Status", LightState.FollowTeams);
            menu.Items.Add(new ToolStripSeparator());
            if (Program.GraphClientId != null && Program.GraphTenantId != null)
            {
                presence = new TeamsPresence(Program.GraphClientId, Program.GraphTenantId, message => ShowBalloon(message, ToolTipIcon.Warning));
                presenceItem = new ToolStripMenuItem("Connect to Microsoft Teams", null, delegate { TogglePresence(); });
                menu.Items.Add(presenceItem);
                menu.Items.Add(new ToolStripSeparator());
            }
            menu.Items.Add("Version " + Program.VersionText).Enabled = false;
            if (Program.UpdateRepo != null) menu.Items.Add("Check for updates", null, delegate { CheckForUpdates(true); });
            menu.Items.Add("Exit tray app", null, delegate { Application.Exit(); });

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.Visible = true;

            // Cycle the tray icon round the color wheel at the light's speed, in half as many steps.
            flashTimer.Interval = LightController.PartyStepMs * 2;
            flashTimer.Tick += delegate { partyHue = (partyHue + LightController.PartyHueStep * 2) % 360; UpdateDisplay(); };

            RegisterHotkeys();
            Refresh();

            light.LightsChanged += delegate
            {
                try { messages.BeginInvoke((Action)Refresh); }
                catch (InvalidOperationException) { } // Window already gone during exit.
            };
            // Red while the Cytracom softphone or Teams has the microphone open, then back to the user's color
            // (or back to Embrava Connect when following Teams).
            calls = new CallDetector(app =>
            {
                try
                {
                    messages.BeginInvoke((Action)delegate
                    {
                        bool wasOverriding = light.OverrideActive;
                        // Teams already shows its own calls as Busy; only phone calls need presence set.
                        if (presence != null)
                        {
                            if (app == "Cytracom") presence.CallStarted();
                            else if (callApp == "Cytracom") presence.CallEnded();
                        }
                        callApp = app;
                        light.SetOverride(app != null ? LightState.Red : null);
                        if (app == null && wasOverriding && light.State == LightState.FollowTeams) HandBackToTeams();
                        Refresh();
                    });
                }
                catch (InvalidOperationException) { } // Window already gone during exit.
            });
            if (presence != null)
                teamsPoll = new System.Threading.Timer(delegate { PollTeams(); }, null, 0, TeamsPollMs);
            if (Program.UpdateRepo != null)
                updateTimer = new System.Threading.Timer(delegate { CheckForUpdates(false); }, null, UpdateFirstCheckMs, UpdateCheckMs);
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal,
                delegate { messages.BeginInvoke((Action)ShowWindow); }, null, -1, false);
            if (showWindow) ShowWindow();
            if (justUpdated)
                tray.ShowBalloonTip(5000, Program.AppName, "Updated to version " + Program.VersionText + ".", ToolTipIcon.Info);
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
            messages.HotkeyPressed += id => SetState(HotkeyStates[id]);
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
        void CheckForUpdates(bool manual)
        {
            if (Interlocked.Exchange(ref updateCheckRunning, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(delegate
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
            if (state == LightState.FollowTeams) HandBackToTeams();
            Refresh();
        }

        // Signs in (browser) to connect to Teams, or signs out to disconnect.
        void TogglePresence()
        {
            if (presence.SignedIn)
            {
                ThreadPool.QueueUserWorkItem(delegate
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
            ThreadPool.QueueUserWorkItem(delegate
            {
                string message;
                ToolTipIcon icon = ToolTipIcon.Info;
                try
                {
                    presence.SignIn();
                    message = "Connected to Teams as " + presence.Account + ". Follow Teams Status now uses your Teams status directly, and Teams shows you as Busy during phone calls.";
                    if (callApp == "Cytracom") presence.CallStarted();
                    PollTeams();
                }
                catch (Exception ex)
                {
                    message = ex.Message;
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
                ThreadPool.QueueUserWorkItem(delegate { PollTeams(); });
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
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
        }

        public string StatusText
        {
            get
            {
                if (light.LightCount == 0) return "Light not found";
                if (light.OverrideActive) return "On a " + callApp + " call (Busy)";
                if (light.State == LightState.FollowTeams && teamsAvailability != null) return "Teams: " + DescribeAvailability(teamsAvailability);
                return LightState.Describe(State);
            }
        }

        void UpdateDisplay()
        {
            if (light.LightCount == 0) DisplayColor = Color.Gray;
            else if (State == LightState.Red) DisplayColor = Color.Red;
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

            tray.Icon = IconFor(DisplayColor);
            string tip = Program.AppName + "\n" + StatusText;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            // Check the user's own choice, even while a call is overriding it.
            foreach (var pair in stateItems) pair.Value.Checked = pair.Key == light.State;
            if (presenceItem != null)
            {
                presenceItem.Checked = presence.SignedIn;
                presenceItem.ToolTipText = presence.SignedIn ? "Connected as " + presence.Account + ". Click to disconnect." : "Sign in to Microsoft so the light follows your Teams status and Teams shows Busy during phone calls.";
            }
            if (DisplayChanged != null) DisplayChanged();
        }

        Icon IconFor(Color color)
        {
            Icon icon;
            if (icons.TryGetValue(color, out icon)) return icon;
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var fill = new SolidBrush(color))
                using (var outline = new Pen(Color.FromArgb(60, 60, 60), 2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(fill, 3, 3, 26, 26);
                    g.DrawEllipse(outline, 3, 3, 26, 26);
                }
                icon = Icon.FromHandle(bmp.GetHicon());
            }
            icons[color] = icon;
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

    // The main window: current color plus buttons. Closing it hides it; the tray app keeps running.
    class ControlForm : Form
    {
        readonly TrayContext tray;
        readonly Panel swatch = new Panel();
        readonly Label statusLabel = new Label();

        public ControlForm(TrayContext tray)
        {
            this.tray = tray;
            Text = Program.AppName;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ClientSize = new Size(400, 350);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);

            swatch.SetBounds(20, 20, 60, 60);
            swatch.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(swatch);

            var caption = new Label { Text = "Current color", ForeColor = Color.Gray, AutoSize = true, Location = new Point(95, 24) };
            Controls.Add(caption);

            statusLabel.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(95, 46);
            Controls.Add(statusLabel);

            AddButton("Red", "(Busy Mode)", LightState.Red, 20, 100, 175, 54);
            AddButton("Green", "(Open Mode)", LightState.Green, 205, 100, 175, 54);
            AddButton("Cycling Colors", "PARTY MODE", LightState.Party, 20, 164, 360, 54);
            AddButton("Off", null, LightState.Off, 20, 228, 175, 40);
            AddButton("Follow Teams Status", null, LightState.FollowTeams, 205, 228, 175, 40);

            var betaNote = new Label
            {
                Text = "This app replaces the Embrava Connect App. When you are on a call, your light should turn red " +
                       "to let others know you are on a call. This is still BETA",
                ForeColor = Color.FromArgb(160, 90, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
                MaximumSize = new Size(360, 0), // Wrap within the window.
                Location = new Point(20, 280),
            };
            Controls.Add(betaNote);

            var hint = new Label
            {
                Text = "Closing this window keeps the tray icon and light running.",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(20, betaNote.Bottom + 8),
            };
            Controls.Add(hint);

            var version = new Label
            {
                Text = "Version " + Program.VersionText + "  ·  Built " + Program.BuildDate,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(20, hint.Bottom + 4),
            };
            Controls.Add(version);
            ClientSize = new Size(ClientSize.Width, version.Bottom + 12);

            tray.DisplayChanged += RefreshDisplay;
            RefreshDisplay();
        }

        // subtitle is shown on a second line under the button's main text.
        void AddButton(string text, string subtitle, string state, int x, int y, int width, int height)
        {
            string keys;
            if (TrayContext.HotkeyText.TryGetValue(state, out keys)) text += "  (" + keys + ")";
            if (subtitle != null) text += "\n" + subtitle;
            var button = new Button { Text = text, Bounds = new Rectangle(x, y, width, height) };
            button.Click += delegate { tray.SetState(state); };
            Controls.Add(button);
        }

        void RefreshDisplay()
        {
            swatch.BackColor = tray.DisplayColor;
            statusLabel.Text = tray.StatusText;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            tray.DisplayChanged -= RefreshDisplay;
            base.OnFormClosing(e);
        }
    }
}
