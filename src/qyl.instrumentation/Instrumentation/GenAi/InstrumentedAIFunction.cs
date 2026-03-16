// =============================================================================
// qyl.instrumentation - Instrumented AI Function
// DelegatingAIFunction wrapper that creates an OTel execute_tool span per
// tool invocation. Used by ChatClientExtensions.AddInstrumentedTools().
// =============================================================================

using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Wraps an <see cref="AIFunction" /> with an OTel <c>execute_tool</c> span per invocation.
///     Follows OTel 1.40 GenAI semantic conventions for tool execution.
/// </summary>
public sealed class InstrumentedAIFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        using var activity = StartToolActivity();

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
                    activity.SetStatus(ActivityStatusCode.Error);
            }
        }
    }

    private Activity? StartToolActivity()
    {
        var activity = ActivitySources.GenAiSource
            .StartActivity($"{GenAiAttributes.Operations.ExecuteTool} {Name}", ActivityKind.Client);

        if (activity is null) return null;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
        activity.SetTag(GenAiAttributes.ToolName, Name);

        if (!string.IsNullOrEmpty(Description))
            activity.SetTag(GenAiAttributes.ToolDescription, Description);

        return activity;
    }
}
