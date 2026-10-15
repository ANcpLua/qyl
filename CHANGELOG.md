# Changelog

## 13.2.0 — 2026-10-15

- Pin semantic conventions 10.1.0 and `Qyl.Telemetry.Hosting` / `Qyl.Telemetry.AutoInstrumentation*` 25.1.0
  across the Collector, CLI and API SDK. The API contracts stay at 12.1.2.
  The instrumentation-scope test follows the pinned registry schema URL.
- Release verification bypasses stale registry caches and keeps the GitHub release in draft until
  the published tool and orchestration consumer pass. Gate startup reports its live log immediately.
  The standalone orchestration consumer declares its imports independently of the repository SDK.
- Apply JetBrains cleanup across the generator, Collector and tests, including disposal-safe resource
  setup, asynchronous test cleanup and redundant imports and null suppressions.

## 13.1.0 — 2026-10-15

- Add `Qyl.Orchestration`, a reusable .NET process host extracted from the qyl CLI's supervisor.
  Applications can compose executables, pass environment and service references, wait for readiness,
  and stop the whole application. The CLI and library use the same process lifecycle implementation.
- The shared supervisor detects early process exits during health checks and retains the exit code and
  child output. The public host cleans up on failed startup, cancellation and readiness deadlines;
  optional process-ID health responses prevent another listener from satisfying readiness.
- Release checks pack and smoke `Qyl.Orchestration` as a standalone consumer before publication and
  again from nuget.org after indexing. The prototype consumes the package without a sibling clone.

## 13.0.1 — 2026-10-15

- Pin semantic conventions 10.0.1 and `Qyl.Telemetry.Hosting` / `Qyl.Telemetry.AutoInstrumentation*` 25.0.1.
- Collector: the regenerated ingest catalog follows the 10.0.1 registry. A `qyl.app.` key is held to the
  registry's denied keys and prefixes by the part after the prefix (`qyl.app.user.email` is dropped like
  `user.email`). `gen_ai.request.max_tokens` and the `gen_ai.usage.{text,audio,image}.*_tokens` counts are
  kept. `build.error.message`, `nservicebus.originating_machine`, `nservicebus.processing_machine` and
  `db.odp.user.statement` are dropped. Verified on api.qyl.at with a live span.

## 13.0.0 — 2026-10-14

- Pin `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 25.0.0 and semantic conventions
  10.0.0. Their analyzers now reach every project that references `Qyl.Telemetry.Hosting`.
- Collector: the ingest deny list is SemConv's generated `RegistryPolicy`. The catalog generator reads it,
  `eng/config/collector-semantic-policy.json` loses `deniedExactPrefixes`, `deniedExactKeys`,
  `deniedKeyTokens`, `deniedTokenExemptKeys` and `httpHeaderAttributePrefixes` (a denied template key such
  as `http.request.header` denies its children), and generation fails when the file names a key for
  persistence that the registry does not persist. No HTTP header is persisted: the six
  `safeHttpSpanHeaderAttributeKeys` (`http.request.header.mcp_name` among them, which carries the resource
  URIs and prompt names the registry denies) are gone. Token-exempt and vendor keys
  (`RegistryPolicy.VendorKeys`, now in the catalog) match ordinally, as in the registry, so a differently
  cased spelling meets the token rule.
- Collector: `gen_ai.agent.name` is persisted, so `get_agent_run`'s `agent_name` and `list_sessions`
  name the agent; `gen_ai.agent.id` and `gen_ai.agent.description` stay denied. The five
  `gen_ai.usage.*_tokens` counts are token-exempt and are kept as attributes beside their columns.
- Collector: business attributes `qyl.app.<key>` (written by `SetAppTag`) are captured: at most 16 keys
  per span, event and link, each a string of at most 256 characters, a 64-bit integer, a double or a bool;
  no key containing a denied token, and no key whose `<key>` the registry denies (`qyl.app.user.email`,
  `qyl.app.session.id`, `qyl.app.host.name`, `qyl.app.url.full`). A dropped key takes none of the 16, and an
  entity reference cannot carry a `qyl.app.*` key onto a resource.
- Qyl.Api.Sdk: `QylSessionBaggageStartupFilter` is deleted. `AddQyl`'s ASP.NET Core startup filter
  stamps the `baggage` member `session.id` on the server span itself, under `QylApiContract`'s rule, so
  `AddQylApi` registers nothing for it. The contract revision reaches the resource through
  `AddOpenTelemetry().ConfigureResource`, since `QylTelemetryOptions.ResourceAttributes` is gone.
- Collector: the analysis reads only keys ingest keeps. The service map names an outbound target by
  `service.peer.name` (ingest renames the deprecated `peer.service` to it), then `server.address`, then the
  database or messaging system; a call whose callee reports its own span counts once, through that span,
  and such a callee stays a service node. A failure's message is the span's status description
  (`exception.message` is denied on spans, events and logs). The catalog drops the `PeerService` and
  `ExceptionMessage` constants and gains `ServicePeerName`.
- Collector: an unhandled or unobserved exception with no current span is recorded on a
  `Spans.Work("UnhandledException")` span that now ends, so it is exported; the old fallback span on
  its own `Qyl.Instrumentation.ErrorCapture` source was never stopped. The collector's unused
  `ActivitySource` and the `telemetry.schema_url` resource attribute (never persisted) are gone.
- Build: one lowercase `artifacts/` folder (Fallout, `release.sh` and MSBuild agreed only on
  case-insensitive file systems).
- Collector: a metric range query with a percentile aggregation (`p50`, `p90`, `p95`, `p99`)
  on a gauge or sum answers 400 `aggregation.requires_histogram` on field `aggregation`
  instead of 500; the contract already limits percentiles to histogram kinds.
- Collector: the analysis `query_metric` operation (MCP `query_metric`) rejects a percentile on a gauge or
  sum with 400 `aggregation.requires_histogram` instead of answering with the average labelled as the
  percentile; an aggregation the metric kind does not support answers `aggregation.unsupported`.

## 12.2.1 — 2026-10-14

- Security: .NET runtime packages 10.0.13, System.Security.Cryptography.Pkcs and .Xml pinned to it (High advisories of 2026-10-13).
