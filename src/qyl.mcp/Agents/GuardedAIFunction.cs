using Microsoft.Extensions.AI;

namespace qyl.mcp.Agents;

/// <summary>
///     Wraps an <see cref="AIFunction" /> to record each invocation via
///     <see cref="InvestigationGuard" />. When the tool call budget is exhausted,
///     the guard throws <see cref="OperationCanceledException" /> with partial results.
/// </summary>
internal sealed class GuardedAIFunction(AIFunction inner, InvestigationGuard guard) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        guard.RecordCall(inner.Name);

        var result = await inner.InvokeAsync(arguments, cancellationToken);

        if (result is string text)
            guard.AddPartialResult(inner.Name, text);

        return result;
    }
}
