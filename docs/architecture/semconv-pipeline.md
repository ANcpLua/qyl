``# Semantic Conventions Pipeline

How qyl keeps its telemetry attributes in sync with the OpenTelemetry standard.

## What It Does

One generator reads the official OTel semantic conventions and produces typed constants for every layer of qyl — dashboard, collector, storage, SDK, and API schemas. When OTel releases new conventions (e.g. v1.40), we bump one version number and regenerate.

## Pipeline

```
@opentelemetry/semantic-conventions (npm package)
                 │
                 ▼
    eng/semconv/generate-semconv.ts
                 │
       ┌─────────┼──────────┬──────────────┬─────────────┐
       ▼         ▼          ▼              ▼             ▼
   TypeScript    C#      C# UTF-8      TypeSpec       DuckDB
   (Dashboard)  (SDK)   (Hot paths)   (API Schema)  (Storage)
```

## Outputs

| Output | File | Consumer | Purpose |
|--------|------|----------|---------|
| TypeScript | `src/qyl.dashboard/src/lib/semconv.ts` | Dashboard | Attribute keys for UI filters, labels |
| C# | `src/qyl.servicedefaults/.../SemanticConventions.g.cs` | .NET SDK | String constants for instrumentation |
| C# UTF-8 | `src/qyl.servicedefaults/.../SemanticConventions.Utf8.g.cs` | Collector | `ReadOnlySpan<byte>` for zero-allocation OTLP parsing |
| TypeSpec | `core/specs/generated/semconv.g.tsp` | API codegen | Typed models for OpenAPI/JSON schema |
| DuckDB SQL | `src/qyl.collector/Storage/promoted-columns.g.sql` | Collector | Column definitions for promoted attributes |

## How To Run

```bash
# All outputs
cd eng/semconv && npm run generate

# Single output
npm run generate:ts    # TypeScript only
npm run generate:cs    # C# only
npm run generate:utf8  # C# UTF-8 only
npm run generate:tsp   # TypeSpec only
npm run generate:sql   # DuckDB only

# Via NUKE (recommended — runs as part of full pipeline)
nuke Generate --force-generate
```

## How To Update OTel Version

1. Edit `eng/semconv/package.json`:
   ```json
   "@opentelemetry/semantic-conventions": "1.40.0"
   ```
2. `cd eng/semconv && npm install`
3. `npm run generate`
4. Review generated diffs
5. Run `dotnet build` + `dotnet test` to verify compatibility

## Attribute Filtering

Not all OTel attributes are relevant. The generator filters by prefix:

| Category | Prefixes |
|----------|----------|
| AI | `gen_ai`, `code`, `openai`, `azure` |
| Transport | `http`, `rpc`, `messaging`, `url`, `signalr`, `kestrel` |
| Data | `db`, `file`, `vcs`, `artifact`, `elasticsearch` |
| Infrastructure | `cloud`, `container`, `k8s`, `host`, `os`, `faas` |
| Security | `network`, `tls`, `dns` |
| Runtime | `process`, `thread`, `system`, `dotnet`, `aspnetcore` |
| Identity | `user`, `client`, `server`, `service`, `telemetry` |
| Observe | `browser`, `session`, `exception`, `error`, `log`, `otel` |
| Ops | `cicd`, `deployment` |

To add a new prefix: edit `CONFIG.includePrefixes` in `generate-semconv.ts`.

## Adding a New Language

To support a new output language (e.g. Python, Go):

1. Add a new generator function in `generate-semconv.ts`:
   ```typescript
   function generatePython(data: ParsedData): string { ... }
   ```
2. Add output path in `CONFIG.outputs`:
   ```typescript
   python: "../../sdks/python/qyl/semconv.py"
   ```
3. Add CLI flag (`--py-only`) and wire it in `main()`
4. Add npm script in `package.json`:
   ```json
   "generate:py": "tsx generate-semconv.ts --py-only"
   ```

The `ParsedData` structure already contains everything needed — attribute names, values, enum groups, and type hints. Each new language just needs a formatting function.

## Architecture Decisions

- **npm as source**: OTel publishes conventions as an npm package with TypeScript declarations. Parsing `.d.ts` files is more reliable than parsing YAML (which has breaking format changes between versions).
- **Single generator**: One script, five outputs. No drift between layers — if the dashboard knows about `gen_ai.system`, so does the collector, the SDK, and the storage.
- **Compile-time only**: All generated files are constants. No runtime dependency on the generator. The npm package is a dev dependency only.
- **Prefix filtering**: OTel has 500+ attributes. qyl only promotes the ones relevant to its use cases. Adding new domains is one line in the config.
