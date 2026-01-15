using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TelemetryLab.Net10.Api.Domain.Telemetry;

/// <summary>
/// .NET 10 Telemetry Setup
///
/// New features:
/// ✅ ActivitySourceOptions with TelemetrySchemaUrl
/// ✅ MeterOptions with TelemetrySchemaUrl
/// ✅ Activity Links and Events serialization
/// </summary>
public static class AppTelemetry
{
    public const string ServiceName = "TelemetryLab.Net10";
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// OTel Semantic Conventions v1.38 schema URL.
    /// This enables schema-aware telemetry processing and migration.
    /// </summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.39.0";

    // 🆕 .NET 10: ActivitySourceOptions with TelemetrySchemaUrl
    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaUrl  // 🆕 .NET 10
    });

    // 🆕 .NET 10: MeterOptions with TelemetrySchemaUrl
    public static readonly Meter Meter = new(new MeterOptions(ServiceName)
    {
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaUrl  // 🆕 .NET 10
    });

    // Metrics
    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>(
            "orders.created",
            unit: "{order}",
            description: "Number of orders created");

    public static readonly Counter<long> OrdersProcessed =
        Meter.CreateCounter<long>(
            "orders.processed",
            unit: "{order}",
            description: "Number of orders processed");

    public static readonly Histogram<double> OrderProcessingDuration =
        Meter.CreateHistogram<double>(
            "orders.processing.duration",
            unit: "ms",
            description: "Order processing duration in milliseconds");

    public static readonly Counter<long> LogsBuffered =
        Meter.CreateCounter<long>(
            "logs.buffered",
            unit: "{log}",
            description: "Number of logs buffered");

    public static readonly Counter<long> LogsFlushed =
        Meter.CreateCounter<long>(
            "logs.flushed",
            unit: "{log}",
            description: "Number of buffered logs flushed");

    // Gen AI Metrics (OTel 1.38)
    public static readonly Histogram<long> GenAiTokenUsage =
        Meter.CreateHistogram<long>(
            "gen_ai.client.token.usage",
            unit: "{token}",
            description: "Token usage per request");

    public static readonly Histogram<double> GenAiOperationDuration =
        Meter.CreateHistogram<double>(
            "gen_ai.client.operation.duration",
            unit: "s",
            description: "Gen AI operation duration");
}