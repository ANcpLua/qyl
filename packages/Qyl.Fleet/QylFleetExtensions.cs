// Copyright (c) 2025-2026 ancplua

using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Fleet.Hosting;

/// <summary>Extension methods for wiring the qyl dashboard aggregator into a distributed-app host.</summary>
public static partial class QylFleetExtensions
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error,
        Message = "qyl dashboard aggregator failed to bind (port in use / network unavailable)")]
    static partial void LogBindFailure(ILogger logger, Exception ex);

    /// <summary>
    /// Registers an in-process reverse proxy that fans dashboard REST + SSE requests out to one
    /// or more <c>qyl.collector</c> backends and serves the unified dashboard surface.
    /// </summary>
    /// <example>
    /// <code>
    /// var collectorDev  = builder.AddProject&lt;Projects.Qyl_Collector&gt;("collector-dev");
    /// var collectorProd = builder.AddProject&lt;Projects.Qyl_Collector&gt;("collector-prod");
    ///
    /// builder.AddQylDashboard("dashboard")
    ///        .WithCollector(collectorDev,  new QylCollectorInfo("dev",  "Local dev")  { Environment = "dev" })
    ///        .WithCollector(collectorProd, new QylCollectorInfo("prod", "Production") { Environment = "prod" })
    ///        .WaitFor(collectorDev)
    ///        .WaitFor(collectorProd);
    /// </code>
    /// </example>
    public static IResourceBuilder<QylDashboardResource> AddQylDashboard(
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new QylDashboardResource(name, port);
        var resourceBuilder = builder.AddResource(resource).ExcludeFromManifest();

        builder.Eventing.Subscribe<InitializeResourceEvent>(resource, async (e, ct) =>
        {
            var logger = e.Services.GetRequiredService<ILoggerFactory>().CreateLogger<QylDashboardAggregatorHostedService>();
            var aggregator = new QylDashboardAggregatorHostedService(resource, logger);

            try
            {
                await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct).ConfigureAwait(false);

                await e.Notifications.PublishUpdateAsync(resource, s => s with { State = KnownResourceStates.Starting }).ConfigureAwait(false);

                await aggregator.StartAsync(ct).ConfigureAwait(false);

                var endpoint = resource.Annotations
                    .OfType<EndpointAnnotation>()
                    .First(a => a.Name == QylDashboardResource.PrimaryEndpointName);
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", aggregator.AllocatedPort);

                var url = $"http://localhost:{aggregator.AllocatedPort}/dashboard/";
                await e.Notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = KnownResourceStates.Running,
                    Urls = [new UrlSnapshot("Dashboard", url, IsInternal: false)],
                }).ConfigureAwait(false);

                var lifetime = e.Services.GetRequiredService<IHostApplicationLifetime>();
                lifetime.ApplicationStopping.Register(() =>
                {
                    e.Notifications.PublishUpdateAsync(resource, s => s with { State = KnownResourceStates.Finished }).GetAwaiter().GetResult();
                    aggregator.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    aggregator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                });
            }
            // OperationCanceledException propagates — it's the AppHost shutting the aggregator
            // down, not a startup failure. Everything outside the three bind-level failure
            // types below is a programmer bug and must surface with a full stack trace.
            catch (SocketException ex) { await FailStartAsync(ex).ConfigureAwait(false); }
            catch (IOException ex) { await FailStartAsync(ex).ConfigureAwait(false); }
            catch (HttpRequestException ex) { await FailStartAsync(ex).ConfigureAwait(false); }

            async Task FailStartAsync(Exception ex)
            {
                LogBindFailure(logger, ex);
                await aggregator.DisposeAsync().ConfigureAwait(false);
                await e.Notifications.PublishUpdateAsync(resource, s => s with { State = KnownResourceStates.FailedToStart }).ConfigureAwait(false);
            }
        });

        return resourceBuilder;
    }

    /// <summary>
    /// Wires a <c>qyl.collector</c> resource as a backend of the dashboard. The aggregator fans
    /// reads to every registered collector and routes writes by the collector id prefix.
    /// </summary>
    public static IResourceBuilder<QylDashboardResource> WithCollector<TSource>(
        this IResourceBuilder<QylDashboardResource> builder,
        IResourceBuilder<TSource> collector,
        QylCollectorInfo? info = null,
        string? idPrefix = null)
        where TSource : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(collector);

        info ??= new QylCollectorInfo(collector.Resource.Name);

        builder.WithAnnotation(new QylCollectorAnnotation(collector.Resource, idPrefix, info));
        builder.WithRelationship(collector.Resource, "qyl-collector");

        return builder;
    }
}
