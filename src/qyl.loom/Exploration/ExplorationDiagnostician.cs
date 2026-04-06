using Microsoft.Extensions.AI;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration;

/// <summary>
///     Bounded sub-agent responsible only for root-cause investigation.
///     Streams LLM reasoning to the caller via StreamUpdate.
/// </summary>
public sealed class ExplorationDiagnostician(IChatClient? llm = null)
{
    public bool IsConfigured => llm is not null;

    public async Task<ExplorationDiagnosisResult> DiagnoseAsync(
        ExplorationContext context,
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
                updates.Add(new StreamUpdate
                {
                    Kind = StreamUpdateKind.Content,
                    Content = chunk.Text,
                    Timestamp = TimeProvider.System.GetUtcNow()
                });
            }
        }
        catch (OperationCanceledException)
        {
            var partial = fullText.ToString();
            return new ExplorationDiagnosisResult(partial, ExplorationResponseParser.TryParseRootCause(partial), updates, true);
        }

        var monologue = fullText.ToString();
        return new ExplorationDiagnosisResult(monologue, ExplorationResponseParser.TryParseRootCause(monologue), updates, false);
    }
}
