namespace Qyl.Collector.Tests.Functional;

// Functional tests boot WebApplication<Program> in-process, which runs DuckDB
// migrations under DuckDbStore. DuckDB takes a process-wide lock on catalog
// ALTER operations even between distinct named in-memory databases, so two
// fixtures running migrations concurrently surface as "TransactionContext
// Error: Catalog write-write conflict on alter". Putting every functional
// test class in this collection with DisableParallelization = true makes the
// migration runs serial without forcing the rest of the test assembly to give
// up parallelism. New functional test classes should add [Collection(Name)].
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FunctionalCollection
{
    public const string Name = "Functional";
}
