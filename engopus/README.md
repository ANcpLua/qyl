# engopus — The 20 Files That Define qyl's Generator Pipeline

Built 2026-04-12 by comparing all 4 Roslyn generators, the nuke pipeline,
and the OTel semconv reference against each other.

## What this is

The minimal set of files required for deterministic, idempotent schema model
generation. If these 20 files are correct, the pipeline produces correct output.
If any of them is wrong, the pipeline silently drifts.

## Layout

```
01-inputs/        Pipeline truth sources (WHAT to generate)
02-routing/       Namespace routing (WHERE outputs go)
03-generators/    Entry points (HOW it orchestrates)
04-models/        The three divergent tool models + unified target
05-emitters/      What actually produces code
06-placeholders/  Files that SHOULD exist but DON'T (created as stubs)
07-breaks/        Intentionally broken files that expose real gaps
```

## The 20 files

| # | File | Status | Purpose |
|---|------|--------|---------|
| 1 | `01-inputs/qyl-extensions.json` | EXISTS | Domain definitions — drives contract + facade generation |
| 2 | `01-inputs/generate-semconv.ts` | EXISTS | Stage 1 — OTel semconv -> 4-language facades |
| 3 | `02-routing/NamespaceRoutingTable.cs` | EXISTS | Single routing table — schema prefix -> C# namespace -> filename |
| 4 | `02-routing/ContractGenerator.cs` | EXISTS | Stage 3 — extensions.json -> DomainContracts.g.cs |
| 5 | `02-routing/SchemaGenerator.cs` | EXISTS | Stage 4 — OpenAPI -> C# models + DuckDB DDL |
| 6 | `03-generators/ToolManifestGenerator.cs` | EXISTS | qyl.mcp entry point — [McpServerToolType] discovery |
| 7 | `03-generators/McpServerGenerator.cs` | EXISTS | Qyl.Agents entry point — [McpServer] discovery |
| 8 | `03-generators/LoomSourceGenerator.cs` | EXISTS | Loom entry point — [LoomTool]/[LoomStep]/[LoomWorkflow] |
| 9 | `03-generators/DuckDbInsertGenerator.cs` | EXISTS | Storage entry point — [DuckDbTable] discovery |
| 10 | `04-models/ToolManifestModels.cs` | EXISTS | MCP generator's tool model (bool hints, no params) |
| 11 | `04-models/ToolModel.cs` | EXISTS | Agents generator's tool model (tri-state hints, full params) |
| 12 | `04-models/InstrumentationModels.cs` | EXISTS | Instrumentation generator's models (vestigial ToolTypeEntry) |
| 13 | `04-models/LoomModels.cs` | EXISTS | Loom's separate model world (duplicated TypeDeclarationModel) |
| 14 | `04-models/UnifiedToolModel.cs` | PLACEHOLDER | The convergence target — what all 3 should become |
| 15 | `05-emitters/ToolManifestEmitter.cs` | EXISTS | MCP emitter — manifest, capabilities, DI registration |
| 16 | `05-emitters/OTelEmitter.cs` | EXISTS | Agents emitter — hardcoded metric string literals |
| 17 | `06-placeholders/LogEmitter.cs` | MISSING | No generator emits LogRecord creation code |
| 18 | `06-placeholders/McpSpanEnricher.cs` | MISSING | mcp.session.id + mcp.protocol.version never emitted |
| 19 | `06-placeholders/MetricCodegenBridge.cs` | MISSING | gen_ai.client.token.usage should be generated, not handwritten |
| 20 | `07-breaks/OTelEmitter.BROKEN.cs` | BROKEN | OTelEmitter with constants instead of string literals — won't compile |

## How to verify

```bash
# Everything in 01-05 should match the real files exactly:
diff engopus/01-inputs/qyl-extensions.json eng/semconv/qyl-extensions.json

# Everything in 06 is a stub that will fail to compile:
dotnet build engopus/06-placeholders/  # expected: CS0246 everywhere

# Everything in 07 is intentionally broken to show what SHOULD change:
dotnet build engopus/07-breaks/  # expected: CS0103 (missing constants)
```

## The pipeline principle

> Fix the generator input, never the output.

If a `.g.cs` file is wrong, fix the TypeSpec/extensions.json/generator — not the output.
These 20 files ARE the inputs. If they're right, `nuke Generate` produces right.
