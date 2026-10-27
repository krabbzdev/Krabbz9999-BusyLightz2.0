using System;
using System.Drawing;
using System.Windows.Forms;

namespace Krabbz.Blynclight
{
    // All of the app's settings in one place. Changes take effect as soon as a box is ticked or unticked.
    class SettingsForm : Form
    {
        const int RefreshMs = 2000;

        readonly TrayContext tray;
        readonly CheckBox startWithWindows, openWindow;
        readonly Label teamsStatus = new Label { AutoSize = true, MaximumSize = new Size(300, 0) };
        readonly Button connectButton = new Button();
        readonly Timer refresh = new Timer { Interval = RefreshMs };
        int y = 16;

        public SettingsForm(TrayContext tray)
        {
            this.tray = tray;
            Text = "Settings - " + Program.AppName;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            const int width = 500;

            Heading("General");
            startWithWindows = Box("Start with Windows", AppSettings.StartWithWindows, v => AppSettings.StartWithWindows = v);
            openWindow = Box("Open the window at sign-in (untick to start in the tray only)", AppSettings.OpenWindowAtSignIn,
                v => AppSettings.OpenWindowAtSignIn = v, 24);
            startWithWindows.CheckedChanged += delegate { openWindow.Enabled = startWithWindows.Checked; };
            openWindow.Enabled = startWithWindows.Checked;
            Box("Show a notification when the lights change", AppSettings.ShowNotifications, v => AppSettings.ShowNotifications = v);
            Box("Hotkeys: Alt+R for Red, Alt+G for Green", AppSettings.HotkeysEnabled, v => { AppSettings.HotkeysEnabled = v; tray.ApplySettings(); });
            var updates = Box("Install updates automatically", AppSettings.AutoUpdate, v => AppSettings.AutoUpdate = v);
            if (Program.UpdateRepo != null) SideButton(updates, "Check now", delegate { tray.CheckForUpdates(true); });

            Heading("Calls");
            var red = Box("Turn lights red when my mic is in use", AppSettings.CallsTurnRed,
                v => { AppSettings.CallsTurnRed = v; tray.ApplySettings(); });
            SideButton(red, "Ignore apps...", delegate
            {
                using (var dialog = new IgnoreAppsDialog()) dialog.ShowDialog(this);
                tray.ApplySettings();
            });
            Box("Show me as Busy in Teams during calls in other apps", AppSettings.TeamsBusyOnCalls,
                v => { AppSettings.TeamsBusyOnCalls = v; tray.ApplySettings(); });

            Heading("Microsoft Teams");
            teamsStatus.Location = new Point(24, y + 6);
            Controls.Add(teamsStatus);
            connectButton.Bounds = new Rectangle(width - 150, y, 130, 32);
            connectButton.Click += delegate
            {
                if (tray.Presence != null && tray.Presence.SignedIn) tray.DisconnectTeams();
                else if (tray.Presence != null) tray.ConnectTeams();
                else ShowTeamsAppDialog();
                RefreshTeams();
            };
            Controls.Add(connectButton);
            y += 44;
            var orgApp = new Button { Text = "Organization's Teams app...", Bounds = new Rectangle(24, y, 220, 32) };
            orgApp.Click += delegate { ShowTeamsAppDialog(); };
            Controls.Add(orgApp);
            y += 52;

            var setup = new Button { Text = "Setup checklist...", Bounds = new Rectangle(20, y, 150, 34) };
            setup.Click += delegate { tray.ShowSetup(); };
            var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(width - 120, y, 100, 34) };
            close.Click += delegate { Close(); };
            Controls.Add(setup);
            Controls.Add(close);
            CancelButton = close;
            ClientSize = new Size(width, close.Bottom + 16);

            refresh.Tick += delegate { RefreshTeams(); };
            Shown += delegate { RefreshTeams(); refresh.Start(); };
            FormClosed += delegate { refresh.Dispose(); };
        }

        void ShowTeamsAppDialog()
        {
            using (var dialog = new TeamsAppDialog(tray)) dialog.ShowDialog(this);
            RefreshTeams();
        }

        void RefreshTeams()
        {
            var presence = tray.Presence;
            if (presence == null)
            {
                teamsStatus.Text = "Your organization's Teams app isn't set up yet.";
                connectButton.Text = "Set up...";
            }
            else if (presence.SignedIn)
            {
                teamsStatus.Text = "Connected as " + presence.Account + ".";
                connectButton.Text = "Disconnect";
            }
            else
            {
                teamsStatus.Text = "Not connected. Connect to show your Teams status on the lights.";
                connectButton.Text = "Connect";
            }
        }

        void Heading(string text)
        {
            if (y > 16) y += 10;
            Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), AutoSize = true, Location = new Point(16, y) });
            y += 30;
        }

        CheckBox Box(string text, bool value, Action<bool> save, int indent = 0)
        {
            var box = new CheckBox { Text = text, Checked = value, AutoSize = true, Location = new Point(24 + indent, y) };
            box.CheckedChanged += delegate { save(box.Checked); };
            Controls.Add(box);
            y += 30;
            return box;
        }

        void SideButton(CheckBox row, string text, EventHandler click)
        {
            var button = new Button { Text = text, Bounds = new Rectangle(350, row.Top - 4, 130, 30) };
            button.Click += click;
            Controls.Add(button);
            row.MaximumSize = new Size(button.Left - row.Left - 8, 0);
        }
    }
}
