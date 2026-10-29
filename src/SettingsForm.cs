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
            AppearancePickers();
            startWithWindows = Box("Start with Windows", AppSettings.StartWithWindows, v => AppSettings.StartWithWindows = v);
            openWindow = Box("Open the window at sign-in (untick to start in the tray only)", AppSettings.OpenWindowAtSignIn,
                v => AppSettings.OpenWindowAtSignIn = v, 24);
            startWithWindows.CheckedChanged += delegate { openWindow.Enabled = startWithWindows.Checked; };
            openWindow.Enabled = startWithWindows.Checked;
            // Red, Green and On A Call override Teams status until this runs out.
            var timesOut = Box("Go back to Follow Teams Status after",
                AppSettings.OverrideTimesOut, v => { AppSettings.OverrideTimesOut = v; tray.ApplySettings(); });
            var minutes = OverrideMinutesPicker(timesOut);
            timesOut.CheckedChanged += delegate { minutes.Enabled = timesOut.Checked; };
            minutes.Enabled = timesOut.Checked;
            Box("Show a notification when the lights change", AppSettings.ShowNotifications, v => AppSettings.ShowNotifications = v);
            Box("Hotkeys: Alt+R for Busy, Alt+G for Available", AppSettings.HotkeysEnabled, v => { AppSettings.HotkeysEnabled = v; tray.ApplySettings(); });
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
            var flash = Box("Flash red and blue instead of steady red", AppSettings.FlashOnCalls,
                v => { AppSettings.FlashOnCalls = v; tray.ApplySettings(); }, 24);
            red.CheckedChanged += delegate { flash.Enabled = red.Checked; };
            flash.Enabled = red.Checked;
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
            y += 44;
            Box("Match my Teams status when I pick Busy, Available or On a call", AppSettings.TeamsBusyWhenPicked,
                v => { AppSettings.TeamsBusyWhenPicked = v; tray.ApplySettings(); });
            var messageOn = Box("Set my Teams status message while my lights are red:",
                AppSettings.TeamsStatusMessageOn, v => { AppSettings.TeamsStatusMessageOn = v; tray.ApplySettings(); });
            var message = StatusMessagePicker(24 + 24);
            messageOn.CheckedChanged += delegate { message.Enabled = messageOn.Checked; };
            message.Enabled = messageOn.Checked;
            y += 16;

            // Small and yellow, on its own line above the bottom buttons.
            var yellow = Color.FromArgb(255, 221, 0);
            var coffee = new Button
            {
                Text = "☕  Buy me a coffee",
                Tag = Theme.KeepColors,
                FlatStyle = FlatStyle.Flat,
                BackColor = yellow,
                ForeColor = Color.FromArgb(24, 24, 28),
                Font = Theme.Tiny,
                Cursor = Cursors.Hand,
            };
            coffee.FlatAppearance.BorderSize = 0;
            coffee.FlatAppearance.MouseOverBackColor = Theme.Mix(yellow, Color.Black, 0.08);
            coffee.Size = new Size(TextRenderer.MeasureText(coffee.Text, coffee.Font).Width + 8, 20);
            coffee.Location = new Point(width - 20 - coffee.Width, y);
            coffee.Click += delegate { tray.OpenCoffee(); };
            Controls.Add(coffee);
            y += 32;

            var setup = new Button { Text = "Setup checklist...", Bounds = new Rectangle(20, y, 150, 34) };
            setup.Click += delegate { tray.ShowSetup(); };
            var help = new Button { Text = "Help", Bounds = new Rectangle(180, y, 90, 34) };
            help.Click += delegate { tray.OpenHelp(); };
            Controls.Add(help);
            var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(width - 120, y, 100, 34) };
            close.Click += delegate { Close(); };
            Controls.Add(setup);
            Controls.Add(close);
            CancelButton = close;
            ClientSize = new Size(width, close.Bottom + 16);

            Load += delegate { Theme.Apply(this); };
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

        // The window style and Light or Dark colors. A change reopens the app's windows in the new look.
        void AppearancePickers()
        {
            Picker("Window style", WindowStyles.Names, Array.IndexOf(WindowStyles.All, AppSettings.WindowStyle),
                index => AppSettings.WindowStyle = WindowStyles.All[index]);
            Picker("Appearance", new[] { "Light", "Dark" }, AppSettings.DarkMode ? 1 : 0, index => AppSettings.DarkMode = index == 1);
        }

        void Picker(string label, string[] choices, int current, Action<int> save)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(24, y + 3) });
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(350, y, 130, 30) };
            picker.Items.AddRange(choices);
            picker.SelectedIndex = current;
            picker.SelectedIndexChanged += delegate
            {
                save(picker.SelectedIndex);
                tray.ApplyTheme();
            };
            Controls.Add(picker);
            y += 36;
        }

        // How long an override lasts, next to its checkbox. A change restarts a running timer with the new length.
        ComboBox OverrideMinutesPicker(CheckBox row)
        {
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(350, row.Top - 3, 130, 30) };
            int current = AppSettings.OverrideMinutes;
            if (Array.IndexOf(AppSettings.OverrideMinuteChoices, current) < 0) current = AppSettings.DefaultOverrideMinutes;
            foreach (int choice in AppSettings.OverrideMinuteChoices)
            {
                picker.Items.Add(DescribeMinutes(choice));
                if (choice == current) picker.SelectedIndex = picker.Items.Count - 1;
            }
            picker.SelectedIndexChanged += delegate
            {
                AppSettings.OverrideMinutes = AppSettings.OverrideMinuteChoices[picker.SelectedIndex];
                tray.RestartOverrideTimer();
            };
            Controls.Add(picker);
            row.MaximumSize = new Size(picker.Left - row.Left - 8, 0);
            return picker;
        }

        // The status message choices, plus "Custom message" with a box to type one. Returns a panel holding both.
        Panel StatusMessagePicker(int x)
        {
            const string Custom = "Custom message...";
            var panel = new Panel { Bounds = new Rectangle(x, y, 480 - x, 32) };
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(0, 0, 230, 30) };
            var custom = new TextBox { Bounds = new Rectangle(238, 1, panel.Width - 238, 28), MaxLength = AppSettings.TeamsStatusMessageMaxLength };
            picker.Items.AddRange(AppSettings.TeamsStatusMessageChoices);
            picker.Items.Add(Custom);

            string saved = AppSettings.TeamsStatusMessage;
            int index = Array.IndexOf(AppSettings.TeamsStatusMessageChoices, saved);
            picker.SelectedIndex = index >= 0 ? index : picker.Items.Count - 1;
            custom.Text = index >= 0 ? "" : saved;
            custom.Visible = index < 0;

            picker.SelectedIndexChanged += delegate
            {
                bool isCustom = (string)picker.SelectedItem == Custom;
                custom.Visible = isCustom;
                if (isCustom) { custom.Focus(); SaveCustom(custom); }
                else
                {
                    AppSettings.TeamsStatusMessage = (string)picker.SelectedItem;
                    tray.ApplySettings();
                }
            };
            // Saved as you type; Teams is updated once you leave the box.
            custom.TextChanged += delegate { if (custom.Text.Trim().Length > 0) AppSettings.TeamsStatusMessage = custom.Text.Trim(); };
            custom.Leave += delegate { SaveCustom(custom); };

            panel.Controls.Add(picker);
            panel.Controls.Add(custom);
            Controls.Add(panel);
            y += 36;
            return panel;
        }

        void SaveCustom(TextBox custom)
        {
            if (custom.Text.Trim().Length == 0) return; // Keeps the last message until something is typed.
            AppSettings.TeamsStatusMessage = custom.Text.Trim();
            tray.ApplySettings();
        }

        static string DescribeMinutes(int minutes)
        {
            if (minutes < 60) return minutes + " minutes";
            return minutes == 60 ? "1 hour" : minutes / 60 + " hours";
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
