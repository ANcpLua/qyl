// Copyright (c) 2025-2026 ancplua

using Qyl.Loom.Agents;
using Qyl.Loom.Autofix.Workflow;
using Qyl.Loom.Workflows;

namespace Qyl.Loom.Autofix;

internal sealed partial class LoomAutofixRunner(
    CollectorClient collector,
    AutofixOrchestrator orchestrator,
    ILogger<LoomAutofixRunner> logger,
    IQylLoomAgentsBuilder agents,
    IQylLoomWorkflowBuilder workflows,
    AutofixRunRegistry registry,
    AutofixReportAssemblyState assembly,
    IAutofixLifecycleBus lifecycle,
    AutofixRunConfigStore configStore,
    CheckpointManager checkpointManager,
    TimeProvider timeProvider)
{
    public async Task RunAsync(string runId, CancellationToken ct = default)
    {
        try
        {
            if (!agents.IsConfigured)
            {
                LogNoLlmConfigured(runId);
                return;
            }

            var run = await collector.GetFixRunAsync(runId, ct).ConfigureAwait(false);
            if (run is null)
            {
                LogRunNotFound(runId);
                return;
            }

            var config = ResolveConfig(runId, run);
            await ExecuteRunAsync(runId, run, config, ct).ConfigureAwait(false);
        }
        finally
        {
            configStore.TryRemove(runId);
        }
    }

    public async Task RunAsync(string runId, AutofixWorkflowConfig config, CancellationToken ct = default)
    {
        try
        {
            if (!agents.IsConfigured)
            {
                LogNoLlmConfigured(runId);
                return;
            }

            var run = await collector.GetFixRunAsync(runId, ct).ConfigureAwait(false);
            if (run is null)
            {
                LogRunNotFound(runId);
                return;
            }

            await ExecuteRunAsync(runId, run, config, ct).ConfigureAwait(false);
        }
        finally
        {
            configStore.TryRemove(runId);
        }
    }

    private AutofixWorkflowConfig ResolveConfig(string runId, FixRunRecord run)
    {
        if (configStore.TryGet(runId, out var registered))
        {
            return registered;
        }

        // Honor the persisted FixRunRecord.StoppingPoint marker — set by callers that
        // wanted HITL but couldn't reach the in-memory configStore (e.g. cross-restart
        // pickup from the collector queue).
        return run.StoppingPoint is { Length: > 0 }
            ? AutofixWorkflowDefaults.Interactive
            : AutofixWorkflowDefaults.Autonomous;
    }

    private async Task ExecuteRunAsync(
        string runId, FixRunRecord run, AutofixWorkflowConfig config, CancellationToken ct)
    {
        var policy = Enum.TryParse<FixPolicy>(run.Policy, true, out var parsedPolicy)
            ? parsedPolicy
            : FixPolicy.RequireReview;

        var issue = await collector.GetIssueByIdAsync(run.IssueId, ct).ConfigureAwait(false);
        if (issue is null)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Issue '{run.IssueId}' not found.", ct: ct)
                .ConfigureAwait(false);
            LogIssueNotFound(runId, run.IssueId);
            return;
        }

        await orchestrator.UpdateFixRunStatusAsync(run.IssueId, runId, "running", ct: ct).ConfigureAwait(false);

        registry.Register(new AutofixRunRegistry.RegisteredRun(runId, run.IssueId, run.Policy));

        try
        {
            var report = await ExecuteWorkflowAsync(run, config, ct).ConfigureAwait(false);

            if (report is null)
            {
                await orchestrator
                    .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                        "Autofix workflow terminated without emitting AutofixWorkflowResult.", ct: ct)
                    .ConfigureAwait(false);
                LogWorkflowNoOutput(runId);
                return;
            }

            var confidence = report.ConfidenceScoreSum / 12.0;
            var nextStatus = PolicyGate.EvaluateNextStatus(policy, confidence);
            var description =
                $"Loom autofix complete | confidence={confidence:F2} ({report.ConfidenceLevel}) | fixability={report.FixabilityScore}/5";
            var changesJson = BuildChangesJson(report);

            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, nextStatus, description,
                    confidence, changesJson, ct)
                .ConfigureAwait(false);

            LogRunCompleted(runId, nextStatus, confidence);
        }
        catch (JsonException ex)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Structured-output schema violation: {ex.Message}", ct: ct)
                .ConfigureAwait(false);
            LogSchemaViolation(runId, ex);
        }
        catch (HttpRequestException ex)
        {
            await orchestrator
                .UpdateFixRunStatusAsync(run.IssueId, runId, "failed",
                    $"Transport failure: {ex.Message}", ct: ct)
                .ConfigureAwait(false);
            LogTransportFailure(runId, ex);
        }
        finally
        {
            // configStore cleanup runs in the outer RunAsync's finally so all early-exit
            // paths (no LLM, missing run record) also drop the entry.
            registry.TryRemove(runId);
            assembly.TryRemove(runId);
        }
    }

    private async Task<AutofixReport?> ExecuteWorkflowAsync(
        FixRunRecord run, AutofixWorkflowConfig config, CancellationToken ct)
    {
        var workflow = workflows.BuildAutofixWorkflow(config);
        var request = new AutofixWorkflowRequest(
            run.RunId,
            run.IssueId,
            run.Policy,
            run.Instruction,
            run.StoppingPoint,
            config);

        var pendingTimeouts = new List<Task>();

        // Wrap the entire streaming-run lifecycle in a try/finally so a startup
        // failure (executor construction, model init, etc.) still completes the
        // lifecycle channel and leaves SSE subscribers unblocked.
        StreamingRun? execution = null;
        try
        {
            execution = await InProcessExecution
                .RunStreamingAsync(workflow, request, checkpointManager, run.RunId, ct)
                .ConfigureAwait(false);

            AutofixWorkflowResult? final = null;

            await foreach (var evt in execution.WatchStreamAsync(ct).ConfigureAwait(false))
            {
                switch (evt)
                {
                    case AutofixLifecycleEvent lifecycleEvent:
                        lifecycle.Publish(run.RunId, ToEnvelope(lifecycleEvent));
                        break;

                    case RequestInfoEvent ri:
                        // Always publish HITL-gate firings to the lifecycle bus so the
                        // dashboard can render the gate even when no auto-resolver is wired.
                        lifecycle.Publish(run.RunId, ToGateEnvelope(ri, run.RunId));

                        if (config.StoppingPointTimeout is { } timeout)
                        {
                            pendingTimeouts.Add(AutoResolveAfterTimeoutAsync(execution, ri, run.RunId, timeout, ct));
                        }
                        else
                        {
                            LogStoppingPointWithoutTimeout(run.RunId);
                        }

                        break;

                    case WorkflowOutputEvent { Data: AutofixWorkflowResult result }:
                        final = result;
                        break;
                }

                if (final is not null) break;
            }

            return final?.Report;
        }
        finally
        {
            lifecycle.Complete(run.RunId);
            foreach (var t in pendingTimeouts)
            {
                try { await t.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            if (execution is not null)
            {
                await execution.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task AutoResolveAfterTimeoutAsync(
        StreamingRun execution, RequestInfoEvent ri, string runId, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            await Task.Delay(timeout, timeProvider, ct).ConfigureAwait(false);

            if (ri.Request.TryGetDataAs<HypothesisVerdict>(out var hyp))
            {
                await execution.SendResponseAsync(ri.Request.CreateResponse(hyp)).ConfigureAwait(false);
                LogStoppingPointAutoApproved(runId, "pre_solution", timeout);
            }
            else if (ri.Request.TryGetDataAs<ConfidenceAudit>(out var audit))
            {
                await execution.SendResponseAsync(ri.Request.CreateResponse(audit)).ConfigureAwait(false);
                LogStoppingPointAutoApproved(runId, "pre_commit", timeout);
            }
        }
        catch (OperationCanceledException)
        {
            // Run completed (response arrived in time, or workflow ended) — nothing to auto-resolve.
        }
    }

    private AutofixLifecycleEnvelope ToEnvelope(AutofixLifecycleEvent evt) =>
        new(
            evt.RunId,
            evt.Stage,
            evt.GetType().Name,
            JsonSerializer.Serialize<object>(evt.Data),
            timeProvider.GetUtcNow());

    private AutofixLifecycleEnvelope ToGateEnvelope(RequestInfoEvent ri, string runId)
    {
        var (gate, payload) = ri.Request.TryGetDataAs<HypothesisVerdict>(out var hyp)
            ? ("pre_solution", JsonSerializer.Serialize<object>(hyp))
            : ri.Request.TryGetDataAs<ConfidenceAudit>(out var audit)
                ? ("pre_commit", JsonSerializer.Serialize<object>(audit))
                : ("unknown", "{}");

        return new AutofixLifecycleEnvelope(
            runId,
            "stopping_point",
            $"StoppingPoint.{gate}",
            payload,
            timeProvider.GetUtcNow());
    }

    private static string BuildChangesJson(AutofixReport report) =>
        JsonSerializer.Serialize(new
        {
            repo = report.SolutionRepo,
            diff = report.SolutionDiff,
            regression_test = report.RegressionTest,
            primary_hypothesis = report.PrimaryHypothesis,
            confidence_level = report.ConfidenceLevel,
            final_report = report.FinalReport
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Loom autofix skipped for run {RunId}: no LLM configured")]
    private partial void LogNoLlmConfigured(string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} not found in collector")]
    private partial void LogRunNotFound(string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId}: issue {IssueId} not found")]
    private partial void LogIssueNotFound(string runId, string issueId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Loom autofix run {RunId} completed: status={Status}, confidence={Confidence:F2}")]
    private partial void LogRunCompleted(string runId, string status, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} schema violation")]
    private partial void LogSchemaViolation(string runId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom autofix run {RunId} transport failure")]
    private partial void LogTransportFailure(string runId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Loom autofix run {RunId}: stopping-point '{Gate}' auto-approved after {Timeout}")]
    private partial void LogStoppingPointAutoApproved(string runId, string gate, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Loom autofix run {RunId} terminated without emitting AutofixWorkflowResult — marking failed")]
    private partial void LogWorkflowNoOutput(string runId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Loom autofix run {RunId}: HITL stopping-point fired but no StoppingPointTimeout configured — workflow may block indefinitely waiting for an approval response")]
    private partial void LogStoppingPointWithoutTimeout(string runId);
}

internal static class AutofixWorkflowDefaults
{
    /// Background-routed runs (TriagePipelineService auto-route, future
    /// scheduled jobs). No HITL — there is no human at the other end. Tool-using
    /// context ON because we want the best evidence the agent can gather.
    public static readonly AutofixWorkflowConfig Autonomous = new(
        HypothesisFanOut: 3,
        HypothesisTemperatureSpread: 0.4,
        HypothesisAlternateModel: null,
        ConfidenceRetryThreshold: 9,
        MaxConfidenceRetries: 1,
        StoppingPointAfterHypothesis: false,
        StoppingPointBeforeCommit: false,
        ToolUsingContext: true,
        ContextToolBudget: 6,
        StoppingPointTimeout: null);

    /// User-initiated runs from the dashboard that explicitly opt into review.
    /// HITL ON at both gates so the user can intervene; 5-minute timeout
    /// auto-approves if the user closes the browser tab so the workflow doesn't
    /// deadlock indefinitely.
    public static readonly AutofixWorkflowConfig Interactive = new(
        HypothesisFanOut: 3,
        HypothesisTemperatureSpread: 0.4,
        HypothesisAlternateModel: null,
        ConfidenceRetryThreshold: 9,
        MaxConfidenceRetries: 1,
        StoppingPointAfterHypothesis: true,
        StoppingPointBeforeCommit: true,
        ToolUsingContext: true,
        ContextToolBudget: 6,
        StoppingPointTimeout: TimeSpan.FromMinutes(5));
}
