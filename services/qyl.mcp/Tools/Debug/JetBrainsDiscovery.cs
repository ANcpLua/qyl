using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Debug;

internal sealed partial class JetBrainsDiscovery(
    TimeProvider timeProvider,
    ILogger<JetBrainsDiscovery> logger)
{
    private static readonly TimeSpan s_scanCooldown = TimeSpan.FromSeconds(30);

    private JetBrainsEndpoints? _cached;
    private DateTimeOffset _lastScan;

    public JetBrainsEndpoints? GetEndpoints()
    {
        if (_cached is not null && timeProvider.GetUtcNow() - _lastScan < s_scanCooldown)
            return _cached;

        var endpoints = ScanLog();
        if (endpoints is not null)
        {
            _cached = endpoints;
            _lastScan = timeProvider.GetUtcNow();
            LogDiscovered(endpoints.BuiltInSseUrl, endpoints.DebuggerStreamableUrl);
        }
        else
        {
            LogNotFound();
        }

        return _cached;
    }

    public JetBrainsEndpoints? Refresh()
    {
        _cached = null;
        _lastScan = default;
        return GetEndpoints();
    }

    private JetBrainsEndpoints? ScanLog()
    {
        var logPath = FindLatestRiderLog();
        if (logPath is null || !File.Exists(logPath))
        {
            LogNoLogFile();
            return null;
        }

        string[] lines;
        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (IOException ex)
        {
            LogReadFailed(ex);
            return null;
        }

        string? builtInPort = null;
        string? debuggerUrl = null;

        foreach (var line in lines)
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

internal sealed record JetBrainsEndpoints(
    string? BuiltInSseUrl,
    string? DebuggerStreamableUrl);
