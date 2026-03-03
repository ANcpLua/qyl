// =============================================================================
// qyl.copilot - Instrumented AI Function
// DelegatingAIFunction wrapper that creates an OTel execute_tool span per
// tool invocation. Used by ChatClientExtensions.AddInstrumentedTools().
// =============================================================================

using Microsoft.Extensions.AI;
using qyl.copilot.Instrumentation;
using qyl.protocol.Attributes;

namespace qyl.copilot.Adapters;

/// <summary>
///     Wraps an <see cref="AIFunction" /> with an OTel <c>execute_tool</c> span per invocation.
///     Follows OTel 1.40 GenAI semantic conventions for tool execution.
/// </summary>
/// <remarks>
///     Attach to tools via <see cref="ChatClientExtensions.AddInstrumentedTools" /> before
///     passing <see cref="ChatOptions" /> to <see cref="InstrumentedChatClient" />.
/// </remarks>
public sealed class InstrumentedAIFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        using var activity = StartToolActivity();

        // Use a success flag in try-finally so all exceptions propagate unchanged —
        // user-defined tool code can throw anything, and we must not interfere.
        bool succeeded = false;
        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken)
                .ConfigureAwait(false);

            succeeded = true;
            return result;
        }
        finally
        {
            if (activity is not null)
            {
                if (succeeded)
                    activity.SetStatus(ActivityStatusCode.Ok);
                else if (!cancellationToken.IsCancellationRequested)
                    activity.SetStatus(ActivityStatusCode.Error); // tool threw — exact type propagates to caller
                // Cancelled: leave status Unset (not an error)
            }
        }
    }

    private Activity? StartToolActivity()
    {
        var activity = CopilotInstrumentation.ActivitySource
            .StartActivity($"{GenAiAttributes.Operations.ExecuteTool} {Name}", ActivityKind.Client);

        if (activity is null) return null;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
        activity.SetTag(GenAiAttributes.ToolName, Name);

        if (!string.IsNullOrEmpty(Description))
            activity.SetTag(GenAiAttributes.ToolDescription, Description);

        return activity;
    }
}
