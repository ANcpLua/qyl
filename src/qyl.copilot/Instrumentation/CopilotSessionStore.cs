using System.Collections.Concurrent;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     Thread-safe session store with TTL eviction for Copilot conversations.
///     Tracks message history per session for multi-turn context.
/// </summary>
internal sealed class CopilotSessionStore
{
    private readonly int _maxSessions;
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;

    public CopilotSessionStore(TimeProvider timeProvider, TimeSpan? ttl = null, int maxSessions = 100)
    {
        _timeProvider = timeProvider;
        _ttl = ttl ?? TimeSpan.FromHours(1);
        _maxSessions = maxSessions;
    }

    public int Count => _sessions.Count;

    /// <summary>
    ///     Gets or creates a session's message history.
    ///     Automatically evicts expired and over-capacity sessions.
    /// </summary>
    public List<(string Role, string Content)> GetOrCreate(string sessionId)
    {
        EvictExpired();
        var entry = _sessions.GetOrAdd(sessionId, _ => new SessionEntry(_timeProvider.GetUtcNow()));
        entry.LastAccessed = _timeProvider.GetUtcNow();
        return entry.Messages;
    }

    public bool Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);

    private void EvictExpired()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var (key, entry) in _sessions)
        {
            if (now - entry.LastAccessed > _ttl)
                _sessions.TryRemove(key, out _);
        }

        while (_sessions.Count > _maxSessions)
        {
            var oldest = _sessions.MinBy(static x => x.Value.LastAccessed);
            _sessions.TryRemove(oldest.Key, out _);
        }
    }

    private sealed class SessionEntry(DateTimeOffset created)
    {
        public List<(string Role, string Content)> Messages { get; } = [];
        public DateTimeOffset LastAccessed { get; set; } = created;
    }
}
