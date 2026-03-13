namespace Qyl.Contracts.Autofix;

/// <summary>Where in the investigation pipeline the session is.</summary>
public enum LoomStage
{
    Idle = 0,
    Insight = 1,
    Exploring = 2,
    Reasoning = 3,
    RootCause = 4,
    Solution = 5,
    CodeItUp = 6
}

/// <summary>What the session is doing at its current stage.</summary>
public enum LoomStatus
{
    /// <summary>Agent is actively running at this stage.</summary>
    Active,
    /// <summary>Agent paused — waiting for user input to continue.</summary>
    Paused,
    /// <summary>Session exists but no agent is running (pre-start or between runs).</summary>
    Idle,
    /// <summary>Terminal: investigation completed successfully.</summary>
    Completed,
    /// <summary>Terminal: unrecoverable error.</summary>
    Failed,
    /// <summary>Terminal: user or system cancelled.</summary>
    Cancelled
}

public static class LoomStageExtensions
{
    public static bool IsTerminal(this LoomStatus status) =>
        status is LoomStatus.Completed or LoomStatus.Failed or LoomStatus.Cancelled;
}
