// =============================================================================
// qyl.copilot - Core Copilot Adapter
// Wraps Microsoft.Agents.AI GitHubCopilotAgent with qyl-specific features
// OTel 1.39 GenAI semantic conventions instrumentation
// =============================================================================

using System.Runtime.CompilerServices;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using qyl.copilot.Auth;
using qyl.copilot.Instrumentation;
using qyl.protocol.Copilot;

namespace qyl.copilot.Adapters;

/// <summary>
///     Core adapter wrapping GitHubCopilotAgent with qyl-specific features.
///     Provides streaming and workflow execution with SDK-managed instrumentation.
/// </summary>
public sealed class QylCopilotAdapter : IAsyncDisposable
{
    private readonly AIAgent _agent;
    private readonly CopilotAuthProvider _authProvider;
    private readonly CopilotClient _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    /// <summary>
    ///     Per-request channel for tool call/result events. Set before each streaming call.
    ///     Null when no tools are configured or no request is active.
    /// </summary>
    private volatile Channel<StreamUpdate>? _toolEventChannel;

    private QylCopilotAdapter(
        CopilotClient client,
        AIAgent agent,
        CopilotAuthProvider authProvider,
        TimeProvider timeProvider)
    {
        _client = client;
        _agent = agent;
        _authProvider = authProvider;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;

            await _client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _initLock.Release();
            _initLock.Dispose();
        }
    }

    /// <summary>
    ///     Creates a new QylCopilotAdapter with automatic authentication.
    ///     Uses SDK's built-in OpenTelemetry instrumentation.
    /// </summary>
    /// <param name="options">Authentication options (auto-detect if null).</param>
    /// <param name="instructions">System instructions for the agent.</param>
    /// <param name="tools">Optional AI tools the agent can invoke during chat.</param>
    /// <param name="timeProvider">Time provider (defaults to System).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Configured adapter ready for use.</returns>
    public static async Task<QylCopilotAdapter> CreateAsync(
        CopilotAuthOptions? options = null,
        string? instructions = null,
        IReadOnlyList<AITool>? tools = null,
        TimeProvider? timeProvider = null,
        CancellationToken ct = default)
    {
        var time = timeProvider ?? TimeProvider.System;
        var authProvider = new CopilotAuthProvider(options, time);

        // Verify authentication
        var authResult = await authProvider.GetTokenAsync(ct).ConfigureAwait(false);
        if (!authResult.Success)
        {
            throw new InvalidOperationException($"Authentication failed: {authResult.Error}");
        }

        // Create and start the Copilot client
        var client = new CopilotClient();
        await client.StartAsync(ct).ConfigureAwait(false);

        // Wrap tools with event interceptors that write to the adapter's per-request channel.
        // Use a holder so the lambda captures a stable reference (not a reassigned local).
        var adapterHolder = new StrongBox<QylCopilotAdapter?>(null);
        List<AITool>? wrappedTools = null;

        if (tools is { Count: > 0 })
        {
            wrappedTools = tools.Select(tool =>
            {
                if (tool is not AIFunction fn) return tool;

                return new ToolEventInterceptor(fn, () => adapterHolder.Value?._toolEventChannel, time);
            }).ToList();
        }

        // Create the agent - OTel instrumentation is handled AUTOMATICALLY by
        // qyl.servicedefaults.generator which intercepts AIAgent.RunAsync() calls
        // at compile time. No manual UseOpenTelemetry() needed!
        var agent = client.AsAIAgent(
            tools: wrappedTools,
            instructions: string.IsNullOrEmpty(instructions) ? null : instructions);

        var adapter = new QylCopilotAdapter(client, agent, authProvider, time);
        adapterHolder.Value = adapter;

        return adapter;
    }

    /// <summary>
    ///     Gets the current authentication status.
    /// </summary>
    public ValueTask<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _authProvider.GetStatusAsync(ct);
    }

    /// <summary>
    ///     Executes a chat interaction with streaming responses.
    ///     Creates OTel spans per 1.39 GenAI semantic conventions.
    ///     Tool call/result events are interleaved with content updates.
    /// </summary>
    /// <param name="prompt">The user's prompt.</param>
    /// <param name="context">Optional execution context (reserved for future use).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of updates during execution.</returns>
    public async IAsyncEnumerable<StreamUpdate> ChatAsync(
        string prompt,
        CopilotContext? context = null, // Reserved for future SDK integration
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _ = context; // Reserved for future SDK integration
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Start OTel span for chat operation
        using var activity = CopilotInstrumentation.StartChatSpan();
        var startTime = _timeProvider.GetUtcNow();

        // Create per-request tool event channel
        var toolChannel = Channel.CreateUnbounded<StreamUpdate>(
            new UnboundedChannelOptions { SingleReader = true });
        _toolEventChannel = toolChannel;

        long outputTokens = 0;
        var updates = new List<StreamUpdate>();
        Exception? caughtException = null;
        bool firstTokenReceived = false;

        // Collect updates without yielding in try block
        var enumerator = _agent.RunStreamingAsync(prompt, cancellationToken: ct).GetAsyncEnumerator(ct);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                // Drain any tool events that arrived while streaming
                while (toolChannel.Reader.TryRead(out var toolEvent))
                    updates.Add(toolEvent);

                var update = enumerator.Current;
                var content = update.ToString();

                // Track time to first token
                if (!firstTokenReceived && !string.IsNullOrEmpty(content))
                {
                    firstTokenReceived = true;
                    var ttft = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    CopilotSpanRecorder.RecordTimeToFirstToken(activity, ttft);
                }

                // Note: Actual token count should come from SDK response metadata
                // This is a placeholder until SDK provides proper token usage
                outputTokens++;

                updates.Add(new StreamUpdate
                {
                    Kind = StreamUpdateKind.Content,
                    Content = content,
                    Timestamp = _timeProvider.GetUtcNow()
                });
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
            CopilotSpanRecorder.RecordError(activity, ex);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            _toolEventChannel = null;
            toolChannel.Writer.TryComplete();
        }

        // Drain any remaining tool events
        while (toolChannel.Reader.TryRead(out var remaining))
            updates.Add(remaining);

        // Record operation duration metric
        var duration = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
        CopilotMetrics.RecordOperationDuration(duration, CopilotInstrumentation.GenAiSystem, CopilotInstrumentation.OperationChat);

        // Now yield outside try-catch
        foreach (var update in updates)
        {
            yield return update;
        }

        if (caughtException is not null)
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = caughtException.Message,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
        else
        {
            CopilotSpanRecorder.RecordSuccess(activity);

            // Final metadata update
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Completed,
                OutputTokens = outputTokens,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
    }

    /// <summary>
    ///     Executes a chat interaction and returns the complete response.
    ///     Creates OTel spans per 1.39 GenAI semantic conventions.
    /// </summary>
    /// <param name="prompt">The user's prompt.</param>
    /// <param name="context">Optional execution context (reserved for future use).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Complete response text.</returns>
    public async Task<string> ChatCompleteAsync(
        string prompt,
        CopilotContext? context = null, // Reserved for future SDK integration
        CancellationToken ct = default)
    {
        _ = context; // Reserved for future SDK integration
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ct.ThrowIfCancellationRequested();

        // Start OTel span for chat operation
        using var activity = CopilotInstrumentation.StartChatSpan();
        var startTime = _timeProvider.GetUtcNow();

        try
        {
            var result = await _agent.RunAsync(prompt, cancellationToken: ct).ConfigureAwait(false);
            var response = result.ToString();

            // Record operation duration
            var duration = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
            CopilotMetrics.RecordOperationDuration(duration, CopilotInstrumentation.GenAiSystem, CopilotInstrumentation.OperationChat);

            CopilotSpanRecorder.RecordSuccess(activity);
            return response;
        }
        catch (Exception ex)
        {
            CopilotSpanRecorder.RecordError(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes a workflow with streaming updates.
    ///     Note: This is an internal method; WorkflowEngine creates the parent span.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="context">Execution context with parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream of updates during execution.</returns>
    public async IAsyncEnumerable<StreamUpdate> ExecuteWorkflowAsync(
        CopilotWorkflow workflow,
        CopilotContext? context = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        Throw.IfNull(workflow);

        var startTime = _timeProvider.GetUtcNow();

        yield return new StreamUpdate
        {
            Kind = StreamUpdateKind.Progress,
            Progress = 0,
            Content = $"Starting workflow: {workflow.Name}",
            Timestamp = startTime
        };

        // Substitute template parameters in instructions
        var instructions = SubstituteParameters(workflow.Instructions, context?.Parameters);

        // Add context if provided
        if (!string.IsNullOrEmpty(context?.AdditionalContext))
        {
            instructions = $"{instructions}\n\n## Context\n{context.AdditionalContext}";
        }

        yield return new StreamUpdate
        {
            Kind = StreamUpdateKind.Progress,
            Progress = 10,
            Content = "Executing workflow...",
            Timestamp = _timeProvider.GetUtcNow()
        };

        var updates = new List<StreamUpdate>();
        long outputTokens = 0;
        Exception? caughtException = null;
        bool firstTokenReceived = false;

        var enumerator = _agent.RunStreamingAsync(instructions, cancellationToken: ct).GetAsyncEnumerator(ct);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var update = enumerator.Current;
                var content = update.ToString();

                // Track time to first token on current activity
                if (!firstTokenReceived && !string.IsNullOrEmpty(content))
                {
                    firstTokenReceived = true;
                    var ttft = (_timeProvider.GetUtcNow() - startTime).TotalSeconds;
                    var currentActivity = Activity.Current;
                    CopilotSpanRecorder.RecordTimeToFirstToken(currentActivity, ttft);
                }

                outputTokens++;

                updates.Add(new StreamUpdate
                {
                    Kind = StreamUpdateKind.Content,
                    Content = content,
                    Timestamp = _timeProvider.GetUtcNow()
                });
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // Yield collected updates
        foreach (var update in updates)
        {
            yield return update;
        }

        if (caughtException is not null)
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = caughtException.Message,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
        else
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Completed,
                OutputTokens = outputTokens,
                Timestamp = _timeProvider.GetUtcNow()
            };
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static string SubstituteParameters(string template, IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count is 0)
        {
            return template;
        }

        var result = template;
        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    ///     Wraps an AIFunction to emit ToolCall/ToolResult StreamUpdate events
    ///     into the adapter's per-request channel when the function is invoked.
    /// </summary>
    private sealed class ToolEventInterceptor(
        AIFunction inner,
        Func<Channel<StreamUpdate>?> getChannel,
        TimeProvider time)
        : DelegatingAIFunction(inner)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var channel = getChannel();
            string? argsJson = null;

            if (arguments.Count > 0)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var kvp in arguments)
                    dict[kvp.Key] = kvp.Value;
                argsJson = JsonSerializer.Serialize(dict);
            }

            // Emit ToolCall event
            channel?.Writer.TryWrite(new StreamUpdate
            {
                Kind = StreamUpdateKind.ToolCall,
                ToolName = Name,
                ToolArguments = argsJson,
                Timestamp = time.GetUtcNow()
            });

            try
            {
                var result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);

                // Emit ToolResult event
                channel?.Writer.TryWrite(new StreamUpdate
                {
                    Kind = StreamUpdateKind.ToolResult,
                    ToolName = Name,
                    ToolResult = result?.ToString(),
                    Timestamp = time.GetUtcNow()
                });

                return result;
            }
            catch (Exception ex)
            {
                // Emit ToolResult event with error
                channel?.Writer.TryWrite(new StreamUpdate
                {
                    Kind = StreamUpdateKind.ToolResult,
                    ToolName = Name,
                    ToolResult = $"Error: {ex.Message}",
                    Error = ex.Message,
                    Timestamp = time.GetUtcNow()
                });

                throw;
            }
        }
    }
}
