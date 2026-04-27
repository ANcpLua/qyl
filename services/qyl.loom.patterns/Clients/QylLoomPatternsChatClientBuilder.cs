// Copyright (c) 2025-2026 ancplua

using ANcpLua.Agents.Testing.ChatClients;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Patterns.Clients;

/// <summary>
///     Default <see cref="IQylLoomPatternsChatClientBuilder" /> backed by
///     <see cref="FakeChatClient" />. Each stage gets a canned narrative response
///     wrapped with <c>UseQylTelemetry</c> — the chat-client-layer telemetry
///     pipeline runs exactly like production, but no API key is required.
/// </summary>
/// <remarks>
///     <para>
///         The underlying <see cref="FakeChatClient" /> instances are cached per stage
///         and disposed with the builder. Callers treat the returned
///         <see cref="IChatClient" /> as borrowed — do not dispose it directly.
///     </para>
///     <para>
///         <c>UseQylTelemetry</c> bundles <c>UseLogging()</c>, which resolves
///         <c>ILoggerFactory</c> from the <see cref="IServiceProvider" /> passed to
///         <c>Build(...)</c>. The composition root owns that provider and hands it in.
///     </para>
/// </remarks>
public sealed class QylLoomPatternsChatClientBuilder(IServiceProvider services)
    : IQylLoomPatternsChatClientBuilder
{
    private static readonly Dictionary<string, string> s_cannedResponses = new(StringComparer.Ordinal)
    {
        ["rca"] =
            "Connection pool exhausted after deploy at 14:02 — stripe-webhook handler leaks SqlConnection on timeout path.",
        ["solution"] =
            "Wrap the timeout branch in a `using` block; add a connection-pool-exhaustion Polly retry for 503s.",
        ["verdict"] = "approved"
    };

    private readonly Dictionary<string, IChatClient> _cache = new(StringComparer.Ordinal);

    private readonly Dictionary<string, FakeChatClient> _fakes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IChatClient BuildChatClient(string stage)
    {
        if (_cache.TryGetValue(stage, out var cached))
        {
            return cached;
        }

        var canned = s_cannedResponses.TryGetValue(stage, out var text)
            ? text
            : $"(no canned response registered for stage '{stage}')";

        var fake = new FakeChatClient
        {
            Metadata = new ChatClientMetadata(
                "fake",
                null,
                $"loom-patterns-{stage}")
        };
        _fakes[stage] = fake; // transfer ownership before the fluent chain runs
        fake.WithResponse(canned);

        var instrumented = new ChatClientBuilder(fake)
            .UseQylTelemetry("qyl.genai")
            .Build(services);

        _cache[stage] = instrumented;
        return instrumented;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var client in _cache.Values) client.Dispose();
        foreach (var fake in _fakes.Values) fake.Dispose();
        _cache.Clear();
        _fakes.Clear();
    }
}
