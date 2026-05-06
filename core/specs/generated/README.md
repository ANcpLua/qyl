# core/specs/generated

This directory holds Weaver-generated TypeSpec files. **Do not edit by hand.**

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

## Pin

`semconv_version: "1.41.0"` (set in `eng/semconv/templates/registry/typespec/weaver.yaml` and `eng/semconv/templates/registry/qyl/weaver.yaml`).

Bumping the pin requires updating both files plus the submodule at `.tools/semconv-upstream` — see `eng/semconv/bootstrap-weaver.sh` for the full procedure.
