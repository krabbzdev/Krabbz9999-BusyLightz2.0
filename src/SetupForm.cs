using System;
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
        readonly Step lightStep, teamsStep, embravaStep, followStep, callsStep;
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
            lightStep = AddStep(ref y, "Blynclight plugged in", "Check again", delegate { Recheck(); });
            teamsStep = AddStep(ref y, "Connect to Microsoft Teams", "Connect", delegate { tray.ConnectTeams(); });
            embravaStep = AddStep(ref y, "Close Embrava Connect", "Quit it", delegate { EmbravaConnect.Quit(); Recheck(); });
            followStep = AddStep(ref y, "Light follows your Teams status", "Turn on", delegate { tray.SetState(LightState.FollowTeams); Recheck(); });
            callsStep = AddStep(ref y, "Red during calls (Cytracom and Teams)", "Test the light", delegate { tray.TestLight(); });

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
                lightFound ? "Found." : "No Blynclight found. Plug it in, then click Check again.", !lightFound);

            var presence = tray.Presence;
            bool teamsConnected = presence != null && presence.SignedIn;
            if (presence == null)
                Show(teamsStep, Mark.Info, "Not available in this version yet. Embrava Connect keeps showing your Teams status for now.", false);
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

            bool cytracom = CallDetector.HasMicRecord("Cytracom");
            bool teams = CallDetector.HasMicRecord("Teams");
            string detail = "Cytracom: " + (cytracom ? "ready" : "not seen yet (make one call so Windows records it)") +
                            ".  Teams: " + (teams ? "ready" : "not seen yet (join one call or meeting)") + ".";
            Show(callsStep, cytracom && teams ? Mark.Done : Mark.Todo, detail, lightFound);
        }
    }
}
