using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

internal sealed class TracedAIFunction(
    AIFunction inner,
    ActivitySource source,
    string operationName = "execute_tool",
    Func<AIFunction, IEnumerable<KeyValuePair<string, object?>>>? tagFactory = null)
    : DelegatingAIFunction(inner)
{
    private readonly ActivitySource _source = source;
    private readonly AIFunction _inner = inner;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        var succeeded = false;
        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
            succeeded = true;
            return result;
        }
        finally
        {
            if (activity is not null)
            {
                if (succeeded)
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    activity.SetStatus(ActivityStatusCode.Error);
                }
            }
        }
    }

    private Activity? StartActivity()
    {
        var activity = _source.StartActivity($"{operationName} {Name}", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("gen_ai.operation.name", operationName);
        activity.SetTag("gen_ai.tool.name", Name);

        if (!string.IsNullOrEmpty(Description))
        {
            activity.SetTag("gen_ai.tool.description", Description);
        }

        if (tagFactory is not null)
        {
            foreach (var pair in tagFactory(_inner))
            {
                activity.SetTag(pair.Key, pair.Value);
            }
        }

        return activity;
    }
}
