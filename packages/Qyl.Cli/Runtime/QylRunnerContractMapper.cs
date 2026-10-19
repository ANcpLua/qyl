using ContractLifecycle = Qyl.Api.Contracts.Runner.RunnerResourceLifecycle;
using ContractLogLine = Qyl.Api.Contracts.Runner.RunnerLogLine;
using ContractLogStream = Qyl.Api.Contracts.Runner.RunnerLogStream;
using ContractResourceKind = Qyl.Api.Contracts.Runner.RunnerResourceKind;
using ContractResourceState = Qyl.Api.Contracts.Runner.RunnerResourceState;

namespace Qyl.Cli.Runtime;

internal static class QylRunnerContractMapper
{
    internal static ContractResourceState ToContract(QylResourceState state) => new()
    {
        Name = state.Name,
        Lifecycle = state.Lifecycle switch
        {
            ResourceLifecycle.Pending => ContractLifecycle.Pending,
            ResourceLifecycle.Starting => ContractLifecycle.Starting,
            ResourceLifecycle.Ready => ContractLifecycle.Ready,
            ResourceLifecycle.Stopping => ContractLifecycle.Stopping,
            ResourceLifecycle.Stopped => ContractLifecycle.Stopped,
            ResourceLifecycle.Failed => ContractLifecycle.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Lifecycle, "Unknown lifecycle")
        },
        Timestamp = state.Timestamp,
        Kind = state.Kind switch
        {
            QylResourceKind.Collector => ContractResourceKind.Collector,
            QylResourceKind.Project => ContractResourceKind.Project,
            QylResourceKind.Command => ContractResourceKind.Command,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unknown resource kind")
        },
        AllocatedPort = state.AllocatedPort,
        Endpoint = state.Endpoint,
        LastError = state.LastError
    };

    internal static ContractLogLine ToContract(QylLogLine line) => new()
    {
        Resource = line.Resource,
        Stream = line.Stream switch
        {
            QylLogStream.Stdout => ContractLogStream.Stdout,
            QylLogStream.Stderr => ContractLogStream.Stderr,
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.Stream, "Unknown log stream")
        },
        Line = line.Line
    };
}
