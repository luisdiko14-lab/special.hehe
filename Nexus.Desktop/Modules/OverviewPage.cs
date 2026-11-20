using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.Modules;

internal sealed class OverviewPage : UserControl
{
    private readonly SystemMetricsService _metrics = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly MetricTile _cpu = new("PROCESSOR", "—", "Waiting for first sample", true);
    private readonly MetricTile _memory = new("PHYSICAL MEMORY", "—", "Total installed memory", true);
    private readonly MetricTile _storage = new("SYSTEM DRIVE", "—", "Used space", true);
    private readonly MetricTile _download = new("NETWORK DOWNLOAD", "—", "Current receive rate", false);
    private readonly MetricTile _upload = new("NETWORK UPLOAD", "—", "Current send rate", false);
    private readonly MetricTile _uptime = new("SYSTEM UPTIME", "—", "Time since Windows started", false);
    private readonly Label _systemInfo = new();

    public OverviewPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        Controls.Add(root);

        var intro = new Panel { Dock = DockStyle.Fill };
        var title = Theme.Heading("System overview", 20);
        title.Location = new Point(2, 0);
        var subtitle = Theme.MutedLabel("Live telemetry from Windows — refreshed every second", 9);
        subtitle.Location = new Point(4, 35);
        intro.Controls.Add(title);
        intro.Controls.Add(subtitle);
        root.Controls.Add(intro, 0, 0);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 8, 8)
        };
        cards.Controls.AddRange(new Control[] { _cpu, _memory, _storage, _download, _upload, _uptime });
        root.Controls.Add(cards, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(16, 12, 16, 8) };
        _systemInfo = Theme.MutedLabel("Detecting system information…", 9);
        _systemInfo.Location = new Point(16, 12);
        _systemInfo.MaximumSize = new Size(900, 0);
        footer.Controls.Add(_systemInfo);

        var taskManager = Theme.Button("Open Task Manager", true, (_, _) => OpenWindowsTool("taskmgr.exe"));
        taskManager.Width = 170;
        taskManager.Location = new Point(16, 38);
        var resourceMonitor = Theme.Button("Resource Monitor", false, (_, _) => OpenWindowsTool("resmon.exe"));
        resourceMonitor.Width = 170;
        resourceMonitor.Location = new Point(194, 38);
        footer.Controls.Add(taskManager);
        footer.Controls.Add(resourceMonitor);
        root.Controls.Add(footer, 0, 2);

        _timer.Tick += (_, _) => RefreshMetrics();
        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                RefreshMetrics();
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        };
    }

    private void RefreshMetrics()
    {
        try
        {
            var snapshot = _metrics.Read();
            _cpu.Set(
                snapshot.CpuPercent.ToString("0") + "%",
                "Total processor utilization",
                snapshot.CpuPercent);

            var ramPercent = snapshot.RamTotalBytes == 0
                ? 0
                : (double)snapshot.RamUsedBytes / snapshot.RamTotalBytes * 100;
            _memory.Set(
                FormatBytes(snapshot.RamUsedBytes) + " / " + FormatBytes(snapshot.RamTotalBytes),
                ramPercent.ToString("0") + "% used",
                ramPercent);

            var diskPercent = snapshot.DiskTotalBytes == 0
                ? 0
                : (double)snapshot.DiskUsedBytes / snapshot.DiskTotalBytes * 100;
            _storage.Set(
                FormatBytes(snapshot.DiskUsedBytes) + " / " + FormatBytes(snapshot.DiskTotalBytes),
                diskPercent.ToString("0") + "% used",
                diskPercent);

            _download.Set(FormatRate(snapshot.DownloadBytesPerSecond), "Live network receive rate");
            _upload.Set(FormatRate(snapshot.UploadBytesPerSecond), "Live network send rate");
            _uptime.Set(FormatDuration(snapshot.Uptime), "Time since Windows started");

            _systemInfo.Text = Environment.MachineName + "   •   " +
                               Environment.ProcessorCount + " logical processors   •   " +
                               snapshot.OperatingSystem;
        }
        catch (Exception ex)
        {
            _systemInfo.Text = "Metrics are temporarily unavailable: " + ex.Message;
        }
    }

    private static string FormatBytes(double value)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString("0.0") + " " + units[unit];
    }

    private static string FormatRate(double bytesPerSecond)
    {
        return FormatBytes(bytesPerSecond) + "/s";
    }

    private static string FormatDuration(TimeSpan time)
    {
        return ((int)time.TotalDays).ToString("00") + "d " +
               time.Hours.ToString("00") + "h " +
               time.Minutes.ToString("00") + "m";
    }

    private static void OpenWindowsTool(string executable)
    {
        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open " + executable + ": " + ex.Message,
                "Nexus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();

        base.Dispose(disposing);
    }
}

internal sealed class MetricTile : Panel
{
    private readonly Label _value;
    private readonly Label _detail;
    private readonly ProgressBar _bar;

    public MetricTile(string heading, string value, string detail, bool showBar)
    {
        Width = 330;
        Height = 154;
        BackColor = Theme.Surface;
        Margin = new Padding(0, 0, 14, 14);
        Padding = new Padding(18);

        var caption = Theme.MutedLabel(heading, 8.5f);
        caption.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        caption.Location = new Point(18, 16);

        _value = new Label
        {
            Text = value,
            AutoSize = false,
            Location = new Point(18, 43),
            Size = new Size(285, 40),
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 20, FontStyle.Bold)
        };

        _detail = Theme.MutedLabel(detail, 8.5f);
        _detail.Location = new Point(20, 91);

        Controls.Add(caption);
        Controls.Add(_value);
        Controls.Add(_detail);

        _bar = new ProgressBar
        {
            Location = new Point(18, 119),
            Size = new Size(285, 7),
            Style = ProgressBarStyle.Continuous,
            Visible = showBar,
            Maximum = 100
        };

        if (showBar)
            Controls.Add(_bar);
    }

    public void Set(string value, string detail, double? percentage = null)
    {
        _value.Text = value;
        _detail.Text = detail;

        if (_bar.Visible)
            _bar.Value = (int)Math.Clamp(percentage ?? 0, 0, 100);
    }
}