namespace Nexus.Models;

internal sealed record SystemMetricsSnapshot(
    double CpuPercent,
    ulong RamUsedBytes,
    ulong RamTotalBytes,
    ulong DiskUsedBytes,
    ulong DiskTotalBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond,
    TimeSpan Uptime,
    string OperatingSystem);