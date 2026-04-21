// Copyright (c) 2025-2026 ancplua

using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Qyl.Fleet.Hosting;

/// <summary>Registers the qyl dashboard aggregator on a plain ASP.NET host.</summary>
public static class QylFleetExtensions
{
    /// <summary>
    /// Registers an <see cref="IHostedService"/> that runs an in-process reverse proxy routing
    /// dashboard REST + SSE across the configured <c>qyl.collector</c> backends.
    /// </summary>
    /// <example>
    /// <code>
    /// var host = Host.CreateApplicationBuilder(args);
    /// host.Services.AddQylFleet(fleet =>
    /// {
    ///     fleet.Port = 5050;
    ///     fleet.WithCollector("dev",  new Uri("http://localhost:5100"), description: "Local dev");
    ///     fleet.WithCollector("prod", new Uri("https://collector.prod"),
    ///                         description: "Production", environment: "prod");
    /// });
    /// host.Build().Run();
    /// </code>
    /// </example>
    public static IServiceCollection AddQylFleet(this IServiceCollection services, Action<QylFleetBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new QylFleetOptions();
        configure(new QylFleetBuilder(options));

        services.AddHttpClient("qyl-fleet-proxy")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton(Options.Create(options));
        services.AddHostedService<QylDashboardAggregatorHostedService>();

        return services;
    }
}

/// <summary>Fluent builder for <see cref="QylFleetOptions"/>.</summary>
public sealed class QylFleetBuilder
{
    private readonly QylFleetOptions _options;

    internal QylFleetBuilder(QylFleetOptions options) => _options = options;

    /// <summary>Port the aggregator listens on. <c>0</c> picks a free port.</summary>
    public int Port { get => _options.Port; set => _options.Port = value; }

    /// <summary>Bind host. Defaults to loopback.</summary>
    public string Host { get => _options.Host; set => _options.Host = value; }

    /// <summary>
    /// Registers a collector backend. The <paramref name="id"/> becomes the URL prefix for
    /// routed requests (e.g. <c>/api/v1/traces/{id}/...</c>).
    /// </summary>
    public QylFleetBuilder WithCollector(
        string id,
        Uri endpoint,
        string? description = null,
        string? name = null,
        string environment = "dev",
        string? region = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(endpoint);

        _options.Collectors.Add(new QylCollectorInfo(id, endpoint, description)
        {
            Name = name ?? id,
            Environment = environment,
            Region = region,
        });
        return this;
    }
}
