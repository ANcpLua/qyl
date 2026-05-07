# core/specs/generated

This directory holds Weaver-generated TypeSpec files. **Do not edit by hand.**

The generated files are the bridge between upstream OpenTelemetry semantic-convention
keys and qyl TypeSpec models. They let qyl reference pinned upstream attribute keys
as constants instead of scattering raw dotted strings through `.tsp` sources.

## Files

| File | Source | Regenerate via |
| --- | --- | --- |
| `otel-keys.gen.tsp` | `.tools/semconv-upstream/model` (pinned upstream OTel semantic-conventions) + `eng/semconv/templates/registry/typespec/` | `./eng/semconv/run-weaver.sh` |

## What `otel-keys.gen.tsp` provides

One TypeSpec namespace per OpenTelemetry root group, each declaring `const <Name>: string = "<dotted.key>"`. qyl `.tsp` models reference these consts inside `@encodedName(...)` instead of hand-typing dotted attribute keys.

```tsp
@encodedName("application/json", Qyl.OTel.Keys.GenAi.System)
system?: string;
```

Deprecated upstream attributes are emitted with `#deprecated "..."` so models that reference them produce a TypeSpec compiler warning matching upstream's own deprecation notes.

## Toolchain

`./eng/semconv/bootstrap-weaver.sh` and `./eng/semconv/bootstrap-weaver.ps1`
prepare the pinned Weaver binary in `.tools/weaver/` and require the pinned
upstream semantic-conventions submodule at `.tools/semconv-upstream`.

`./eng/semconv/run-weaver.sh` performs generation after bootstrap. For this
directory, it runs the `typespec` template target against `.tools/semconv-upstream/model`
and writes `core/specs/generated/otel-keys.gen.tsp`.

## Pin

`semconv_version: "1.41.0"` (set in `eng/semconv/templates/registry/typespec/weaver.yaml` and `eng/semconv/templates/registry/qyl/weaver.yaml`).

Bumping the pin requires updating both files plus the submodule at `.tools/semconv-upstream` — see `eng/semconv/bootstrap-weaver.sh` for the full procedure.
