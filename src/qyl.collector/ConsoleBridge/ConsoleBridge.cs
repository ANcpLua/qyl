// qyl.collector - Console Bridge
// Frontend→Backend console log bridge for AI agent debugging
// Compact design: agents see errors without burning tokens on browser MCP

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace qyl.collector.ConsoleBridge;

/// <summary>Frontend console log bridge for AI agent debugging.</summary>
public sealed class FrontendConsole
{
    private readonly ConcurrentQueue<ConsoleLogEntry> _ring = new();
    private readonly ConcurrentDictionary<string, Channel<ConsoleLogEntry>> _subs = new();
    private int _count;
    private const int MaxLogs = 5000;

    /// <summary>Ingests a log and broadcasts to SSE subscribers.</summary>
    public ConsoleLogEntry Ingest(ConsoleIngestRequest req)
    {
        var entry = new ConsoleLogEntry(
            Guid.NewGuid().ToString("N")[..8],
            ParseLevel(req.Level),
            req.Message ?? "",
            DateTime.UtcNow,
            req.SessionId, req.Url, req.Stack);

        _ring.Enqueue(entry);
        if (Interlocked.Increment(ref _count) > MaxLogs)
            while (_ring.TryDequeue(out _)) Interlocked.Decrement(ref _count);

        foreach (var ch in _subs.Values)
            ch.Writer.TryWrite(entry);

        return entry;
    }

    /// <summary>Query logs: by level, session, or pattern.</summary>
    public ConsoleLogEntry[] Query(ConsoleLevel? minLevel = null, string? session = null, string? pattern = null, int limit = 50)
        => _ring.Reverse()
            .Where(e => (!minLevel.HasValue || e.Lvl >= minLevel) &&
                       (session == null || e.Session == session) &&
                       (pattern == null || e.Msg.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .Take(limit).ToArray();

    /// <summary>Get only errors (warn+error).</summary>
    public ConsoleLogEntry[] Errors(int limit = 20) => Query(ConsoleLevel.Warn, limit: limit);

    /// <summary>Subscribe to live log stream.</summary>
    public IDisposable Subscribe(string id, Channel<ConsoleLogEntry> ch)
    {
        _subs[id] = ch;
        return new Sub(this, id);
    }

    public int Count => _count;

    private static ConsoleLevel ParseLevel(string? s) => s?.ToLowerInvariant() switch
    {
        "debug" => ConsoleLevel.Debug, "info" => ConsoleLevel.Info,
        "warn" or "warning" => ConsoleLevel.Warn, "error" => ConsoleLevel.Error,
        _ => ConsoleLevel.Log
    };

    private sealed class Sub(FrontendConsole b, string id) : IDisposable
    {
        public void Dispose() => b._subs.TryRemove(id, out _);
    }
}

// Types at namespace level - use ConsoleLevel to avoid collision with Microsoft.Extensions.Logging.LogLevel
public enum ConsoleLevel { Debug, Log, Info, Warn, Error }

public record ConsoleLogEntry(
    string Id, ConsoleLevel Lvl, string Msg, DateTime At,
    string? Session = null, string? Url = null, string? Stack = null);

public record ConsoleIngestRequest(string? Level, string? Message, string? SessionId, string? Url, string? Stack);
