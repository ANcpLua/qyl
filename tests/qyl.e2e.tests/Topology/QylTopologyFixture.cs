using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Qyl.E2E.Tests.Topology;

public sealed class QylTopologyFixture : IAsyncLifetime
{
    private const int CollectorInternalPort = 5100;
    private const int McpInternalPort = 5200;

    private readonly QylTopologyOptions _options = QylTopologyOptions.Default;

    private INetwork? _network;
    private IContainer? _collector;
    private IContainer? _mcp;
    private WireMockServer? _llm;

    public WireMockServer Llm =>
        _llm ?? throw new InvalidOperationException(
            "Topology fixture not initialized — InitializeAsync has not completed.");

    public Uri CollectorBaseUrl =>
        _collector is null
            ? throw new InvalidOperationException("Collector container not started.")
            : new Uri($"http://{_collector.Hostname}:{_collector.GetMappedPublicPort(CollectorInternalPort)}/");

    public Uri McpBaseUrl =>
        _mcp is null
            ? throw new InvalidOperationException("MCP container not started.")
            : new Uri($"http://{_mcp.Hostname}:{_mcp.GetMappedPublicPort(McpInternalPort)}/");

    public async ValueTask InitializeAsync()
    {
        using var bootstrapCts = new CancellationTokenSource(_options.StartupTimeout);
        var ct = bootstrapCts.Token;

        try
        {
            _llm = WireMockServer.Start();
            ConfigureDefaultLlmResponse(_llm);

            _network = new NetworkBuilder()
                .WithName($"qyl-e2e-{Guid.NewGuid():N}")
                .Build();
            await _network.CreateAsync(ct).ConfigureAwait(false);

            _collector = new ContainerBuilder()
                .WithImage(_options.CollectorImage)
                .WithImagePullPolicy(static _ => false)
                .WithNetwork(_network)
                .WithNetworkAliases("qyl-collector")
                .WithPortBinding(CollectorInternalPort, true)
                .WithEnvironment("QYL_PORT", CollectorInternalPort.ToString(CultureInfo.InvariantCulture))
                .WithEnvironment("ASPNETCORE_URLS", $"http://+:{CollectorInternalPort}")
                .WithEnvironment("QYL_OTLP_AUTH_MODE", "Unsecured")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(static r => r.ForPath("/health").ForPort(CollectorInternalPort)))
                .Build();
            await _collector.StartAsync(ct).ConfigureAwait(false);

            _mcp = new ContainerBuilder()
                .WithImage(_options.McpImage)
                .WithImagePullPolicy(static _ => false)
                .WithNetwork(_network)
                .WithNetworkAliases("qyl-mcp")
                .WithPortBinding(McpInternalPort, true)
                .WithEnvironment("ASPNETCORE_URLS", $"http://+:{McpInternalPort}")
                .WithEnvironment("QYL_COLLECTOR_URL", $"http://qyl-collector:{CollectorInternalPort}")
                .WithExtraHost("host.docker.internal", "host-gateway")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(static r => r.ForPath("/alive").ForPort(McpInternalPort)))
                .Build();
            await _mcp.StartAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_mcp is not null)
        {
            await _mcp.DisposeAsync().ConfigureAwait(false);
            _mcp = null;
        }

        if (_collector is not null)
        {
            await _collector.DisposeAsync().ConfigureAwait(false);
            _collector = null;
        }

        if (_network is not null)
        {
            await _network.DeleteAsync().ConfigureAwait(false);
            _network = null;
        }

        if (_llm is not null)
        {
            _llm.Stop();
            _llm.Dispose();
            _llm = null;
        }
    }

    public void ResetLlmStub()
    {
        if (_llm is null) return;
        _llm.Reset();
        ConfigureDefaultLlmResponse(_llm);
    }

    private static void ConfigureDefaultLlmResponse(WireMockServer llm) =>
        llm.Given(Request.Create().WithPath("/v1/chat/completions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "chatcmpl-e2e-default",
                    @object = "chat.completion",
                    created = 0,
                    model = "qyl-e2e-stub",
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new { role = "assistant", content = "qyl e2e default response" },
                            finish_reason = "stop",
                        },
                    },
                    usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
                }));
}
