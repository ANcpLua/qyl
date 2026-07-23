namespace Qyl.Cli.Runtime;

internal sealed class QylAppOptions
{
    public int RunnerPort { get; init; } = QylConstants.Ports.RunnerApi;

    public int StartupTimeoutSeconds { get; init; } = QylConstants.Orchestrator.StartupTimeoutSeconds;
}
