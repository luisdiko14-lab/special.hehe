using System.Drawing;
using System.Windows.Forms;
using Nexus.Modules;

namespace Nexus;

internal sealed class NexusForm : Form
{
    private readonly OverviewPage _overview = new();
    private readonly GameHubPage _gameHub = new();
    private readonly SentinelPage _sentinel = new();
    private readonly ToolsPage _tools = new();
    private readonly Panel _pageHost = new();
    private readonly Label _pageTitle = new();
    private readonly Label _pageSubtitle = new();
    private readonly Label _clock = new();
    private readonly Label _systemStatus = new();
    private readonly List<Button> _navigation = new();
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 15000 };

    public NexusForm()
    {
        Text = "Nexus — PC Command Center";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 700);
        Size = new Size(1280, 820);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        Navigate(_overview, "System overview", "Performance, memory, storage and network telemetry", _navigation[0]);
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Padding = new Padding(14)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Sidebar,
            Padding = new Padding(14)
        };
        shell.Controls.Add(sidebar, 0, 0);

        var brand = new Label
        {
            Text = "NEXUS",
            AutoSize = true,
            Location = new Point(17, 20),
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 25, FontStyle.Bold)
        };
        var brandSub = Theme.MutedLabel("PC COMMAND CENTER", 8.5f);
        brandSub.Location = new Point(20, 62);
        sidebar.Controls.Add(brand);
        sidebar.Controls.Add(brandSub);

        var workspace = Theme.MutedLabel("WORKSPACE", 8);
        workspace.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        workspace.Location = new Point(20, 118);
        sidebar.Controls.Add(workspace);

        var nav = new FlowLayoutPanel
        {
            Location = new Point(10, 146),
            Width = 190,
            Height = 310,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Sidebar
        };
        sidebar.Controls.Add(nav);

        _navigation.Add(CreateNavButton("◈   Overview", () =>
            Navigate(_overview, "System overview", "Performance, memory, storage and network telemetry", _navigation[0])));
        _navigation.Add(CreateNavButton("▦   GameHub", () =>
            Navigate(_gameHub, "GameHub", "Your games, shortcuts and playtime in one library", _navigation[1])));
        _navigation.Add(CreateNavButton("⬡   Sentinel", () =>
            Navigate(_sentinel, "Sentinel", "File integrity checks and folder audits", _navigation[2])));
        _navigation.Add(CreateNavButton("⚙   Toolbox", () =>
            Navigate(_tools, "Windows toolbox", "Process manager and everyday system utilities", _navigation[3])));

        foreach (var button in _navigation)
            nav.Controls.Add(button);

        var sidebarFooter = Theme.MutedLabel("NEXUS DESKTOP  •  v1.0.0", 8);
        sidebarFooter.Location = new Point(20, 0);
        sidebarFooter.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        sidebarFooter.Top = sidebar.ClientSize.Height - 34;
        sidebar.Resize += (_, _) => sidebarFooter.Top = sidebar.ClientSize.Height - 34;
        sidebar.Controls.Add(sidebarFooter);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(20, 4, 4, 4)
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(main, 1, 0);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _pageTitle.Text = "Nexus";
        _pageTitle.AutoSize = true;
        _pageTitle.Location = new Point(2, 3);
        _pageTitle.ForeColor = Theme.Text;
        _pageTitle.Font = new Font("Segoe UI", 21, FontStyle.Bold);

        _pageSubtitle.Text = "One desktop. Three powerful tools.";
        _pageSubtitle.AutoSize = true;
        _pageSubtitle.Location = new Point(4, 43);
        _pageSubtitle.ForeColor = Theme.Muted;
        _pageSubtitle.Font = new Font("Segoe UI", 9);

        _systemStatus.Text = "●  SYSTEM READY";
        _systemStatus.AutoSize = true;
        _systemStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _systemStatus.ForeColor = Theme.Good;
        _systemStatus.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        _systemStatus.Location = new Point(680, 12);

        _clock.AutoSize = true;
        _clock.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clock.ForeColor = Theme.Muted;
        _clock.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _clock.Location = new Point(680, 45);

        header.Controls.Add(_pageTitle);
        header.Controls.Add(_pageSubtitle);
        header.Controls.Add(_systemStatus);
        header.Controls.Add(_clock);
        header.Resize += (_, _) =>
        {
            _systemStatus.Left = Math.Max(10, header.ClientSize.Width - _systemStatus.Width - 10);
            _clock.Left = Math.Max(10, header.ClientSize.Width - _clock.Width - 10);
        };
        main.Controls.Add(header, 0, 0);

        _pageHost.Dock = DockStyle.Fill;
        _pageHost.BackColor = Theme.Background;
        main.Controls.Add(_pageHost, 0, 1);
    }

    private Button CreateNavButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 188,
            Height = 47,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Sidebar,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 7)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SurfaceRaised;
        button.Paint += (_, e) =>
        {
            if (button.BackColor == Theme.SurfaceRaised)
            {
                using var brush = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(brush, 0, 7, 3, button.Height - 14);
            }
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void Navigate(UserControl page, string title, string subtitle, Button selected)
    {
        foreach (var button in _navigation)
        {
            button.BackColor = button == selected ? Theme.SurfaceRaised : Theme.Sidebar;
            button.ForeColor = button == selected ? Theme.Text : Theme.Muted;
        }

        _pageTitle.Text = title;
        _pageSubtitle.Text = subtitle;
        _pageHost.Controls.Clear();
        page.Dock = DockStyle.Fill;
        _pageHost.Controls.Add(page);
    }

    private void UpdateClock()
    {
        _clock.Text = DateTime.Now.ToString("ddd, dd MMM   •   HH:mm");
        _clock.Left = Math.Max(10, _clock.Parent!.ClientSize.Width - _clock.Width - 10);
        _systemStatus.Left = Math.Max(10, _systemStatus.Parent!.ClientSize.Width - _systemStatus.Width - 10);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _clockTimer.Stop();
        _clockTimer.Dispose();
        _overview.Dispose();
        _gameHub.Dispose();
        _sentinel.Dispose();
        _tools.Dispose();
        base.OnFormClosed(e);
    }
}