# Clock inventory

Refreshed from `main` at `df7458d4` on 2026-10-17, before this change. Session activity,
heartbeat scheduling and retention scheduling already used injected clocks. The refreshed
baseline contained 35 direct `DateTime.UtcNow` / `DateTimeOffset.UtcNow` / `DateTime.Now`
reads, plus the explicit system-clock calls listed below. This is a clock and ownership
review, not the broader analyzer audit.

## Behavioral clocks

| Source | Purpose | Result |
| --- | --- | --- |
| `CollectorEndpointExtensions.SavedQueries.cs`, `CollectorEndpointExtensions.Analysis.cs` | Relative analysis and saved-query windows; failure-status update timestamps | Already injected. Preserve `DateTimeOffset` tick precision when converting window boundaries and storage read plans to nanoseconds. Reject timestamps outside the OTLP range as field validation errors. |
| `CollectorEndpointExtensions.cs` session routes | Active-session cutoff | Existing single injected snapshot and inclusive cutoff retained. |
| `CollectorEndpointExtensions.cs` log stream | Heartbeat timestamps and interval | Existing injected provider and provider-aware delay retained. |
| `RetentionService.cs` | Retention/idempotency cutoffs, initial and recurring timers | Existing injected provider retained. Its execution-duration metric correctly uses `Stopwatch`. |
| `ResourceHttp.cs` | Resource creation/update timestamps and idempotency expiration | Existing injected provider retained. |
| `OtlpConverter.cs` | Receive timestamp when a log lacks observation time | Existing injected provider retained; fixture receive timestamps now use fixed clocks. |
| `samples/qyl.sample/TodoStore.cs` | Calendar dates for seeded todos | Inject `TimeProvider`, use its local timezone, and capture the local date when the store is created. |

The saved-query tests cover traces, logs and metric points immediately below, at and above both boundaries
of a sub-millisecond window. They specify `[start, end)` independently of production
conversion code, advance a new run by one tick, and continue an old cursor after its original
window has expired relative to the current clock. Analysis HTTP reads also exercise the
storage read-plan boundary. This preserves .NET's 100 ns timestamp precision; it does not
add a parser for timestamps finer than a `DateTimeOffset` tick.

## Elapsed budgets and readiness

| Source | Purpose | Result |
| --- | --- | --- |
| `eng/build/BuildApiSdk.cs`: `WaitUntilServing`, `AwaitSession`, `AwaitControl`, `WaitUntil` | External host/readback polling budgets | Use `Stopwatch`; keep real polling intervals and existing HTTP request timeouts. These loops measure elapsed polling budgets, not hard deadlines for arbitrary uncooperative I/O. |
| `eng/tools/QylSdkConformance/Program.cs`: `DriveAppAsync`, `WaitForReadbackAsync` | External process startup and telemetry readback | Use `Stopwatch`; retain real process/transport waits. |
| `eng/tools/QylToolSmoke/Program.cs`: `WaitUntilAsync` | Installed/package tool readiness | Use `Stopwatch`; retain real polling. |
| `HttpHealthProbe.cs` | Overall startup budget, including an in-flight HTTP request | One owned provider-aware cancellation deadline covers requests and retries. An elapsed deadline returns not ready; caller cancellation propagates. Requests are awaited and responses disposed before returning. |
| `QylOrchestrator.cs`: `WaitForDependenciesAsync` | Ready/failed/stopped prerequisite transitions | Reuse registry subscriptions, subscribing before inspecting state. No losing `Task.Delay` remains after readiness. |
| `CompositionTests.cs` | Redirected process output | Subscribe before launch, await output and process exit, and kill/join the owned child on failure. |
| `RunnerControlTests.cs` helpers | Registry state and process-output readiness | Reuse registry/log subscriptions. Startup is inside the cleanup scope; both service owners are stopped before disposal even after cancellation. |

The health-probe tests use a fresh fake clock per invocation, observe timer registration
before advancement, and await completion after advancement. Cases cover a stalled request,
forward/backward wall-clock adjustments, caller cancellation during a request or retry, and
response disposal. A frozen-clock orchestration case covers a failed prerequisite with its
dependent already waiting. Existing real-process smoke cases cover dependency readiness,
startup failure, cancellation and child cleanup.

## Fixture timestamps

`CollectorApiTestHost` creates a fresh default fake clock at its shared fixed fixture epoch;
callers can still supply a scenario-specific provider. The epoch is also used by
`OtlpTestSpans`, `InstrumentationScopeTests`, `AnalysisApiTests` and its partials,
`ReadPagingTests`, `SavedQueryApiTests`, and `TelemetryMetadataTests`. Telemetry is seeded
inside the requested window rather than relying on wall time moving after insertion.

Direct converter fixtures in `LogEventContractTests`, `PublishedContractV1Tests`,
`SemanticPolicyJsonRpcTests`, `ResourceEntityRefProjectionTests` and
`TelemetryMetadataTests` use fixed receive clocks. The existing fixed-clock test doubles
and the shared timer observer remain explicit controlled inputs.

## Deliberately retained system time

There are 11 remaining direct `DateTime`/`DateTimeOffset` reads, and one explicit
`TimeProvider.System.GetUtcNow()` startup read. Each has the following purpose:

| Source | Reads | Purpose |
| --- | ---: | --- |
| `services/qyl.collector/Program.cs` | 1 | Process startup diagnostic before application composition. |
| `eng/build/BuildRelease.cs` | 1 | UTC timestamp on release progress records; elapsed duration already uses `Stopwatch`. |
| `eng/build/BuildApiSdk.cs`: `NewSessionId` | 1 | PID/time-based smoke-session identity, not a timeout or fixture time-window expectation. |
| `eng/tools/QylToolSmoke/Program.cs` | 2 | Progress-log timestamp and emitted telemetry timestamp for a live external Collector. |
| `eng/tools/QylToolSmoke/CiTelemetry.cs` | 1 | Live smoke-run telemetry timestamps. |
| `eng/tools/CollectorAotSmoke/Program.cs` | 6 | Two live telemetry timestamps and four gRPC absolute UTC deadlines. |

Other retained `TimeProvider.System` arguments are composition defaults in the Collector,
orchestrator and sample; CLI registry timestamps in tests whose assertions concern process
or stream behavior; and the two oversized-payload log ingestion cases in
`OtlpRequestSizeTests`, which reject the body before the observation clock is read.

Real cancellation budgets remain for application startup, analysis execution, owned test
operations, HTTP requests and process shutdown. UI refreshes, workload latency, external
readiness/readback polling, release retry backoff, the conformance export-flush delay, and
the Runner control test's bounded post-stop observation remain real waits. Fake time does
not accelerate a child process, transport or exporter. The changed fixtures stop/join the owner before
disposing dependencies; timer advancement alone is not completion evidence.
