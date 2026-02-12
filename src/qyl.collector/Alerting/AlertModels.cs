namespace qyl.collector.Alerting;

/// <summary>
///     A single alerting rule loaded from YAML configuration.
/// </summary>
public sealed record AlertRule(
    string Name,
    string Description,
    string Query,
    string Condition,
    TimeSpan Interval,
    TimeSpan Cooldown,
    IReadOnlyList<NotificationChannel> Channels);

/// <summary>
///     Notification channel for alert delivery.
/// </summary>
public sealed record NotificationChannel(string Type, string? Url);

/// <summary>
///     A fired or resolved alert event.
/// </summary>
public sealed record AlertEvent(
    string Id,
    string RuleName,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    double QueryResult,
    string Condition,
    string Status);

/// <summary>
///     Root configuration loaded from YAML.
/// </summary>
public sealed record AlertConfiguration(IReadOnlyList<AlertRule> Alerts);

/// <summary>
///     Runtime state of a single alert rule.
/// </summary>
public sealed class AlertRuleState
{
    public required string RuleName { get; init; }
    public bool IsFiring { get; set; }
    public DateTimeOffset? LastFiredAt { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public double? LastQueryResult { get; set; }
    public string? ActiveAlertId { get; set; }
}
