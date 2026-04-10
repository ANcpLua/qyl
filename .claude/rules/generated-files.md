---
globs: "**/*.g.cs,**/*.g.tsp,**/*.g.sql,**/*.g.ts,**/openapi.yaml"
alwaysApply: true
---

## Generated Files Are Sacred — Never Hand-Edit

Files matching `*.g.cs`, `*.g.tsp`, `*.g.sql`, `*.g.ts`, and `core/openapi/openapi.yaml` are **generated outputs**. They are deterministic, idempotent artifacts of the generation pipeline.

### Absolute Rules

1. **NEVER edit a `.g` file directly.** Your edits WILL be destroyed on next `nuke Generate`. No exceptions.
2. **NEVER claim generated output is "done" without running `nuke Generate`.** The pipeline is the only authority.
3. **If a generated file is wrong, fix the generator input** — not the output. The chain is:
   - TypeSpec (`.tsp`) -> `openapi.yaml` -> C# contracts (`.g.cs`) + DuckDB DDL
   - Semconv upstream -> `generate-semconv.ts` -> `SemanticConventions.g.cs` + `.g.tsp`
   - `[DuckDbTable]` attributes -> Roslyn source generator -> insert/map methods (in obj/, not source-controlled)
4. **If the generator doesn't produce expected output**, the fix is one of:
   - Missing route in `eng/build/NamespaceRoutingTable.cs`
   - Missing import/using in `core/specs/main.tsp` or `core/specs/api/routes.tsp`
   - Missing `x-duckdb-table` or `x-csharp-type` extension on the TypeSpec model
   - Missing prefix in `CONFIG.includePrefixes` in `eng/semconv/generate-semconv.ts`
5. **If you cannot fix the generator pipeline**, say so. Do not hand-write a `.g` file as a workaround. Ask for help from a plugin, skill, or the user.
6. **`nuke Generate` is idempotent.** Anyone can run it anytime. It regenerates everything. Running it is never destructive — it only overwrites generated files.

### How to Verify

After any TypeSpec or generator change:
```
nuke Generate
dotnet build src/qyl.collector/qyl.collector.csproj
```

If `nuke Generate` shows `[UNCHANGED]` for a file you expected to change, either:
- Your TypeSpec model isn't referenced from `routes.tsp` (won't appear in `openapi.yaml`)
- The namespace has no route in `NamespaceRoutingTable.cs` (falls to default bucket)
- The `DuckDbSchema.g.cs` skip guard is active (use `--force-generate`)