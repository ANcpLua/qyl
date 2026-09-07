# Changelog

## 4.0.0 — 2026-09-07

Breaking. Semantic conventions move to the Weaver-only architecture, and the
`qyl.sample` repository is fused into this one.

### Semantic conventions

- `Qyl.Telemetry.SemanticConventions` and `.Incubating` are pinned at 9.1.0, the
  release generated entirely by Weaver from registry YAML. The constants ship
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
  validation, `[GenerateXml]` XML output described in the OpenAPI contract, and a
  committed OpenAPI document, all behind `AddQylApi()`.
- `samples/qyl.sample` is the API it is proven against. The eight checks that were
  the sample repository's `verify.sh` are Nuke targets under `eng/build` and run
  from `Ci`.

### Removed

- `ARCHITECTURE-1.0.0.md`. The 2026-09-07 workspace decision is the architecture
  of record; the document's §10 gap list moved there verbatim before deletion.
