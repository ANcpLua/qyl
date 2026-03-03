namespace qyl.collector.Autofix;

/// <summary>
///     Background service that polls for new deployments and runs regression detection.
///     Checks if resolved errors have re-appeared after a deploy and triggers re-triage
///     via <see cref="TriagePipelineService"/> when regressions are found.
/// </summary>
public sealed partial class RegressionDetectionService(
    DuckDbStore store,
    TriagePipelineService triagePipeline,
    IConfiguration configuration,
    ILogger<RegressionDetectionService> logger)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue("QYL_REGRESSION_DETECTION_ENABLED", true);
    private readonly int _intervalSeconds = configuration.GetValue("QYL_REGRESSION_CHECK_INTERVAL_SECONDS", 60);

    private DateTime _lastChecked = TimeProvider.System.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            LogRegressionDetectionDisabled();
            return;
        }

        // Warmup delay — let deployments accumulate
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        LogRegressionDetectionStarted(_intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await CheckForRegressionsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRegressionCheckError(ex);
            }
        }
    }

    internal async Task CheckForRegressionsAsync(CancellationToken ct)
    {
        IReadOnlyList<DeploymentRecord> deployments = await store.GetDeploymentsAfterAsync(_lastChecked, ct)
            .ConfigureAwait(false);

        if (deployments.Count == 0) return;

        int totalRegressions = 0;

        foreach (DeploymentRecord deployment in deployments)
        {
            LogCheckingDeployment(deployment.ServiceName, deployment.ServiceVersion);

            IReadOnlyList<string> regressedIds = await store.DetectRegressionsAsync(
                deployment.ServiceName, deployment.ServiceVersion, ct).ConfigureAwait(false);

            foreach (string issueId in regressedIds)
            {
                LogRegressionDetected(issueId, deployment.ServiceName, deployment.ServiceVersion);
            }

            totalRegressions += regressedIds.Count;
        }

        // Update checkpoint to the latest deployment's StartTime
        _lastChecked = deployments[^1].StartTime;

        // If any regressions were found, trigger re-triage
        if (totalRegressions > 0)
        {
            await triagePipeline.TriageUntriagedIssuesAsync(ct).ConfigureAwait(false);
        }

        LogRegressionCheckComplete(deployments.Count, totalRegressions);
    }

    // ── Log Methods ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Regression detection disabled via QYL_REGRESSION_DETECTION_ENABLED=false")]
    private partial void LogRegressionDetectionDisabled();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Regression detection started (interval: {IntervalSeconds}s)")]
    private partial void LogRegressionDetectionStarted(int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Checking deployment: {ServiceName} v{Version}")]
    private partial void LogCheckingDeployment(string serviceName, string version);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Regression detected: issue {IssueId} regressed in {ServiceName} v{Version}")]
    private partial void LogRegressionDetected(string issueId, string serviceName, string version);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Regression check complete: {DeploymentsChecked} deployments checked, {RegressionsFound} regressions found")]
    private partial void LogRegressionCheckComplete(int deploymentsChecked, int regressionsFound);

    [LoggerMessage(Level = LogLevel.Error, Message = "Regression detection error")]
    private partial void LogRegressionCheckError(Exception ex);
}
