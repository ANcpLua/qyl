
using ANcpLua.Agents.Testing.ChatClients;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Patterns.Clients;

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
        _fakes[stage] = fake;
        fake.WithResponse(canned);

        var instrumented = new ChatClientBuilder(fake)
            .UseQylTelemetry("qyl.genai")
            .Build(services);

        _cache[stage] = instrumented;
        return instrumented;
    }

    public void Dispose()
    {
        foreach (var client in _cache.Values) client.Dispose();
        foreach (var fake in _fakes.Values) fake.Dispose();
        _cache.Clear();
        _fakes.Clear();
    }
}
