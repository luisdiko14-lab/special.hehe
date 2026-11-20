namespace Nexus.Models;

internal sealed record SentinelScanResult(
    string FullPath,
    string FileName,
    long SizeBytes,
    DateTime LastModifiedUtc,
    string Sha256,
    string Category,
    bool IsDuplicate);

internal sealed record SentinelScanProgress(int FilesProcessed, int FilesSkipped, string CurrentFile);

internal sealed record SentinelScanSummary(
    IReadOnlyList<SentinelScanResult> Files,
    int FilesSkipped,
    int DuplicateFiles,
    bool ReachedFileLimit);