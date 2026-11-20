using System.Text.Json;
using Nexus.Models;

namespace Nexus.Services;

internal sealed class GameLibraryService
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nexus",
        "games.json");

    private readonly object _gate = new();
    private readonly List<GameEntry> _games;

    public GameLibraryService()
    {
        _games = LoadFromDisk();
    }

    public IReadOnlyList<GameEntry> GetGames()
    {
        lock (_gate)
            return _games.Select(Clone).ToList();
    }

    public bool Add(string name, string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected game executable no longer exists.", fullPath);

        lock (_gate)
        {
            if (_games.Any(game => string.Equals(game.ExecutablePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                return false;

            _games.Add(new GameEntry
            {
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fullPath) : name.Trim(),
                ExecutablePath = fullPath
            });
            SaveToDisk();
            return true;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            var game = _games.FirstOrDefault(item => item.Id == id);
            if (game is null)
                return false;

            _games.Remove(game);
            SaveToDisk();
            return true;
        }
    }

    public void MarkLaunched(Guid id)
    {
        lock (_gate)
        {
            var game = _games.FirstOrDefault(item => item.Id == id);
            if (game is null)
                return;

            game.LastPlayedAtUtc = DateTime.UtcNow;
            game.LaunchCount++;
            SaveToDisk();
        }
    }

    public void AddPlaytime(Guid id, long seconds)
    {
        if (seconds <= 0)
            return;

        lock (_gate)
        {
            var game = _games.FirstOrDefault(item => item.Id == id);
            if (game is null)
                return;

            game.TotalPlaySeconds += seconds;
            SaveToDisk();
        }
    }

    private List<GameEntry> LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new List<GameEntry>();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<GameEntry>>(json) ?? new List<GameEntry>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new List<GameEntry>();
        }
    }

    private void SaveToDisk()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static GameEntry Clone(GameEntry source)
    {
        return new GameEntry
        {
            Id = source.Id,
            Name = source.Name,
            ExecutablePath = source.ExecutablePath,
            Arguments = source.Arguments,
            AddedAtUtc = source.AddedAtUtc,
            LastPlayedAtUtc = source.LastPlayedAtUtc,
            TotalPlaySeconds = source.TotalPlaySeconds,
            LaunchCount = source.LaunchCount
        };
    }
}