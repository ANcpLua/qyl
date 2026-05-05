
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

internal sealed partial class LspManagerTempDirectoryCleanup(
    TimeProvider time,
    ILogger<LspManagerTempDirectoryCleanup> logger) : IHostedService
{
    private static readonly TimeSpan s_maxAge = TimeSpan.FromDays(7);
    private static readonly string[] s_prefixes = ["csharp-ls-", "typescript-language-server-"];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var tempRoot = Path.GetTempPath();
        if (!Directory.Exists(tempRoot))
            return Task.CompletedTask;

        var cutoff = time.GetUtcNow() - s_maxAge;
        foreach (var prefix in s_prefixes)
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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void TryPrune(string directory, DateTimeOffset cutoff)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            if (info.LastWriteTimeUtc >= cutoff.UtcDateTime)
                return;

            info.Delete(true);
            LogPrunedStaleDir(logger, directory);
        }
        catch (IOException ex)
        {
            LogSkippedDirIoLocked(logger, ex, directory);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogSkippedDirAccessDenied(logger, ex, directory);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pruned stale LSP temp dir {Directory}")]
    private static partial void LogPrunedStaleDir(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped LSP temp dir {Directory}: in use or locked")]
    private static partial void LogSkippedDirIoLocked(ILogger logger, Exception ex, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped LSP temp dir {Directory}: access denied")]
    private static partial void LogSkippedDirAccessDenied(ILogger logger, Exception ex, string directory);
}
