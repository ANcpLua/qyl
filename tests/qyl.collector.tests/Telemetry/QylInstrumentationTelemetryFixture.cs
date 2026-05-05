
using ANcpLua.Agents.Testing.ChatClients;
using ANcpLua.Agents.Testing.Conformance;
using ANcpLua.Agents.Testing.Conformance.Telemetry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Telemetry;

public sealed class QylInstrumentationTelemetryFixture : IAgentFixture, ITelemetryAssertingFixture
{
    private FakeChatClient? _fake;
    private IChatClient? _instrumented;
    private AIAgent? _agent;
    private ServiceProvider? _services;

    public AIAgent Agent => _agent ?? throw new InvalidOperationException("Fixture not initialized.");

    public IReadOnlyCollection<string> ExpectedActivitySources => ["qyl.genai", "qyl.agent"];

    public ValueTask InitializeAsync()
    {
        _services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

        _fake = new FakeChatClient
        {
            Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini")
        };
        _fake.WithResponse("Telemetry conformance fake response.");

        _instrumented = _fake.WithQylTelemetry(GenAiConstants.SourceName);

        _agent = _instrumented
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "QylInstrumentationTelemetryFixtureAgent",
                Description = "Drives qyl.genai + qyl.agent activity emission for conformance.",
                ChatOptions = new ChatOptions
                {
                    Instructions = "You are the qyl telemetry conformance fake agent.",
                    ModelId = "gpt-4o-mini"
                }
            })
            .AsBuilder()
            .UseQylAgentTelemetry()
            .Build(_services);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _instrumented?.Dispose();
        _fake?.Dispose();
        if (_services is not null)
            await _services.DisposeAsync().ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ChatMessage>> GetChatHistoryAsync(AIAgent agent, AgentSession session) =>
        Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    public Task DeleteSessionAsync(AgentSession session) => Task.CompletedTask;
}
