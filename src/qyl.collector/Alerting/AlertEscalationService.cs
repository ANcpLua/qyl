namespace qyl.collector.Alerting;

/// <summary>
///     Manages escalation chains for unacknowledged alerts.
///     Levels: notify → page → incident. Escalates after configurable time windows.
/// </summary>
public sealed partial class AlertEscalationService
{
    private readonly ILogger<AlertEscalationService> _logger;
    private readonly AlertNotifier _notifier;
    private readonly ConcurrentDictionary<string, EscalationState> _states = new();
    private readonly TimeProvider _timeProvider;

    public AlertEscalationService(
        AlertNotifier notifier,
        ILogger<AlertEscalationService> logger,
        TimeProvider? timeProvider = null)
    {
        _notifier = notifier;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    ///     Gets the current escalation states for diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, EscalationState> States => _states;

    /// <summary>
    ///     Tracks a newly fired alert for potential escalation.
    /// </summary>
    public void TrackAlert(AlertRule rule, AlertEvent alertEvent)
    {
        if (alertEvent.Status != "firing")
        {
            // Resolved — remove from escalation tracking
            if (_states.TryRemove(alertEvent.Id, out _))
                LogEscalationCleared(_logger, alertEvent.RuleName);

            return;
        }

        var priority = DeterminePriority(rule);
        var escalationPolicy = GetEscalationPolicy(priority);

        _states[alertEvent.Id] = new EscalationState
        {
            AlertId = alertEvent.Id,
            RuleName = alertEvent.RuleName,
            FiredAt = alertEvent.FiredAt,
            Priority = priority,
            CurrentLevel = EscalationLevel.Notify,
            LastEscalatedAt = alertEvent.FiredAt,
            Policy = escalationPolicy,
            Rule = rule
        };

        LogAlertTracked(_logger, alertEvent.RuleName, priority);
    }

    /// <summary>
    ///     Evaluates all tracked alerts and escalates any that have exceeded their time window.
    /// </summary>
    public async Task EvaluateEscalationsAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var kvp in _states)
        {
            var state = kvp.Value;
            if (state.CurrentLevel >= EscalationLevel.Incident)
                continue;

            var elapsed = now - state.LastEscalatedAt;
            var threshold = GetEscalationThreshold(state.Policy, state.CurrentLevel);

            if (elapsed < threshold)
                continue;

            var nextLevel = state.CurrentLevel + 1;
            state.CurrentLevel = nextLevel;
            state.LastEscalatedAt = now;

            LogEscalation(_logger, state.RuleName, nextLevel);

            var escalationEvent = new AlertEvent(
                state.AlertId,
                $"{state.RuleName} [ESCALATED:{nextLevel}]",
                state.FiredAt,
                null,
                0,
                $"escalation_level={nextLevel}",
                "firing");

            try
            {
                await _notifier.NotifyAsync(state.Rule, escalationEvent, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEscalationError(_logger, state.RuleName, nextLevel, ex);
            }
        }
    }

    /// <summary>
    ///     Acknowledges an alert, stopping further escalation.
    /// </summary>
    public bool Acknowledge(string alertId)
    {
        if (_states.TryRemove(alertId, out var state))
        {
            LogAcknowledged(_logger, state.RuleName, alertId);
            return true;
        }

        return false;
    }

    private static AlertPriority DeterminePriority(AlertRule rule)
    {
        // Rules with short intervals or containing "critical" are high priority
        var nameLower = rule.Name.ToLowerInvariant();
        if (nameLower.Contains("critical") || nameLower.Contains("incident"))
            return AlertPriority.Critical;
        if (nameLower.Contains("error") || nameLower.Contains("failure"))
            return AlertPriority.High;
        if (nameLower.Contains("warning") || nameLower.Contains("degradation"))
            return AlertPriority.Medium;
        return AlertPriority.Low;
    }

    private static EscalationPolicy GetEscalationPolicy(AlertPriority priority) =>
        priority switch
        {
            AlertPriority.Critical => new EscalationPolicy(
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)),
            AlertPriority.High => new EscalationPolicy(
                TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)),
            AlertPriority.Medium => new EscalationPolicy(
                TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)),
            _ => new EscalationPolicy(
                TimeSpan.FromHours(1), TimeSpan.FromHours(4))
        };

    private static TimeSpan GetEscalationThreshold(EscalationPolicy policy, EscalationLevel currentLevel) =>
        currentLevel switch
        {
            EscalationLevel.Notify => policy.NotifyToPage,
            EscalationLevel.Page => policy.PageToIncident,
            _ => TimeSpan.MaxValue
        };

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alert '{RuleName}' tracked for escalation (priority={Priority})")]
    private static partial void LogAlertTracked(ILogger logger, string ruleName, AlertPriority priority);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alert '{RuleName}' escalated to {Level}")]
    private static partial void LogEscalation(ILogger logger, string ruleName, EscalationLevel level);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alert '{RuleName}' escalation cleared (resolved)")]
    private static partial void LogEscalationCleared(ILogger logger, string ruleName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alert '{RuleName}' acknowledged (id={AlertId})")]
    private static partial void LogAcknowledged(ILogger logger, string ruleName, string alertId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to send escalation notification for '{RuleName}' at level {Level}")]
    private static partial void
        LogEscalationError(ILogger logger, string ruleName, EscalationLevel level, Exception ex);
}

// ==========================================================================
// Escalation Models
// ==========================================================================

public enum AlertPriority { Low, Medium, High, Critical }

public enum EscalationLevel { Notify, Page, Incident }

public sealed record EscalationPolicy(TimeSpan NotifyToPage, TimeSpan PageToIncident);

public sealed class EscalationState
{
    public required string AlertId { get; init; }
    public required string RuleName { get; init; }
    public required DateTimeOffset FiredAt { get; init; }
    public required AlertPriority Priority { get; init; }
    public required EscalationPolicy Policy { get; init; }
    public required AlertRule Rule { get; init; }
    public EscalationLevel CurrentLevel { get; set; }
    public DateTimeOffset LastEscalatedAt { get; set; }
}
