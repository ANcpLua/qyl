namespace Qyl.Collector.Intelligence.Evidence;

public sealed record IssueEvidence(
    string IssueId,
    string Fingerprint,
    string ServiceName,
    string Environment,
    string Title,
    string Severity,
    int EventCount,
    IReadOnlyList<EvidenceFact> Facts);
