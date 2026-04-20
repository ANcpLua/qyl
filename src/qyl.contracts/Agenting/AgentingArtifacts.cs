namespace Qyl.Contracts.Agenting;

/// <summary>
///     Canonical artifact reference for a run.
/// </summary>
public sealed record AgentRunArtifactRef
{
    public required string ArtifactId { get; init; }
    public required string RunId { get; init; }
    public required AgentRunArtifactKind Kind { get; init; }
    public required string Locator { get; init; }
    public required string ContentType { get; init; }
    public required string ProducedBy { get; init; }
    public DateTimeOffset ProducedAtUtc { get; init; }
    public string? Summary { get; init; }
    public string? Checksum { get; init; }
    public long? ByteSize { get; init; }
    public IReadOnlyList<string>? Labels { get; init; }
}

/// <summary>
///     Snapshot of an execution artifact payload.
/// </summary>
public sealed record AgentRunArtifact
{
    public required string ArtifactId { get; init; }
    public required AgentRunArtifactKind Kind { get; init; }
    public required string RunId { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }
    public required string BodyJson { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string? SourceTool { get; init; }
    public string? SourceStep { get; init; }
}

/// <summary>
///     Input/output boundaries for evidence and artifact exchange between planes.
/// </summary>
public sealed record AgentRunEvidencePack
{
    public required string PackId { get; init; }
    public required string RunId { get; init; }
    public required string IssueId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required IReadOnlyList<AgentRunArtifactRef> Artifacts { get; init; }
    public string? ContextJson { get; init; }
    public string? SignalsSummaryJson { get; init; }
    public string? CausalHintsJson { get; init; }
}
