using ANcpLua.Roslyn.Utilities;

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
using Qyl.Instrumentation.Instrumentation.GenAi;
using Qyl.Instrumentation.Instrumentation.Inventory;

namespace Qyl.Instrumentation.Instrumentation;

public static class QylServiceDefaultsExtensions
{
    private static readonly string[] s_qylActivitySources =
    [
        "qyl.genai",
        "qyl.db",
        "qyl.traced",
        "qyl.agent",
        "qyl.mcp",
        "Qyl.Instrumentation.ErrorCapture"
    ];

    private static readonly string[] s_genAiExternalActivitySources =
    [
        "OpenAI.*",
        "Azure.AI.OpenAI.*",
        "Anthropic.*",
        "Microsoft.Extensions.AI",
        "Microsoft.Agents.AI",
        "Experimental.Microsoft.Agents.AI"
    ];

    private static readonly string[] s_genAiExternalMeterNames =
    [
        "Microsoft.Extensions.AI",
        "Microsoft.Agents.AI",
        "Experimental.Microsoft.Agents.AI"
    ];

    public static WebApplication MapQylEndpoints(this WebApplication app)
    {
        Guard.NotNull(app);

        var options = app.Services.GetService<QylOptions>() ?? new QylOptions();

        if (options.EnableDefaultHealthEndpoints)
        {
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

        app.MapQylAgentInventory();

        return app;
    }


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

    internal static void ConfigureQylTelemetry<TBuilder>(TBuilder builder, QylOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(otlpEndpoint) && options.EnableAutoDiscovery)
        {
            var discovered = CollectorDiscovery.DiscoverEndpoint();
            if (discovered is not null)
            {
                Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", discovered.ToString());
                otlpEndpoint = discovered.ToString();
            }
        }

        var hasExporter = !string.IsNullOrWhiteSpace(otlpEndpoint);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            options.ConfigureLogging?.Invoke(logging);

            if (hasExporter)
                logging.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName, serviceVersion: serviceVersion)
                    .AddAttributes([
                        new KeyValuePair<string, object>("telemetry.schema_url",
                            "https://opentelemetry.io/schemas/1.40.0")
                    ]);

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

                metrics.AddMeter(ActivitySources.GenAi);

                foreach (var meter in s_genAiExternalMeterNames)
                    metrics.AddMeter(meter);

                metrics.AddMeter(ActivitySources.Db);

                metrics.AddMeter(ActivitySources.Agent);

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

                tracing
                    .AddSource(serviceName)
                    .AddAspNetCoreInstrumentation(aspnet =>
                    {
                        aspnet.Filter = static ctx =>
                            ctx.Request.Path != QylEndpoints.Health &&
                            ctx.Request.Path != QylEndpoints.Alive;
                    })
                    .AddHttpClientInstrumentation();

                foreach (var source in s_genAiExternalActivitySources)
                    tracing.AddSource(source);

                foreach (var source in s_qylActivitySources)
                    tracing.AddSource(source);

                foreach (var source in options.AdditionalActivitySources)
                    tracing.AddSource(source);

                tracing.AddProcessor(new QylGenAiCostProcessor());
                tracing.AddProcessor<QylAgentActivityProcessor>();

                options.ConfigureTracing?.Invoke(tracing);

                if (hasExporter)
                    tracing.AddOtlpExporter();
            });
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

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public TBuilder UseQyl(Action<QylOptions>? configure = null)
        {
            Guard.NotNull(builder);

            if (builder.Services.Any(static d => d.ServiceType == typeof(QylServiceDefaultsMarker)))
                return builder;
            builder.Services.AddSingleton<QylServiceDefaultsMarker>();

            var options = new QylOptions();
            configure?.Invoke(options);

            builder.Services.TryAddSingleton(options);

            builder.Services.AddQylAgentInventory();

            ConfigureServiceProvider(builder, options);
            ConfigureKestrel(builder.Services);
            ConfigureJson(builder.Services, options);

            ConfigureQylTelemetry(builder, options);
            if (options.EnableDefaultHealthChecks)
            {
                ConfigureHealthChecks(builder);
            }

            if (options.EnableAutoDiscovery)
            {
                builder.Services.AddHostedService<CollectorDiscoveryLogger>();
            }

            ConfigureHttpClients(builder);

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

            builder.Services.AddHostedService<ExceptionHookRegistrar>();

            return builder;
        }

        public TBuilder AddQylServiceDefaults(Action<QylOptions>? configure = null) =>
            builder.UseQyl(configure);
    }
}

public sealed class QylOptions
{
    public bool ValidateOnBuild { get; set; } = true;

    public bool EnableOpenApi { get; set; } = true;

    public bool EnableAntiforgery { get; set; }

    public bool EnableValidation { get; set; } = true;

    public bool EnableDefaultHealthChecks { get; set; } = true;

    public bool EnableDefaultHealthEndpoints { get; set; } = true;

    public bool EnableAutoDiscovery { get; set; } = true;

    public List<string> AdditionalActivitySources { get; } = [];

    public List<string> AdditionalMeterNames { get; } = [];

    public List<KeyValuePair<string, object>> CapabilityAttributes { get; } = [];

    public Action<JsonSerializerOptions>? ConfigureJson { get; set; }

    public Action<OpenTelemetryLoggerOptions>? ConfigureLogging { get; set; }

    public Action<ResourceBuilder>? ConfigureResource { get; set; }

    public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

    public ObservabilityMode ObservabilityMode { get; set; } = ObservabilityMode.AlwaysOn;
}

public enum ObservabilityMode
{
    AlwaysOn,

    OnDemand,

    Warm
}

internal sealed class QylServiceDefaultsMarker;
