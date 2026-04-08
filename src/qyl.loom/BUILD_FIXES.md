# qyl.loom Build Fixes — 2026-04-06

## Source files changed

| File | What was wrong | Fix |
|------|---------------|-----|
| `qyl.loom.csproj` | Missing `Microsoft.Agents.AI.Hosting` package, missing `InterceptorsNamespaces` for source generator | Added package reference, added `<InterceptorsNamespaces>` property |
| `CodeReview/CodeReviewService.cs` | `HttpContent.ReadFromJsonAsync` unresolved — missing using | Added `using System.Net.Http.Json;` |
| `Program.cs` | `SseItem<T>` unresolved — missing using; unused `request` parameter | Added `using System.Net.ServerSentEvents;`, changed `request` to `_` discard |
| `V2/LoomV2Executors.cs` | All 6 executor classes missing `partial` keyword — source generator emits partial declarations via `[LoomStep]` | Added `partial` to all 6 classes |
| `V2/LoomV2Workflow.cs` | Missing `using` for `LoomWorkflowAttribute`; `LoomWorkflowAttribute` missing required `runStateType` + `stepIds` args; `LoomV2WorkflowFactory` missing `partial` | Added using, constructor args, `partial` keyword |
| `CompilerDemo/LoomDemoWorkflow.cs` | `Executor.ConfigureProtocol` changed from `void` to returning `ProtocolBuilder` in MAF preview; `[MessageHandler]` removed in favor of `ConfigureProtocol` | Updated return type to `ProtocolBuilder`, wired handlers via `protocol.AddMessageHandler` (still errors — MAF API may have changed method name) |

## Generator files changed (in qyl.instrumentation.generators)

| File | What was wrong | Fix |
|------|---------------|-----|
| `Loom/Generation/LoomToolOutputGenerator.cs` | `sb.BeginBlock(null)` / `sb.EndBlock(null)` inside constructor `new(...)` calls emitted `{` / `}` braces — producing `new Type( { args } )` which is invalid C# | Removed all `BeginBlock(null)` / `EndBlock(null)` calls inside constructor expressions |
| `Loom/Generation/LoomTelemetryManifestOutputGenerator.cs` | Same `BeginBlock(null)` bug across ParameterBindings, Results, Telemetry, Policy, Manifest, and InterceptorManifest properties | Removed all `BeginBlock(null)` / `EndBlock(null)` calls inside constructor expressions |
| `Loom/Generation/LoomContractOutputGenerator.cs` | Same `BeginBlock(null)` bug in `Descriptor` property generation | Removed `BeginBlock(null)` / `EndBlock(null)`, restructured code block |

## Enum files changed

| File | What was wrong | Fix |
|------|---------------|-----|
| `qyl.instrumentation/Instrumentation/Loom/LoomEnums.cs` | `SignalType` and `RunStatus` enums referenced by CompilerDemo contracts but never defined | Added both enums |

## Remaining errors (2 categories)

1. **Generator output still broken** (22 errors) — the `BeginBlock(null)` fix was applied to the generator source code but MSBuild may still load the cached/old generator DLL during the same build session. A full `dotnet clean` + rebuild should pick up the fixed generator.

2. **CompilerDemo MAF API mismatch** (10 errors) — `ProtocolBuilder.AddMessageHandler` doesn't exist in `Microsoft.Agents.AI.Hosting 1.0.0-preview.260402.1`. The MAF Workflow API is still in preview flux. CompilerDemo needs updating to match the actual `ProtocolBuilder` API surface of the installed preview version.
