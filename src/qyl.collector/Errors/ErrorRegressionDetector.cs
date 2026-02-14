namespace qyl.collector.Errors;

/// <summary>
///     Detects regressions by comparing current errors against previously-resolved errors.
///     Auto-transitions resolved → regressed if the same fingerprint reappears.
/// </summary>
public sealed partial class ErrorRegressionDetector(DuckDbStore store, ILogger<ErrorRegressionDetector> logger)
{
    /// <summary>
    ///     Scans for regressions in a service. Checks if any resolved errors have
    ///     the same fingerprint as newly-appeared errors.
    /// </summary>
    /// <returns>List of error IDs that were transitioned to regressed.</returns>
    public async Task<IReadOnlyList<string>> CheckForRegressionsAsync(
        string serviceName,
        string? deployVersion = null,
        CancellationToken ct = default)
    {
        var regressedIds = await store.DetectRegressionsAsync(serviceName, deployVersion, ct).ConfigureAwait(false);

        if (regressedIds.Count > 0)
            LogRegressionsDetected(serviceName, regressedIds.Count, deployVersion);
        else
            LogNoRegressions(serviceName);

        return regressedIds;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Detected {Count} regression(s) in service {ServiceName} (deploy: {DeployVersion})")]
    private partial void LogRegressionsDetected(string serviceName, int count, string? deployVersion);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "No regressions detected in service {ServiceName}")]
    private partial void LogNoRegressions(string serviceName);
}
