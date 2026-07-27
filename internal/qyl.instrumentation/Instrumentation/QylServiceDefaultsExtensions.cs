using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Qyl.Instrumentation;
using Qyl.Instrumentation.ErrorCapture;
using OtelSchemaUrl = Qyl.Telemetry.SemanticConventions.SchemaUrl;
using ContractHealthCheckEntry = Qyl.Api.Contracts.Health.HealthCheckEntry;
using ContractHealthReport = Qyl.Api.Contracts.Health.HealthReport;
using ContractHealthStatus = Qyl.Api.Contracts.Health.HealthStatus;

namespace Qyl.Instrumentation.Instrumentation;

public static class QylServiceDefaultsExtensions
{
    public static WebApplication MapQylEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetService<QylOptions>() ?? new QylOptions();

        if (options.EnableDefaultHealthEndpoints)
        {
            app.MapHealthChecks(QylEndpoints.Health,
                new HealthCheckOptions
                {
                    Predicate = static check => check.Tags.Contains(QylEndpoints.ReadyTag),
                    ResponseWriter = WriteHealthReportJsonAsync
                });

            app.MapHealthChecks(QylEndpoints.Alive,
                new HealthCheckOptions
                {
                    Predicate = static check => check.Tags.Contains(QylEndpoints.LiveTag),
                    ResponseWriter = WriteHealthReportJsonAsync
                });
        }

        app.UseMiddleware<ExceptionCaptureMiddleware>();

        if (options.EnableOpenApi)
        {
            app.MapOpenApi().CacheOutput();
        }

        return app;
    }

    private static Task WriteHealthReportJsonAsync(HttpContext context, HealthReport report)
    {
        var contract = new ContractHealthReport
        {
            Status = ToContractStatus(report.Status),
            ContractRevision = Qyl.Api.Contracts.ContractRevision.Value,
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Entries = report.Entries.ToDictionary(
                static pair => pair.Key,
                static pair => new ContractHealthCheckEntry
                {
                    Status = ToContractStatus(pair.Value.Status),
                    Description = pair.Value.Description,
                    DurationMs = pair.Value.Duration.TotalMilliseconds
                },
                StringComparer.Ordinal)
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            contract,
            QylHealthJsonContext.Default.HealthReport,
            context.RequestAborted);
    }

    private static ContractHealthStatus ToContractStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ContractHealthStatus.Healthy,
        HealthStatus.Degraded => ContractHealthStatus.Degraded,
        _ => ContractHealthStatus.Unhealthy
    };


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
#if NET10_0_OR_GREATER
            json.RespectNullableAnnotations = true;
            json.RespectRequiredConstructorParameters = true;
#endif
            options.ConfigureJson?.Invoke(json);
        }

        services.Configure<JsonOptions>(opt => Json(opt.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt => Json(opt.SerializerOptions));
    }

    // Composition of the producer pipeline belongs to Qyl.Telemetry.Hosting, which owns the OTel wiring, the
    // instrumentation inventory, the version-pinned external GenAI sources, and collector
    // discovery. This process is a customer of that package like any other application; the only
    // things it adds are the facts Qyl.Telemetry.Hosting cannot know about it.
    internal static void ConfigureQylTelemetry<TBuilder>(TBuilder builder, QylOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        // OTEL_SERVICE_NAME wins over the assembly name so two processes of the same project (e.g. a
        // collector plus its dedicated diagnostics collector) keep distinct service identities.
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } configuredName
            ? configuredName
            : builder.Environment.ApplicationName;

        builder.AddQyl(qyl =>
        {
            qyl.ServiceName = serviceName;
            qyl.ServiceVersion = BuildVersion.ProductVersion;
            qyl.EnableCollectorDiscovery = options.EnableAutoDiscovery;

            // This process is itself an OTLP destination. "No endpoint configured" must mean "do
            // not export", never the exporter's localhost default — that default is our own ingest
            // port, and exporting there would feed our output back into our input.
            qyl.RequireConfiguredEndpoint = true;

            qyl.ResourceAttributes.Add(
                new KeyValuePair<string, object>("telemetry.schema_url", OtelSchemaUrl.Current));

            qyl.AdditionalSources.Add(serviceName);

            // OTEL_SERVICE_NAME may rename the service, but apps following the assembly-name
            // ActivitySource convention must stay subscribed.
            if (!string.Equals(serviceName, builder.Environment.ApplicationName, StringComparison.Ordinal))
                qyl.AdditionalSources.Add(builder.Environment.ApplicationName);

            qyl.AdditionalSources.Add(ActivitySources.ErrorCapture);

            foreach (var source in options.AdditionalActivitySources)
                qyl.AdditionalSources.Add(source);

            // Which endpoints are noise is this application's policy, not the producer's.
            qyl.ConfigureTracing = static tracing => tracing.AddProcessor(new HealthProbeSpanFilter());
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
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue(env.ApplicationName, BuildVersion.ProductVersion));
            });
        });
    }

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public TBuilder UseQyl(Action<QylOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (builder.Services.Any(static d => d.ServiceType == typeof(QylServiceDefaultsMarker)))
                return builder;
            builder.Services.AddSingleton<QylServiceDefaultsMarker>();

            var options = new QylOptions();
            configure?.Invoke(options);

            builder.Services.TryAddSingleton(options);

            ConfigureServiceProvider(builder, options);
            ConfigureKestrel(builder.Services);
            ConfigureJson(builder.Services, options);

            ConfigureQylTelemetry(builder, options);
            if (options.EnableDefaultHealthChecks)
            {
                ConfigureHealthChecks(builder);
            }

            ConfigureHttpClients(builder);

            // OpenAPI registers EndpointMetadataApiDescriptionProvider, which needs EndpointDataSource —
            // a service only the web host (WebApplicationBuilder) provides. Skip it on generic hosts
            // (console/worker apps) so ValidateOnBuild doesn't throw.
            if (options.EnableOpenApi && builder is WebApplicationBuilder)
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

    public Action<JsonSerializerOptions>? ConfigureJson { get; set; }
}

internal sealed class QylServiceDefaultsMarker;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ContractHealthReport))]
internal sealed partial class QylHealthJsonContext : JsonSerializerContext;
