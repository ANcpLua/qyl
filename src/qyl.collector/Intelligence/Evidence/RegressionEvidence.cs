namespace Qyl.Collector.Intelligence.Evidence;

public sealed record RegressionEvidence(
    string SignalType,
    string BaselineWindow,
    string ComparisonWindow,
    bool RegressionDetected,
    double DeltaPercent,
    IReadOnlyList<EvidenceFact> Facts);
