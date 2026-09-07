# Changelog

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
  request header's `session.id` member onto the server span, and the session
  processor carries it to every span the request causes; the agent then reads its
  own run back through `list_sessions` / `get_trace`. Zero lines in the API.
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
