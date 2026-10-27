using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Krabbz.Blynclight
{
    // Setup checklist: shows each step with a status mark and a button to fix it. Shown once on first launch,
    // and any time from the tray menu. Re-checks every couple of seconds while open.
    class SetupForm : Form
    {
        const int RecheckMs = 2000;

        enum Mark { Done, Todo, Info }

        class Step
        {
            public Label MarkLabel, Title, Detail;
            public Button Action;
        }

        readonly TrayContext tray;
        readonly Step lightStep, orgStep, teamsStep, embravaStep, followStep, callsStep;
        readonly Timer recheck = new Timer { Interval = RecheckMs };

        public SetupForm(TrayContext tray)
        {
            this.tray = tray;
            Text = "Set up " + Program.AppName;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            ClientSize = new Size(560, 100);

            Controls.Add(new Label
            {
                Text = "Follow these steps once. A green check means the step is done.",
                AutoSize = true,
                Location = new Point(20, 18),
            });

            int y = 54;
            lightStep = AddStep(ref y, "Light plugged in", "Check again", delegate { Recheck(); });
            orgStep = AddStep(ref y, "Your organization's Teams app", "Set up...", delegate
            {
                using (var dialog = new TeamsAppDialog(tray)) dialog.ShowDialog(this);
                Recheck();
            });
            teamsStep = AddStep(ref y, "Connect to Microsoft Teams", "Connect", delegate { tray.ConnectTeams(); });
            embravaStep = AddStep(ref y, "Close Embrava Connect", "Quit it", delegate { EmbravaConnect.Quit(); Recheck(); });
            followStep = AddStep(ref y, "Light follows your Teams status", "Turn on", delegate { tray.SetState(LightState.FollowTeams); Recheck(); });
            callsStep = AddStep(ref y, "Red while your microphone is in use", "Ignore apps...", delegate
            {
                using (var dialog = new IgnoreAppsDialog()) dialog.ShowDialog(this);
                Recheck();
            });

            var test = new Button { Text = "Test the light", Bounds = new Rectangle(20, y + 6, 130, 34) };
            test.Click += delegate { tray.TestLight(); };
            Controls.Add(test);
            var done = new Button { Text = "Done", Bounds = new Rectangle(440, y + 6, 100, 34) };
            done.Click += delegate { Close(); };
            Controls.Add(done);
            AcceptButton = done;
            ClientSize = new Size(560, done.Bottom + 16);

            recheck.Tick += delegate { Recheck(); };
            Shown += delegate { Recheck(); recheck.Start(); };
            FormClosed += delegate { recheck.Dispose(); };
        }

        Step AddStep(ref int y, string title, string actionText, EventHandler action)
        {
            var step = new Step
            {
                MarkLabel = new Label { Font = new Font("Segoe UI", 15, FontStyle.Bold), Bounds = new Rectangle(16, y, 34, 34), TextAlign = ContentAlignment.MiddleCenter },
                Title = new Label { Text = title, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Location = new Point(56, y) },
                Detail = new Label { ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9), AutoSize = true, MaximumSize = new Size(350, 0), Location = new Point(56, y + 22) },
                Action = new Button { Text = actionText, Bounds = new Rectangle(420, y + 4, 120, 32) },
            };
            step.Action.Click += action;
            Controls.Add(step.MarkLabel);
            Controls.Add(step.Title);
            Controls.Add(step.Detail);
            Controls.Add(step.Action);
            y += 80; // Room for a detail that wraps to three lines.
            return step;
        }

        static void Show(Step step, Mark mark, string detail, bool showAction)
        {
            step.MarkLabel.Text = mark == Mark.Done ? "✓" : mark == Mark.Todo ? "!" : "–";
            step.MarkLabel.ForeColor = mark == Mark.Done ? Color.SeaGreen : mark == Mark.Todo ? Color.DarkOrange : Color.Gray;
            step.Detail.Text = detail;
            step.Action.Visible = showAction;
        }

        void Recheck()
        {
            var light = tray.Light;
            bool lightFound = light.LightCount > 0;
            Show(lightStep, lightFound ? Mark.Done : Mark.Todo,
                lightFound ? "Found: " + light.LightSummary + "."
                           : "No Blynclight or Luxafor Flag found. Plug one in (with a data cable, not charge-only), then click Check again.", !lightFound);

            if (TeamsAppConfig.UsingOwnApp)
                Show(orgStep, Mark.Done, "Using your organization's own app registration.", true);
            else if (TeamsAppConfig.BuiltInCoversThisPc)
                Show(orgStep, Mark.Done, "Already set up: this PC belongs to the organization the app is registered with.", true);
            else if (TeamsAppConfig.Configured && TeamsAppConfig.PcTenantId == null)
                Show(orgStep, Mark.Info, "Using the built-in app registration. If Connect fails, your IT admin needs to set one up for your organization.", true);
            else
                Show(orgStep, Mark.Todo, "Your IT admin needs to set this up once for your organization (about 3 minutes).", true);
            orgStep.Action.Text = orgStep.MarkLabel.Text == "✓" ? "Change..." : "Set up...";

            var presence = tray.Presence;
            bool teamsConnected = presence != null && presence.SignedIn;
            if (presence == null)
                Show(teamsStep, Mark.Info, "Set up your organization's Teams app first (the step above).", false);
            else
                Show(teamsStep, teamsConnected ? Mark.Done : Mark.Todo,
                    teamsConnected ? "Connected as " + presence.Account + "." : "Sign in with your work account so the light can show your Teams status.", !teamsConnected);

            bool embravaRunning = EmbravaConnect.IsRunning;
            if (!teamsConnected)
                Show(embravaStep, Mark.Info, "Keep Embrava Connect until you've connected to Teams; it's what shows your Teams status until then.", false);
            else if (embravaRunning)
                Show(embravaStep, Mark.Todo, "It's running and will fight this app over the light. Quit it, then uninstall it from Settings > Apps so it doesn't start again.", true);
            else
                Show(embravaStep, Mark.Done, EmbravaConnect.IsInstalled
                    ? "Not running. Uninstall it from Settings > Apps so it doesn't start again at sign-in."
                    : "Not installed.", false);

            bool following = light.State == LightState.FollowTeams;
            Show(followStep, following ? Mark.Done : Mark.Todo,
                following ? "On. Red or Green from the tray menu overrides it until you choose Follow Teams Status again."
                          : "Off: the light stays on the color you picked.", !following);

            int ignored = CallDetector.IgnoredIds().Count;
            Show(callsStep, Mark.Done,
                "Any app using your microphone (Teams, Zoom, softphones and so on) turns the light red. " +
                (ignored == 0 ? "No apps ignored." : ignored + (ignored == 1 ? " app ignored." : " apps ignored.")), true);
        }
    }

    // Sets which Entra app registration "Connect to Microsoft Teams" uses: instructions for the IT admin who creates
    // it, and boxes for the two IDs it produces.
    class TeamsAppDialog : Form
    {
        const string EntraAppsUrl = "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade";

        const string AdminInstructions =
            "Please create a Microsoft Entra app registration for Krabbz9999 Blynclight App Control (about 3 minutes):\r\n\r\n" +
            "1. Entra admin center > App registrations > New registration.\r\n" +
            "2. Name: Krabbz9999 Blynclight. Supported account types: this organizational directory only (single tenant).\r\n" +
            "3. Redirect URI: platform \"Public client/native (mobile & desktop)\", value http://localhost\r\n" +
            "4. API permissions > Add a permission > Microsoft Graph > Delegated > Presence.ReadWrite, then \"Grant admin consent\".\r\n" +
            "5. No client secret or certificate is needed. Leave \"Allow public client flows\" set to No.\r\n" +
            "6. If an app named Krabbz9999 Blynclight already exists, use that one instead of creating another.\r\n\r\n" +
            "Then send me the Application (client) ID and Directory (tenant) ID from the app's Overview page.\r\n" +
            "To deploy to everyone, add TEAMSCLIENTID=<client ID> TEAMSTENANTID=<tenant ID> to the installer's command line.\r\n\r\n" +
            "Script alternative (creates or reuses the registration): scripts/New-EntraAppRegistration.ps1 in " +
            "https://github.com/showtechclove1/Krabbz9999-Blynclight";

        readonly TrayContext tray;
        readonly TextBox clientBox = new TextBox();
        readonly TextBox tenantBox = new TextBox();
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(500, 0) };

        public TeamsAppDialog(TrayContext tray)
        {
            this.tray = tray;
            Text = "Your organization's Teams app";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);

            var intro = new Label
            {
                Text = "To read your Teams status, the app needs an app registration in your organization's Microsoft 365. " +
                       "An IT admin creates it once (or reuses one that already exists), and everyone uses the same two IDs.",
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                Location = new Point(20, 16),
            };
            Controls.Add(intro);

            var copy = new Button { Text = "Copy admin instructions", Bounds = new Rectangle(20, intro.Bottom + 12, 200, 32) };
            copy.Click += delegate
            {
                Clipboard.SetText(AdminInstructions);
                status.ForeColor = Color.SeaGreen;
                status.Text = "Instructions copied. Paste them into an email or ticket for your IT admin.";
            };
            var open = new Button { Text = "Open Entra admin center", Bounds = new Rectangle(230, intro.Bottom + 12, 200, 32) };
            open.Click += delegate { Process.Start(EntraAppsUrl); };
            Controls.Add(copy);
            Controls.Add(open);

            int y = copy.Bottom + 18;
            Controls.Add(new Label { Text = "Application (client) ID", AutoSize = true, Location = new Point(20, y) });
            clientBox.Bounds = new Rectangle(20, y + 22, 500, 28);
            Controls.Add(clientBox);
            y = clientBox.Bottom + 10;
            Controls.Add(new Label { Text = "Directory (tenant) ID", AutoSize = true, Location = new Point(20, y) });
            tenantBox.Bounds = new Rectangle(20, y + 22, 500, 28);
            Controls.Add(tenantBox);

            status.Location = new Point(20, tenantBox.Bottom + 10);
            Controls.Add(status);

            // Prefill with what's in use, or the PC's own organization for the tenant.
            if (TeamsAppConfig.UsingOwnApp)
            {
                clientBox.Text = TeamsAppConfig.ClientId;
                tenantBox.Text = TeamsAppConfig.TenantId;
            }
            else if (TeamsAppConfig.PcTenantId != null)
            {
                tenantBox.Text = TeamsAppConfig.PcTenantId;
            }

            int buttonsY = tenantBox.Bottom + 56;
            var save = new Button { Text = "Save", Bounds = new Rectangle(316, buttonsY, 96, 32) };
            save.Click += delegate { Save(); };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(424, buttonsY, 96, 32) };
            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;

            if (TeamsAppConfig.UsingOwnApp && Program.GraphClientId != null)
            {
                var builtIn = new Button { Text = "Use built-in app", Bounds = new Rectangle(20, buttonsY, 150, 32) };
                builtIn.Click += delegate
                {
                    TeamsAppConfig.UseBuiltIn();
                    tray.ApplyTeamsApp();
                    DialogResult = DialogResult.OK;
                };
                Controls.Add(builtIn);
            }
            ClientSize = new Size(540, buttonsY + 48);
        }

        void Save()
        {
            string client = clientBox.Text.Trim(), tenant = tenantBox.Text.Trim();
            status.ForeColor = Color.Firebrick;
            if (!TeamsAppConfig.IsGuid(client) || !TeamsAppConfig.IsGuid(tenant))
            {
                status.Text = "Both IDs look like 8-4-4-4-12 groups of letters and numbers, e.g. 0213fb00-29af-4f12-a2f9-8b90447266d3.";
                return;
            }
            Cursor = Cursors.WaitCursor;
            bool? exists = TeamsAppConfig.TenantExists(tenant);
            Cursor = Cursors.Default;
            if (exists == false)
            {
                status.Text = "Microsoft doesn't recognize that Directory (tenant) ID. Check it on the app's Overview page.";
                return;
            }
            TeamsAppConfig.Save(client, tenant);
            tray.ApplyTeamsApp();
            DialogResult = DialogResult.OK;
        }
    }

    // Lets the user pick apps whose microphone use shouldn't turn the light red (recorders, dictation,
    // noise-cancelling tools that keep the mic open).
    class IgnoreAppsDialog : Form
    {
        readonly CheckedListBox list = new CheckedListBox { CheckOnClick = true, IntegralHeight = false };
        readonly List<MicApp> apps = CallDetector.AppsInRecord();

        public IgnoreAppsDialog()
        {
            Text = "Apps to ignore";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            ClientSize = new Size(460, 380);

            Controls.Add(new Label
            {
                Text = "Ticked apps won't turn the light red when they use your microphone. " +
                       "This lists every app Windows has seen use the microphone on this PC.",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Location = new Point(20, 16),
            });

            list.Bounds = new Rectangle(20, 70, 420, 250);
            var ignored = new HashSet<string>(CallDetector.IgnoredIds(), StringComparer.OrdinalIgnoreCase);
            foreach (var app in apps)
            {
                string text = app.Name + "   (" + (app.InUse ? "using the mic now" : "last used " + app.LastUsed.ToString("MMM d")) + ")";
                list.Items.Add(text, ignored.Contains(app.Id));
            }
            Controls.Add(list);

            var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Bounds = new Rectangle(240, 332, 96, 32) };
            save.Click += delegate { Save(ignored); };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(344, 332, 96, 32) };
            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        // Keeps ignored apps that aren't in the list right now (e.g. their record was cleared) ignored.
        void Save(HashSet<string> previouslyIgnored)
        {
            for (int i = 0; i < apps.Count; i++)
            {
                if (list.GetItemChecked(i)) previouslyIgnored.Add(apps[i].Id);
                else previouslyIgnored.Remove(apps[i].Id);
            }
            CallDetector.SetIgnoredIds(previouslyIgnored);
        }
    }
}
