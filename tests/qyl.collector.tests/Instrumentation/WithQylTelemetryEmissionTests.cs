// Copyright (c) 2025-2026 ancplua

using System.Diagnostics;
using ANcpLua.Agents.Testing.ChatClients;
using ANcpLua.Agents.Testing.Diagnostics;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Xunit;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     End-to-end span-emission smoke test for
///     <see cref="GenAiInstrumentation.WithQylTelemetry(IChatClient, string?, bool?)" />.
///     The other tests in this folder assert pipeline shape (what wraps what); this file
///     asserts that a real <see cref="IChatClient.GetResponseAsync" /> invocation through
///     the wrapped pipeline actually emits an <see cref="Activity" /> on <c>qyl.genai</c>
///     with GenAI semconv 1.40 attributes populated — the missing smoke-test leg of
///     PR #138's collapse.
/// </summary>
public sealed class WithQylTelemetryEmissionTests
{
    [Fact]
    public async Task WithQylTelemetry_emits_qyl_genai_activity_on_GetResponseAsync()
    {
        using var collector = new ActivityCollector("qyl.genai");

        var inner = new FakeChatClient
            {
                Metadata = new ChatClientMetadata(
                    providerName: "openai",
                    providerUri: null,
                    defaultModelId: "gpt-4o-mini")
            }
            .WithResponse("Hello from fake.");

        var instrumented = inner.WithQylTelemetry(sourceName: "qyl.genai");

        var response = await instrumented.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            new ChatOptions { ModelId = "gpt-4o-mini" },
            CancellationToken.None);

        // response plumbed through the pipeline
        response.Text.Should().Contain("Hello from fake.");

        // at least one qyl.genai activity emitted
        collector.Activities.Should().NotBeEmpty(
            "WithQylTelemetry must emit at least one Activity on 'qyl.genai' per invocation");

        var chatActivity = collector.Activities
            .First(static a => a.OperationName.ContainsIgnoreCase("chat")
                            || a.Tags.Any(static t => t.Key == "gen_ai.operation.name"));

        // GenAI semconv 1.40 core attributes
        chatActivity.AssertHasTag("gen_ai.operation.name");
        chatActivity.AssertHasTag("gen_ai.request.model");

        // Provider identification — OTel GenAI semconv 1.40.
        chatActivity.Tags.Should().Contain(
            static t => t.Key == "gen_ai.provider.name",
            "GenAI spans must identify the provider via the 1.40 attribute");
    }

    [Fact]
    public async Task WithQylTelemetry_records_call_through_inner_client()
    {
        var inner = new FakeChatClient
            {
                Metadata = new ChatClientMetadata("openai", null, "gpt-4o-mini")
            }
            .WithResponse("ok");

        var instrumented = inner.WithQylTelemetry(sourceName: "qyl.genai");

        await instrumented.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: CancellationToken.None);

        // Middleware must forward to the inner client — regression guard against a
        // WithQylTelemetry refactor that accidentally short-circuits the pipeline.
        inner.CallCount.Should().Be(1);
        inner.LastOptions.Should().BeNull();
    }
}
