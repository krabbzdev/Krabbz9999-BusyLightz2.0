using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Krabbz.Blynclight
{
    // Colors and drawing helpers shared by the main window's controls.
    static class Theme
    {
        public static readonly Color Page = Color.FromArgb(247, 247, 249);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(228, 228, 233);
        public static readonly Color BorderHover = Color.FromArgb(190, 190, 200);
        public static readonly Color Text = Color.FromArgb(27, 27, 31);
        public static readonly Color Muted = Color.FromArgb(107, 107, 118);
        public static readonly Color Red = Color.FromArgb(209, 52, 56);
        public static readonly Color Green = Color.FromArgb(16, 124, 16);
        public static readonly Color CallRed = Color.FromArgb(164, 38, 44);
        public static readonly Color CallBlue = Color.FromArgb(0, 120, 212);
        public static readonly Color Teams = Color.FromArgb(91, 95, 199);
        public static readonly Color TeamsTint = Color.FromArgb(238, 240, 251);
        public static readonly Color TeamsText = Color.FromArgb(59, 63, 143);
        public static readonly Color OverrideTint = Color.FromArgb(255, 244, 206);
        public static readonly Color OverrideText = Color.FromArgb(122, 90, 0);
        public static readonly Font Body = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Strong = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Status = new Font("Segoe UI Semibold", 16f);

        // The tray's display colors are pure web colors; the window shows softer versions of them.
        public static Color Soften(Color color)
        {
            if (color == Color.Red) return Red;
            if (color == Color.Lime) return Color.FromArgb(19, 161, 14);
            if (color == Color.Gold) return Color.FromArgb(234, 179, 8);
            if (color == Color.MediumPurple) return Color.FromArgb(135, 100, 184);
            if (color == Color.Black) return Color.FromArgb(59, 59, 66);
            if (color == Color.Gray) return Color.FromArgb(160, 160, 168);
            return color;
        }

        // color mixed toward white; amount 0 is the color, 1 is white.
        public static Color Tint(Color color, double amount)
        {
            return Color.FromArgb(
                (int)(color.R + (255 - color.R) * amount),
                (int)(color.G + (255 - color.G) * amount),
                (int)(color.B + (255 - color.B) * amount));
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            using (var path = Rounded(r, radius))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        // A small round pill with text, e.g. "Override on". Returns its width.
        public static int Pill(Graphics g, string text, int right, int y, Color fill, Color textColor)
        {
            var size = TextRenderer.MeasureText(g, text, Small, Size.Empty, TextFormatFlags.NoPadding);
            var r = new Rectangle(right - size.Width - 16, y, size.Width + 16, size.Height + 6);
            using (var path = Rounded(r, r.Height / 2))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, text, Small, r, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return r.Width;
        }
    }

    // Base for the window's owner-drawn parts: double-buffered and antialiased.
    class PaintedControl : Control
    {
        public PaintedControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            base.OnPaint(e);
        }
    }

    // The status card: a glowing dot in the light's color, what it means, and which lights are connected.
    class StatusCard : PaintedControl
    {
        public Color DotColor = Color.Gray;
        public string Status = "";
        public string Lights = "";
        public bool OverrideOn;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.FillRounded(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, Theme.Card, Theme.Border);

            var dot = Theme.Soften(DotColor);
            using (var halo = new SolidBrush(Theme.Tint(dot, 0.82)))
                g.FillEllipse(halo, 16, 14, 68, 68);
            using (var fill = new SolidBrush(dot))
                g.FillEllipse(fill, 28, 26, 44, 44);

            TextRenderer.DrawText(g, "Your lights are", Theme.Small, new Point(98, 16), Theme.Muted);
            TextRenderer.DrawText(g, Status, Theme.Status, new Rectangle(96, 32, Width - 104, 34), Theme.Text,
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Lights, Theme.Small, new Point(98, 66), Theme.Muted);

            if (OverrideOn) Theme.Pill(g, "Override on", Width - 12, 12, Theme.OverrideTint, Theme.OverrideText);
        }
    }

    // One line under the status card. While an override is on it shows how long is left and a link back to
    // Teams; otherwise a quiet tip. It always takes the same space so the buttons below never move.
    class OverrideStrip : PaintedControl
    {
        public string Message = "";
        public string Link;
        public bool Highlighted;
        public event Action LinkClicked;
        Rectangle linkBounds;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var text = Highlighted ? Theme.TeamsText : Theme.Muted;
            if (Highlighted) Theme.FillRounded(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, Theme.TeamsTint, Theme.TeamsTint);
            TextRenderer.DrawText(g, Message, Theme.Body, new Rectangle(12, 0, Width - 24, Height), text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            linkBounds = Rectangle.Empty;
            if (Link == null) return;
            using (var underline = new Font(Theme.Body, FontStyle.Underline))
            {
                var size = TextRenderer.MeasureText(g, Link, underline);
                linkBounds = new Rectangle(Width - size.Width - 8, 0, size.Width, Height);
                TextRenderer.DrawText(g, Link, underline, linkBounds, text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = linkBounds.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (linkBounds.Contains(e.Location) && LinkClicked != null) LinkClicked();
            base.OnMouseClick(e);
        }
    }

    // A mode button: a colored dot, a title and a hint. The picked mode fills with its color.
    class ModeTile : PaintedControl
    {
        public readonly string State;
        readonly string title, hint;
        readonly Color accent;
        bool active, hover;

        public ModeTile(string state, string title, string hint, Color accent)
        {
            State = state;
            this.title = title;
            this.hint = hint;
            this.accent = accent;
            AccessibleName = title;
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        public bool Active
        {
            get { return active; }
            set { if (active != value) { active = value; Invalidate(); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = active ? accent : hover ? Theme.Tint(accent, 0.94) : Theme.Card;
            Color border = active ? accent : hover ? Theme.BorderHover : Theme.Border;
            Theme.FillRounded(g, r, 8, fill, border);

            // On A Call flashes red and blue, so its dot is half of each.
            var dot = new Rectangle(14, 16, 12, 12);
            if (State == LightState.CallFlash && !active)
            {
                using (var red = new SolidBrush(Theme.CallRed)) g.FillPie(red, dot, 90, 180);
                using (var blue = new SolidBrush(Theme.CallBlue)) g.FillPie(blue, dot, 270, 180);
            }
            else using (var brush = new SolidBrush(active ? Color.White : accent)) g.FillEllipse(brush, dot);

            Color text = active ? Color.White : Theme.Text;
            Color sub = active ? Theme.Tint(accent, 0.75) : Theme.Muted;
            TextRenderer.DrawText(g, title, Theme.Strong, new Point(32, 10), text);
            TextRenderer.DrawText(g, hint, Theme.Small, new Point(33, 34), sub);

            if (Focused && ShowFocusCues)
                using (var pen = new Pen(active ? Color.White : accent) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, 4, 4, Width - 9, Height - 9);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyUp(e);
        }
    }

    // The main window: the current color, the four main modes, and the extras in the footer.
    // Closing it hides it; the tray app keeps running.
    class ControlForm : Form
    {
        const int WindowWidth = 400, Pad = 16;
        const string BackNowLink = "Back to Teams now";

        readonly TrayContext tray;
        readonly StatusCard status = new StatusCard();
        readonly OverrideStrip strip = new OverrideStrip();
        readonly ModeTile[] tiles;
        readonly LinkLabel testLink = new LinkLabel(), offLink = new LinkLabel();

        public ControlForm(TrayContext tray)
        {
            this.tray = tray;
            Text = Program.AppName;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.Body;
            BackColor = Theme.Page;
            DoubleBuffered = true;

            // Header: name, and Settings on the right.
            var header = new Panel { Bounds = new Rectangle(0, 0, WindowWidth, 52), BackColor = Theme.Card };
            header.Paint += (sender, e) =>
            {
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            header.Controls.Add(new Label { Text = "BusyLightz 2.0", Font = Theme.Strong, ForeColor = Theme.Text, AutoSize = true, Location = new Point(Pad, 9) });
            header.Controls.Add(new Label { Text = "by KrabbzDev", Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(Pad + 1, 29) });
            var settings = new Button
            {
                Text = "⚙  Settings",
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Theme.Small,
                Cursor = Cursors.Hand,
                Bounds = new Rectangle(WindowWidth - Pad - 92, 12, 92, 28),
            };
            settings.FlatAppearance.BorderColor = Theme.Border;
            settings.FlatAppearance.MouseOverBackColor = Theme.Page;
            settings.Click += delegate { tray.ShowSettings(); };
            header.Controls.Add(settings);
            Controls.Add(header);

            status.Bounds = new Rectangle(Pad, header.Bottom + 14, WindowWidth - 2 * Pad, 96);
            Controls.Add(status);

            strip.Bounds = new Rectangle(Pad, status.Bottom + 8, WindowWidth - 2 * Pad, 34);
            strip.LinkClicked += delegate { tray.SetState(LightState.FollowTeams); };
            Controls.Add(strip);

            tiles = new[]
            {
                new ModeTile(LightState.Red, "Busy", HintFor(LightState.Red, "Red"), Theme.Red),
                new ModeTile(LightState.Green, "Open", HintFor(LightState.Green, "Green"), Theme.Green),
                new ModeTile(LightState.CallFlash, "On a call", "Flashes red and blue", Theme.CallRed),
                new ModeTile(LightState.FollowTeams, "Follow Teams", "Automatic", Theme.Teams),
            };
            int tileWidth = (WindowWidth - 2 * Pad - 8) / 2;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                tile.Bounds = new Rectangle(Pad + (i % 2) * (tileWidth + 8), strip.Bottom + 10 + (i / 2) * 68, tileWidth, 60);
                tile.TabIndex = i;
                tile.Click += delegate { tray.SetState(tile.State); };
                Controls.Add(tile);
            }
            int tilesBottom = tiles[tiles.Length - 1].Bottom;

            // Footer: Test lights and Off, then version, links and the beta note.
            var footer = new Panel { Bounds = new Rectangle(0, tilesBottom + 16, WindowWidth, 92), BackColor = Theme.Card };
            footer.Paint += (sender, e) =>
            {
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.Pill(e.Graphics, "Beta", footer.Width - Pad, 12, Theme.OverrideTint, Theme.OverrideText);
            };
            FooterLink(footer, testLink, "Test lights", Pad, LightState.Party);
            FooterLink(footer, offLink, "Off", Pad + 120, LightState.Off);

            string versionText = "Version " + Program.VersionText + "  ·  Built " + Program.BuildDate;
            const string UpdatesLink = "Check for updates", ProjectLink = "Project page", HelpLink = "Help";
            if (Program.UpdateRepo != null) versionText += "\n" + UpdatesLink + "  ·  " + ProjectLink + "  ·  " + HelpLink;
            var version = new LinkLabel
            {
                Text = versionText,
                ForeColor = Theme.Muted,
                LinkColor = Theme.Muted,
                ActiveLinkColor = Theme.Text,
                Font = Theme.Small,
                AutoSize = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Location = new Point(Pad, 42),
            };
            version.Links.Clear();
            if (Program.UpdateRepo != null)
            {
                version.Links.Add(versionText.IndexOf(UpdatesLink), UpdatesLink.Length, "updates");
                version.Links.Add(versionText.IndexOf(ProjectLink), ProjectLink.Length, "project");
                version.Links.Add(versionText.LastIndexOf(HelpLink), HelpLink.Length, "help");
            }
            version.LinkClicked += (sender, e) =>
            {
                if ((string)e.Link.LinkData == "updates") tray.CheckForUpdates(true);
                else if ((string)e.Link.LinkData == "help") tray.OpenHelp();
                else tray.OpenProjectPage();
            };
            footer.Controls.Add(version);

            // The beta note and the close-to-tray hint live in a tooltip so the window stays clean.
            var tips = new ToolTip { AutoPopDelay = 15000 };
            tips.SetToolTip(footer, "This app replaces the Embrava Connect app. When you're on a call, your light turns red " +
                "to let others know. It's still in beta.\n\nClosing this window keeps the tray icon and lights running.");
            Controls.Add(footer);

            ClientSize = new Size(WindowWidth, footer.Bottom);

            tray.DisplayChanged += RefreshDisplay;
            RefreshDisplay();
        }

        static string HintFor(string state, string color)
        {
            string keys;
            return TrayContext.HotkeyText.TryGetValue(state, out keys) ? keys + "  ·  " + color : color;
        }

        void FooterLink(Panel footer, LinkLabel link, string text, int x, string state)
        {
            link.Text = text;
            link.Font = Theme.Body;
            link.AutoSize = true;
            link.LinkColor = Theme.Text;
            link.ActiveLinkColor = Theme.Teams;
            link.LinkBehavior = LinkBehavior.HoverUnderline;
            link.Location = new Point(x, 12);
            link.LinkClicked += delegate { tray.SetState(state); };
            footer.Controls.Add(link);
        }

        void RefreshDisplay()
        {
            string picked = tray.Light.State;
            status.DotColor = tray.DisplayColor;
            status.Status = tray.StatusText;
            status.Lights = "Lights: " + tray.Light.LightSummary;
            status.OverrideOn = tray.OverrideOn;
            status.Invalidate();

            string left = tray.OverrideTimeLeft;
            strip.Highlighted = tray.OverrideOn;
            if (!tray.OverrideOn)
            {
                strip.Message = picked == LightState.FollowTeams
                    ? "Following your Teams status automatically."
                    : HotkeyTip();
                strip.Link = null;
            }
            else
            {
                strip.Message = left != null ? "⏱  Back to Teams status in " + left : "Stays on until you change it";
                strip.Link = BackNowLink;
            }
            strip.Invalidate();

            foreach (var tile in tiles) tile.Active = tile.State == picked;
            testLink.LinkColor = picked == LightState.Party ? Theme.Teams : Theme.Text;
            offLink.LinkColor = picked == LightState.Off ? Theme.Teams : Theme.Text;
        }

        static string HotkeyTip()
        {
            return AppSettings.HotkeysEnabled ? "Tip: Alt+R and Alt+G work from any app." : "Pick a mode below.";
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
