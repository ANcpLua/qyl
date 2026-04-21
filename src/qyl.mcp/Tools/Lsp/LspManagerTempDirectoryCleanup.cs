// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Prunes stale LSP workspace temp directories on startup. csharp-ls creates per-workspace
///     temp state under <c>$TMPDIR/csharp-ls-*</c>; left over from prior crashes, they can eat
///     gigabytes over time. Removes directories older than <see cref="MaxAge" />.
/// </summary>
internal sealed class LspManagerTempDirectoryCleanup(
    TimeProvider time,
    ILogger<LspManagerTempDirectoryCleanup> logger) : IHostedService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private static readonly string[] Prefixes = ["csharp-ls-", "typescript-language-server-"];

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var tempRoot = Path.GetTempPath();
        if (!Directory.Exists(tempRoot))
            return Task.CompletedTask;

        var cutoff = time.GetUtcNow() - MaxAge;
        foreach (var prefix in Prefixes)
        {
            foreach (var directory in Directory.EnumerateDirectories(tempRoot, prefix + "*",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryPrune(directory, cutoff);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void TryPrune(string directory, DateTimeOffset cutoff)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            if (info.LastWriteTimeUtc >= cutoff.UtcDateTime)
                return;

            info.Delete(true);
            logger.LogInformation("Pruned stale LSP temp dir {Directory}", directory);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Skipped LSP temp dir {Directory}: in use or locked", directory);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Skipped LSP temp dir {Directory}: access denied", directory);
        }
    }
}
