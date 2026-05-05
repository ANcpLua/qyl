#nullable enable

namespace Qyl.Domains.Observe.Session;

public sealed class SessionAttributes
{
    public required string Id { get; init; }
    public string? PreviousId { get; init; }
}

public sealed class SessionEvent
{
    public required Qyl.Domains.Observe.Session.SessionEventName EventName { get; init; }
    public required string SessionId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string EventDomain { get; init; }
}

public sealed class SessionEntity
{
    public required string SessionId { get; init; }
    public string? UserId { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public double? DurationMs { get; init; }
    public required int TraceCount { get; init; }
    public required int SpanCount { get; init; }
    public required int ErrorCount { get; init; }
    public required IReadOnlyList<string> Services { get; init; }
    public required Qyl.Domains.Observe.Session.SessionState State { get; init; }
    public Qyl.Domains.Observe.Session.SessionClientInfo? Client { get; init; }
    public Qyl.Domains.Observe.Session.SessionGeoInfo? Geo { get; init; }
    public Qyl.Domains.Observe.Session.SessionGenAiUsage? GenaiUsage { get; init; }
}

public sealed class SessionClientInfo
{
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public Qyl.Domains.Observe.Session.DeviceType? DeviceType { get; init; }
    public string? Os { get; init; }
    public string? Browser { get; init; }
    public string? BrowserVersion { get; init; }
}

public sealed class SessionGeoInfo
{
    public string? CountryCode { get; init; }
    public string? CountryName { get; init; }
    public string? Region { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Timezone { get; init; }
}

public sealed class SessionGenAiUsage
{
    public required int RequestCount { get; init; }
    public required long TotalInputTokens { get; init; }
    public required long TotalOutputTokens { get; init; }
    public required IReadOnlyList<string> ModelsUsed { get; init; }
    public required IReadOnlyList<string> ProvidersUsed { get; init; }
    public double? EstimatedCostUsd { get; init; }
}

public sealed class SessionStats
{
    public required long ActiveSessions { get; init; }
    public required long TotalSessions { get; init; }
    public required long UniqueUsers { get; init; }
    public required double AvgDurationMs { get; init; }
    public required long SessionsWithErrors { get; init; }
    public required long SessionsWithGenAi { get; init; }
    public required double BounceRate { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Session.SessionDeviceStats>? ByDeviceType { get; init; }
    public IReadOnlyList<Qyl.Domains.Observe.Session.SessionCountryStats>? ByCountry { get; init; }
}

public sealed class SessionDeviceStats
{
    public required Qyl.Domains.Observe.Session.DeviceType DeviceType { get; init; }
    public required long Count { get; init; }
    public required double Percentage { get; init; }
}

public sealed class SessionCountryStats
{
    public required string CountryCode { get; init; }
    public required string CountryName { get; init; }
    public required long Count { get; init; }
    public required double Percentage { get; init; }
}

public enum SessionEventName
{
    SessionStart,
    SessionEnd
}

public enum SessionState
{
    Active,
    Idle,
    Ended,
    TimedOut,
    Invalidated
}

public enum DeviceType
{
    Desktop,
    Mobile,
    Tablet,
    Tv,
    Console,
    Wearable,
    Iot,
    Bot,
    Unknown
}
