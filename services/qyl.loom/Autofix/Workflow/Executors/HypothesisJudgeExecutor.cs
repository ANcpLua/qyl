
namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class HypothesisJudgeExecutor(
    string id,
    AIAgent agent,
    AutofixReportAssemblyState state,
    IAutofixStepLedger ledger)
    : Executor<List<HypothesisCandidate>>(id)
{
    private readonly List<HypothesisCandidate> _buffer = [];
    private string? _runId;
    private int _retryIteration;

    public override ValueTask HandleAsync(
        List<HypothesisCandidate> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        _buffer.AddRange(message);
        if (_buffer.Count > 0) _runId = _buffer[0].RunId;
        return default;
    }

    protected override async ValueTask OnMessageDeliveryFinishedAsync(
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (_buffer.Count is 0 || _runId is null) return;

        var prompt = BuildPrompt(_buffer);
        var response = await agent
            .RunAsync<HypothesisVerdictDraft>(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var draft = response.Result;

        var verdict = new HypothesisVerdict(
            _runId,
            draft.Primary,
            draft.Alternative,
            draft.Rationale ?? "(no rationale)",
            _retryIteration);

        state.Record(_runId, verdict);
        await ledger.RecordHypothesisAsync(verdict, cancellationToken).ConfigureAwait(false);
        await context.AddEventAsync(new HypothesisRecorded(verdict), cancellationToken).ConfigureAwait(false);
        await context
            .QueueStateUpdateAsync(AutofixAssemblyKeys.Hypothesis, verdict, AutofixAssemblyKeys.Scope, cancellationToken)
            .ConfigureAwait(false);

        _buffer.Clear();
        _retryIteration++;

        await context.SendMessageAsync(verdict, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPrompt(IReadOnlyList<HypothesisCandidate> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Pick the strongest primary hypothesis from the candidates below. Cite the most");
        sb.AppendLine(
            "signals, name a mechanism (not a symptom), be internally consistent. Optionally");
        sb.AppendLine("rank one alternative.");
        sb.AppendLine();
        sb.AppendLine("## Candidates");
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            sb.AppendLine(
                $"### Candidate {i + 1} — perspective={c.BranchId}, self_confidence={c.SelfReportedConfidence:F2}");
            sb.AppendLine($"Primary: {c.Primary}");
            sb.AppendLine($"Alternative: {c.Alternative ?? "(none)"}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private sealed record HypothesisVerdictDraft(string Primary, string? Alternative, string? Rationale);
}
