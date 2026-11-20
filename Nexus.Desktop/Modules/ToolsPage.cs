using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Nexus.Modules;

internal sealed class ToolsPage : UserControl
{
    private readonly ListView _processes = new();
    private readonly TextBox _search = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 5000 };
    private bool _loading;

    public ToolsPage()
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);

        var top = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var title = Theme.Heading("Windows toolbox", 20);
        title.Location = new Point(2, 0);
        var subtitle = Theme.MutedLabel("A practical control panel for everyday Windows tasks", 9);
        subtitle.Location = new Point(4, 35);
        top.Controls.Add(title);
        top.Controls.Add(subtitle);

        var actions = new FlowLayoutPanel
        {
            Location = new Point(0, 62),
            Size = new Size(900, 43),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background
        };
        actions.Controls.Add(Theme.Button("Refresh processes", true, (_, _) => RefreshProcesses()));
        actions.Controls.Add(Theme.Button("End selected task", false, (_, _) => EndSelectedProcess()));
        actions.Controls.Add(Theme.Button("Open file location", false, (_, _) => OpenSelectedLocation()));
        top.Controls.Add(actions);
        root.Controls.Add(top, 0, 0);

        ConfigureProcessList();
        root.Controls.Add(_processes, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _status.AutoSize = true;
        _status.ForeColor = Theme.Muted;
        _status.Font = new Font("Segoe UI", 8.5f);
        _status.Location = new Point(2, 10);
        footer.Controls.Add(_status);
        root.Controls.Add(footer, 0, 2);

        _refreshTimer.Tick += (_, _) => RefreshProcesses();
        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                RefreshProcesses();
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
            }
        };
        RefreshProcesses();
    }

    private void ConfigureProcessList()
    {
        _processes.Dock = DockStyle.Fill;
        _processes.View = View.Details;
        _processes.FullRowSelect = true;
        _processes.MultiSelect = false;
        _processes.HideSelection = false;
        _processes.BorderStyle = BorderStyle.None;
        _processes.BackColor = Theme.Surface;
        _processes.ForeColor = Theme.Text;
        _processes.Font = new Font("Segoe UI", 9);
        _processes.GridLines = false;
        _processes.Columns.Add("PROCESS", 250);
        _processes.Columns.Add("PID", 90);
        _processes.Columns.Add("MEMORY", 130);
        _processes.Columns.Add("WINDOW", 300);
        _processes.Columns.Add("PATH", 420);
        _processes.DoubleClick += (_, _) => OpenSelectedLocation();
    }

    private void RefreshProcesses()
    {
        if (_loading || IsDisposed)
            return;

        _loading = true;
        try
        {
            var selectedPid = SelectedPid();
            var query = _search.Text.Trim();
            var rows = new List<ListViewItem>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var name = process.ProcessName;
                    var pid = process.Id;
                    var memory = process.WorkingSet64;
                    var window = process.MainWindowTitle;
                    var path = "";
                    try { path = process.MainModule?.FileName ?? ""; }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or System.Security.SecurityException) { }

                    if (query.Length > 0 &&
                        !name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                        !pid.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) &&
                        !window.Contains(query, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var item = new ListViewItem(name);
                    item.SubItems.Add(pid.ToString());
                    item.SubItems.Add(FormatBytes(memory));
                    item.SubItems.Add(window);
                    item.SubItems.Add(path);
                    item.Tag = pid;
                    if (pid == selectedPid)
                        item.Selected = true;
                    rows.Add(item);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or System.Security.SecurityException)
                {
                    // Processes can exit or deny access while the snapshot is being collected.
                }
                finally
                {
                    process.Dispose();
                }
            }

            rows.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));
            _processes.BeginUpdate();
            _processes.Items.Clear();
            _processes.Items.AddRange(rows.ToArray());
            _processes.EndUpdate();
            _status.Text = $"{rows.Count:N0} processes shown  •  Auto-refresh every 5 seconds  •  Some protected process details require administrator access";
        }
        finally
        {
            _loading = false;
        }
    }

    private int SelectedPid()
    {
        return _processes.SelectedItems.Count == 0 || _processes.SelectedItems[0].Tag is not int pid ? -1 : pid;
    }

    private void EndSelectedProcess()
    {
        var pid = SelectedPid();
        if (pid < 0)
        {
            MessageBox.Show(this, "Select a process first.", "Nexus Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var name = _processes.SelectedItems[0].Text;
        if (pid == Environment.ProcessId ||
            string.Equals(name, "System", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Nexus will not terminate itself or core system processes.", "Protected process",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this, $"End process '{name}' (PID {pid})? Unsaved work in that application may be lost.",
            "Confirm end task", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            process.WaitForExit(2000);
            RefreshProcesses();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or System.Security.SecurityException)
        {
            MessageBox.Show(this, "The process could not be ended. It may have exited or require elevated permissions.\n\n" + ex.Message,
                "End task failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSelectedLocation()
    {
        if (_processes.SelectedItems.Count == 0)
            return;

        var path = _processes.SelectedItems[0].SubItems[4].Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "The executable path is unavailable for this process.", "Nexus Toolbox",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open file location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string FormatBytes(long bytes)
    {
        double value = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString("0.0") + " " + units[unit];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _refreshTimer.Dispose();
        base.Dispose(disposing);
    }
}
