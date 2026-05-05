using Microsoft.Extensions.AI;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration;

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
