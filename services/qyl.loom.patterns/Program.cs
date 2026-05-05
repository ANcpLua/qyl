
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Loom.Patterns.Agents;
using Qyl.Loom.Patterns.Clients;
using Qyl.Loom.Patterns.Patterns;

var pattern = args.Length > 0 ? args[0].ToLowerInvariant() : "all-combined";

await using var services = new ServiceCollection()
    .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
    .BuildServiceProvider();

using IQylLoomPatternsChatClientBuilder clients = new QylLoomPatternsChatClientBuilder(services);
IQylLoomPatternsAgentsBuilder agents = new QylLoomPatternsAgentsBuilder(clients, services);

using var cts = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
Console.CancelKeyPress += cancelHandler;

Console.WriteLine($"── pattern: {pattern} ──");

try
{
    var selected = pattern switch
    {
        "switch-routing" => Pattern01_SwitchRouting.RunAsync(agents, cts.Token),
        "sub-workflow" => Pattern02_SubWorkflow.RunAsync(agents, cts.Token),
        "checkpoint-resume" => Pattern03_CheckpointResume.RunAsync(agents, cts.Token),
        "hitl" => Pattern04_HitlViaExternalCall.RunAsync(agents, cts.Token),
        "stateful-executor" => Pattern05_StatefulExecutor.RunAsync(agents, cts.Token),
        "all-combined" => Pattern06_AllCombined.RunAsync(agents, cts.Token),
        _ => PrintUsageAsync()
    };

    await selected.ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n(cancelled)");
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

return;

static Task PrintUsageAsync()
{
    Console.WriteLine("""
                      usage: dotnet run --project services/qyl.loom.patterns -- <pattern>

                      patterns:
                        switch-routing       AddSwitch / AddCase<T> / WithDefault — severity triage
                        sub-workflow         Workflow.BindAsExecutor — inner graph as one node
                        checkpoint-resume    CheckpointManager + RestoreCheckpointAsync
                        hitl                 AddExternalCall<TReq,TResp> + ForwardMessage — one-line HITL
                        stateful-executor    StatefulExecutor<TState,TIn,TOut> + InvokeWithStateAsync + AddEventAsync
                        all-combined         autofix-shaped end-to-end touching every primitive (default)
                      """);
    return Task.CompletedTask;
}
