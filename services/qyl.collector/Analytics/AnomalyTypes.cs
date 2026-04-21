namespace Qyl.Collector.Analytics;

/// <summary>Anomaly classification confidence level.</summary>
public enum AnomalyType
{
    None,
    NoData,
    LowConfidence,
    HighConfidence
}

/// <summary>Detection sensitivity — controls Z-score threshold.</summary>
public enum AnomalySensitivity
{
    Low,
    Medium,
    High
}

/// <summary>Expected seasonality patterns for time-series decomposition.</summary>
public enum AnomalySeasonality
{
    Auto,
    Hourly,
    Daily,
    Weekly,
    HourlyDaily,
    HourlyWeekly,
    DailyWeekly,
    HourlyDailyWeekly
}

/// <summary>Which direction triggers an anomaly.</summary>
public enum AnomalyThresholdDirection
{
    Above,
    Below,
    AboveAndBelow
}
