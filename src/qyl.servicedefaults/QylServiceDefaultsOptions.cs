using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults;

/// <summary>
///     Options for configuring ANcpSdk default conventions and behaviors.
/// </summary>
public sealed class QylServiceDefaultsOptions
{
    internal bool MapCalled { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to validate dependency injection containers on startup.
    ///     <value>The default value is <see langword="true"/>.</value>
    /// </summary>
    public bool ValidateDependencyContainersOnStartup { get; set; } = true;

    /// <summary>
    ///     Gets the configuration options for HTTPS.
    /// </summary>
    public HttpsConfiguration Https { get; } = new();

    /// <summary>
    ///     Gets the configuration options for OpenAPI.
    /// </summary>
    public OpenApiConfiguration OpenApi { get; } = new();

    /// <summary>
    ///     Gets the configuration options for OpenTelemetry.
    /// </summary>
    public OpenTelemetryConfiguration OpenTelemetry { get; } = new();

    /// <summary>
    ///     Gets or sets a delegate to configure <see cref="JsonSerializerOptions"/>.
    ///     <para>
    ///         These options are applied to both minimal APIs and controller-based APIs.
    ///     </para>
    /// </summary>
    public Action<JsonSerializerOptions>? ConfigureJsonOptions { get; set; }

    /// <summary>
    ///     Gets the configuration options for Anti-Forgery.
    /// </summary>
    public AntiForgeryConfiguration AntiForgery { get; } = new();

    /// <summary>
    ///     Gets the configuration options for static assets.
    /// </summary>
    public StaticAssetsConfiguration StaticAssets { get; } = new();

    /// <summary>
    ///     Gets the configuration options for forwarded headers.
    /// </summary>
    public ForwardedHeadersConfiguration ForwardedHeaders { get; } = new();

    /// <summary>
    ///     Gets the configuration options for developer logging.
    /// </summary>
    public DevLogsConfiguration DevLogs { get; } = new();

    /// <summary>
    ///     Configuration options for Anti-Forgery protection.
    /// </summary>
    public sealed class AntiForgeryConfiguration
    {
        /// <summary>
        ///     Gets or sets a value indicating whether Anti-Forgery services and middleware are enabled.
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    ///     Configuration options for the developer logging endpoint.
    /// </summary>
    public sealed class DevLogsConfiguration
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the developer logging endpoint is enabled.
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets the route pattern for the logging endpoint.
        ///     <value>The default value is <c>"/api/dev-logs"</c>.</value>
        /// </summary>
        public string RoutePattern { get; set; } = "/api/dev-logs";

        /// <summary>
        ///     Gets or sets a value indicating whether the logging endpoint is enabled in production environments.
        ///     <value>The default value is <see langword="false"/>.</value>
        /// </summary>
        public bool EnableInProduction { get; set; }
    }

    /// <summary>
    ///     Configuration options for forwarded headers middleware.
    /// </summary>
    public sealed class ForwardedHeadersConfiguration
    {
        /// <summary>
        ///     Gets or sets the forwarded headers to process.
        ///     <value>The default is <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor"/> | <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto"/> | <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost"/>.</value>
        /// </summary>
        public ForwardedHeaders ForwardedHeaders { get; set; } =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost;
    }

    /// <summary>
    ///     Configuration options for HTTPS redirection and HSTS.
    /// </summary>
    public sealed class HttpsConfiguration
    {
        /// <summary>
        ///     Gets or sets a value indicating whether HTTPS redirection is enabled.
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether HTTP Strict Transport Security (HSTS) is enabled.
        ///     <para>
        ///         HSTS is only enabled in non-development environments.
        ///     </para>
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool HstsEnabled { get; set; } = true;
    }

    /// <summary>
    ///     Configuration options for OpenAPI document generation and serving.
    /// </summary>
    public sealed class OpenApiConfiguration
    {
        /// <summary>
        ///     Gets or sets a value indicating whether OpenAPI support is enabled.
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets a delegate to configure <see cref="OpenApiOptions"/>.
        /// </summary>
        public Action<OpenApiOptions>? ConfigureOpenApi { get; set; }

        /// <summary>
        ///     Gets or sets the route pattern for the OpenAPI JSON document.
        ///     <value>The default value is <c>"/openapi/{documentName}.json"</c>.</value>
        /// </summary>
        [StringSyntax("Route")]
        public string RoutePattern { get; set; } = "/openapi/{documentName}.json";
    }

    /// <summary>
    ///     Configuration options for OpenTelemetry instrumentation.
    /// </summary>
    public sealed class OpenTelemetryConfiguration
    {
        /// <summary>
        ///     Gets or sets a delegate to configure OpenTelemetry logging.
        /// </summary>
        public Action<OpenTelemetryLoggerOptions>? ConfigureLogging { get; set; }

        /// <summary>
        ///     Gets or sets a delegate to configure OpenTelemetry metrics.
        /// </summary>
        public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

        /// <summary>
        ///     Gets or sets a delegate to configure OpenTelemetry tracing.
        /// </summary>
        public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }
    }

    /// <summary>
    ///     Configuration options for static asset serving.
    /// </summary>
    public sealed class StaticAssetsConfiguration
    {
        /// <summary>
        ///     Gets or sets a value indicating whether static assets should be served.
        ///     <value>The default value is <see langword="true"/>.</value>
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
