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

        readonly LightController light = new LightController();
        readonly System.Threading.Timer updateTimer;
        int updateCheckRunning;
        readonly NotifyIcon tray;
        readonly MessageWindow messages = new MessageWindow();
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
        readonly RegisteredWaitHandle showWait;
        readonly Dictionary<Color, Icon> icons = new Dictionary<Color, Icon>();
        readonly Dictionary<string, ToolStripMenuItem> stateItems = new Dictionary<string, ToolStripMenuItem>();
        ControlForm form;
        bool flashRed = true;

        public string State { get { return light.State; } }
        public Color DisplayColor { get; private set; }
        public event Action DisplayChanged;

        public TrayContext(EventWaitHandle showSignal, bool showWindow, bool justUpdated)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open " + Program.AppName, null, delegate { ShowWindow(); }).Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());
            AddStateItem(menu, "Red (Busy Mode)", LightState.Red);
            AddStateItem(menu, "Green (Open Mode)", LightState.Green);
            AddStateItem(menu, "Flashing Red / Blue (PANIC MODE)", LightState.FlashRedBlue);
            AddStateItem(menu, "Off", LightState.Off);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Version " + Program.VersionText).Enabled = false;
            if (Program.UpdateRepo != null) menu.Items.Add("Check for updates", null, delegate { CheckForUpdates(true); });
            menu.Items.Add("Exit tray app", null, delegate { Application.Exit(); });

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.Visible = true;

            // Swap the tray icon at the light's rate so it flashes along with the light.
            flashTimer.Interval = LightController.AlternateMs;
            flashTimer.Tick += delegate { flashRed = !flashRed; UpdateDisplay(); };

            RegisterHotkeys();
            Refresh();

            light.LightsChanged += delegate
            {
                try { messages.BeginInvoke((Action)Refresh); }
                catch (InvalidOperationException) { } // Window already gone during exit.
            };
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
            Refresh();
        }

        void Refresh()
        {
            bool flashing = State == LightState.FlashRedBlue && light.LightCount > 0;
            if (flashing && !flashTimer.Enabled) flashRed = true;
            flashTimer.Enabled = flashing;
            UpdateDisplay();
        }

        public string StatusText
        {
            get { return light.LightCount == 0 ? "Light not found" : LightState.Describe(State); }
        }

        void UpdateDisplay()
        {
            if (light.LightCount == 0) DisplayColor = Color.Gray;
            else if (State == LightState.Red) DisplayColor = Color.Red;
            else if (State == LightState.Green) DisplayColor = Color.Lime;
            else if (State == LightState.FlashRedBlue) DisplayColor = flashRed ? Color.Red : Color.Blue;
            else DisplayColor = Color.Black;

            tray.Icon = IconFor(DisplayColor);
            string tip = Program.AppName + "\n" + StatusText;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            foreach (var pair in stateItems) pair.Value.Checked = pair.Key == State;
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
            AddButton("Flashing Red / Blue", "PANIC MODE", LightState.FlashRedBlue, 20, 164, 360, 54);
            AddButton("Off", null, LightState.Off, 20, 228, 360, 40);

            var overrideNote = new Label
            {
                Text = "Your Teams and Calendar status will override this.",
                ForeColor = Color.FromArgb(160, 90, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 280),
            };
            Controls.Add(overrideNote);

            var hint = new Label
            {
                Text = "Closing this window keeps the tray icon and light running.",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(20, 304),
            };
            Controls.Add(hint);

            var version = new Label
            {
                Text = "Version " + Program.VersionText + "  ·  Built " + Program.BuildDate,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(20, 324),
            };
            Controls.Add(version);

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
