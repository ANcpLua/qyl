using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using ModelContextProtocol.Client;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Qyl.Host;

namespace Qyl.Host.Mcp;

/// <summary>
/// Adds connection-only MCP resources. Readiness requires initialize plus tools/list;
/// connected clients back <c>/runner/mcp</c> passthrough, and official SDK tracing is
/// configured once.
/// </summary>
public static class QylMcpBuilderExtensions
{
    // ModelContextProtocol.Core publishes ActivitySource events under this name.
    // The SDK keeps the value internal, so this constant must track the package.
    internal const string OfficialDiagnosticsName = "Experimental.ModelContextProtocol";

    private const string McpTelemetryEnvironmentVariable = "QYL_MCP_TELEMETRY";
    private const string QylOtlpEndpointEnvironmentVariable = "QYL_OTLP_ENDPOINT";
    private const string OtlpEndpointEnvironmentVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string OtlpTracesEndpointEnvironmentVariable =
        "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";

    private const string TelemetryDisabledValue = "0";
    private const string DefaultOtlpEndpoint = "http://127.0.0.1:4318";
    private const string OtlpTracesPath = "/v1/traces";

    /// <summary>
    /// Adds an MCP client that communicates with a child process over standard input
    /// and standard output.
    /// </summary>
    /// <remarks>
    /// The MCP SDK transport owns the child process and manages its lifetime. The child
    /// inherits this process's environment; <paramref name="environment"/> entries are
    /// applied on top, and a <see langword="null"/> value removes the inherited variable.
    /// </remarks>
    public static IQylResourceBuilder AddMcpStdio(
        this QylAppBuilder app,
        string name,
        string command,
        IEnumerable<string>? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        QylGuard.NotNull(app);
        QylGuard.NotNullOrWhiteSpace(name);
        QylGuard.NotNullOrWhiteSpace(command);

        var transportOptions = new StdioClientTransportOptions
        {
            Name = name,
            Command = command,
            Arguments = arguments?.ToArray() ?? [],
            WorkingDirectory = workingDirectory,
            EnvironmentVariables = environment is null
                ? null
                : new Dictionary<string, string?>(environment, StringComparer.Ordinal)
        };

        return AddMcpResource(
            app,
            name,
            QylResourceKind.McpStdio,
            (_, cancellationToken) => McpClient.CreateAsync(
                new StdioClientTransport(transportOptions),
                cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Adds an MCP client connected to an existing server over HTTP.
    /// </summary>
    public static IQylResourceBuilder AddMcpHttp(
        this QylAppBuilder app,
        string name,
        Uri endpoint)
    {
        QylGuard.NotNull(app);
        QylGuard.NotNullOrWhiteSpace(name);
        QylGuard.NotNull(endpoint);

        var transportOptions = new HttpClientTransportOptions
        {
            Name = name,
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        };

        return AddMcpResource(
            app,
            name,
            QylResourceKind.McpHttp,
            (_, cancellationToken) => McpClient.CreateAsync(
                new HttpClientTransport(transportOptions),
                cancellationToken: cancellationToken));
    }

    private static IQylResourceBuilder AddMcpResource(
        QylAppBuilder app,
        string name,
        QylResourceKind kind,
        Func<QylResourceState, CancellationToken, Task<McpClient>> connectionFactory)
    {
        var registry = EnsureMcpServices(app);
        var startupTimeout = TimeSpan.FromSeconds(
            QylConstants.Orchestrator.StartupTimeoutSeconds);

        var resource = new QylResource
        {
            Name = name,
            Kind = kind,
            Port = 0,
            Launch = null,
            ReadinessProbe = new McpHandshakeProbe(
                connectionFactory,
                registry,
                startupTimeout,
                TimeProvider.System)
        };

        return app.AddResource(resource);
    }

    /// <summary>
    /// Ensures that composition-wide MCP services are registered exactly once.
    /// </summary>
    /// <remarks>
    /// The registry must be created during composition because resource probes capture
    /// it before the host service provider is built. It is therefore registered as an
    /// implementation instance and discovered through the service collection on later
    /// <c>AddMcp*</c> calls.
    /// </remarks>
    private static McpClientRegistry EnsureMcpServices(QylAppBuilder app)
    {
        var services = app.Host.Services;

        var existingRegistration = services.FirstOrDefault(static descriptor =>
            descriptor.ServiceType == typeof(McpClientRegistry) &&
            descriptor.ImplementationInstance is McpClientRegistry);

        if (existingRegistration?.ImplementationInstance is McpClientRegistry existingRegistry)
        {
            return existingRegistry;
        }

        var registry = new McpClientRegistry();

        services.AddSingleton(registry);

        services.AddHostedService(static serviceProvider =>
            serviceProvider.GetRequiredService<McpClientRegistry>());

        ConfigureOfficialMcpTelemetry(app.Host);

        services.AddSingleton<IQylRunnerRequestHandler>(static serviceProvider =>
            new McpPassthroughHandler(
                serviceProvider.GetRequiredService<McpClientRegistry>(),
                serviceProvider.GetRequiredService<IReadOnlyList<QylResource>>()));

        return registry;
    }

    private static void ConfigureOfficialMcpTelemetry(HostApplicationBuilder host)
    {
        var telemetrySetting =
            Environment.GetEnvironmentVariable(McpTelemetryEnvironmentVariable);

        if (string.Equals(
                telemetrySetting,
                TelemetryDisabledValue,
                StringComparison.Ordinal))
        {
            return;
        }

        var traceEndpoint = ResolveTraceEndpoint();

        host.Services
            .AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(OfficialDiagnosticsName)
                .AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Endpoint = traceEndpoint;
                    options.ExportProcessorType = ExportProcessorType.Batch;
                }));
    }

    internal static Uri ResolveTraceEndpoint()
    {
        var explicitTraceEndpoint =
            Environment.GetEnvironmentVariable(OtlpTracesEndpointEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(explicitTraceEndpoint))
        {
            return RequireHttpEndpoint(explicitTraceEndpoint);
        }

        var configuredEndpoint =
            Environment.GetEnvironmentVariable(QylOtlpEndpointEnvironmentVariable) ??
            Environment.GetEnvironmentVariable(OtlpEndpointEnvironmentVariable) ??
            DefaultOtlpEndpoint;

        var baseEndpoint = RequireHttpEndpoint(configuredEndpoint);

        if (HasOtlpTracesPath(baseEndpoint))
        {
            return baseEndpoint;
        }

        return AppendOtlpTracesPath(baseEndpoint);
    }

    private static bool HasOtlpTracesPath(Uri endpoint)
    {
        var normalizedPath = endpoint.AbsolutePath.TrimEnd('/');

        return normalizedPath.EndsWith(
            OtlpTracesPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Uri AppendOtlpTracesPath(Uri endpoint)
    {
        var builder = new UriBuilder(endpoint);
        var basePath = builder.Path.TrimEnd('/');

        builder.Path = basePath + OtlpTracesPath;

        return builder.Uri;
    }

    private static Uri RequireHttpEndpoint(string value)
    {
        var isValidEndpoint =
            Uri.TryCreate(value, UriKind.Absolute, out var endpoint) &&
            (string.Equals(
                 endpoint.Scheme,
                 Uri.UriSchemeHttp,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 endpoint.Scheme,
                 Uri.UriSchemeHttps,
                 StringComparison.OrdinalIgnoreCase));

        if (!isValidEndpoint)
        {
            throw new InvalidOperationException(
                $"The MCP OTLP endpoint must be an absolute HTTP(S) URI, but was '{value}'.");
        }

        return endpoint!;
    }
}
