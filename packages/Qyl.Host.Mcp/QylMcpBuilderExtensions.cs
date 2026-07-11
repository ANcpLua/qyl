using System.IO.Pipelines;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Run;

namespace Qyl.Host.Mcp;

/// <summary>
/// MCP resource kinds for the qyl runner — the C# port of qyl.mcp's TS app-builder surface.
/// All three are connection-only resources: readiness is <see cref="McpHandshakeProbe"/>
/// (initialize + tools/list), the live client lands in <see cref="McpClientRegistry"/>, and
/// the <c>/runner/mcp</c> passthrough (plus its OTLP self-monitoring) is wired on first use.
/// </summary>
public static class QylMcpBuilderExtensions
{
    /// <summary>
    /// MCP server as an SDK-spawned stdio child (the SDK transport owns the process). Mirrors
    /// qyl.mcp's <c>addServer(kind: "stdio")</c>.
    /// </summary>
    public static IQylResourceBuilder AddMcpStdio(this QylAppBuilder app, string name, string command,
        IEnumerable<string>? arguments = null, string? workingDirectory = null)
    {
        Guard.NotNull(app);
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNullOrWhiteSpace(command);

        var options = new StdioClientTransportOptions
        {
            Command = command,
            Arguments = arguments?.ToArray() ?? [],
            WorkingDirectory = workingDirectory,
            Name = name
        };

        return AddConnectionOnly(app, name, McpResourceKinds.Stdio,
            (_, ct) => McpClient.CreateAsync(new StdioClientTransport(options), cancellationToken: ct));
    }

    /// <summary>
    /// Already-running MCP server reached over HTTP (streamable HTTP / SSE). Mirrors qyl.mcp's
    /// <c>addServer(kind: "http")</c>.
    /// </summary>
    public static IQylResourceBuilder AddMcpHttp(this QylAppBuilder app, string name, Uri endpoint)
    {
        Guard.NotNull(app);
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(endpoint);

        return AddConnectionOnly(app, name, McpResourceKinds.Http,
            (_, ct) => McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint, Name = name }),
                cancellationToken: ct));
    }

    /// <summary>
    /// MCP server hosted inside the runner process over an in-memory stream pair — qyl.mcp's
    /// <c>addInProcessServer(name, serverFactory)</c> (its <c>InMemoryTransport.createLinkedPair()</c>).
    /// The factory receives the server-side transport because the C# SDK couples server and
    /// transport at <see cref="McpServer.Create"/> (and its Core package exposes the abstract
    /// <see cref="McpServer"/>, not an interface); return the created server and the runner
    /// runs it for the composition's lifetime.
    /// </summary>
    public static IQylResourceBuilder AddMcpInProc(this QylAppBuilder app, string name,
        Func<ITransport, McpServer> serverFactory)
    {
        Guard.NotNull(app);
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(serverFactory);

        return AddConnectionOnly(app, name, McpResourceKinds.InProc, async (state, ct) =>
        {
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();

            var server = serverFactory(new StreamServerTransport(
                clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream(), name));

            // Runs until the composition shuts down and the pipes tear the transport down with it.
            var serverLifetime = server.RunAsync(CancellationToken.None);

            return await McpClient.CreateAsync(
                    new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
                    cancellationToken: ct)
                .ConfigureAwait(false);
        });
    }

    private static IQylResourceBuilder AddConnectionOnly(QylAppBuilder app, string name, string kind,
        Func<QylResourceState, CancellationToken, Task<McpClient>> connect)
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

    // One registry / telemetry / passthrough wiring per composition, created on the first AddMcp*
    // call. The registry instance must exist at composition time (the probes capture it), so it is
    // registered as an instance and rediscovered through the service collection on later calls.
    private static McpClientRegistry EnsureMcpServices(QylAppBuilder app)
    {
        var existing = app.Host.Services.FirstOrDefault(static d =>
            d.ServiceType == typeof(McpClientRegistry) && d.ImplementationInstance is not null);
        if (existing?.ImplementationInstance is McpClientRegistry registered) return registered;

        var registry = new McpClientRegistry();
        app.Host.Services.AddSingleton(registry);
        app.Host.Services.AddSingleton<McpTelemetry>();
        app.Host.Services.AddHostedService(static sp => sp.GetRequiredService<McpTelemetry>());
        app.Host.Services.AddSingleton<IQylRunnerRequestHandler>(static sp => new McpPassthroughHandler(
            sp.GetRequiredService<McpClientRegistry>(),
            sp.GetRequiredService<IReadOnlyList<QylResource>>(),
            sp.GetRequiredService<McpTelemetry>(),
            sp.GetRequiredService<TimeProvider>()));
        return registry;
    }
}
