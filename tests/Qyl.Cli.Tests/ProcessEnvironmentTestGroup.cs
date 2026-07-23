namespace Qyl.Cli.Tests;

// Environment variables are process-wide. Tests in this collection must not overlap any other
// test that composes a host from ambient configuration.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentTestGroup
{
    public const string Name = "Process environment";
}
