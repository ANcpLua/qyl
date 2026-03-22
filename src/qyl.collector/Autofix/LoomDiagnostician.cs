using Microsoft.Extensions.AI;
using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Bounded Loom sub-agent responsible only for root-cause investigation.
/// </summary>
public sealed class LoomDiagnostician(IChatClient? llm = null)
{
    public bool IsConfigured => llm is not null;

    public async Task<LoomDiagnosisResult> DiagnoseAsync(
        IssueContext context,
        CancellationToken ct = default)
    {
        if (llm is null)
            return LoomDiagnosisResult.Unconfigured;

        var prompt = $"""
                      {LoomPrompts.ExplorerMonologue}

                      Error context:
                      {context.FormattedBlock}
                      """;

        List<StreamUpdate> updates = [];
        StringBuilder fullText = new();

        try
        {
            await foreach (var chunk in llm.GetStreamingResponseAsync(prompt, cancellationToken: ct).ConfigureAwait(false))
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
            return new LoomDiagnosisResult(partial, LoomResponseParser.TryParseRootCause(partial), updates, true);
        }

        var monologue = fullText.ToString();
        return new LoomDiagnosisResult(monologue, LoomResponseParser.TryParseRootCause(monologue), updates, false);
    }
}

public sealed record LoomDiagnosisResult(
    string Monologue,
    LoomRootCause? RootCause,
    IReadOnlyList<StreamUpdate> Updates,
    bool IsInterrupted)
{
    public static LoomDiagnosisResult Unconfigured { get; } = new(string.Empty, null, [], false);
}
