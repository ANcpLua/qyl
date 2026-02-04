// =============================================================================
// qyl.copilot - Workflow Engine
// Orchestrates workflow discovery, execution, and state management
// Uses SDK's built-in telemetry for gen_ai.* metrics; adds qyl-specific workflow metrics
// =============================================================================

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using qyl.copilot.Adapters;
using qyl.copilot.Instrumentation;
using qyl.protocol.Copilot;

namespace qyl.copilot.Workflows;

/// <summary>
///     Engine for discovering, managing, and executing workflows.
/// </summary>
public sealed class WorkflowEngine : IAsyncDisposable
{
    private readonly QylCopilotAdapter _adapter;

    private readonly SemaphoreSlim _discoverLock = new(1, 1);

    private readonly ConcurrentDictionary<string, WorkflowExecution>
        _executions = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _executionsLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CopilotWorkflow> _workflows = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _workflowsDirectory;
    private bool _disposed;

    /// <summary>
    ///     Creates a new workflow engine.
    /// </summary>
    /// <param name="adapter">The Copilot adapter for execution.</param>
    /// <param name="workflowsDirectory">Directory containing workflow files.</param>
    /// <param name="timeProvider">Time provider (defaults to System).</param>
    public WorkflowEngine(
        QylCopilotAdapter adapter,
        string? workflowsDirectory = null,
        TimeProvider? timeProvider = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _workflowsDirectory = workflowsDirectory ?? WorkflowParser.GetDefaultWorkflowsDirectory();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _discoverLock.Dispose();
        await _adapter.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Discovers and loads all workflows from the workflows directory.
    /// </summary>
    public async Task<IReadOnlyList<CopilotWorkflow>> DiscoverWorkflowsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _discoverLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var workflows = await WorkflowParser.DiscoverWorkflowsAsync(_workflowsDirectory, "*.md", ct)
                .ConfigureAwait(false);

            _workflows.Clear();
            foreach (var workflow in workflows)
            {
                _workflows[workflow.Name] = workflow;
            }

            return workflows;
        }
        finally
        {
            _discoverLock.Release();
        }
    }

    /// <summary>
    ///     Gets all discovered workflows.
    /// </summary>
    public IReadOnlyList<CopilotWorkflow> GetWorkflows()
    {
        ThrowIfDisposed();
        return _workflows.Values.ToList();
    }

    /// <summary>
    ///     Gets a specific workflow by name.
    /// </summary>
    public CopilotWorkflow? GetWorkflow(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _workflows.TryGetValue(name, out var workflow) ? workflow : null;
    }

    /// <summary>
    ///     Executes a workflow by name with streaming updates.
    /// </summary>
    /// <param name="workflowName">Name of the workflow to execute.</param>
    /// <param name="parameters">Template parameters to substitute.</param>
    /// <param name="additionalContext">Additional context data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of execution updates.</returns>
    public async IAsyncEnumerable<StreamUpdate> ExecuteAsync(
        string workflowName,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? additionalContext = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);

        var workflow = GetWorkflow(workflowName);
        if (workflow is null)
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = $"Workflow not found: {workflowName}",
                Timestamp = _timeProvider.GetUtcNow()
            };
            yield break;
        }

        await foreach (var update in ExecuteWorkflowAsync(workflow, parameters, additionalContext, ct)
                           .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    ///     Executes a workflow with streaming updates.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="parameters">Template parameters to substitute.</param>
    /// <param name="additionalContext">Additional context data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of execution updates.</returns>
    public async IAsyncEnumerable<StreamUpdate> ExecuteWorkflowAsync(
        CopilotWorkflow workflow,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? additionalContext = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(workflow);

        var executionId = Guid.NewGuid().ToString("N");
        var startTime = _timeProvider.GetUtcNow();
        var triggerName = workflow.Trigger.ToString().ToLowerInvariant();

        // Start OTel span for workflow execution
        using var activity = CopilotInstrumentation.StartWorkflowSpan(
            workflow.Name,
            executionId,
            triggerName);

        // Create and track execution
        var execution = new WorkflowExecution
        {
            Id = executionId,
            WorkflowName = workflow.Name,
            Status = WorkflowStatus.Running,
            StartedAt = startTime,
            Parameters = parameters,
            TraceId = activity?.TraceId.ToString()
        };

        _executions[executionId] = execution;

        var context = new CopilotContext { Parameters = parameters, AdditionalContext = additionalContext };

        long totalInputTokens = 0;
        long totalOutputTokens = 0;
        var resultBuilder = new StringBuilder();
        var success = false;
        string? error = null;
        var collectedUpdates = new List<StreamUpdate>();

        // Record qyl-specific workflow start metric
        CopilotInstrumentation.WorkflowExecutions.Add(1,
            new TagList
            {
                { CopilotInstrumentation.AttrWorkflowName, workflow.Name },
                { CopilotInstrumentation.AttrWorkflowTrigger, triggerName }
            });

        // Collect updates from adapter
        var enumerator = _adapter.ExecuteWorkflowAsync(workflow, context, ct).GetAsyncEnumerator(ct);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var update = enumerator.Current;

                // Accumulate results
                if (update.Kind == StreamUpdateKind.Content && update.Content is not null)
                {
                    resultBuilder.Append(update.Content);
                }

                if (update.InputTokens.HasValue)
                {
                    totalInputTokens = update.InputTokens.Value;
                }

                if (update.OutputTokens.HasValue)
                {
                    totalOutputTokens = update.OutputTokens.Value;
                }

                if (update.Kind == StreamUpdateKind.Error)
                {
                    error = update.Error;
                }

                if (update.Kind == StreamUpdateKind.Completed)
                {
                    success = true;
                }

                collectedUpdates.Add(update);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            error = "Workflow cancelled";
            collectedUpdates.Add(new StreamUpdate
            {
                Kind = StreamUpdateKind.Error, Error = error, Timestamp = _timeProvider.GetUtcNow()
            });
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CopilotSpanRecorder.RecordError(activity, ex);
            collectedUpdates.Add(new StreamUpdate
            {
                Kind = StreamUpdateKind.Error, Error = error, Timestamp = _timeProvider.GetUtcNow()
            });
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // Update execution state
        var endTime = _timeProvider.GetUtcNow();
        var duration = (endTime - startTime).TotalSeconds;

        var statusName = error is not null ? "failed" :
            ct.IsCancellationRequested ? "cancelled" : "completed";

        var finalExecution = execution with
        {
            Status = error is not null ? WorkflowStatus.Failed :
            ct.IsCancellationRequested ? WorkflowStatus.Cancelled :
            WorkflowStatus.Completed,
            CompletedAt = endTime,
            Result = success ? resultBuilder.ToString() : null,
            Error = error,
            InputTokens = totalInputTokens,
            OutputTokens = totalOutputTokens
        };

        _executions[executionId] = finalExecution;

        // Record span attributes
        CopilotSpanRecorder.RecordTokenUsage(activity, totalInputTokens, totalOutputTokens);
        CopilotSpanRecorder.RecordWorkflowStatus(activity, statusName);

        if (success)
        {
            CopilotSpanRecorder.RecordSuccess(activity);
        }
        else if (error is not null && activity?.Status != ActivityStatusCode.Error)
        {
            CopilotSpanRecorder.RecordError(activity, error);
        }

        // Record qyl-specific workflow duration metric
        CopilotInstrumentation.WorkflowDuration.Record(duration,
            new TagList
            {
                { CopilotInstrumentation.AttrWorkflowName, workflow.Name },
                { CopilotInstrumentation.AttrWorkflowStatus, statusName }
            });

        // Record OTel gen_ai operation duration metric
        CopilotInstrumentation.OperationDuration.Record(duration,
            new TagList
            {
                { CopilotInstrumentation.AttrGenAiSystem, CopilotInstrumentation.GenAiSystem },
                { CopilotInstrumentation.AttrGenAiOperationName, CopilotInstrumentation.OperationWorkflow }
            });

        // Now yield all collected updates outside try-catch
        foreach (var update in collectedUpdates)
        {
            yield return update;
        }
    }

    /// <summary>
    ///     Gets all workflow executions.
    /// </summary>
    public IReadOnlyList<WorkflowExecution> GetExecutions()
    {
        ThrowIfDisposed();
        return _executions.Values.OrderByDescending(static e => e.StartedAt).ToList();
    }

    /// <summary>
    ///     Gets a specific execution by ID.
    /// </summary>
    public WorkflowExecution? GetExecution(string executionId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        return _executions.TryGetValue(executionId, out var execution) ? execution : null;
    }

    /// <summary>
    ///     Clears execution history, optionally keeping recent entries.
    /// </summary>
    /// <param name="keepRecent">Number of recent executions to keep (0 = clear all).</param>
    public void ClearExecutionHistory(int keepRecent = 0)
    {
        ThrowIfDisposed();

        if (keepRecent <= 0)
        {
            _executions.Clear();
            return;
        }

        using (_executionsLock.EnterScope())
        {
            var toKeep = _executions.Values
                .OrderByDescending(e => e.StartedAt)
                .Take(keepRecent)
                .Select(e => e.Id)
                .ToHashSet();

            var toRemove = _executions.Keys.Where(k => !toKeep.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                _executions.TryRemove(key, out _);
            }
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
