using System.Collections.Concurrent;

namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Immutable lineage snapshot for a workflow run within a spawn tree.
///     Mirrors <c>resolveSubagentSpawnContext</c> from oh-my-openagent, which walks
///     session parent chains to find root, depth, and descendant budget.
/// </summary>
public sealed record LoomSpawnContext(
    string RootRunId,
    string ParentRunId,
    int Depth,
    int DescendantCount);

/// <summary>
///     Tracks parent-child relationships between workflow runs and enforces
///     depth and descendant limits from <see cref="LoomPolicyDescriptor"/>.
///     Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
///     Cycle detection prevents infinite loops in corrupted lineage chains.
/// </summary>
public sealed class LoomSpawnTracker
{
    private readonly ConcurrentDictionary<string, string?> _parents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _descendantCounts = new(StringComparer.Ordinal);

    /// <summary>
    ///     Register a new workflow run with its parent.
    ///     Returns the spawn context for the new run.
    ///     Throws <see cref="LoomSpawnLimitExceededException"/> if depth or descendant limits are exceeded.
    /// </summary>
    public LoomSpawnContext Register(
        string runId,
        string? parentRunId,
        LoomPolicyDescriptor policy)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(policy);

        var (rootRunId, depth) = parentRunId is not null
            ? ResolveLineage(parentRunId)
            : (runId, 0);

        var childDepth = parentRunId is not null ? depth + 1 : 0;

        if (childDepth > policy.MaxAttempts)
        {
            throw new LoomSpawnLimitExceededException(
                runId, "depth", policy.MaxAttempts, childDepth, rootRunId);
        }

        // Use MaxToolCalls * 5 if configured, otherwise default to 50 --
        // matching oh-my-openagent's DEFAULT_MAX_ROOT_SESSION_SPAWN_BUDGET.
        var maxDescendants = policy.MaxToolCalls > 0 ? policy.MaxToolCalls * 5 : 50;
        var descendantCount = _descendantCounts.AddOrUpdate(rootRunId, 1, static (_, count) => count + 1);

        if (descendantCount > maxDescendants)
        {
            // Roll back the increment before throwing.
            _descendantCounts.AddOrUpdate(rootRunId, 0, static (_, count) => Math.Max(0, count - 1));
            throw new LoomSpawnLimitExceededException(
                runId, "descendants", maxDescendants, descendantCount, rootRunId);
        }

        _parents[runId] = parentRunId;

        return new LoomSpawnContext(rootRunId, parentRunId ?? runId, childDepth, descendantCount);
    }

    /// <summary>
    ///     Unregister a workflow run on completion or failure.
    ///     Decrements the descendant count for the root run.
    /// </summary>
    public void Unregister(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        // Resolve root before removal -- the parent chain entry is needed for the walk.
        var (rootRunId, _) = ResolveLineage(runId);

        if (_parents.TryRemove(runId, out _))
        {
            _descendantCounts.AddOrUpdate(rootRunId, 0, static (_, count) => Math.Max(0, count - 1));
        }
    }

    /// <summary>
    ///     Get the spawn context for an existing run, or <c>null</c> if the run is not tracked.
    /// </summary>
    public LoomSpawnContext? GetContext(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        if (!_parents.ContainsKey(runId))
            return null;

        var (rootRunId, depth) = ResolveLineage(runId);
        var parentRunId = _parents.GetValueOrDefault(runId);
        var descendantCount = _descendantCounts.GetValueOrDefault(rootRunId);
        return new LoomSpawnContext(rootRunId, parentRunId ?? runId, depth, descendantCount);
    }

    /// <summary>
    ///     Get total descendant count for a root run.
    /// </summary>
    public int GetDescendantCount(string rootRunId)
    {
        ArgumentNullException.ThrowIfNull(rootRunId);
        return _descendantCounts.GetValueOrDefault(rootRunId);
    }

    /// <summary>
    ///     Reset all tracking state.
    /// </summary>
    public void Reset()
    {
        _parents.Clear();
        _descendantCounts.Clear();
    }

    private (string RootRunId, int Depth) ResolveLineage(string? runId)
    {
        if (runId is null)
            return (string.Empty, 0);

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = runId;
        var depth = 0;

        while (true)
        {
            if (!visited.Add(current))
            {
                throw new InvalidOperationException(
                    $"Cycle detected in spawn lineage at run '{current}'.");
            }

            if (!_parents.TryGetValue(current, out var parent) || parent is null)
                return (current, depth);

            current = parent;
            depth++;
        }
    }
}

/// <summary>
///     Thrown when a workflow spawn exceeds the configured depth or descendant limit.
///     Callers should reuse an existing workflow run instead of spawning another.
/// </summary>
public sealed class LoomSpawnLimitExceededException : InvalidOperationException
{
    public LoomSpawnLimitExceededException() : base("Loom spawn limit exceeded.") { }
    public LoomSpawnLimitExceededException(string message) : base(message) { }
    public LoomSpawnLimitExceededException(string message, Exception innerException) : base(message, innerException) { }

    public LoomSpawnLimitExceededException(string runId, string limitKind, int limit, int actual, string rootRunId)
        : base($"Loom spawn limit exceeded for run '{runId}': {limitKind} limit is {limit}, actual is {actual}. " +
               $"Root run: {rootRunId}. Reuse an existing workflow run instead of spawning another.")
    {
        RunId = runId;
        LimitKind = limitKind;
        Limit = limit;
        Actual = actual;
        RootRunId = rootRunId;
    }

    public string? RunId { get; }
    public string? LimitKind { get; }
    public int Limit { get; }
    public int Actual { get; }
    public string? RootRunId { get; }
}
