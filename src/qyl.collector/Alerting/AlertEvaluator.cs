namespace qyl.collector.Alerting;

/// <summary>
///     Evaluates alert rules by executing SQL against DuckDB and parsing conditions.
///     Tracks cooldown state per rule to prevent alert storms.
/// </summary>
public sealed partial class AlertEvaluator
{
    private readonly DuckDbStore _store;
    private readonly ILogger<AlertEvaluator> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, AlertRuleState> _states = new();

    public AlertEvaluator(
        DuckDbStore store,
        ILogger<AlertEvaluator> logger,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Gets the current state snapshot for all tracked rules.
    /// </summary>
    public IReadOnlyDictionary<string, AlertRuleState> States => _states;

    /// <summary>
    ///     Evaluates a single alert rule. Returns an AlertEvent if the alert should fire or resolve.
    ///     Returns null if no state change or still in cooldown.
    /// </summary>
    public async Task<AlertEvent?> EvaluateAsync(AlertRule rule, CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(rule.Name, static name => new AlertRuleState { RuleName = name });
        var now = _timeProvider.GetUtcNow();

        double queryResult;
        try
        {
            queryResult = await ExecuteQueryAsync(rule.Query, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogQueryError(_logger, rule.Name, ex);
            state.LastEvaluatedAt = now;
            return null;
        }

        state.LastEvaluatedAt = now;
        state.LastQueryResult = queryResult;

        var conditionMet = EvaluateCondition(queryResult, rule.Condition);
        LogEvaluation(_logger, rule.Name, queryResult, rule.Condition, conditionMet);

        if (conditionMet)
        {
            if (state.IsFiring)
            {
                // Already firing — check cooldown
                if (state.LastFiredAt.HasValue && now - state.LastFiredAt.Value < rule.Cooldown)
                    return null;
            }

            // Fire or re-fire
            state.IsFiring = true;
            state.LastFiredAt = now;
            var id = Guid.NewGuid().ToString("N");
            state.ActiveAlertId = id;

            return new AlertEvent(id, rule.Name, now, null, queryResult, rule.Condition, "firing");
        }

        // Condition not met
        if (state.IsFiring)
        {
            // Resolve
            state.IsFiring = false;
            var resolvedId = state.ActiveAlertId ?? Guid.NewGuid().ToString("N");
            state.ActiveAlertId = null;

            return new AlertEvent(resolvedId, rule.Name, state.LastFiredAt ?? now, now, queryResult, rule.Condition, "resolved");
        }

        return null;
    }

    private async Task<double> ExecuteQueryAsync(string query, CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = query;

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

        return result switch
        {
            null or DBNull => 0.0,
            double d => d,
            float f => f,
            decimal m => (double)m,
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            _ => double.TryParse(result.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.0
        };
    }

    /// <summary>
    ///     Evaluates a condition string against a query result value.
    ///     Supports: &gt; X, &lt; X, &gt;= X, &lt;= X, == X, != X
    /// </summary>
    internal static bool EvaluateCondition(double value, string condition)
    {
        var trimmed = condition.AsSpan().Trim();

        if (trimmed.StartsWith(">="))
            return TryParseThreshold(trimmed[2..], out var t) && value >= t;
        if (trimmed.StartsWith("<="))
            return TryParseThreshold(trimmed[2..], out var t) && value <= t;
        if (trimmed.StartsWith("!="))
            return TryParseThreshold(trimmed[2..], out var t) && Math.Abs(value - t) > 0.0001;
        if (trimmed.StartsWith("=="))
            return TryParseThreshold(trimmed[2..], out var t) && Math.Abs(value - t) < 0.0001;
        if (trimmed.StartsWith(">"))
            return TryParseThreshold(trimmed[1..], out var t) && value > t;
        if (trimmed.StartsWith("<"))
            return TryParseThreshold(trimmed[1..], out var t) && value < t;

        // Default: treat as "> threshold"
        return TryParseThreshold(trimmed, out var threshold) && value > threshold;
    }

    private static bool TryParseThreshold(ReadOnlySpan<char> span, out double threshold) =>
        double.TryParse(span.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out threshold);

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Error, Message = "Alert query failed for rule '{RuleName}'")]
    private static partial void LogQueryError(ILogger logger, string ruleName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Alert evaluation: {RuleName} query={QueryResult}, condition='{Condition}', met={ConditionMet}")]
    private static partial void LogEvaluation(ILogger logger, string ruleName, double queryResult, string condition, bool conditionMet);
}
