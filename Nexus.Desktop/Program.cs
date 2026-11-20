using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Drawing;
using System.Windows.Forms;

namespace Nexus;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new NexusForm());
    }
}

sealed class NexusForm : Form
{
    readonly Color bg = Color.FromArgb(14, 17, 25), panel = Color.FromArgb(23, 28, 40);
    readonly Color accent = Color.FromArgb(93, 124, 255), muted = Color.FromArgb(153, 164, 184);
    readonly Label cpuValue = new(), ramValue = new(), diskValue = new(), uptimeValue = new();
    readonly ProgressBar ramBar = new(), cpuBar = new();
    readonly ListBox games = new(), scanResults = new();
    readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus");
    readonly string gamesFile;
    readonly System.Windows.Forms.Timer monitor = new();
    readonly PerformanceCounter? cpuCounter;
    readonly Stopwatch uptime = Stopwatch.StartNew();

    public NexusForm()
    {
        gamesFile = Path.Combine(dataDir, "games.json");
        Directory.CreateDirectory(dataDir);
        Text = "NEXUS — PC COMMAND CENTER";
        MinimumSize = new Size(1000, 680); Size = new Size(1180, 780);
        BackColor = bg; ForeColor = Color.White; Font = new Font("Segoe UI", 10);
        try { cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); cpuCounter.NextValue(); } catch { }
        BuildShell();
        LoadGames();
        monitor.Interval = 1200;
        monitor.Tick += (_, _) => RefreshStats();
        monitor.Start();
        RefreshStats();
    }

    void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg, Padding = new Padding(22) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        var header = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var brand = new Label { Text = "NEXUS", Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(0, 2) };
        var sub = new Label { Text = "PC COMMAND CENTER     /     MONITOR • PLAY • PROTECT", ForeColor = muted, AutoSize = true, Location = new Point(3, 51), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        header.Controls.Add(brand); header.Controls.Add(sub); root.Controls.Add(header, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
        tabs.TabPages.Add(MakeDashboard()); tabs.TabPages.Add(MakeGameHub()); tabs.TabPages.Add(MakeSentinel()); root.Controls.Add(tabs, 0, 1);
    }

    TabPage Page(string title)
    {
        var p = new TabPage(title) { BackColor = bg, ForeColor = Color.White, Padding = new Padding(16) };
        return p;
    }
    Label Heading(string text) => new() { Text = text, ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
    Label Small(string text) => new() { Text = text, ForeColor = muted, AutoSize = true, Font = new Font("Segoe UI", 9) };
    Panel Tile(string title, Label value, ProgressBar? bar = null)
    {
        var p = new Panel { BackColor = panel, Size = new Size(240, 135), Margin = new Padding(0, 0, 14, 14), Padding = new Padding(16) };
        var t = new Label { Text = title.ToUpperInvariant(), ForeColor = muted, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Location = new Point(16, 14) };
        value.Font = new Font("Segoe UI", 24, FontStyle.Bold); value.ForeColor = Color.White; value.Location = new Point(16, 43); value.AutoSize = true;
        p.Controls.Add(t); p.Controls.Add(value);
        if (bar != null) { bar.Location = new Point(16, 94); bar.Size = new Size(200, 9); bar.Style = ProgressBarStyle.Continuous; p.Controls.Add(bar); }
        return p;
    }
    TabPage MakeDashboard()
    {
        var page = Page("Overview");
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
        flow.Controls.Add(Heading("System overview")); flow.SetFlowBreak(flow.Controls[0], true);
        flow.Controls.Add(Tile("CPU usage", cpuValue, cpuBar)); flow.Controls.Add(Tile("Memory usage", ramValue, ramBar));
        flow.Controls.Add(Tile("System drive", diskValue)); flow.Controls.Add(Tile("Session uptime", uptimeValue));
        var note = new Label { Text = "Live metrics refresh automatically. Some counters may be unavailable on restricted Windows installations.", ForeColor = muted, AutoSize = true, Margin = new Padding(2, 8, 0, 0) };
        flow.Controls.Add(note); page.Controls.Add(flow); return page;
    }
    TabPage MakeGameHub()
    {
        var page = Page("GameHub");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = bg };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var left = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(16) };
        var title = Heading("Your game library"); title.Location = new Point(16, 14); left.Controls.Add(title);
        games.Location = new Point(16, 58); games.Size = new Size(560, 430); games.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        games.BackColor = Color.FromArgb(17, 21, 31); games.ForeColor = Color.White; games.BorderStyle = BorderStyle.None; games.Font = new Font("Segoe UI", 11);
        left.Controls.Add(games);
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, BackColor = bg, Padding = new Padding(18, 0, 0, 0), WrapContents = false };
        right.Controls.Add(Heading("Library tools"));
        right.Controls.Add(ActionButton("＋  Add game / executable", AddGame));
        right.Controls.Add(ActionButton("▶  Launch selected", LaunchGame));
        right.Controls.Add(ActionButton("－  Remove selected", RemoveGame));
        right.Controls.Add(Small("Games are stored locally in your Windows profile. Nexus does not download or modify game files."));
        layout.Controls.Add(left, 0, 0); layout.Controls.Add(right, 1, 0); page.Controls.Add(layout); return page;
    }
    TabPage MakeSentinel()
    {
        var page = Page("Sentinel");
        var layout = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var title = Heading("Sentinel security tools"); title.Location = new Point(8, 8); layout.Controls.Add(title);
        var info = Small("Run an on-demand file inventory and SHA-256 check. This is a basic inspection aid, not a replacement for Microsoft Defender or a certified malware scanner.");
        info.Location = new Point(10, 48); info.MaximumSize = new Size(900, 0); layout.Controls.Add(info);
        var scan = ActionButton("⌕  Scan a folder", ScanFolder); scan.Location = new Point(8, 86); layout.Controls.Add(scan);
        scanResults.Location = new Point(8, 145); scanResults.Size = new Size(950, 430); scanResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        scanResults.BackColor = Color.FromArgb(17, 21, 31); scanResults.ForeColor = Color.FromArgb(215, 224, 241); scanResults.BorderStyle = BorderStyle.None; scanResults.Font = new Font("Consolas", 9);
        layout.Controls.Add(scanResults); page.Controls.Add(layout); return page;
    }
    Button ActionButton(string text, Action action)
    {
        var b = new Button { Text = text, Width = 245, Height = 44, FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b;
    }
    void RefreshStats()
    {
        try { var cpu = cpuCounter?.NextValue() ?? 0; cpu = Math.Clamp(cpu, 0, 100); cpuValue.Text = cpu.ToString("0") + "%"; cpuBar.Value = (int)cpu; } catch { cpuValue.Text = "N/A"; }
        try { var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; var used = GC.GetTotalMemory(false); var pct = total > 0 ? Math.Clamp((double)used / total * 100, 0, 100) : 0; ramValue.Text = "App " + FormatBytes(used); ramBar.Value = (int)pct; } catch { ramValue.Text = "N/A"; }
        try { var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); diskValue.Text = $"{FormatBytes(d.AvailableFreeSpace)} free"; } catch { diskValue.Text = "N/A"; }
        uptimeValue.Text = uptime.Elapsed.ToString(@"d:hh:mm:ss");
    }
    static string FormatBytes(long bytes) { string[] units = ["B","KB","MB","GB","TB"]; double n = bytes; int i = 0; while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; } return $"{n:0.0} {units[i]}"; }
    void LoadGames()
    {
        try { if (File.Exists(gamesFile)) foreach (var path in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(gamesFile)) ?? []) if (File.Exists(path)) games.Items.Add(path); } catch { }
    }
    void SaveGames()
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(gamesFile, JsonSerializer.Serialize(games.Items.Cast<string>().ToList(), new JsonSerializerOptions { WriteIndented = true }));
    }
    void AddGame()
    {
        using var d = new OpenFileDialog { Title = "Select a game executable", Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*" };
        if (d.ShowDialog(this) == DialogResult.OK && !games.Items.Contains(d.FileName)) { games.Items.Add(d.FileName); SaveGames(); }
    }
    void LaunchGame()
    {
        if (games.SelectedItem is not string path) { MessageBox.Show(this, "Select a game first."); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void RemoveGame() { if (games.SelectedIndex >= 0) { games.Items.RemoveAt(games.SelectedIndex); SaveGames(); } }
    async void ScanFolder()
    {
        using var d = new FolderBrowserDialog { Description = "Choose a folder to inventory and hash" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        scanResults.Items.Clear(); scanResults.Items.Add("Starting SHA-256 inventory: " + d.SelectedPath);
        var root = d.SelectedPath;
        try
        {
            var files = await Task.Run(() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Take(5000).ToList());
            scanResults.Items.Add($"Files found (maximum 5,000): {files.Count}");
            foreach (var file in files)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
                    scanResults.Items.Add($"{hash[..16]}…  {file}");
                }
                catch (Exception ex) { scanResults.Items.Add($"[SKIPPED] {file} — {ex.Message}"); }
                if (scanResults.Items.Count % 25 == 0) { scanResults.TopIndex = scanResults.Items.Count - 1; await Task.Yield(); }
            }
            scanResults.Items.Add("Inventory complete. Hashes identify file contents; they do not determine whether a file is malicious.");
        }
        catch (Exception ex) { scanResults.Items.Add("Scan error: " + ex.Message); }
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { monitor.Stop(); cpuCounter?.Dispose(); base.OnFormClosed(e); }
}
