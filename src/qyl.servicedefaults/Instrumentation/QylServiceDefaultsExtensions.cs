// =============================================================================
// Qyl.ServiceDefaults - Zero-Config Observability for .NET
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
using Qyl.ServiceDefaults.Instrumentation;

namespace Qyl.ServiceDefaults;

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
///             <description>Use <c>.UseQylInstrumentation()</c> in ChatClientBuilder</description>
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
        "Microsoft.Agents.AI"
    ];

    private static readonly string[] SGenAiMeterNames =
    [
        ActivitySources.GenAi,
        "Microsoft.Extensions.AI",
        "Microsoft.Agents.AI"
    ];

    /// <summary>
    ///     Adds qyl service defaults with OpenTelemetry, health checks, and resilience.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    ///     <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.UseQyl();
    /// </code>
    /// </example>
    public static TBuilder UseQyl<TBuilder>(
        this TBuilder builder,
        Action<QylOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new QylOptions();
        configure?.Invoke(options);

        // Register options for later use
        builder.Services.TryAddSingleton(options);

        // Core services
        ConfigureServiceProvider(builder, options);
        ConfigureKestrel(builder.Services);
        ConfigureJson(builder.Services, options);

        // Observability
        ConfigureOpenTelemetry(builder, options);
        ConfigureHealthChecks(builder);

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

        return builder;
    }

    /// <summary>
    ///     Maps qyl default endpoints (health, OpenAPI).
    /// </summary>
    public static void MapQylEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetService<QylOptions>() ?? new QylOptions();

        // Health endpoints (Aspire-compatible)
        app.MapHealthChecks("/health",
            new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") });

        app.MapHealthChecks("/alive",
            new HealthCheckOptions { Predicate = static check => check.Tags.Contains("live") });

        // OpenAPI
        if (options.EnableOpenApi)
        {
            app.MapOpenApi().CacheOutput();
        }
    }

    // =========================================================================
    // Private Configuration Methods
    // =========================================================================

    private static void ConfigureServiceProvider<TBuilder>(TBuilder builder, QylOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        if (!options.ValidateOnBuild) return;

        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true, ValidateScopes = true
        }));
    }

    private static void ConfigureKestrel(IServiceCollection services) =>
        services.Configure<KestrelServerOptions>(static options =>
        {
            options.AddServerHeader = false;
        });

    private static void ConfigureJson(IServiceCollection services, QylOptions options)
    {
        var configureJson = (JsonSerializerOptions json) =>
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
        };

        services.Configure<JsonOptions>(opt => configureJson(opt.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt => configureJson(opt.SerializerOptions));
    }

    private static void ConfigureOpenTelemetry<TBuilder>(TBuilder builder, QylOptions options)
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

        // OpenTelemetry SDK
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName, serviceVersion: serviceVersion);
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

                // Custom meters from options
                foreach (var meter in options.AdditionalMeterNames)
                    metrics.AddMeter(meter);

                options.ConfigureMetrics?.Invoke(metrics);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(serviceName)
                    .AddAspNetCoreInstrumentation(aspnet =>
                    {
                        aspnet.Filter = static ctx =>
                            ctx.Request.Path != "/health" &&
                            ctx.Request.Path != "/alive";
                    })
                    .AddHttpClientInstrumentation();

                // GenAI activity sources (qyl + SDK providers)
                foreach (var source in SGenAiActivitySources)
                    tracing.AddSource(source);

                // Db instrumentation
                tracing.AddSource(ActivitySources.Db);

                // Custom sources from options
                foreach (var source in options.AdditionalActivitySources)
                    tracing.AddSource(source);

                options.ConfigureTracing?.Invoke(tracing);
            });

        // OTLP exporter (auto-enabled if endpoint configured)
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    private static void ConfigureHealthChecks<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck("ready", static () => HealthCheckResult.Healthy(), ["ready"]);

    private static void ConfigureHttpClients<TBuilder>(TBuilder builder)
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
    ///     Additional activity sources to register for tracing.
    /// </summary>
    public List<string> AdditionalActivitySources { get; } = [];

    /// <summary>
    ///     Additional meter names to register for metrics.
    /// </summary>
    public List<string> AdditionalMeterNames { get; } = [];

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
}
