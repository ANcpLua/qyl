// Copyright (c) 2025-2026 ancplua

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

/// <summary>
///     Fixture binding qyl's telemetry pipeline (<c>WithQylTelemetry</c> +
///     <c>UseQylAgentTelemetry</c>) to the provider-agnostic
///     <see cref="TelemetryConformanceTests{TFixture}" /> base. A
///     <see cref="FakeChatClient" /> at the bottom of the decorator chain emits a
///     scripted response, the chat-client wrap emits <c>qyl.genai</c> spans, and the
///     agent wrap emits <c>qyl.agent</c> spans — exactly the pair every qyl
///     composition root produces in production.
/// </summary>
public sealed class QylInstrumentationTelemetryFixture : IAgentFixture, ITelemetryAssertingFixture
{
    private FakeChatClient? _fake;
    private IChatClient? _instrumented;
    private AIAgent? _agent;
    private ServiceProvider? _services;

    /// <inheritdoc />
    public AIAgent Agent => _agent ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <inheritdoc />
    public IReadOnlyCollection<string> ExpectedActivitySources => ["qyl.genai", "qyl.agent"];

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _instrumented?.Dispose();
        _fake?.Dispose();
        if (_services is not null)
            await _services.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChatMessage>> GetChatHistoryAsync(AIAgent agent, AgentSession session) =>
        Task.FromResult<IReadOnlyList<ChatMessage>>([]);

    /// <inheritdoc />
    public Task DeleteSessionAsync(AgentSession session) => Task.CompletedTask;
}
