namespace qyl.collector.Alerting;

/// <summary>
///     Dispatches alert notifications to configured channels: webhook, console, SSE.
/// </summary>
public sealed partial class AlertNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelemetrySseBroadcaster _broadcaster;
    private readonly ILogger<AlertNotifier> _logger;

    public AlertNotifier(
        IHttpClientFactory httpClientFactory,
        ITelemetrySseBroadcaster broadcaster,
        ILogger<AlertNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    ///     Sends the alert event to all configured notification channels for the rule.
    /// </summary>
    public async Task NotifyAsync(AlertRule rule, AlertEvent alertEvent, CancellationToken ct = default)
    {
        foreach (var channel in rule.Channels)
        {
            try
            {
                switch (channel.Type.ToLowerInvariant())
                {
                    case "webhook":
                        await SendWebhookAsync(channel, alertEvent, ct).ConfigureAwait(false);
                        break;

                    case "sse":
                        SendSse(alertEvent);
                        break;

                    default:
                        SendConsole(rule, alertEvent);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogNotificationError(_logger, channel.Type, rule.Name, ex);
            }
        }
    }

    private async Task SendWebhookAsync(NotificationChannel channel, AlertEvent alertEvent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channel.Url))
        {
            LogWebhookNoUrl(_logger, alertEvent.RuleName);
            return;
        }

        using var client = _httpClientFactory.CreateClient("AlertWebhook");
        using var response = await client.PostAsJsonAsync(channel.Url, alertEvent, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogWebhookFailed(_logger, alertEvent.RuleName, channel.Url, (int)response.StatusCode);
        }
        else
        {
            LogWebhookSent(_logger, alertEvent.RuleName, channel.Url);
        }
    }

    private void SendSse(AlertEvent alertEvent)
    {
        var message = new TelemetryMessage(
            TelemetrySignal.Logs, // Reuse Logs signal for alerts via SSE
            alertEvent,
            alertEvent.FiredAt);

        _broadcaster.Publish(message);
        LogSseSent(_logger, alertEvent.RuleName);
    }

    private void SendConsole(AlertRule rule, AlertEvent alertEvent)
    {
        if (alertEvent.Status == "firing")
        {
            LogAlertFiring(_logger, rule.Name, rule.Description, alertEvent.QueryResult, alertEvent.Condition);
        }
        else
        {
            LogAlertResolved(_logger, rule.Name, rule.Description);
        }
    }

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to send {ChannelType} notification for alert '{RuleName}'")]
    private static partial void LogNotificationError(ILogger logger, string channelType, string ruleName, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook URL not configured for alert '{RuleName}'")]
    private static partial void LogWebhookNoUrl(ILogger logger, string ruleName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook failed for alert '{RuleName}' to {Url}: HTTP {StatusCode}")]
    private static partial void LogWebhookFailed(ILogger logger, string ruleName, string url, int statusCode);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Webhook sent for alert '{RuleName}' to {Url}")]
    private static partial void LogWebhookSent(ILogger logger, string ruleName, string url);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SSE notification sent for alert '{RuleName}'")]
    private static partial void LogSseSent(ILogger logger, string ruleName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ALERT FIRING: {Name} - {Description} (value={QueryResult}, condition={Condition})")]
    private static partial void LogAlertFiring(ILogger logger, string name, string description, double queryResult, string condition);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ALERT RESOLVED: {Name} - {Description}")]
    private static partial void LogAlertResolved(ILogger logger, string name, string description);
}
