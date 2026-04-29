using ANcpLua.Roslyn.Utilities;
// =============================================================================
// Qyl.Instrumentation - Zero-Config Observability for .NET
// Single entry point: builder.UseQyl() and app.MapQylEndpoints()
// =============================================================================

using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qyl.Contracts.Observability;
using Qyl.Instrumentation.Discovery;
using Qyl.Instrumentation.ErrorCapture;
using Qyl.Instrumentation.Instrumentation.Inventory;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Extension methods to configure qyl service defaults with zero-config observability.
/// </summary>
/// <remarks>
///     <para>Provides two instrumentation paths:</para>
///     <list type="number">
///         <item>
///             <term>Interceptors (compile-time)</term>
///             <description>Automatic via source generator - no code changes needed</description>
///         </item>
///         <item>
///             <term>IChatClient decorator (runtime)</term>
///             <description>Use <c>.UseQylTelemetry()</c> in ChatClientBuilder</description>
///         </item>
///     </list>
/// </remarks>
public static class QylServiceDefaultsExtensions
{
    private static readonly string[] SGenAiActivitySources =
    [
        ActivitySources.GenAi,
        "OpenAI.*",
        "Azure.AI.OpenAI.*",
        "Anthropic.*",
        "Microsoft.Extensions.AI",
        "Microsoft.Agents.AI",
        "Experimental.Microsoft.Agents.AI"
    ];

    private static readonly string[] SGenAiMeterNames =
    [
        ActivitySources.GenAi,
        "Microsoft.Extensions.AI",
        "Microsoft.Agents.AI",
        "Experimental.Microsoft.Agents.AI"
    ];

    /// <summary>
    ///     Maps qyl default endpoints (health probes, OpenAPI) and wires the exception-capture
    ///     middleware. Returns the app for chaining: <c>app.MapQylEndpoints().MapMyStuff();</c>
    /// </summary>
    public static WebApplication MapQylEndpoints(this WebApplication app)
    {
        Guard.NotNull(app);

        var options = app.Services.GetService<QylOptions>() ?? new QylOptions();

        if (options.EnableDefaultHealthEndpoints)
        {
            // Aspire-compatible probes. Paths + tag names come from Qyl.Contracts.Observability.QylEndpoints
            // so qyl.mcp (which doesn't reference qyl.instrumentation) stays in lockstep.
            app.MapHealthChecks(QylEndpoints.Health,
                new HealthCheckOptions { Predicate = static check => check.Tags.Contains(QylEndpoints.ReadyTag) });

            app.MapHealthChecks(QylEndpoints.Alive,
                new HealthCheckOptions { Predicate = static check => check.Tags.Contains(QylEndpoints.LiveTag) });
        }

        app.UseMiddleware<ExceptionCaptureMiddleware>();

        if (options.EnableOpenApi)
        {
            app.MapOpenApi().CacheOutput();
        }

        // PRD #173: agent inventory under /qyl/inventory/agents — gated on auth or dev-only.
        app.MapQylAgentInventory();

        return app;
    }

    // =========================================================================
    // Private Configuration Methods
    // =========================================================================

    internal static void ConfigureServiceProvider<TBuilder>(TBuilder builder, QylOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        if (!options.ValidateOnBuild) return;

        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true, ValidateScopes = true
        }));
    }

    internal static void ConfigureKestrel(IServiceCollection services) =>
        services.Configure<KestrelServerOptions>(static options =>
        {
            options.AddServerHeader = false;
        });

    internal static void ConfigureJson(IServiceCollection services, QylOptions options)
    {
        void Json(JsonSerializerOptions json)
        {
            json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
#if NET10_0_OR_GREATER
            json.RespectNullableAnnotations = true;
            json.RespectRequiredConstructorParameters = true;
#endif
            options.ConfigureJson?.Invoke(json);
        }

        services.Configure<JsonOptions>(opt => Json(opt.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt => Json(opt.SerializerOptions));
    }

    internal static void ConfigureOpenTelemetry<TBuilder>(TBuilder builder, QylOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

        // Logging
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            options.ConfigureLogging?.Invoke(logging);
        });

        // Resolve exporter destination before wiring tracing.
        // Sources are only registered when a collector is reachable — without them
        // HasListeners() returns false and StartActivity() returns null at zero cost.
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(otlpEndpoint) && options.EnableAutoDiscovery)
        {
            var discovered = CollectorDiscovery.DiscoverEndpoint();
            if (discovered is not null)
            {
                // Set env var so UseOtlpExporter() picks it up
                Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", discovered.ToString());
                otlpEndpoint = discovered.ToString();
            }
        }

        var hasExporter = !string.IsNullOrWhiteSpace(otlpEndpoint);

        // OpenTelemetry SDK
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName, serviceVersion: serviceVersion)
                    .AddAttributes([
                        new KeyValuePair<string, object>("telemetry.schema_url",
                            "https://opentelemetry.io/schemas/1.40.0")
                    ]);

                // Compile-time capability manifest (populated by source generator)
                if (options.CapabilityAttributes.Count > 0)
                    resource.AddAttributes(options.CapabilityAttributes);

                options.ConfigureResource?.Invoke(resource);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                // GenAI meters (qyl + SDK providers)
                foreach (var meter in SGenAiMeterNames)
                    metrics.AddMeter(meter);

                // Db meter
                metrics.AddMeter(ActivitySources.Db);

                // Agent meter
                metrics.AddMeter(ActivitySources.Agent);

                // Custom meters from options
                foreach (var meter in options.AdditionalMeterNames)
                    metrics.AddMeter(meter);

                options.ConfigureMetrics?.Invoke(metrics);
            })
            .WithTracing(tracing =>
            {
                tracing.SetSampler(options.ObservabilityMode switch
                {
                    ObservabilityMode.OnDemand => new AlwaysOffSampler(),
                    ObservabilityMode.Warm => new ParentBasedSampler(new AlwaysOffSampler()),
                    _ => new ParentBasedSampler(new AlwaysOnSampler())
                });

                // Only register sources when there is an exporter to consume them.
                // Without sources the OTel ActivityListener's ShouldListenTo returns false
                // for every ActivitySource → HasListeners() = false → StartActivity() = null
                // → zero allocation. Sources are registered dynamically when a collector
                // connects (Phase 1 of zero-cost-until-observed).
                if (hasExporter)
                {
                    tracing
                        .AddSource(serviceName)
                        .AddAspNetCoreInstrumentation(aspnet =>
                        {
                            aspnet.Filter = static ctx =>
                                ctx.Request.Path != QylEndpoints.Health &&
                                ctx.Request.Path != QylEndpoints.Alive;
                        })
                        .AddHttpClientInstrumentation();

                    // GenAI activity sources (qyl + SDK providers)
                    foreach (var source in SGenAiActivitySources)
                        tracing.AddSource(source);

                    // Db + traced + agent + MCP instrumentation
                    tracing.AddSource(ActivitySources.Db);
                    tracing.AddSource(ActivitySources.Traced);
                    tracing.AddSource(ActivitySources.Agent);
                    tracing.AddSource(ActivitySources.Mcp);

                    // Custom sources from options
                    foreach (var source in options.AdditionalActivitySources)
                        tracing.AddSource(source);
                }

                options.ConfigureTracing?.Invoke(tracing);
            });

        if (hasExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    internal static void ConfigureHealthChecks<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), [QylEndpoints.LiveTag])
            .AddCheck("ready", static () => HealthCheckResult.Healthy(), [QylEndpoints.ReadyTag]);

    internal static void ConfigureHttpClients<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(static http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
            http.ConfigureHttpClient(static (sp, client) =>
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue(env.ApplicationName, version));
            });
        });
    }

    /// <param name="builder">The host application builder.</param>
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        ///     Adds qyl service defaults with OpenTelemetry, health checks, and resilience.
        /// </summary>
        /// <param name="configure">Optional configuration callback.</param>
        /// <returns>The builder for chaining.</returns>
        /// <example>
        ///     <code>
        /// var builder = WebApplication.CreateBuilder(args);
        /// builder.UseQyl();
        /// </code>
        /// </example>
        public TBuilder UseQyl(Action<QylOptions>? configure = null)
        {
            Guard.NotNull(builder);

            // Idempotency guard: the source-generated interceptor may also call
            // TryUseQylConventions → UseQyl, causing double OTel registration.
            if (builder.Services.Any(static d => d.ServiceType == typeof(QylServiceDefaultsMarker)))
                return builder;
            builder.Services.AddSingleton<QylServiceDefaultsMarker>();

            var options = new QylOptions();
            configure?.Invoke(options);

            // Register options for later use
            builder.Services.TryAddSingleton(options);

            // PRD #173: agent inventory singleton + observable gauge.
            builder.Services.AddQylAgentInventory();

            // Core services
            ConfigureServiceProvider(builder, options);
            ConfigureKestrel(builder.Services);
            ConfigureJson(builder.Services, options);

            // Observability (includes auto-discovery)
            ConfigureOpenTelemetry(builder, options);
            if (options.EnableDefaultHealthChecks)
            {
                ConfigureHealthChecks(builder);
            }

            // Log discovery result once at startup
            if (options.EnableAutoDiscovery)
            {
                builder.Services.AddHostedService<CollectorDiscoveryLogger>();
            }

            // Resilience & Discovery
            ConfigureHttpClients(builder);

            // Optional features
            if (options.EnableOpenApi)
                builder.Services.AddOpenApi();

            if (options.EnableAntiforgery)
                builder.Services.AddAntiforgery();

            if (options.EnableValidation)
            {
#if NET10_0_OR_GREATER
                builder.Services.AddValidation();
#endif
            }

            builder.Services.AddProblemDetails();

            // Exception capture (AppDomain + TaskScheduler hooks)
            builder.Services.AddHostedService<ExceptionHookRegistrar>();

            return builder;
        }

        /// <summary>
        ///     Alias for <see cref="UseQyl{TBuilder}(TBuilder, Action{QylOptions}?)" />.
        ///     Adds qyl service defaults including OTel, health checks, and resilience.
        /// </summary>
        /// <param name="configure">Optional configuration callback.</param>
        /// <returns>The builder for chaining.</returns>
        public TBuilder AddQylServiceDefaults(Action<QylOptions>? configure = null) =>
            builder.UseQyl(configure);
    }
}

/// <summary>
///     Configuration options for qyl service defaults.
/// </summary>
public sealed class QylOptions
{
    /// <summary>
    ///     Validate DI container on build. Default: true in Development.
    /// </summary>
    public bool ValidateOnBuild { get; set; } = true;

    /// <summary>
    ///     Enable OpenAPI endpoint. Default: true.
    /// </summary>
    public bool EnableOpenApi { get; set; } = true;

    /// <summary>
    ///     Enable antiforgery protection. Default: false.
    /// </summary>
    public bool EnableAntiforgery { get; set; }

    /// <summary>
    ///     Enable .NET 10 validation. Default: true.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    ///     Register default liveness/readiness health checks in UseQyl.
    ///     Disable when the host provides its own named health checks.
    /// </summary>
    public bool EnableDefaultHealthChecks { get; set; } = true;

    /// <summary>
    ///     Map default /health and /alive endpoints in MapQylEndpoints.
    ///     Disable when the host exposes custom health endpoints.
    /// </summary>
    public bool EnableDefaultHealthEndpoints { get; set; } = true;

    /// <summary>
    ///     Enable automatic collector discovery via network probes when no
    ///     explicit OTEL_EXPORTER_OTLP_ENDPOINT is configured. Default: true.
    /// </summary>
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <summary>
    ///     Additional activity sources to register for tracing.
    /// </summary>
    public List<string> AdditionalActivitySources { get; } = [];

    /// <summary>
    ///     Additional meter names to register for metrics.
    /// </summary>
    public List<string> AdditionalMeterNames { get; } = [];

    /// <summary>
    ///     Compile-time capability attributes to register as OTel Resource attributes.
    ///     Populated by the source generator; consumers should not modify directly.
    /// </summary>
    public List<KeyValuePair<string, object>> CapabilityAttributes { get; } = [];

    /// <summary>
    ///     Custom JSON serializer configuration.
    /// </summary>
    public Action<JsonSerializerOptions>? ConfigureJson { get; set; }

    /// <summary>
    ///     Custom OpenTelemetry logging configuration.
    /// </summary>
    public Action<OpenTelemetryLoggerOptions>? ConfigureLogging { get; set; }

    /// <summary>
    ///     Custom OpenTelemetry resource configuration.
    /// </summary>
    public Action<ResourceBuilder>? ConfigureResource { get; set; }

    /// <summary>
    ///     Custom OpenTelemetry metrics configuration.
    /// </summary>
    public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

    /// <summary>
    ///     Custom OpenTelemetry tracing configuration.
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

    /// <summary>
    ///     Controls the default sampling strategy for all ActivitySources.
    ///     <value>The default is <see cref="ObservabilityMode.AlwaysOn" />.</value>
    /// </summary>
    public ObservabilityMode ObservabilityMode { get; set; } = ObservabilityMode.AlwaysOn;
}

/// <summary>
///     Controls the default sampling strategy used by <c>UseQyl()</c>.
/// </summary>
public enum ObservabilityMode
{
    /// <summary>
    ///     All spans are created and exported. Equivalent to <c>ParentBasedSampler(AlwaysOnSampler)</c>.
    ///     Use in development, staging, or small-scale production where full visibility outweighs overhead.
    /// </summary>
    AlwaysOn,

    /// <summary>
    ///     All ActivitySources are dormant until a subscription activates them via <c>POST /api/v1/observe</c>.
    ///     Root <c>StartActivity()</c> calls return <see langword="null" /> — zero allocation.
    ///     Use in production environments where services are instrumented comprehensively but observed selectively.
    /// </summary>
    OnDemand,

    /// <summary>
    ///     Root spans are zero-cost, but child spans with a propagated W3C trace context create a minimal
    ///     <see cref="System.Diagnostics.Activity" /> to preserve the trace ID chain across service boundaries.
    ///     Equivalent to <c>ParentBasedSampler(AlwaysOffSampler)</c>.
    ///     Use in distributed systems where trace continuity matters but most services should not actively export.
    /// </summary>
    Warm
}

/// <summary>Marker to prevent double UseQyl registration when source-generated interceptors also call it.</summary>
internal sealed class QylServiceDefaultsMarker;
