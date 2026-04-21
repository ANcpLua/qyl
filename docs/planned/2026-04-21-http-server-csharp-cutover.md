# Execution Mandate — `@typespec/http-server-csharp` alpha cutover

Follow-up to Commit 7 of the TypeSpec-native-emitters mandate. Commit 7 intended to swap
`services/qyl.collector/`'s HTTP surface from hand-written minimal-APIs to controllers
emitted by `@typespec/http-server-csharp@0.58.0-alpha.27`. Investigation during the
2026-04-21 session surfaced two blocking issues; the emitter stays installed but
deactivated until both are resolved.

## Blocker 1 — alpha emitter produces syntactically invalid C#

With `output-type: models` and a `models-only` emit:

```
services/qyl.collector/Generated/generated/models/ServiceInfo.cs(32,98):
  error CS1009: Unrecognized escape sequence
```

The emitter appears to interpolate JSON-Schema `pattern` strings into C# string literals
without escaping backslashes. 108 CS errors across multiple models; not fixable via
post-emit sed without understanding the exact lowering rule.

**Resolution path**: file a repro against `microsoft/typespec`, pin to the first alpha
that fixes it, then re-run Commit 7.

## Blocker 2 — model-namespace collision with `@qyl/typespec-emit-csharp`

Both emitters honor the TypeSpec-declared `@service` namespace. With both active, the
same type gets emitted into the same namespace from two different emitters:

| Type | `@qyl/typespec-emit-csharp` | `@typespec/http-server-csharp` |
|------|-----------------------------|--------------------------------|
| `Qyl.OTel.Traces.SpanEvent` | `public sealed class SpanEvent` (required init) | `public partial class SpanEvent` (mutable + JsonPropertyName) |
| `Qyl.Common.Attribute` | same shape, different mutability | duplicate definition |

The emitter offers no `namespace-override` option; the namespace is driven by the
TypeSpec source. Compile fails with `CS0101: duplicate definition`.

**Resolution path** — pick one:

- **(A)** Drop `@qyl/typespec-emit-csharp` entirely. Let `@typespec/http-server-csharp`
  own both models and controllers. Every `services/*` and `internal/*` reference that
  currently imports from `Qyl.Contracts.*` switches to `Qyl.Collector.Generated.*`.
  Largest blast radius, cleanest end state.
- **(B)** Fork `@typespec/http-server-csharp` into `@qyl/typespec-emit-server-csharp`
  that emits only controllers (delegates model types to our existing emitter).
  Medium complexity; keeps `packages/Qyl.Contracts` as the published truth for
  third-party consumers.
- **(C)** Keep minimal-APIs forever. Accept that the REST surface is hand-wired and
  TypeSpec only drives client SDKs + the lint library.

Option (A) matches the mandate's "no parallel copies" invariant the closest but
requires the full 173-endpoint migration + a Qyl.Contracts deprecation strategy.

## Architectural note — `services/qyl.collector/` has no MVC controllers

The mandate's Commit 7 says:

> For every emitted `interface I<Name>Controller`, create a partial class implementation
> under `services/qyl.collector/Controllers/Impl/`.

The current collector does NOT use `Controllers/` — all 173 endpoints are registered via
Minimal APIs (`app.MapGet(...)` / `app.MapPost(...)`) across 33 `*Endpoints.cs` files.
Any variant of Commit 7 is therefore a minimal-API → MVC-controller migration, not a
drop-in controller replacement.

## OTLP carveout — stays hand-written forever

Regardless of which resolution path lands:

- `services/qyl.collector/Grpc/` (OTLP gRPC receiver on :4317) stays hand-written. The
  wire protocol is `opentelemetry-proto`, owned by OTel upstream, never modeled in our
  TypeSpec.
- OTLP HTTP `/v1/{traces,metrics,logs}` endpoints stay hand-written. Same reason.

Anything in `services/qyl.collector/` whose TypeSpec model does NOT exist in
`core/specs/**/*.tsp` stays hand-written. That set is larger than a single grep can
enumerate — do the audit during the cutover, not before.

## Current state

- `@typespec/http-server-csharp@0.58.0-alpha.27` installed as devDep in
  `core/specs/package.json`.
- `tspconfig.yaml` lists it in a commented-out block under `emit:` with a pointer back
  to this doc.
- No `services/qyl.collector/Generated/` output exists; the staging tree from the
  2026-04-21 probe was deleted.
- `packages/Qyl.Client/DEFERRED.md` — deleted; Qyl.Client now builds and packs clean
  against the sibling `@typespec/http-client-csharp` alpha with post-emit patches.

## When to execute

Re-engage when either:

- An alpha ≥ `0.58.0-alpha.30` fixes Blocker 1 AND a namespace-override option
  resolves Blocker 2, OR
- The user decides between resolution paths A/B/C above.
