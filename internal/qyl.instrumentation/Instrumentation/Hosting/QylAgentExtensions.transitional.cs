// Copyright (c) 2025-2026 ancplua
//
// TRANSITIONAL — delete when MAF.Advanced.Patterns 1.4.x publishes the
// canonical Qyl.Hosting.QylAgentExtensions to nuget.org and qyl flips CPM
// from ANcpLua.Agents → MAF.Advanced.Patterns.{Governance,Testing,Testing.Workflows}.
//
// This shim mirrors the upstream signature exactly so the migration step is
// a clean swap: add PackageReference + delete this file, in the same commit.
// Factory bodies in QylLoomAgentsBuilder / QylLoomPatternsAgentsBuilder /
// QylMcpAgentsBuilder do NOT change at flip time — they consume Qyl.Hosting
// regardless of provider.
//
// Source of truth at the time of authoring:
//   MAF.Advanced.Patterns/src/MAF.Advanced.Patterns/QylAgentExtensions.cs

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Qyl.Hosting;

/// <summary>
///     Mega-facade over the four-call agent-construction shape (options +
///     <c>AsAIAgent</c> + <c>AsBuilder</c> + <c>Build</c>). Bundles
///     <see cref="ChatClientAgentOptions" /> construction with an optional
///     <see cref="AIAgentBuilder" /> middleware callback so consumers can attach
///     <c>UseQylAgentTelemetry()</c> in one fluent line.
/// </summary>
public static class QylAgentExtensions
{
    public static AIAgent AsQylAgent(
        this IChatClient client,
        string name,
        string description,
        string instructions,
        Action<AIAgentBuilder>? telemetry = null,
        IServiceProvider? services = null)
    {
        Guard.NotNull(client);
        Guard.NotNullOrEmpty(name);
        Guard.NotNull(description);
        Guard.NotNull(instructions);

        ChatClientAgentOptions options = new()
        {
            Name = name,
            Description = description,
            ChatOptions = new ChatOptions { Instructions = instructions }
        };

        var builder = client.AsAIAgent(options).AsBuilder();
        telemetry?.Invoke(builder);
        return services is null ? builder.Build() : builder.Build(services);
    }
}
