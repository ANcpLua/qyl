namespace qyl.collector.ConsoleBridge;

public sealed class FrontendConsole
{
    private const int MaxLogs = 5000;
    private readonly ConcurrentQueue<ConsoleLogEntry> _ring = new();
    private readonly ConcurrentDictionary<string, Channel<ConsoleLogEntry>> _subs = new();
    private int _count;

    public int Count => _count;

    public ConsoleLogEntry Ingest(ConsoleIngestRequest req)
    {
        var entry = new ConsoleLogEntry(
            Guid.NewGuid().ToString("N")[..8],
            ParseLevel(req.Level),
            req.Message ?? "",
            TimeProvider.System.GetUtcNow().UtcDateTime,
            req.SessionId, req.Url, req.Stack);

        _ring.Enqueue(entry);
        if (Interlocked.Increment(ref _count) > MaxLogs)
        {
            while (_ring.TryDequeue(out _))
                Interlocked.Decrement(ref _count);
        }

        foreach (var ch in _subs.Values)
            ch.Writer.TryWrite(entry);

        return entry;
    }

    public ConsoleLogEntry[] Query(ConsoleLevel? minLevel = null, string? session = null, string? pattern = null,
        int limit = 50) =>
    [
        .. _ring.Reverse()
            .Where(e => (!minLevel.HasValue || e.Lvl >= minLevel) &&
                        (session is null || e.Session == session) &&
                        (pattern is null || e.Msg.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
    ];

    public ConsoleLogEntry[] Errors(int limit = 20) => Query(ConsoleLevel.Warn, limit: limit);

    public IDisposable Subscribe(string id, Channel<ConsoleLogEntry> ch)
    {
        _subs[id] = ch;
        return new Sub(this, id);
    }

    private static ConsoleLevel ParseLevel(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "debug" => ConsoleLevel.Debug, "info" => ConsoleLevel.Info,
            "warn" or "warning" => ConsoleLevel.Warn, "error" => ConsoleLevel.Error,
            _ => ConsoleLevel.Log
        };

    private sealed class Sub(FrontendConsole b, string id) : IDisposable
    {
        private readonly FrontendConsole _b = b;
        private readonly string _id = id;
        public void Dispose() => _b._subs.TryRemove(_id, out _);
    }
}

public enum ConsoleLevel
{
    Debug,
    Log,
    Info,
    Warn,
    Error
}

public record ConsoleLogEntry(
    string Id,
    ConsoleLevel Lvl,
    string Msg,
    DateTime At,
    string? Session = null,
    string? Url = null,
    string? Stack = null);

public record ConsoleIngestRequest(string? Level, string? Message, string? SessionId, string? Url, string? Stack);
