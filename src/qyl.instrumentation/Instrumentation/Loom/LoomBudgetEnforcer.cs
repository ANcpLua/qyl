using System.Collections.Concurrent;

namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Disposable reservation handle for a Loom budget claim.
///     When disposed without a prior <see cref="Commit" /> call, the reservation is rolled back
///     by decrementing the enforcer's counter. Use with <c>await using</c> for automatic rollback on failure.
/// </summary>
public sealed class LoomBudgetReservation : IAsyncDisposable
{
    private readonly LoomBudgetEnforcer _enforcer;
    private readonly string _toolName;
    private bool _committed;

    internal LoomBudgetReservation(LoomBudgetEnforcer enforcer, string toolName)
    {
        _enforcer = enforcer;
        _toolName = toolName;
    }

    /// <summary>
    ///     Marks the reservation as successfully consumed. Rollback is skipped on disposal.
    /// </summary>
    public void Commit() => _committed = true;

    public ValueTask DisposeAsync()
    {
        if (!_committed)
            _enforcer.Rollback(_toolName);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     Per-tool attempt and tool-call tracking with reservation/rollback semantics.
///     Budget is reserved before execution via <see cref="ReserveAttempt" /> or <see cref="ReserveToolCall" />,
///     and automatically rolled back on failure through the returned <see cref="LoomBudgetReservation" />.
///     Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}" />.
/// </summary>
public sealed class LoomBudgetEnforcer
{
    private readonly ConcurrentDictionary<string, int> _attemptCounts = new();
    private readonly ConcurrentDictionary<string, int> _toolCallCounts = new();

    /// <summary>
    ///     Reserves an attempt for a tool. Throws <see cref="LoomBudgetExceededException" /> if the
    ///     <see cref="LoomPolicyDescriptor.MaxAttempts" /> budget is exceeded.
    /// </summary>
    public LoomBudgetReservation ReserveAttempt(string toolName, LoomPolicyDescriptor policy)
    {
        var current = _attemptCounts.AddOrUpdate(toolName, 1, static (_, count) => count + 1);

        if (current > policy.MaxAttempts)
        {
            _attemptCounts.AddOrUpdate(toolName, 0, static (_, count) => Math.Max(0, count - 1));
            throw new LoomBudgetExceededException(toolName, "MaxAttempts", policy.MaxAttempts, current);
        }

        return new LoomBudgetReservation(this, toolName);
    }

    /// <summary>
    ///     Reserves a tool call for a tool. Throws <see cref="LoomBudgetExceededException" /> if the
    ///     <see cref="LoomPolicyDescriptor.MaxToolCalls" /> budget is exceeded.
    /// </summary>
    public LoomBudgetReservation ReserveToolCall(string toolName, LoomPolicyDescriptor policy)
    {
        var current = _toolCallCounts.AddOrUpdate(toolName, 1, static (_, count) => count + 1);

        if (current > policy.MaxToolCalls)
        {
            _toolCallCounts.AddOrUpdate(toolName, 0, static (_, count) => Math.Max(0, count - 1));
            throw new LoomBudgetExceededException(toolName, "MaxToolCalls", policy.MaxToolCalls, current);
        }

        return new LoomBudgetReservation(this, toolName);
    }

    /// <summary>
    ///     Gets the current attempt count for a tool.
    /// </summary>
    public int GetAttemptCount(string toolName)
        => _attemptCounts.GetValueOrDefault(toolName);

    /// <summary>
    ///     Gets the current tool call count for a tool.
    /// </summary>
    public int GetToolCallCount(string toolName)
        => _toolCallCounts.GetValueOrDefault(toolName);

    /// <summary>
    ///     Rolls back an attempt reservation. Called by <see cref="LoomBudgetReservation.DisposeAsync" />
    ///     when the reservation was not committed.
    /// </summary>
    internal void Rollback(string toolName)
    {
        _attemptCounts.AddOrUpdate(toolName, 0, static (_, count) => Math.Max(0, count - 1));
    }

    /// <summary>
    ///     Resets all attempt and tool-call counters. Intended for use at the start of a new workflow run.
    /// </summary>
    public void Reset()
    {
        _attemptCounts.Clear();
        _toolCallCounts.Clear();
    }

    /// <summary>
    ///     Resets attempt and tool-call counters for a specific tool.
    /// </summary>
    public void Reset(string toolName)
    {
        _attemptCounts.TryRemove(toolName, out _);
        _toolCallCounts.TryRemove(toolName, out _);
    }
}

/// <summary>
///     Thrown when a Loom budget limit is exceeded during reservation.
///     Contains diagnostic properties identifying the tool, budget kind, limit, and attempted value.
/// </summary>
public sealed class LoomBudgetExceededException : InvalidOperationException
{
    public LoomBudgetExceededException() : base("Loom budget exceeded.") { }
    public LoomBudgetExceededException(string message) : base(message) { }
    public LoomBudgetExceededException(string message, Exception innerException) : base(message, innerException) { }

    public LoomBudgetExceededException(string toolName, string budgetKind, int limit, int attempted)
        : base($"Loom budget exceeded for tool '{toolName}': {budgetKind} limit is {limit}, attempted {attempted}. " +
               "Reuse an existing invocation or increase the budget via [LoomBudget].")
    {
        ToolName = toolName;
        BudgetKind = budgetKind;
        Limit = limit;
        Attempted = attempted;
    }

    public string? ToolName { get; }
    public string? BudgetKind { get; }
    public int Limit { get; }
    public int Attempted { get; }
}
