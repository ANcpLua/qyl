namespace qyl.mcp.Agents;

/// <summary>
///     Tracks the ancestry of nested agent investigations to enforce bounded autonomy.
///     Uses <see cref="AsyncLocal{T}" /> to automatically thread lineage through async call chains.
/// </summary>
/// <remarks>
///     Three enforcement layers:
///     <list type="bullet">
///         <item><b>Max depth</b> — how deep the spawn tree can grow (default 3, env <c>QYL_AGENT_MAX_DEPTH</c>)</item>
///         <item><b>Root budget</b> — total spawns allowed from one root investigation (default 10, env <c>QYL_AGENT_MAX_SPAWNS</c>)</item>
///         <item><b>Cycle detection</b> — refuses if a session ID appears twice in the ancestor chain</item>
///     </list>
/// </remarks>
internal sealed class InvestigationLineage
{
    private static readonly AsyncLocal<InvestigationLineage?> CurrentLineage = new();

    private readonly InvestigationLineage _root;
    private int _spawnCount;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..12];
    public string? ParentSessionId { get; }
    public int Depth { get; }
    public IReadOnlyList<string> AncestorChain { get; }

    private InvestigationLineage(
        string? parentSessionId,
        int depth,
        IReadOnlyList<string> ancestorChain,
        InvestigationLineage? root)
    {
        ParentSessionId = parentSessionId;
        Depth = depth;
        AncestorChain = ancestorChain;
        _root = root ?? this;
    }

    /// <summary>
    ///     Attempts to enter a new investigation scope. Returns the lineage context if allowed,
    ///     or a refusal reason if a budget or depth limit is exceeded.
    /// </summary>
    public static InvestigationLineageResult TryEnter()
    {
        var maxDepth = ReadEnvInt("QYL_AGENT_MAX_DEPTH", 3);
        var maxSpawns = ReadEnvInt("QYL_AGENT_MAX_SPAWNS", 10);
        var parent = CurrentLineage.Value;

        var depth = parent is null ? 0 : parent.Depth + 1;

        if (depth > maxDepth)
        {
            return InvestigationLineageResult.Refused(
                $"Investigation depth limit reached ({depth}/{maxDepth}). " +
                $"Lineage: {FormatChain(parent)}. " +
                "Use narrower tools instead of spawning another meta-agent.");
        }

        var root = parent?._root;

        if (root is not null)
        {
            var currentSpawns = Interlocked.Increment(ref root._spawnCount);
            if (currentSpawns > maxSpawns)
            {
                Interlocked.Decrement(ref root._spawnCount);
                return InvestigationLineageResult.Refused(
                    $"Root investigation spawn budget exhausted ({currentSpawns}/{maxSpawns}). " +
                    $"Lineage: {FormatChain(parent)}. " +
                    "The root investigation has spawned too many sub-investigations.");
            }
        }

        var ancestors = parent is null
            ? (IReadOnlyList<string>)[]
            : [.. parent.AncestorChain, parent.SessionId];

        var lineage = new InvestigationLineage(parent?.SessionId, depth, ancestors, root);

        if (ancestors.Contains(lineage.SessionId, StringComparer.Ordinal))
        {
            return InvestigationLineageResult.Refused(
                $"Cycle detected: session {lineage.SessionId} already in ancestor chain.");
        }

        CurrentLineage.Value = lineage;
        return InvestigationLineageResult.Allowed(lineage);
    }

    /// <summary>
    ///     Marks this investigation scope as complete and restores the parent lineage context.
    /// </summary>
    public void Complete()
    {
        if (CurrentLineage.Value?.SessionId == SessionId)
        {
            CurrentLineage.Value = ParentSessionId is not null
                ? FindParentInChain()
                : null;
        }
    }

    public string FormatLineageSummary() =>
        $"Session: {SessionId}, Depth: {Depth}/{ReadEnvInt("QYL_AGENT_MAX_DEPTH", 3)}, " +
        $"Ancestors: [{string.Join(" → ", AncestorChain)}]";

    private InvestigationLineage? FindParentInChain()
    {
        var current = this;
        while (current is not null)
        {
            if (current.ParentSessionId is null)
                return null;
            if (current._root != current && current._root.SessionId == current.ParentSessionId)
                return current._root;
            current = null;
        }

        return null;
    }

    private static string FormatChain(InvestigationLineage? lineage) =>
        lineage is null ? "(root)" : string.Join(" → ", [.. lineage.AncestorChain, lineage.SessionId]);

    private static int ReadEnvInt(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return raw is not null && int.TryParse(raw, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }
}

internal readonly record struct InvestigationLineageResult
{
    public bool IsAllowed { get; private init; }
    public InvestigationLineage? Lineage { get; private init; }
    public string? RefusalReason { get; private init; }

    public static InvestigationLineageResult Allowed(InvestigationLineage lineage) =>
        new() { IsAllowed = true, Lineage = lineage };

    public static InvestigationLineageResult Refused(string reason) =>
        new() { IsAllowed = false, RefusalReason = reason };
}
