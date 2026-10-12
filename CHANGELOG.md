# Changelog

## 12.0.2 — 2026-10-12

- Keep historical plain-text log bodies readable through the log API and live stream.
  Current structured bodies still use the generated contract decoder; older text no longer
  makes a whole result page fail with HTTP 500. Stored telemetry is not rewritten.

## 12.0.1 — 2026-10-12

- Capture the generated `browser.web_vital.*` log attributes and `url.path` so canonical
  browser Web Vitals events retain their measurements, identity, navigation and route.
  Keep legacy browser attributes readable and preserve the existing privacy denials.
- Publish and index runtime packages before the tool pointer, preventing fresh installs
  from racing an unindexed runtime dependency.

## 12.0.0 — 2026-10-12

- Adopt `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 24.0.1 together with
  `Qyl.Telemetry.SemanticConventions*` 9.9.0. The shared qyl registry now generates C# and
  TypeScript definitions; MCP consumes the same vocabulary in 7.6.1. Refresh the generated
  collector attribute catalog without changing its normalization or privacy policy.

- Breaking configuration change: every `QYL_OTLP_PROJECT_KEYS` entry must be an object with
  `key` and a nonempty `roles` array. Plain strings and implicit roles are removed. Allowed
  roles are `ingest`, `read`, `write` and `delete`; invalid or duplicate roles, duplicate object
  properties, and reused credentials across projects fail startup. Existing explicit credentials
  keep their configured permissions. See `docs/configuration.md` for the final format.
- Update DuckDB, protobuf and build/test dependencies to their current compatible releases.
- Keep the running build tool and its dependencies intact during `Clean`, so later verification
  can load Roslyn and NuGet assemblies.

## 11.1.0 — 2026-10-11

- `qyl up`: the runner process observes itself. `AddQyl()` from `Qyl.Telemetry.Hosting` runs in the
  runner (service `qyl-runner`), so its health-probe HTTP calls, logs and meters are exported, and only
  to the diagnostics collector on `:5200`, the dead end the product collector's self-telemetry already
  uses. The product collector on `:5100` never receives runner telemetry; without a diagnostics
  collector nothing is exported. The packaging smoke reads the runner's spans back from the
  diagnostics collector and checks the product collector has none.

## 11.0.0 — 2026-10-11

- `Qyl.Api.Contracts` and `@ancplua/qyl-api-schema` 11.6.0 → 12.0.0 (contract revision
  `sha256:b29439eba28e752b`). The collector and the `qyl` runner serve the Contract 12.0.0 surface,
  which is breaking for API clients:
  - Every problem body is RFC 9457 plus the `code` extension: `type` is one of the ten
    `https://qyl.at/problems/<name>` URIs and `code` its stable string (`validation_failed`,
    `not_found`, …). `about:blank`, `error_code`, `trace_id`, `request_id` and `timestamp` are gone
    from problem bodies; the collector's 500 still carries `X-Trace-Id` as a header. The former
    `error_code` (`collector.unhandled_exception`, `runner.action_failed`) is the `detail`.
  - One page limit on every list route and on `SavedQueryRunRequest.limit`: 1 to 1000, 100 when
    omitted. Logs no longer accept 10000, metrics default to 100 instead of 200, and
    `GET /api/v1/traces/{trace_id}/spans` pages by 100 when `limit` is omitted instead of serving
    the whole trace; follow `next_cursor` for the rest. `series_limit` on the metric query keeps
    1 to 500, 50 by default.
  - `next_cursor` is present exactly when `has_more` is true; the last page omits it.
- Collector image: the base is `runtime-deps:10.0` again, the runtime the `net10.0` collector is
  published for; Renovate #658 had moved it to 11.0 on its own. The major of that image no longer
  gets proposed; it moves with the TargetFramework (#674).

## 10.0.0 — 2026-10-11

- `qyl` runner: a request still in flight when the runner shuts down, or accepted while it drains,
  is answered `503` `application/problem+json` with `reason: runner.shutting_down` instead of an
  empty `200`. Shutdown cancels the handlers, waits up to 2 seconds for them to answer, and only
  then stops the listener (QYL-F1).
- Breaking, `Qyl.Api.Sdk`: names that still used `Qyl.Sdk` (now retired) say `Qyl.Api.Sdk`
  instead, and the old names are gone with no aliases. The properties are `QylApiSdkRoot`,
  `QylApiSdkSourcesPath`, `QylApiSdkXmlProject`, `QylApiSdkApiProject`,
  `QylApiSdkXmlGeneratorProject` and `QylApiSdkPackaged` (previously `QylSdk*`). The files are
  `Build/Qyl.Api.Sdk.props`, `Build/Qyl.Api.Sdk.targets` and `Build/Qyl.Api.Sdk.Packages.props`
  (previously `Build/Qyl.Sdk.Api.*` and `Build/Qyl.Sdk.Packages.props`). The generated class is
  `Qyl.QylApiSdkBuild` (previously `QylSdkBuild`), the target is `QylGenerateApiSdkBuildInfo`
  (previously `QylGenerateSdkBuildInfo`), and the generator is `Qyl.Api.Sdk.Xml.Generator`
  (previously `Qyl.Sdk.Xml.Generator`). `QylOpenApiVersion`, `QylOpenApiDocumentsDirectory`,
  `QylOpenApiDocumentFile`, `UsingQylApiSdk`, the diagnostic IDs, the telemetry names and the
  contract are unchanged. The naming rule is in `packages/Qyl.Api.Sdk/README.md`. To migrate,
  a repository with central package management imports `Build/Qyl.Api.Sdk.Packages.props`. Code
  that reads `QylSdkBuild` now reads `QylApiSdkBuild`. A project that sets `QylSdkPackaged`
  sets `QylApiSdkPackaged` instead, because the SDK ignores the old name and gives no error.
- `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 23.1.0 → 24.0.0, the release
  that renames `QylSdkOptions` to `QylTelemetryOptions`, `QylSdkHostApplicationBuilderExtensions` to
  `QylHostApplicationBuilderExtensions` and `EnableSessionPropagation` to
  `EnableSessionTagInheritance`. qyl uses none of the renamed names; the pin, the `README.md` row and
  the `Build/Qyl.Api.Sdk.Packages.props` an API consumer imports move with it.
- Collector: an OTLP export whose `string` field holds an invalid UTF-8 sequence is accepted, the
  sequence decoded as U+FFFD, on OTLP/HTTP protobuf, OTLP/gRPC and OTLP/JSON (opentelemetry-proto
  1.11.1 "UTF-8 String Handling"); an OTLP/JSON escape of an unpaired surrogate decodes the same
  way. Before, each path refused the whole export (OTLP-1).
- Collector: an OTLP/HTTP body over the 16 MiB decompressed bound answers `413 Content Too Large`
  with the OTLP status envelope in the request's encoding instead of `400` (OTLP 1.11 request size
  limits; the gRPC half shipped in 9.2.0) (OTLP-2).
- Collector: the vendored OTLP definitions move from opentelemetry-proto v1.8.0 to v1.11.1. The
  Alpha profiles-only `AnyValue.string_value_strindex` converts to an absent value and
  `KeyValue.key_strindex` is ignored, as both were while unknown fields (OTLP-3).
- Collector: `jsonrpc.*` span, log and metric attributes are stored (`jsonrpc.protocol.version`
  and `jsonrpc.request.id`, into which the deprecated `rpc.jsonrpc.request_id` is renamed); they
  were dropped as unknown keys (QYL-U1).

## 9.2.0 — 2026-10-11

- `Qyl.Api.Contracts` and `@ancplua/qyl-api-schema` 11.5.0 → 11.6.0 (contract revision
  `sha256:d926c3c6ab829cbf`). The collector serves the Contract 11.6.0 surface:
  - Every paged list (traces, a trace's spans, sessions, a session's traces, logs, metrics,
    metric series, annotations, saved queries) takes `cursor` and returns `next_cursor` from a
    keyset position and a `limit + 1` read. Cursors are opaque and per list; the sessions list no
    longer takes an offset and no longer serves `prev_cursor`.
  - `GET /api/v1/traces` filters whole traces server-side by `start_time` (inclusive) and
    `end_time` (exclusive) on the trace start, `service_name` and `errors_only`.
  - `GET /api/v1/sessions/{session_id}/traces` is bounded (`limit` 100, at most 1000);
    `GET /api/v1/traces/{trace_id}/spans` takes `limit` (at most 10000) and stays whole without it.
  - Annotations (`/api/v1/annotations`): a note of at most 2000 characters on a trace, span or
    session of the caller's project, with create, list, get, update and delete.
  - Saved queries (`/api/v1/saved-queries`): named trace, log or metric filters, unique per
    project, with create, list, get, update, delete and `:run`.
  - The write base: `Idempotency-Key` on creating POSTs (replayed for 24 hours, `409` for another
    body), `ETag` and `Location` on create, `If-Match` on PATCH and DELETE (`428` when missing,
    `412` when stale).
- Collector keys carry roles `ingest`, `read`, `write` and `delete`, enforced per route; a
  credential configured as a string keeps `ingest` and `read` (`docs/configuration.md`).
- Storage: the three new tables are created in place on a database written by 9.1.0, whose
  stored schema identity equals the identity of its telemetry tables alone; every other schema
  difference still fails closed while data exists. Retention expires annotations with the
  telemetry window, counted from their last change, and idempotency keys after 24 hours.

- Collector: gRPC request messages are decompressed into a buffer bounded by
  `MaxReceiveMessageSize` (4 MiB by default); a message that expands beyond it fails with
  `ResourceExhausted` before the API-key interceptor runs. Before, only the compressed size was
  bounded.

## 9.1.0 — 2026-10-11

- `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 23.0.0 → 23.1.0;
  `Qyl.Api.Sdk` pins `Qyl.Telemetry.Hosting`, so an API built with this SDK moves with it.
- `Qyl.Telemetry.SemanticConventions*` 9.7.0 → 9.8.0.
- `ANcpLua.Roslyn.Utilities.Sources` 2.2.46 → 2.2.47, the version `global.json` already pins
  for the `ANcpLua.Roslyn.Utilities` MSBuild SDK of the same release line.
- `Qyl.Api.Sdk`: an unknown `QylOpenApiVersion` fails the build with `QYLSDK0003`; the
  `--openapi-version` argument is appended only when `OpenApiGenerateDocumentsOptions` does not
  already carry one; the `QylEmitOpenApiDocument` property is gone (setting it to `false` only
  made `AddQylApi` throw at start-up).
- Collector: the logs, metrics and metric-series lists compute `has_more` from a `limit + 1`
  read, as the sessions list does, so a page that exactly fills the limit no longer reports
  more; an unknown `/api/*` route answers `404` as `application/problem+json`.
- The `qyl` tool no longer references `OpenTelemetry` or
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`, which it never used.
- Build: the Versionize `Release` and `Changelog` targets are gone; a release is the
  `v<QylVersion>` tag. `RenderReadmeVersions` is listed. README and `docs/configuration.md`
  changes run the backend lane on CI. `mise.toml` pins Node 24.21.0 instead of the `24` line.
- The collector container image runs on `mcr.microsoft.com/dotnet/runtime-deps:11.0`, not
  `10.0` (#658).
- Repository: `AGENTS.md` carries the release ripple across the qyl-dev repos (#660, #662);
  the README's `qyl-mcp-server` row says 7.2.3 and its build section runs through `mise` and
  `./eng/build.sh`; `SECURITY.md` patches the latest release line; `docs/dagger.md` is gone;
  `github/codeql-action` 4.38.3 (#657).

## 9.0.0 — 2026-10-10

- `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 22.0.0 → 23.0.0,
  the line on which every instrumented library is at its newest stable release:
  Microsoft.Data.SqlClient 7.1.1, MongoDB.Driver 3.12.0, Quartz 4.4.0,
  StackExchange.Redis 3.4.0, Confluent.Kafka 2.16.0, Grpc.Net.Client 2.84.0,
  Elastic.Clients.Elasticsearch 9.5.3, Azure.Core 1.63.0, ODP.NET 23.26.301,
  MassTransit 8.5.11. `Qyl.Api.Sdk` pins `Qyl.Telemetry.Hosting` for every API it
  builds, so an API built with this SDK moves with it.
- `Qyl.Telemetry.SemanticConventions*` 9.6.0 → 9.7.0, whose vendor models were
  re-read at exactly those releases. Quartz 4.4.0 writes three new keys,
  `quartz.job.result`, `quartz.jobstore.lock` and
  `quartz.jobstore.trigger.fired_on_acquire`; the collector passes them through
  as vendor keys, through the package's `AttributeMapping.IsVendorPassThrough`,
  so the generated catalog itself is unchanged. The schema URL the collector
  advertises is
  `https://qyl.at/schemas/9.7.0`.
- `Qyl.Api.Contracts` 11.3.0 → 11.5.0: TypeSpec 1.17 and contract revision
  `sha256:3605ae27d2edf632` arrive with 11.4.0 (#651, #653); 11.5.0 changes only
  qyl-api-schema's toolchain.

## 8.0.0 — 2026-10-10

- `Qyl.Telemetry.SemanticConventions*` 9.4.0 → 9.6.0. Upstream
  semantic-conventions-genai dropped `gen_ai.client.token.usage` and
  `gen_ai.token.type` for `gen_ai.client.inference.usage.*` and
  `gen_ai.token.modality`; Microsoft.Extensions.AI 10.10.0 and Agent Framework
  1.24.0 still emit the old pair, so 9.6.0 declares them as vendor rows, together
  with the sixteen `workflow.*`, `executor.*`, `message.*` and `edge_group.*` keys
  the workflow runtime writes. The regenerated collector catalog passes all of
  them through instead of counting them as dropped; `executor.input`,
  `executor.output` and `message.content` join `deniedExactKeys` beside
  `gen_ai.input.messages`, because they are the message bodies. The workflow
  runtime's `message.type` stays dropped: it is upstream's obsoleted RPC key, and
  the registry cannot redeclare a key its catalog has. The schema URL the
  collector advertises is `https://qyl.at/schemas/9.6.0`.
- `Qyl.Telemetry.Hosting` and `Qyl.Telemetry.AutoInstrumentation*` 21.1.0 →
  22.0.0, the line built on that registry, Microsoft.Agents.AI 1.24.0,
  Microsoft.Extensions.AI 10.10.0 and OpenTelemetry 1.19.1. `Qyl.Api.Sdk` pins
  `Qyl.Telemetry.Hosting` for every API it builds, so an API built with this SDK
  moves with it.
- Platform pins: the .NET runtime packages 10.0.12, Microsoft.Extensions 10.10.0,
  OpenTelemetry 1.19.1.
- The README's `qyl-mcp-server` row says 7.1.1, the version npm has had since the
  2026-10-07 wave; it said 7.0.0.

## 7.2.0 — 2026-09-30

- `qyl.dashboard.esproj` restores, builds and tests with Bun
  (`bun install --frozen-lockfile`, `bun run build`, `bun run test`), the same
  commands Fallout and CI run. `bun.lock` is the only lockfile; npm is no
  longer part of the build.

## 7.1.0 — 2026-09-17

- `Qyl.Api.Contracts` 11.0.1 → 11.2.0. Series and resource identity are the
  contract's `AttributeValue.ToStableString()`, the same string the TypeScript
  runtime computes; the collector's own `ToIdentityString` and its
  format are gone. Stored series ids change with it (the database is
  exchanged, not migrated).

## 7.0.0 — 2026-09-17

- `Qyl.Api.Contracts` 10.0.1 → 11.0.1 (revision `sha256:5c1aa27ccdf48067`).
  Attribute values and log bodies are the contract's `AttributeValue` on the
  API, not `object?`: the collector's 200-line hand parser and its own tagged
  encoder are gone, and storage writes the same bytes through the contract's
  converter. A log body is persisted as its value, not flattened to text at
  ingest; a plain string body is a bare JSON string on the wire, and the three
  `LogBody` shapes the collector never produced no longer exist.
- The dashboard decodes attribute values through
  `@ancplua/qyl-api-schema/runtime` instead of a local copy.

## 6.0.0 — 2026-09-12

- `Qyl.Api.Sdk`: `Sdk/Sdk.props` sets `UsingQylApiSdk=true`; neither entry
  point carries a comment or a version any more.

## 5.1.0 — 2026-09-11

- The producer pins move with their lines: `Qyl.Telemetry.Hosting` and
  `Qyl.Telemetry.AutoInstrumentation*` 21.0.1 → 21.1.0,
  `Qyl.Telemetry.SemanticConventions*` 9.3.0 → 9.4.0, `Qyl.Api.Contracts` and
  `@ancplua/qyl-api-schema` 10.0.0 → 10.0.1. The contract's revision changes
  with 10.0.1 (`sha256:264a9e4f1d70289a` → `sha256:cd69d9c37a41916c`), so the
  collector's health response now advertises the new revision and
  `qyl-mcp-server` 5.1.0 is the line that handshakes with it; an older server
  refuses this collector with a legible `-32022`. `Qyl.Api.Sdk` pins
  `Qyl.Telemetry.Hosting` for every API it builds, so an API built with this
  SDK moves to 21.1.0, whose semantic-convention schema URL resolves at
  `https://qyl.at/schemas/9.4.0`.
- The publish workflow waits up to 30 minutes for nuget.org to index the
  release set, not 15; both 5.0.x releases needed a manual re-run because
  `qyl.win-x64` validated for longer than the old deadline.

## 5.0.2 — 2026-09-11

- `qyl` and `Qyl.Api.Sdk` ship their own READMEs. Both packages, and the six
  `qyl.<rid>` packages behind the tool, carried the whole repository README as
  their nuget.org readme. `packages/Qyl.Cli/README.md` and
  `packages/Qyl.Api.Sdk/README.md` are consumer pages now: what the package is,
  the install line, the first call, and links out. `Directory.Build.targets`
  packs `$(MSBuildProjectDirectory)/README.md`. No code change.
- The dashboard is built with Bun (`bun.lock`, `packageManager: bun@1.4.2`);
  every CI job and the collector Dockerfile follow.

## 5.0.1 — 2026-09-11

- `Qyl.Api.Sdk` 5.0.0 could not build a project that had no committed OpenAPI
  document: the document tool ran the API's host, `AddQylApi` threw on the empty
  contract revision, the document was never written, and every rebuild repeated
  it. The first build of a fresh project now writes the document and fails with
  `QYLSDK0001` asking for a commit; the second build succeeds. The publish
  workflow proves this against the published package (#611).
- The producer pins move to what the registries hold: `Qyl.Telemetry.Hosting`
  and `Qyl.Telemetry.AutoInstrumentation*` from 14.1.0 to 21.0.1,
  `Qyl.Telemetry.SemanticConventions*` from 9.2.0 to 9.3.0. `Qyl.Api.Sdk` pins
  `Qyl.Telemetry.Hosting` for every API it builds, so an API built with this SDK
  version moves to the 21.0.1 line; see the
  [AutoInstrumentation changelog](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation/blob/main/CHANGELOG.md)
  for what changed between those majors. The pins had trailed for no reason
  other than nobody moving them; the README no longer claims otherwise.
- The build SDK is 10.0.401.

## 5.0.0 — 2026-09-07

Breaking. Semantic conventions move to the Weaver-only architecture, the
`qyl.sample` repository is fused into this one, and `AddQylApi` moves from
`IServiceCollection` to `IHostApplicationBuilder` — a signature every Qyl API calls,
which is what makes this line 5.0.0 rather than 4.1.0.

### Semantic conventions

- `Qyl.Telemetry.SemanticConventions` and `.Incubating` are pinned at 9.2.0, the
  Weaver-generated line, whose 9.2.0 release adds `qyl.api.contract.revision` for
  the Qyl.Api.Sdk fusion below. The constants ship
  pre-built; nothing is generated at this repository's compile time any more.
- `Qyl.Telemetry.SemanticConventions.SourceGeneration` is retired upstream and its
  reference is gone. `Qyl.Run.Workload` sets its attributes through the
  pre-generated `Activities` classes instead of local `[SemanticConvention*]`
  marker types, and `packages/Qyl.Run.Workload/SemConv.cs` is deleted.
- The collector normalizes at ingest: `AttributeMapping.TryGetRename` rewrites a
  deprecated key to its live key before the allowlist check, vendor keys pass
  through unchanged, and every dropped key increments
  `qyl.collector.attributes.dropped` with the namespace it came from. Entries the
  mapping now covers are gone from `eng/config/collector-semantic-policy.json`.
- A vendor pass-through key is exempt from the denied-substring rule, which the
  vendor keys never opted into by name: twelve of the ninety-seven pinned vendor
  keys spell `message` or `result` (`messaging.masstransit.message_id`,
  `nservicebus.message_intent`, `execution.result` and the rest) and were dropped
  before they reached the vendor check. The header, `enduser.` and `user.`
  prefixes and the exact denials are privacy rules and still run ahead of the
  vendor check, for a vendor key exactly as for any other.
- A gate rejects a `qyl.` or `messaging.system` string literal anywhere in
  `services/qyl.collector` outside generated code.

### Qyl.Api.Sdk

- `Qyl.Api.Sdk` is a new package published from this repository at the repository
  version: an MSBuild SDK for Native AOT ASP.NET Core APIs with compile-time
  validation, `[GenerateXml]` XML output described in the OpenAPI contract, a
  committed OpenAPI document, and telemetry, all behind `AddQylApi()`.
- Breaking: `AddQylApi` extends `IHostApplicationBuilder`, not `IServiceCollection`;
  the `IServiceCollection` overload is deleted. `builder.Services.AddQylApi(ctx)`
  becomes `builder.AddQylApi(ctx)`.
- A Qyl API observes itself. `AddQylApi` ends in `AddQyl()` from
  `Qyl.Telemetry.Hosting`, which the SDK pins at `QylTelemetryVersion` and pulls
  implicitly with its interceptor generator. Telemetry is as mandatory as
  validation: no `WithTelemetry`, no opt-out. The one exception is the build-time
  OpenAPI document tool, where collector discovery is off — it builds the host and
  never starts it.
- The producer family is pinned at 14.1.0, where the qyl ASP.NET Core middleware no
  longer opens a server span of its own: the `Microsoft.AspNetCore` hosting activity is
  the one `SERVER` span per request, enriched in place with `http.request.method`,
  `url.*`, `http.route`, `http.response.status_code`, `error.type`,
  `qyl.instrumentation.domain` and the name `{method} {route}`. A request is one span
  again instead of two, and the session stage asserts exactly that.
- The sample's todo endpoints are mapped at their full paths instead of through
  `MapGroup("/todos")`: a group's collection endpoint has the route template `/todos/`,
  while the contract spells it `/todos`, and the span and the document have to name an
  endpoint the same way. The committed document is unchanged.
- An agent is a Qyl API's first consumer. `Qyl.Api` stamps the W3C `baggage`
  request header's `session.id` member onto the ASP.NET Core server span, and the
  session processor carries it to every span the request causes; the agent then
  reads its own run back through `list_sessions` / `get_trace`. Zero lines in the
  API. The member is admitted by an allow-list —
  `^(?!\.+$)[A-Za-z0-9._~-]{1,128}$` — because it becomes a storage key and a path
  segment; anything else is ignored. The header itself is parsed by ASP.NET Core,
  not by qyl, which is a behaviour change against reading the raw header: an
  application that installs `DistributedContextPropagator.CreateNoOutputPropagator`
  extracts no baggage, and is therefore no longer tagged.
- Breaking, sample only: the todo endpoints are mapped at their full paths instead
  of through `MapGroup("/todos")`, so `http.route` is `/todos` — the path the
  contract spells — rather than `/todos/`.
- The SDK enforces contract-is-committed for every consumer: `QYLSDK0001` fails the
  build when the regenerated OpenAPI document differs from the revision compiled
  into it, naming the file to commit. `AddQylApi` refuses to run without a revision.
- Every span names its contract: the SHA-256 of the committed OpenAPI document is
  written into the compilation by `Qyl.Sdk.Api.targets` and exported as the
  resource attribute `qyl.api.contract.revision` — the registry's name, taken from
  `QylAttributes.ApiContractRevision`, in the registry's value format `sha256:<hex>`,
  which the collector persists. The collector's
  `qylResourceAttributeAllowList` is now resolved against the pinned packages like
  every other list in the generated catalog.
- `samples/qyl.sample` is the API it is proven against, still with no telemetry
  line of its own. The nine checks — the eight that were the sample repository's
  `verify.sh` plus the session stage, which runs a real collector and asserts an
  agent's session through its read API — are Nuke targets under `eng/build` and run
  from `Ci`.

### Removed

- `ARCHITECTURE-1.0.0.md`. The 2026-09-07 workspace decision is the architecture
  of record; the document's §10 gap list moved there verbatim before deletion.
