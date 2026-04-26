using Microsoft.Extensions.AI;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration;

/// <summary>
///     Bounded sub-agent responsible only for root-cause investigation.
///     Streams LLM reasoning chunks to an optional per-chunk callback as they arrive, then returns the
///     accumulated monologue plus parsed root cause. The callback exists so the workflow diagnose executor
///     can republish chunks as <c>ExplorationStreamEvent</c>s in real time rather than after the LLM has
///     finished producing tokens.
/// </summary>
public sealed class ExplorationDiagnostician(IChatClient? llm = null)
{
    public bool IsConfigured => llm is not null;

    public async Task<ExplorationDiagnosisResult> DiagnoseAsync(
        ExplorationContext context,
        Func<StreamUpdate, ValueTask>? onChunk = null,
        CancellationToken ct = default)
    {
        if (llm is null)
            return ExplorationDiagnosisResult.Unconfigured;

        var prompt = $"""
                      {ExplorationPrompts.ExplorerMonologue}

                      Error context:
                      {context.FormattedBlock}
                      """;

        List<StreamUpdate> updates = [];
        StringBuilder fullText = new();

        try
        {
            await foreach (var chunk in llm.GetStreamingResponseAsync(prompt, cancellationToken: ct)
                               .ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(chunk.Text))
                    continue;

                fullText.Append(chunk.Text);
                var update = new StreamUpdate
                {
                    Kind = StreamUpdateKind.Content,
                    Content = chunk.Text,
                    Timestamp = TimeProvider.System.GetUtcNow()
                };
                updates.Add(update);

                if (onChunk is not null)
                    await onChunk(update).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            var partial = fullText.ToString();
            return new ExplorationDiagnosisResult(partial, ExplorationResponseParser.TryParseRootCause(partial),
                updates, true);
        }

        var monologue = fullText.ToString();
        return new ExplorationDiagnosisResult(monologue, ExplorationResponseParser.TryParseRootCause(monologue),
            updates, false);
    }
}
