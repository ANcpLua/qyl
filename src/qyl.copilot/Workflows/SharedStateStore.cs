// =============================================================================
// qyl.copilot - Shared State Store
// Thread-safe versioned KV store scoped to a workflow run
// =============================================================================

using System.Collections.Concurrent;

namespace qyl.copilot.Workflows;

/// <summary>
///     Thread-safe versioned key-value store scoped to a single workflow run.
///     Uses compare-and-swap semantics to prevent concurrent clobbering.
/// </summary>
public sealed class SharedStateStore
{
    private readonly ConcurrentDictionary<string, VersionedEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>
    ///     Gets all keys currently in the store.
    /// </summary>
    public IReadOnlyCollection<string> Keys => _entries.Keys.ToList();

    /// <summary>
    ///     Gets a value by key, returning default if not found.
    /// </summary>
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        var result = _entries.TryGetValue(key, out var entry) ? (T?)entry.Value : default;
        return Task.FromResult(result);
    }

    /// <summary>
    ///     Sets a value with compare-and-swap. Returns true if the expected version matched.
    ///     Use expectedVersion 0 for initial insert.
    /// </summary>
    public Task<bool> SetAsync<T>(string key, T value, long expectedVersion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        using (_lock.EnterScope())
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.Version != expectedVersion)
                    return Task.FromResult(false);

                _entries[key] = new VersionedEntry(value, existing.Version + 1);
                return Task.FromResult(true);
            }

            if (expectedVersion != 0)
                return Task.FromResult(false);

            _entries[key] = new VersionedEntry(value, 1);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    ///     Gets a value along with its current version for optimistic concurrency.
    /// </summary>
    public Task<(T? Value, long Version)> GetVersionedAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (_entries.TryGetValue(key, out var entry))
            return Task.FromResult(((T?)entry.Value, entry.Version));

        return Task.FromResult((default(T?), 0L));
    }

    /// <summary>
    ///     Returns a snapshot of the store as a dictionary (unversioned).
    /// </summary>
    public IReadOnlyDictionary<string, object?> Snapshot() =>
        _entries.ToDictionary(
            static kvp => kvp.Key,
            static kvp => kvp.Value.Value,
            StringComparer.OrdinalIgnoreCase);

    private sealed record VersionedEntry(object? Value, long Version);
}
