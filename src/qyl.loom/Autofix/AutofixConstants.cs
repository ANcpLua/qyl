namespace Qyl.Loom;

/// <summary>Fixability score thresholds used to classify issue fix difficulty.</summary>
public enum FixabilityLevel
{
    SuperLow,
    Low,
    Medium,
    High,
    SuperHigh
}

public static class FixabilityThresholds
{
    public const double SuperHigh = 0.76;
    public const double High = 0.66;
    public const double Medium = 0.40;
    public const double Low = 0.25;
    public const double SuperLow = 0.0;

    public static FixabilityLevel Classify(double score) => score switch
    {
        >= SuperHigh => FixabilityLevel.SuperHigh,
        >= High => FixabilityLevel.High,
        >= Medium => FixabilityLevel.Medium,
        >= Low => FixabilityLevel.Low,
        _ => FixabilityLevel.SuperLow
    };
}

/// <summary>Autofix pipeline status.</summary>
public enum AutofixStatus
{
    Processing,
    Completed,
    Error,
    NeedMoreInformation,
    Cancelled,
    WaitingForUserResponse
}

/// <summary>Automation tuning level — controls how aggressively autofix runs.</summary>
public enum AutofixTuning
{
    Off,
    SuperLow,
    Low,
    Medium,
    High,
    Always
}

/// <summary>Autofix pipeline step identifiers.</summary>
public enum AutofixStep
{
    RootCause,
    Solution,
    CodeChanges,
    ImpactAssessment,
    Triage
}
