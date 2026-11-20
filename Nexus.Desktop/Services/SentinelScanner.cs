using System.Security.Cryptography;
using Nexus.Models;

namespace Nexus.Services;

internal sealed class SentinelScanner
{
    public const int MaximumFilesPerScan = 12000;

    public async Task<SentinelScanSummary> ScanAsync(
        string rootDirectory,
        IProgress<SentinelScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException("The selected folder does not exist.");

        var pendingDirectories = new Stack<string>();
        var results = new List<SentinelScanResult>();
        pendingDirectories.Push(Path.GetFullPath(rootDirectory));
        var skipped = 0;
        var reachedLimit = false;

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pendingDirectories.Pop();

            try
            {
                foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var attributes = File.GetAttributes(childDirectory);
                        if ((attributes & FileAttributes.ReparsePoint) == 0)
                            pendingDirectories.Push(childDirectory);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        skipped++;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                skipped++;
            }

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (results.Count >= MaximumFilesPerScan)
                    {
                        reachedLimit = true;
                        break;
                    }

                    try
                    {
                        var info = new FileInfo(filePath);
                        var size = info.Length;
                        var modified = info.LastWriteTimeUtc;

                        await using var stream = new FileStream(
                            filePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            1024 * 128,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);

                        var digest = await SHA256.HashDataAsync(stream, cancellationToken);
                        var hash = Convert.ToHexString(digest).ToLowerInvariant();

                        results.Add(new SentinelScanResult(
                            filePath,
                            info.Name,
                            size,
                            modified,
                            hash,
                            DescribeFileType(info.Extension),
                            false));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                    {
                        skipped++;
                    }

                    progress?.Report(new SentinelScanProgress(results.Count, skipped, filePath));

                    if (results.Count > 0 && results.Count % 12 == 0)
                        await Task.Yield();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                skipped++;
            }

            if (reachedLimit)
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duplicateHashes = results
            .GroupBy(item => item.Sha256, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var finalResults = results
            .Select(item => item with { IsDuplicate = duplicateHashes.Contains(item.Sha256) })
            .OrderByDescending(item => item.IsDuplicate)
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicateFiles = finalResults.Count(item => item.IsDuplicate);
        return new SentinelScanSummary(finalResults, skipped, duplicateFiles, reachedLimit);
    }

    private static string DescribeFileType(string extension)
    {
        var value = extension.ToLowerInvariant();
        if (value is ".exe" or ".msi" or ".com")
            return "Windows application";
        if (value is ".dll" or ".sys" or ".ocx")
            return "System component";
        if (value is ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js" or ".wsf")
            return "Script";
        if (value is ".zip" or ".7z" or ".rar" or ".gz")
            return "Archive";

        return "File";
    }
}