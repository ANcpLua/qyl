using Qyl.DuckDb.Aot;

namespace Qyl.Collector.Tests;

public sealed class DuckDbAotWarmupTests
{
    [Fact]
    public void Warmup_round_trips_every_supported_type_and_is_idempotent()
    {
        // Warmup's _completed flag is process-wide, so a single test owns the whole story:
        // reset to guarantee THIS call runs the full verification (not a fast-path no-op from
        // an earlier test), then call twice to cover idempotency. On JIT this is a DuckDB.NET
        // behavior self-test; under Native AOT the same code is the load-bearing
        // generic-instantiation root (see internal/qyl.duckdb.aot/README.md). Warmup throws
        // with the offending type named if any read path regresses.
        DuckDbAot.ResetForVerification();
        DuckDbAot.Warmup();
        DuckDbAot.Warmup();
    }
}
