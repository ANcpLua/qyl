// Copyright (c) 2025-2026 ancplua

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// <summary>
///     Step 5: scores the proposed fix. Deserialization failures fall back to a conservative
///     <c>review</c> recommendation at confidence 0.5 to match prior behavior.
/// </summary>
internal sealed class ConfidenceExecutor(CollectorClient collector, IChatClient llm)
    : AutofixPipelineExecutor("autofix.confidence", stepNumber: 5, stepName: "confidence_scoring", collector)
{
    private static readonly ConfidenceResult ParseFailedFallback = new()
    {
        Confidence = 0.5,
        Reasoning = "Parse failed",
        Recommendation = "review",
    };

    protected override async ValueTask<(AutofixRunState State, string OutputJson)> DoWorkAsync(
        AutofixRunState state, CancellationToken cancellationToken)
    {
        var userMessage = $"""
                           Root Cause Analysis:
                           {state.RcaReport}

                           Proposed Fix:
                           {state.ChangesJson}
                           """;

        var agent = llm.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AutofixConfidenceAgent",
            Description = "Scores autofix confidence and returns a structured recommendation.",
            ChatOptions = new ChatOptions { Instructions = AutofixPrompts.ConfidenceScoring },
        }).AsBuilder().UseQylAgentTelemetry().Build();

        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = AutofixJson.ExtractObject(response.Text ?? string.Empty);

        var result = TryDeserialize(json) ?? ParseFailedFallback;
        var serialized = JsonSerializer.Serialize(result, AutofixJsonContext.Default.ConfidenceResult);
        return (state with { Confidence = result }, serialized);
    }

    private static ConfidenceResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AutofixJsonContext.Default.ConfidenceResult);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
