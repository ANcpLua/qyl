using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Debug;

/// <summary>
///     Discovers JetBrains Rider MCP endpoints by parsing the IDE's log file.
///     Finds the built-in server port (SSE) and debugger MCP port (Streamable HTTP).
/// </summary>
internal sealed partial class JetBrainsDiscovery(
    TimeProvider timeProvider,
    ILogger<JetBrainsDiscovery> logger)
{
    private static readonly TimeSpan ScanCooldown = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private JetBrainsEndpoints? _cached;
    private DateTimeOffset _lastScan;

    /// <summary>
    ///     Returns the currently known Rider MCP endpoints, re-scanning the log if stale (>30s).
    /// </summary>
    public JetBrainsEndpoints? GetEndpoints()
    {
        lock (_lock)
        {
            if (_cached is not null && timeProvider.GetUtcNow() - _lastScan < ScanCooldown)
                return _cached;

            if (_cached is null && _lastScan != default && timeProvider.GetUtcNow() - _lastScan < ScanCooldown)
                return null;

            var endpoints = ScanLog();
            _lastScan = timeProvider.GetUtcNow();

            if (endpoints is not null)
            {
                _cached = endpoints;
                LogDiscovered(endpoints.BuiltInSseUrl, endpoints.DebuggerStreamableUrl);
            }
            else
            {
                LogNotFound();
            }

            return _cached;
        }
    }

    /// <summary>
    ///     Forces a fresh scan regardless of cooldown.
    /// </summary>
    public JetBrainsEndpoints? Refresh()
    {
        lock (_lock)
        {
            _cached = null;
            _lastScan = default;
        }

        return GetEndpoints();
    }

    /// <summary>
    ///     Maximum bytes to read from the tail of the log file.
    ///     256 KB covers several thousand lines — more than enough for recent session entries.
    /// </summary>
    private const int TailBytes = 256 * 1024;

    private JetBrainsEndpoints? ScanLog()
    {
        var logPath = FindLatestRiderLog();
        if (logPath is null || !File.Exists(logPath))
        {
            LogNoLogFile();
            return null;
        }

        string? builtInPort = null;
        string? debuggerUrl = null;

        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Seek to the tail of the file so we only read recent entries
            if (fs.Length > TailBytes)
                fs.Seek(-TailBytes, SeekOrigin.End);

            using var reader = new StreamReader(fs);

            // If we seeked mid-file, discard the first partial line
            if (fs.Position > 0)
                reader.ReadLine();

            // Scan forward so later entries win (latest Rider session)
            while (reader.ReadLine() is { } line)
            {
                if (line.Contains("built-in server started, port"))
                {
                    var match = BuiltInPortRegex().Match(line);
                    if (match.Success)
                        builtInPort = match.Groups[1].Value;
                }
                else if (line.Contains("MCP Server available at:"))
                {
                    var match = DebuggerUrlRegex().Match(line);
                    if (match.Success)
                        debuggerUrl = match.Groups[1].Value;
                }
            }
        }
        catch (IOException ex)
        {
            LogReadFailed(ex);
            return null;
        }

        if (builtInPort is null && debuggerUrl is null)
            return null;

        return new JetBrainsEndpoints(
            builtInPort is not null ? $"http://127.0.0.1:{builtInPort}/sse" : null,
            debuggerUrl);
    }

    private static string? FindLatestRiderLog()
    {
        var logDir = GetJetBrainsLogDir();
        if (logDir is null || !Directory.Exists(logDir))
            return null;

        // Find the latest Rider version directory (e.g., Rider2026.1)
        var riderDirs = Directory.GetDirectories(logDir, "Rider*")
            .OrderByDescending(static d => d)
            .ToArray();

        foreach (var dir in riderDirs)
        {
            var logPath = Path.Combine(dir, "idea.log");
            if (File.Exists(logPath))
                return logPath;
        }

        return null;
    }

    private static string? GetJetBrainsLogDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(home, "Library", "Logs", "JetBrains");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Path.Combine(home, ".cache", "JetBrains", "log");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JetBrains", "Logs");
        }

        return null;
    }

    [GeneratedRegex(@"built-in server started, port (\d+)")]
    private static partial Regex BuiltInPortRegex();

    [GeneratedRegex(@"MCP Server available at: (http://\S+)")]
    private static partial Regex DebuggerUrlRegex();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Discovered Rider endpoints — builtin: {BuiltIn}, debugger: {Debugger}")]
    private partial void LogDiscovered(string? builtIn, string? debugger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not discover Rider MCP endpoints from IDE log")]
    private partial void LogNotFound();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rider log not found at expected location")]
    private partial void LogNoLogFile();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to read Rider log")]
    private partial void LogReadFailed(Exception ex);
}

/// <summary>
///     Discovered JetBrains Rider MCP endpoint URLs.
/// </summary>
internal sealed record JetBrainsEndpoints(
    string? BuiltInSseUrl,
    string? DebuggerStreamableUrl);
