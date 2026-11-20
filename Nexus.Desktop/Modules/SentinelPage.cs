using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.Modules;

internal sealed class SentinelPage : UserControl
{
    private readonly SentinelScanner _scanner = new();
    private readonly TextBox _folderPath = new();
    private readonly ListView _results = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _scanButton;
    private readonly Button _cancelButton;
    private CancellationTokenSource? _scanCancellation;
    private SentinelScanSummary? _lastSummary;

    public SentinelPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var intro = new Panel { Dock = DockStyle.Fill };
        var title = Theme.Heading("Sentinel file audit", 20);
        title.Location = new Point(2, 0);
        var subtitle = Theme.MutedLabel("SHA-256 inventory, duplicate detection and file-type overview", 9);
        subtitle.Location = new Point(4, 36);
        intro.Controls.Add(title);
        intro.Controls.Add(subtitle);
        root.Controls.Add(intro, 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0)
        };

        _folderPath.Width = 390;
        _folderPath.Height = 36;
        _folderPath.Margin = new Padding(0, 2, 8, 0);
        _folderPath.BackColor = Theme.Surface;
        _folderPath.ForeColor = Theme.Text;
        _folderPath.BorderStyle = BorderStyle.FixedSingle;
        _folderPath.Font = new Font("Segoe UI", 9);
        _folderPath.PlaceholderText = "Choose a folder to inspect…";
        toolbar.Controls.Add(_folderPath);
        toolbar.Controls.Add(Theme.Button("Browse", false, (_, _) => BrowseFolder()));

        _scanButton = Theme.Button("Start audit", true, async (_, _) => await StartScanAsync());
        _scanButton.Width = 132;
        toolbar.Controls.Add(_scanButton);

        _cancelButton = Theme.Button("Cancel", false, (_, _) => _scanCancellation?.Cancel());
        _cancelButton.Width = 100;
        _cancelButton.Enabled = false;
        toolbar.Controls.Add(_cancelButton);

        var export = Theme.Button("Export CSV", false, (_, _) => ExportCsv());
        export.Width = 116;
        toolbar.Controls.Add(export);
        root.Controls.Add(toolbar, 0, 1);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12, 8, 12, 4) };
        _status.Text = "Choose a folder to create a local file-integrity inventory.";
        _status.ForeColor = Theme.Muted;
        _status.Font = new Font("Segoe UI", 8.5f);
        _status.AutoSize = true;
        _status.Location = new Point(12, 9);

        _progress.Width = 190;
        _progress.Height = 12;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Location = new Point(720, 9);
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        statusPanel.Controls.Add(_status);
        statusPanel.Controls.Add(_progress);
        root.Controls.Add(statusPanel, 0, 2);

        ConfigureResults();
        root.Controls.Add(_results, 0, 3);
    }

    private void ConfigureResults()
    {
        _results.Dock = DockStyle.Fill;
        _results.View = View.Details;
        _results.FullRowSelect = true;
        _results.MultiSelect = false;
        _results.HideSelection = false;
        _results.BorderStyle = BorderStyle.None;
        _results.BackColor = Theme.Surface;
        _results.ForeColor = Theme.Text;
        _results.Font = new Font("Segoe UI", 8.5f);
        _results.GridLines = false;

        _results.Columns.Add("FILE", 250);
        _results.Columns.Add("SIZE", 105);
        _results.Columns.Add("SHA-256", 390);
        _results.Columns.Add("TYPE", 145);
        _results.Columns.Add("AUDIT NOTE", 150);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder Sentinel should audit",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(_folderPath.Text) && Directory.Exists(_folderPath.Text))
            dialog.SelectedPath = _folderPath.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _folderPath.Text = dialog.SelectedPath;
    }

    private async Task StartScanAsync()
    {
        var root = _folderPath.Text.Trim().Trim('"');
        if (!Directory.Exists(root))
        {
            MessageBox.Show(this, "Choose an existing folder first.",
                "Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _scanCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        _lastSummary = null;
        _results.Items.Clear();
        _scanButton.Enabled = false;
        _cancelButton.Enabled = true;
        _progress.Style = ProgressBarStyle.Marquee;
        _status.Text = "Auditing files…";

        try
        {
            var progress = new Progress<SentinelScanProgress>(update =>
            {
                _status.Text = "Hashed " + update.FilesProcessed.ToString("N0") +
                               " files   •   Skipped " + update.FilesSkipped.ToString("N0") +
                               "   •   " + update.CurrentFile;
            });

            var summary = await _scanner.ScanAsync(root, progress, cancellation.Token);
            _lastSummary = summary;
            PopulateResults(summary);

            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 100;
            _status.Text = "Audit complete: " + summary.Files.Count.ToString("N0") +
                           " files hashed   •   " + summary.FilesSkipped.ToString("N0") +
                           " skipped   •   " + summary.DuplicateFiles.ToString("N0") +
                           " duplicate files";

            if (summary.ReachedFileLimit)
                _status.Text += "   •   File limit reached (" + SentinelScanner.MaximumFilesPerScan.ToString("N0") + ")";
        }
        catch (OperationCanceledException)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            _status.Text = "Audit cancelled. Partial results were discarded.";
            _results.Items.Clear();
        }
        catch (Exception ex)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            _status.Text = "Audit failed.";
            MessageBox.Show(this, ex.Message, "Sentinel audit failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _scanCancellation = null;
            cancellation.Dispose();
            _scanButton.Enabled = true;
            _cancelButton.Enabled = false;
        }
    }

    private void PopulateResults(SentinelScanSummary summary)
    {
        _results.BeginUpdate();
        _results.Items.Clear();

        foreach (var file in summary.Files)
        {
            var note = file.IsDuplicate ? "Duplicate hash" : "Inventoried";
            var item = new ListViewItem(file.FileName);
            item.SubItems.Add(FormatBytes(file.SizeBytes));
            item.SubItems.Add(file.Sha256);
            item.SubItems.Add(file.Category);
            item.SubItems.Add(note);
            item.ToolTipText = file.FullPath;
            item.Tag = file;

            if (file.IsDuplicate)
                item.ForeColor = Theme.Warning;

            _results.Items.Add(item);
        }

        _results.EndUpdate();
    }

    private void ExportCsv()
    {
        if (_lastSummary is null || _lastSummary.Files.Count == 0)
        {
            MessageBox.Show(this, "Run an audit before exporting results.",
                "Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export Sentinel audit",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = "Nexus-Sentinel-Audit.csv",
            AddExtension = true,
            DefaultExt = "csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("FullPath,FileName,SizeBytes,LastModifiedUtc,SHA256,Category,DuplicateHash");

            foreach (var file in _lastSummary.Files)
            {
                csv.Append(Csv(file.FullPath)).Append(',')
                    .Append(Csv(file.FileName)).Append(',')
                    .Append(file.SizeBytes).Append(',')
                    .Append(Csv(file.LastModifiedUtc.ToString("O"))).Append(',')
                    .Append(Csv(file.Sha256)).Append(',')
                    .Append(Csv(file.Category)).Append(',')
                    .Append(file.IsDuplicate ? "true" : "false").AppendLine();
            }

            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show(this, "Audit exported successfully.",
                "Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string FormatBytes(long bytes)
    {
        double value = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
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
        {
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
        }

        base.Dispose(disposing);
    }
}