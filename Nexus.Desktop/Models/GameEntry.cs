namespace Nexus.Models;

internal sealed class GameEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedAtUtc { get; set; }
    public long TotalPlaySeconds { get; set; }
    public int LaunchCount { get; set; }
}