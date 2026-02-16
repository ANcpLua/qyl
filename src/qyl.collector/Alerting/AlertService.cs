namespace qyl.collector.Alerting;

/// <summary>
///     Background service that periodically evaluates alert rules.
///     Each rule runs on its own timer interval, all within one hosted service.
/// </summary>
public sealed partial class AlertService : BackgroundService
{
    private readonly AlertConfigLoader _configLoader;
    private readonly AlertEvaluator _evaluator;
    private readonly ILogger<AlertService> _logger;
    private readonly AlertNotifier _notifier;
    private readonly DuckDbStore _store;
    private readonly TimeProvider _timeProvider;

    public AlertService(
        AlertConfigLoader configLoader,
        AlertEvaluator evaluator,
        AlertNotifier notifier,
        DuckDbStore store,
        ILogger<AlertService> logger,
        TimeProvider? timeProvider = null)
    {
        _configLoader = configLoader;
        _evaluator = evaluator;
        _notifier = notifier;
        _store = store;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay for ingestion warmup
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken).ConfigureAwait(false);

        var config = _configLoader.LoadAndWatch();
        LogServiceStarted(_logger, config.Alerts.Count);

        if (config.Alerts.Count == 0)
        {
            // No alerts configured — wait for config change
            await WaitForConfigChangeAsync(stoppingToken).ConfigureAwait(false);
            return;
        }

        // Start evaluation loops for each rule
        var tasks = new List<Task>(config.Alerts.Count);
        foreach (var rule in config.Alerts)
        {
            tasks.Add(RunRuleLoopAsync(rule, stoppingToken));
        }

        // Listen for config changes and restart when needed
        _configLoader.OnConfigurationChanged(OnConfigChanged);

        void OnConfigChanged(AlertConfiguration _) => LogConfigChanged(_logger);

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task RunRuleLoopAsync(AlertRule rule, CancellationToken ct)
    {
        LogRuleStarted(_logger, rule.Name, rule.Interval.TotalSeconds);

        using var timer = new PeriodicTimer(rule.Interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var alertEvent = await _evaluator.EvaluateAsync(rule, ct).ConfigureAwait(false);
                if (alertEvent is null)
                    continue;

                // Notify on state change
                await _notifier.NotifyAsync(rule, alertEvent, ct).ConfigureAwait(false);

                // Store in alert history
                await StoreAlertEventAsync(alertEvent, rule, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRuleError(_logger, rule.Name, ex);
            }
        }
    }

    private async Task StoreAlertEventAsync(AlertEvent alertEvent, AlertRule rule, CancellationToken ct)
    {
        try
        {
            await using var lease = await _store.GetReadConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = lease.Connection.CreateCommand();

            if (alertEvent.Status == "firing")
            {
                cmd.CommandText = """
                                  INSERT INTO alert_history (id, rule_name, fired_at, query_result, condition_text, status, notification_channels)
                                  VALUES ($1, $2, $3, $4, $5, $6, $7)
                                  ON CONFLICT (id) DO NOTHING
                                  """;
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.Id });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.RuleName });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.FiredAt.UtcDateTime });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.QueryResult });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.Condition });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.Status });
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = string.Join(",", rule.Channels.Select(static c => c.Type))
                });
            }
            else
            {
                // Update resolved_at for existing alert
                cmd.CommandText = """
                                  UPDATE alert_history SET resolved_at = $1, status = $2
                                  WHERE id = $3
                                  """;
                cmd.Parameters.Add(new DuckDBParameter
                {
                    Value = alertEvent.ResolvedAt?.UtcDateTime ?? _timeProvider.GetUtcNow().UtcDateTime
                });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.Status });
                cmd.Parameters.Add(new DuckDBParameter { Value = alertEvent.Id });
            }

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogStoreError(_logger, alertEvent.RuleName, ex);
        }
    }

    private async Task WaitForConfigChangeAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _configLoader.OnConfigurationChanged(_ => tcs.TrySetResult());
        await tcs.Task.ConfigureAwait(false);
    }

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information, Message = "Alert service started with {Count} rules")]
    private static partial void LogServiceStarted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Starting evaluation loop for rule '{RuleName}' every {IntervalSeconds}s")]
    private static partial void LogRuleStarted(ILogger logger, string ruleName, double intervalSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error evaluating alert rule '{RuleName}'")]
    private static partial void LogRuleError(ILogger logger, string ruleName, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store alert event for rule '{RuleName}'")]
    private static partial void LogStoreError(ILogger logger, string ruleName, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Alert configuration changed, will apply on next evaluation cycle")]
    private static partial void LogConfigChanged(ILogger logger);
}
