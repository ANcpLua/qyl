namespace Qyl.Collector.Intelligence.Evidence;

public sealed record DeploymentEvidence(
    string DeploymentId,
    string ServiceName,
    string Environment,
    string Version,
    DateTimeOffset DeployedAtUtc,
    IReadOnlyList<EvidenceFact> Facts);
