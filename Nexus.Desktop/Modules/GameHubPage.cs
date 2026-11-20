using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.Modules;

internal sealed class GameHubPage : UserControl
{
    private readonly GameLibraryService _library = new();
    private readonly ListView _games = new();
    private readonly TextBox _search = new();
    private readonly Label _summary = new();
    private readonly System.Windows.Forms.Timer _sessionTimer = new() { Interval = 5000 };
    private readonly List<ActiveSession> _sessions = new();

    public GameHubPage()
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        Controls.Add(root);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };

        toolbar.Controls.Add(Theme.Button("+  Add game", true, (_, _) => AddGame()));
        toolbar.Controls.Add(Theme.Button("Play selected", false, (_, _) => LaunchSelected()));
        toolbar.Controls.Add(Theme.Button("Remove", false, (_, _) => RemoveSelected()));
        toolbar.Controls.Add(Theme.Button("Open folder", false, (_, _) => OpenSelectedFolder()));

        _search.Width = 220;
        _search.Height = 38;
        _search.Margin = new Padding(4, 1, 0, 0);
        _search.BackColor = Theme.Surface;
        _search.ForeColor = Theme.Text;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Font = new Font("Segoe UI", 9);
        _search.PlaceholderText = "Search your library…";
        _search.TextChanged += (_, _) => RefreshGames();
        toolbar.Controls.Add(_search);
        root.Controls.Add(toolbar, 0, 0);

        ConfigureList();
        root.Controls.Add(_games, 0, 1);

        _summary.Text = "Your library is stored locally on this PC.";
        _summary.AutoSize = true;
        _summary.ForeColor = Theme.Muted;
        _summary.Font = new Font("Segoe UI", 8.5f);
        _summary.Location = new Point(2, 8);
        root.Controls.Add(_summary, 0, 2);

        _games.DoubleClick += (_, _) => LaunchSelected();
        _sessionTimer.Tick += (_, _) => FlushSessions(false);
        _sessionTimer.Start();
        RefreshGames();
    }

    private void ConfigureList()
    {
        _games.Dock = DockStyle.Fill;
        _games.View = View.Details;
        _games.FullRowSelect = true;
        _games.MultiSelect = false;
        _games.HideSelection = false;
        _games.BorderStyle = BorderStyle.None;
        _games.BackColor = Theme.Surface;
        _games.ForeColor = Theme.Text;
        _games.Font = new Font("Segoe UI", 9.5f);
        _games.GridLines = false;

        _games.Columns.Add("GAME", 250);
        _games.Columns.Add("TOTAL PLAY TIME", 145);
        _games.Columns.Add("LAST PLAYED", 180);
        _games.Columns.Add("LAUNCHES", 95);
        _games.Columns.Add("LOCATION", 430);
    }

    private void RefreshGames()
    {
        var filter = _search.Text.Trim();
        _games.BeginUpdate();
        _games.Items.Clear();

        var entries = _library.GetGames()
            .Where(game => string.IsNullOrWhiteSpace(filter) ||
                           game.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                           game.ExecutablePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var game in entries)
        {
            var item = new ListViewItem(game.Name);
            item.SubItems.Add(FormatPlaytime(game.TotalPlaySeconds));
            item.SubItems.Add(game.LastPlayedAtUtc?.ToLocalTime().ToString("dd MMM yyyy, HH:mm") ?? "Never");
            item.SubItems.Add(game.LaunchCount.ToString());
            item.SubItems.Add(File.Exists(game.ExecutablePath) ? game.ExecutablePath : "Missing executable");
            item.Tag = game;

            if (!File.Exists(game.ExecutablePath))
                item.ForeColor = Theme.Warning;

            _games.Items.Add(item);
        }

        _games.EndUpdate();
        var count = _library.GetGames().Count;
        _summary.Text = count.ToString() + (count == 1 ? " game" : " games") +
                        " in your library   •   Double-click a game to launch it   •   Playtime is tracked while Nexus is open";
    }

    private GameEntry? SelectedGame()
    {
        return _games.SelectedItems.Count == 0 ? null : _games.SelectedItems[0].Tag as GameEntry;
    }

    private void AddGame()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add a game executable",
            Filter = "Windows applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var added = _library.Add(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName);
            if (!added)
            {
                MessageBox.Show(this, "This executable is already in your library.",
                    "GameHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            RefreshGames();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not add game",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LaunchSelected()
    {
        var game = SelectedGame();
        if (game is null)
        {
            MessageBox.Show(this, "Select a game in your library first.",
                "GameHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!File.Exists(game.ExecutablePath))
        {
            MessageBox.Show(this, "The executable could not be found. Remove this entry and add the game again.",
                "GameHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                Arguments = game.Arguments,
                WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });

            if (process is not null)
            {
                _library.MarkLaunched(game.Id);
                _sessions.Add(new ActiveSession(game.Id, process));
                RefreshGames();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Game launch failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveSelected()
    {
        var game = SelectedGame();
        if (game is null)
            return;

        var answer = MessageBox.Show(this,
            "Remove " + game.Name + " from your library? The game files will not be deleted.",
            "Remove game", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer == DialogResult.Yes)
        {
            _library.Remove(game.Id);
            RefreshGames();
        }
    }

    private void OpenSelectedFolder()
    {
        var game = SelectedGame();
        if (game is null)
            return;

        if (!File.Exists(game.ExecutablePath))
        {
            MessageBox.Show(this, "The executable path is no longer available.",
                "GameHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + game.ExecutablePath + "\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open folder",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FlushSessions(bool closeTracker)
    {
        foreach (var session in _sessions.ToArray())
        {
            var totalSeconds = (long)session.Clock.Elapsed.TotalSeconds;
            var pending = totalSeconds - session.CommittedSeconds;

            if (pending > 0)
            {
                _library.AddPlaytime(session.GameId, pending);
                session.CommittedSeconds = totalSeconds;
            }

            var exited = closeTracker;
            try
            {
                exited |= session.Process.HasExited;
            }
            catch
            {
                exited = true;
            }

            if (exited)
            {
                session.Clock.Stop();
                session.Process.Dispose();
                _sessions.Remove(session);
            }
        }

        if (!IsDisposed && IsHandleCreated)
            RefreshGames();
    }

    private static string FormatPlaytime(long seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalDays >= 1)
            return ((int)duration.TotalDays).ToString() + "d " + duration.Hours.ToString("00") + "h";

        return ((int)duration.TotalHours).ToString("00") + "h " + duration.Minutes.ToString("00") + "m";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionTimer.Stop();
            FlushSessions(true);
            _sessionTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class ActiveSession
    {
        public Guid GameId { get; }
        public Process Process { get; }
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public long CommittedSeconds { get; set; }

        public ActiveSession(Guid gameId, Process process)
        {
            GameId = gameId;
            Process = process;
        }
    }
}