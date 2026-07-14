using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Qyl.Host;

namespace Qyl.Host.Mcp;

/// <summary>
/// MCP resource kinds for the qyl runner — the C# port of qyl.mcp's TS app-builder surface.
/// All three are connection-only resources: readiness is <see cref="McpHandshakeProbe"/>
/// (initialize + tools/list), the live client lands in <see cref="McpClientRegistry"/>, and
/// the <c>/runner/mcp</c> passthrough (plus export of the official MCP SDK ActivitySource) is wired on first use.
/// </summary>
public static class QylMcpBuilderExtensions
{
    // ModelContextProtocol.Core 1.4.1 owns its ActivitySource under this name. The SDK keeps the
    // constant internal, so this value is pinned by the exact package version. Its Meter uses the
    // same name, but qyl exposes no metrics receiver and therefore does not export that signal.
    internal const string OfficialDiagnosticsName = "Experimental.ModelContextProtocol";

    /// <summary>
    /// MCP server as an SDK-spawned stdio child (the SDK transport owns the process). Mirrors
    /// qyl.mcp's <c>addServer(kind: "stdio")</c>.
    /// </summary>
    public static IQylResourceBuilder AddMcpStdio(this QylAppBuilder app, string name, string command,
        IEnumerable<string>? arguments = null, string? workingDirectory = null)
    {
        QylGuard.NotNull(app);
        QylGuard.NotNullOrWhiteSpace(name);
        QylGuard.NotNullOrWhiteSpace(command);

        var options = new StdioClientTransportOptions
        {
            Command = command,
            Arguments = arguments?.ToArray() ?? [],
            WorkingDirectory = workingDirectory,
            Name = name
        };

        return AddConnectionOnly(app, name, QylResourceKind.McpStdio, async (_, ct) =>
            new McpConnection(await McpClient.CreateAsync(
                    new StdioClientTransport(options), cancellationToken: ct)
                .ConfigureAwait(false)));
    }

    /// <summary>
    /// Already-running MCP server reached over HTTP (streamable HTTP / SSE). Mirrors qyl.mcp's
    /// <c>addServer(kind: "http")</c>.
    /// </summary>
    public static IQylResourceBuilder AddMcpHttp(this QylAppBuilder app, string name, Uri endpoint)
    {
        QylGuard.NotNull(app);
        QylGuard.NotNullOrWhiteSpace(name);
        QylGuard.NotNull(endpoint);

        return AddConnectionOnly(app, name, QylResourceKind.McpHttp, async (_, ct) =>
            new McpConnection(await McpClient.CreateAsync(
                    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint, Name = name }),
                    cancellationToken: ct)
                .ConfigureAwait(false)));
    }

    /// <summary>
    /// MCP server hosted inside the runner process over an in-memory stream pair — qyl.mcp's
    /// <c>addInProcessServer(name, serverFactory)</c> (its <c>InMemoryTransport.createLinkedPair()</c>).
    /// The factory receives the server-side transport because the C# SDK couples server and
    /// transport at <see cref="McpServer.Create"/> (and its Core package exposes the abstract
    /// <see cref="McpServer"/>, not an interface); return the created server and the runner
    /// runs it for the composition's lifetime.
    /// </summary>
    public static IQylResourceBuilder AddMcpInProcess(this QylAppBuilder app, string name,
        Func<ITransport, McpServer> serverFactory)
    {
        QylGuard.NotNull(app);
        QylGuard.NotNullOrWhiteSpace(name);
        QylGuard.NotNull(serverFactory);

        return AddConnectionOnly(app, name, QylResourceKind.McpInProcess,
            (_, ct) => CreateInProcessConnectionAsync(name, serverFactory, ct));
    }

    private static IQylResourceBuilder AddConnectionOnly(QylAppBuilder app, string name, QylResourceKind kind,
        Func<QylResourceState, CancellationToken, Task<McpConnection>> connect)
    {
        var registry = EnsureMcpServices(app);

        return app.AddResource(new QylResource
        {
            Name = name,
            Kind = kind,
            Port = 0,
            Launch = null,
            ReadinessProbe = new McpHandshakeProbe(connect, registry,
                TimeSpan.FromSeconds(QylConstants.Orchestrator.StartupTimeoutSeconds), TimeProvider.System)
        });
    }

    // One registry / official SDK tracing / passthrough wiring per composition, created on the first AddMcp*
    // call. The registry instance must exist at composition time (the probes capture it), so it is
    // registered as an instance and rediscovered through the service collection on later calls.
    private static McpClientRegistry EnsureMcpServices(QylAppBuilder app)
    {
        var existing = app.Host.Services.FirstOrDefault(static d =>
            d.ServiceType == typeof(McpClientRegistry) && d.ImplementationInstance is not null);
        if (existing?.ImplementationInstance is McpClientRegistry registered) return registered;

        var registry = new McpClientRegistry();
        app.Host.Services.AddSingleton(registry);
        app.Host.Services.AddHostedService(static sp => sp.GetRequiredService<McpClientRegistry>());
        ConfigureOfficialMcpTelemetry(app.Host);
        app.Host.Services.AddSingleton<IQylRunnerRequestHandler>(static sp => new McpPassthroughHandler(
            sp.GetRequiredService<McpClientRegistry>(),
            sp.GetRequiredService<IReadOnlyList<QylResource>>()));
        return registry;
    }

    private static void ConfigureOfficialMcpTelemetry(HostApplicationBuilder host)
    {
        if (Environment.GetEnvironmentVariable("QYL_MCP_TELEMETRY") == "0") return;

        var traceEndpoint = ResolveTraceEndpoint();
        host.Services.AddOpenTelemetry()
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
        var exact = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(exact))
            return RequireHttpEndpoint(exact);

        var configured = Environment.GetEnvironmentVariable("QYL_OTLP_ENDPOINT")
                         ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                         ?? "http://127.0.0.1:4318";
        var baseUri = RequireHttpEndpoint(configured);
        if (baseUri.AbsolutePath.TrimEnd('/').EndsWith("/v1/traces", StringComparison.OrdinalIgnoreCase))
            return baseUri;

        var builder = new UriBuilder(baseUri);
        var basePath = builder.Path.TrimEnd('/');
        builder.Path = basePath + "/v1/traces";
        return builder.Uri;
    }

    private static Uri RequireHttpEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"The MCP OTLP endpoint must be an absolute HTTP(S) URI, but was '{value}'.");
        }

        return endpoint;
    }

    private static async Task<McpConnection> CreateInProcessConnectionAsync(
        string name,
        Func<ITransport, McpServer> serverFactory,
        CancellationToken cancellationToken)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var serverLifetime = new CancellationTokenSource();
        McpServer? server = null;
        Task? serverLoop = null;
        McpClient? client = null;
        ITransport? serverTransport = null;

        try
        {
#pragma warning disable CA2000 // ownership transfers to McpConnection (and the SDK server session)
            serverTransport = new StreamServerTransport(
                clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream(), name);
#pragma warning restore CA2000
            server = serverFactory(serverTransport);
            serverLoop = Task.Run(() => server.RunAsync(serverLifetime.Token), serverLifetime.Token);
            client = await McpClient.CreateAsync(
                    new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new McpConnection(client, server, serverLoop, serverLifetime, serverTransport);
        }
        catch
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            serverLifetime.Cancel();

            if (serverLoop is not null)
            {
                try
                {
                    await serverLoop.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException)
                {
                    // Expected cleanup for a failed handshake.
                }
            }

            if (server is not null) await server.DisposeAsync().ConfigureAwait(false);
            if (serverTransport is not null) await serverTransport.DisposeAsync().ConfigureAwait(false);
            serverLifetime.Dispose();
            throw;
        }
    }
}
