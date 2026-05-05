
namespace Qyl.Loom.Workflows.ReviewBot;

public sealed record ReviewBotComment
{
    public required string Author { get; init; }

    public required string File { get; init; }

    public required int? Line { get; init; }

    public required string Bug { get; init; }

    public required ReviewBotSeverity Severity { get; init; }

    public required string SeverityText { get; init; }

    public required double? Confidence { get; init; }

    public required string DetailedAnalysis { get; init; }

    public required string SuggestedFix { get; init; }

    public required string AgentPrompt { get; init; }
}

public enum ReviewBotSeverity
{
    Unknown = 0,

    Info = 1,

    Low = 2,

    Medium = 3,

    High = 4,

    Critical = 5
}

public sealed record ReviewBotRawComment(string? Author, string? File, int? Line, string? Body);
