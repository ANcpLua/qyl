namespace Qyl.Collector.Analytics;

public enum AnomalyType
{
    None,
    NoData,
    LowConfidence,
    HighConfidence
}

public enum AnomalySensitivity
{
    Low,
    Medium,
    High
}

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

public enum AnomalyThresholdDirection
{
    Above,
    Below,
    AboveAndBelow
}
