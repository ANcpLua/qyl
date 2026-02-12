namespace qyl.collector.Alerting;

/// <summary>
///     DI registration for the alerting subsystem.
///     Call <c>AddAlertingServices()</c> in DI setup and <c>MapAlertEndpoints()</c> on the endpoint builder.
/// </summary>
public static class AlertServiceExtensions
{
    /// <summary>
    ///     Registers all alerting services: config loader, evaluator, notifier, and background service.
    /// </summary>
    public static IServiceCollection AddAlertingServices(this IServiceCollection services)
    {
        services.AddSingleton<AlertConfigLoader>();
        services.AddSingleton<AlertEvaluator>();
        services.AddSingleton<AlertNotifier>();
        services.AddHostedService<AlertService>();
        services.AddHttpClient("AlertWebhook");

        return services;
    }

    /// <summary>
    ///     Maps alert REST API endpoints onto the endpoint route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder endpoints) =>
        AlertEndpoints.MapAlertEndpoints(endpoints);

    /// <summary>
    ///     Initializes the alert history DuckDB schema.
    ///     Call during app startup after DuckDbStore is constructed.
    /// </summary>
    public static void InitializeAlertSchema(this DuckDbStore store)
    {
        DuckDbSchemaAlerts.InitializeAlertSchema(store.Connection);
    }
}
