namespace Qyl.E2E.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<Topology.QylTopologyFixture>
{
    public const string Name = "E2E";
}
