# Changelog

## Unreleased

- Construct the orchestration smoke's hosted service explicitly so CI analysis recognizes its activation.

## 15.0.3 — 2026-10-17

- Consume AutoInstrumentation and Hosting 28.0.0 with generated-code ABI V28, and SemanticConventions 13.1.2.
- Supply telemetry 28.0.0 through the API SDK's consumer package pins. The Collector catalog and analysis metadata match SemanticConventions 13.1.2 without generated-code changes.

## 15.0.2 — 2026-10-17

- Stop dependent processes before their `WaitFor` prerequisites during application shutdown. Final exports can reach a live destination instead of racing its termination.
- Verify final delivery between real processes in the orchestration consumer, including its Native AOT build.
- Consume API contracts 14.0.1 in the Collector and dashboard. The contract revision is unchanged.

## 15.0.1 — 2026-10-17

- Preserve metric resource, scope and point-attribute identity before persistence filtering. Opaque digests keep separate service instances and denied dimensions distinct without storing their original values; historical series stay separate because erased identity cannot be reconstructed.
- Let supervised Unix services finish hosted-service shutdown before terminating them. This preserves final telemetry exports during disposal, stop and restart. Fall back to terminating the process tree after a bounded grace period. Windows requests window closure where available; headless Windows processes still require forced termination.
- Verify asynchronous child cleanup through the real packaged orchestration consumer. Keep one supervisor responsible for process shutdown, including control requests.
- Wait for NuGet registration metadata as well as package-content indexes before installing the published tool, preventing a stale-registration failure after a successful upload.

## 15.0.0 — 2026-10-17

- Accept native typed application attributes on spans, events, links, logs, metrics and resources. Remove registry allowlists and the forced `qyl.app` prefix's scalar, key-count and length restrictions. Keep original keys and values; existing explicit persistence-policy denials remain visible through the dropped-attribute metric.
- Preserve custom metric dimensions in series identity instead of collapsing them into one series. Verify the values after reopening the database.
- Coalesce identical typed duplicate attributes from logging state and scopes, retaining native trace correlation. Report this normalization through `qyl.collector.attributes.coalesced`; reject conflicting duplicates explicitly and preserve valid sibling records.
- Bound gzip HTTP bodies on both encoded and decompressed bytes. Chunked requests without Content-Length return an OTLP 413 when gzip headers consume the wire budget, even when their decoded payload is small.
- Retain sanitized `exception.message` and `exception.stacktrace` across OTLP attributes and metadata. Preserve harmless formatting, redact embedded credentials, apply size bounds after sanitization and report failures without the original value. Keep all unrelated key denials and their precedence.
- Count decoded UTF-8 content against the structured log-body retention budget, so JSON Unicode escaping cannot turn a bounded map into a truncated string.
- Resolve source-package paths through NuGet's generated path property, including fallback folders used by isolated verification caches.
- Consume SemanticConventions 13.1.1 and AutoInstrumentation/Hosting 27.0.0. The API contract remains 14.0.0 because its native attribute representation is unchanged.
- Keep release phase timings and per-package compiler logs with each attempt so a slow or failed step can be diagnosed without rerunning unrelated checks.

## 14.0.0 — 2026-10-16

- Pin semantic-convention bindings and analyzers to 12.0.0 and the instrumentation
  package family to 26.0.0 across the Collector, CLI and API SDK.
- Update the OpenTelemetry SDK and OTLP exporter to 1.20.0 for concurrent metric
  collection, async suppression isolation and HTTP/protobuf shutdown log delivery.
- Freeze Microsoft.Build at 18.9.6 for the .NET 10 build host; 18.10/18.11 target
  .NET 11 and .NET Framework rather than a compatible modern host framework.

- Upgrade the coordinated Extensions packages to 10.11.0, which includes the
  [bounded log-tag enumeration fix](https://github.com/dotnet/extensions/pull/7819).
  Verify enabled request enrichment, classified redaction and original-format ordering.
- Accept the API schema's default point limit on metadata-only metric-series reads;
  require an exact series only for original-point continuations.
- Preserve independent resource and instrumentation-scope metadata, trace flags,
  trace state and dropped counts through ingestion, storage and API responses.
  Keep producer log severity text and record receive time when observation time
  is absent. Separate differently scoped metric streams and report incompatible
  instrument descriptors instead of overwriting them.
- Page metric catalogs by resource, scope and instrument name. Keep the longer
  description without changing identity, and allow exact `series_id` selection
  when a metric name has conflicting descriptors.
- Use observation time to find and display logs without an event timestamp,
  preserving the original fields. Reject metric points without an end timestamp
  through OTLP partial-success accounting and leave unknown start times unknown.
- Retain original metric numbers, native exponential histograms, summaries and full
  ordered exemplars in an authoritative observation journal. Page original points
  through HTTP and the existing `get_metric_series` tool. Aggregate queries disclose
  their float64 representation and reject contradictory measurements. Preserve old
  projections during database upgrades without inventing erased originals.
- Reject malformed spans and logs per item through OTLP partial-success responses.
  Preserve valid records from the same export. Store unknown log event times as
  NULL, use observation time for analysis, and preserve undated historical logs
  during time-based retention. Keep identical untimed records distinct within an export.
- Continue legacy zero-time log cursors without repeating undated records. Treat
  profiling-only key indexes as non-fatal in the other OTLP signals.
- Hash complete original log bodies before applying the existing storage limit, so
  distinct messages with the same retained prefix do not merge. Recover missing
  declared nonunique indexes on open without repeating DuckDB's unsafe combined DDL.
- Run the .NET suites with MSTest 4.5.1 and Microsoft.Testing.Platform 2.5.1.
  Use DotCov 4.7.0 for the existing coverage gates.
- Make internal helpers private, remove unused session calculations and build
  metadata, and align source namespaces with their projects.
- Keep configuration immutable after construction and reuse HTTP clients in the
  network tests. Keep orchestration's retained output readable after failed
  startup or disposal, verified with a real child process.
- Validate repaired protobuf output in Release builds as well as Debug builds.

## 13.2.1 — 2026-10-16

- Query stored logs with cursor pagination in the dashboard. Severity, exact service
  name and message filters apply before paging, including errors older than the
  live stream's recent tail.
- Keep loaded pages during live refresh, expose older pages without a buffer cap,
  and show query and continuation failures with retry. Downloads identify their
  scope as the loaded matching records.
- Exercise older errors, equal timestamps, complete history, filter changes and
  failed-page retries against the real collector in browser tests.
- Remove unused constant aliases, mapping helpers and intermediate record fields;
  correct field and constant names reported by inspections.

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
