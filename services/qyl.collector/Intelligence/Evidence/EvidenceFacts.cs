using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Intelligence.Evidence;

public sealed record EvidenceFact(
    string Category,
    string Key,
    string Value,
    double Confidence,
    string Source,
    string? ContextJson = null);

public sealed record AutofixEvidenceInput(
    string RunId,
    string IssueId,
    DateTimeOffset CreatedAtUtc,
    IssueEvidence Issue,
    RegressionEvidence? Regression = null,
    DeploymentEvidence? Deployment = null,
    IReadOnlyList<AgentRunArtifactRef>? Artifacts = null,
    IReadOnlyList<EvidenceFact>? ContextFacts = null,
    IReadOnlyList<EvidenceFact>? CausalFacts = null);
