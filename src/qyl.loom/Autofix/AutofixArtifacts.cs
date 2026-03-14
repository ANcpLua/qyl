namespace Qyl.Loom;

/// <summary>Root cause analysis artifact produced by the explorer agent.</summary>
public sealed record RootCauseArtifact
{
    public required string OneLineDescription { get; init; }
    public required string[] FiveWhys { get; init; }
    public string[] ReproductionSteps { get; init; } = [];
}

/// <summary>A single step in a solution plan.</summary>
public sealed record SolutionStep(string Title, string Description);

/// <summary>Solution plan artifact produced by the explorer agent.</summary>
public sealed record SolutionArtifact
{
    public required string OneLineSummary { get; init; }
    public required SolutionStep[] Steps { get; init; }
}

/// <summary>A specific impact with severity rating.</summary>
public sealed record ImpactItem(
    string Label,
    ImpactRating Rating,
    string ImpactDescription,
    string Evidence);

public enum ImpactRating { Low, Medium, High }

/// <summary>Impact assessment artifact.</summary>
public sealed record ImpactAssessmentArtifact
{
    public required string OneLineDescription { get; init; }
    public required ImpactItem[] Impacts { get; init; }
}

/// <summary>A commit suspected of introducing the issue.</summary>
public sealed record SuspectCommit(
    string Sha,
    string RepoName,
    string Message,
    string AuthorName,
    string AuthorEmail,
    string CommittedDate,
    string Description);

/// <summary>Suggested person to assign the issue to.</summary>
public sealed record SuggestedAssignee(string Name, string Email, string Why);

/// <summary>Triage artifact with suspect commit and assignee.</summary>
public sealed record TriageArtifact
{
    public SuspectCommit? SuspectCommit { get; init; }
    public SuggestedAssignee? SuggestedAssignee { get; init; }
}
