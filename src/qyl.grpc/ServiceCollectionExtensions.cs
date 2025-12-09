using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using qyl.Grpc.Abstractions;
using qyl.Grpc.Api;
using qyl.Grpc.Models;
using qyl.Grpc.Protocol;
using qyl.Grpc.Services;
using qyl.Grpc.Stores;
using qyl.Grpc.Streaming;

namespace qyl.Grpc;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OTLP gRPC receiver services to the service collection.
    /// </summary>
    public static IServiceCollection AddOtlpGrpcReceiver(this IServiceCollection services)
    {
        // Protocol converter
        services.AddSingleton<OtlpConverter>();

        // In-memory stores
        services.AddSingleton<ITelemetryStore<SpanModel>>(new InMemoryTelemetryStore<SpanModel>());
        services.AddSingleton<ITelemetryStore<MetricModel>>(new InMemoryTelemetryStore<MetricModel>());
        services.AddSingleton<ITelemetryStore<LogModel>>(new InMemoryTelemetryStore<LogModel>());

        // Aggregators and registries
        services.AddSingleton<ITraceAggregator, TraceAggregator>();
        services.AddSingleton<ISessionAggregator, SessionAggregator>();
        services.AddSingleton<ServiceRegistry>();
        services.AddSingleton<IServiceRegistry>(sp => sp.GetRequiredService<ServiceRegistry>());

        // SSE broadcasting
        services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

        // Streaming broadcaster (bridges to SSE)
        services.AddSingleton<ITelemetryBroadcaster>(sp =>
            new TelemetryBroadcaster(sp.GetRequiredService<ITelemetrySseBroadcaster>()));

        return services;
    }

    /// <summary>
    /// Maps OTLP gRPC services for traces, metrics, and logs.
    /// </summary>
    public static IEndpointRouteBuilder MapOtlpGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<OtlpTraceService>();
        endpoints.MapGrpcService<OtlpMetricsService>();
        endpoints.MapGrpcService<OtlpLogsService>();

        return endpoints;
    }

    /// <summary>
    /// Maps all telemetry REST API endpoints (sessions, SSE).
    /// </summary>
    public static IEndpointRouteBuilder MapTelemetryApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSessionApi();
        endpoints.MapTelemetrySse();

        return endpoints;
    }
}
