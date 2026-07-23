namespace Qyl.Cli.Tests;

// These tests bind real loopback listeners after asking the OS for an ephemeral
// port. Keep that inherently non-atomic claim-and-bind sequence out of parallel
// runner tests while leaving the rest of the suite parallel.
[CollectionDefinition(RunnerNetworkTestGroup.Name)]
public sealed class RunnerNetworkTestGroup
{
    public const string Name = "Runner network";
}
