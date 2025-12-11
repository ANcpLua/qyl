using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using qyl.grpc.Abstractions;
using qyl.grpc.Aggregation;
using qyl.grpc.Api;
using qyl.grpc.Models;
using qyl.grpc.Protocol;
using qyl.grpc.Services;
using qyl.grpc.Stores;
using qyl.grpc.Streaming;

namespace qyl.grpc;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtlpGrpcReceiver(this IServiceCollection services)
    {
        services.AddSingleton<OtlpConverter>();

        services.AddSingleton<ITelemetryStore<SpanModel>>(new InMemoryTelemetryStore<SpanModel>());
        services.AddSingleton<ITelemetryStore<MetricModel>>(new InMemoryTelemetryStore<MetricModel>());
        services.AddSingleton<ITelemetryStore<LogModel>>(new InMemoryTelemetryStore<LogModel>());

        services.AddSingleton<ITraceAggregator, TraceAggregator>();
        services.AddSingleton<ISessionAggregator, SessionAggregator>();
        services.AddSingleton<ServiceRegistry>();
        services.AddSingleton<IServiceRegistry>(sp => sp.GetRequiredService<ServiceRegistry>());

        services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

        services.AddSingleton<ITelemetryBroadcaster>(sp =>
            new TelemetryBroadcaster(sp.GetRequiredService<ITelemetrySseBroadcaster>()));

        return services;
    }

    public static IEndpointRouteBuilder MapOtlpGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<OtlpTraceService>();
        endpoints.MapGrpcService<OtlpMetricsService>();
        endpoints.MapGrpcService<OtlpLogsService>();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapTelemetryApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSessionApi();
        endpoints.MapTelemetrySse();

        return endpoints;
    }
}
