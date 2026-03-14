# Qyl.Agents Fat Upgrade — Design Spec

**Date:** 2026-03-12
**Scope:** All 5 Qyl.Agents projects in ANcpLua.Roslyn.Utilities
**Goal:** Fix behavioral bugs, adopt shared utilities, align with OTel GenAI semantic conventions

---

## Semconv Migration Policy

Qyl.Agents is unshipped (all files untracked). No backward compatibility required. The generator starts fresh on the
current experimental GenAI semantic conventions shape. No `OTEL_SEMCONV_STABILITY_OPT_IN` gating for v1 — when the
conventions stabilize, the generator ships a breaking version bump.

---

## Chunk 1: Models & Abstractions

### 1A. ReturnKind enum replaces triple-bool
/resume
**File:** `Qyl.Agents.Generator/Models/ToolModel.cs`

Replace `ReturnsTask`, `ReturnsValueTask`, `ReturnsVoid` booleans with:

```csharp
public enum ReturnKind : byte { Void, Task, ValueTask, TaskOfT, ValueTaskOfT }
```

`ToolModel` carries `ReturnKind ReturnKind` and `string ResultTypeFullyQualified` (empty for Void/Task/ValueTask).
`DispatchEmitter` switches on `ReturnKind` instead of checking three flags.
`JsonContextEmitter` checks `ReturnKind is not (Void or Task or ValueTask)` to decide whether to collect the return
type.

### 1B. Required init on info classes

**Files:** `Qyl.Agents.Abstractions/McpServerInfo.cs`, `McpToolInfo.cs`

`Name` becomes `required init`. Other properties stay `init` (optional). Prevents constructing info objects with empty
defaults.

### 1C. AnalyzerReleases

**Files:** `AnalyzerReleases.Shipped.md`, `AnalyzerReleases.Unshipped.md`

Move QA0001–QA0011 to Shipped under `## Release 1.0`. Remove QA0012 (dead, see 2F). Unshipped becomes empty.

---

## Chunk 2: Extractor Upgrades

### 2A. Replace manual attribute lookups (4 sites)

**Files:** `ServerExtractor.cs`, `ToolExtractor.cs`, `ParameterExtractor.cs`

| Current                                               | Replacement                                                             |
|-------------------------------------------------------|-------------------------------------------------------------------------|
| `ServerExtractor.GetMcpServerAttribute` (manual loop) | `symbol.GetAttribute(McpServerAttributeName)`                           |
| `ServerExtractor.HasToolAttribute` (manual loop)      | `method.HasAttribute(ToolAttributeName)`                                |
| `ToolExtractor.GetToolAttribute` (manual loop)        | `method.GetAttribute(ToolAttributeName)`                                |
| `ParameterExtractor.GetDescription` (manual loop)     | `parameter.GetAttributeConstructorArgument<string>(DescriptionAttr, 0)` |

### 2B. Delete duplicated GetStringProperty

**Files:** `ServerExtractor.cs`, `ToolExtractor.cs`

Both have identical `GetStringProperty` private methods. Replace all call sites with:

- `attr.GetConstructorArgument<string>(index)` for positional args
- `attr.GetNamedArgument<string>(name)` for named args

Delete both private methods.

### 2C. AwaitableContext for return type classification

**File:** `ToolExtractor.cs`

Replace `ClassifyReturnType` string matching (`ToDisplayString() == "System.Threading.Tasks.Task"`) with:

- Construct `AwaitableContext` from `Compilation`
- Use `AwaitableContext` to reliably detect `Task`, `ValueTask`, `Task<T>`, `ValueTask<T>`
- Return `ReturnKind` enum value + extracted result type FQN

**Scope boundary:** `AwaitableContext.IsTaskLike()` is broader than the five `ReturnKind` values (it matches custom
task-like types). The implementation must explicitly check only for `Task`/`ValueTask` from the well-known
`System.Threading.Tasks` namespace. Custom task-like return types remain unsupported and must still trigger QA0007.

### 2D. Use StringExtensions.ToParameterName

**File:** `ParameterExtractor.cs`

Replace `ToCamelCase(string)` with `name.ToParameterName()` from `ANcpLua.Roslyn.Utilities.StringExtensions`.

### 2E. Fail on duplicate tool names (QA0011 → Error severity)

**File:** `ServerExtractor.cs`

Current behavior: duplicate names emit QA0011 as a warning but extraction continues with `Ok(tools)`, producing a broken
switch statement with two identical arms.

Fix: QA0011 becomes `DiagnosticSeverity.Error`. After detecting duplicates, return `DiagnosticFlow.Fail(diagnostics)` to
stop generation for that server. Update `DiagnosticDescriptors.DuplicateToolName` severity from Warning to Error.

### 2F. Remove dead QA0012

**Files:** `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`

`ComplexTypeNestingTooDeep` is declared but never emitted. The "object" fallback in `ParameterExtractor.MapToJsonSchema`
is the correct v1 behavior. Remove the descriptor entirely.

---

## Chunk 3: Emitter Fixes

### 3A. Wire JsonContextEmitter into DispatchEmitter (AOT-safe serialization)

**Files:** `DispatchEmitter.cs`, `JsonContextEmitter.cs`

Current: `DispatchEmitter` emits `s_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = CamelCase }` and
uses reflection-based serialization. `JsonContextEmitter` generates a `[JsonSerializable]` context that is never
referenced. The generated code is not AOT-safe.

Fix:

- `DispatchEmitter` uses `{ClassName}JsonContext.Default.Options` for serialization/deserialization
- Delete `s_jsonOptions` field emission
- For parameter types: `JsonSerializer.Deserialize<T>(element, {ClassName}JsonContext.Default.{TypeName})`
- For return types: `JsonSerializer.Serialize(result, {ClassName}JsonContext.Default.{TypeName})`

**Type coverage requirement:** `JsonContextEmitter.CollectReturnTypes` must emit `[JsonSerializable]` for every closed
type the dispatch layer touches. This includes:

- All parameter types (already done)
- All return types (already done for non-void)
- `Nullable<T>` wrappers for optional parameters
- Array/List/Dictionary types when used as parameters or return types
- Enum types (STJ handles these, but the context must include them)

The existing `CollectReturnTypes` iterates `tool.Parameters` and `tool.ReturnTypeFullyQualified`. This must be extended
to also register `global::System.Nullable<{type}>` when a parameter's `TypeFullyQualified` is a nullable value type. For
collection types, the fully-qualified type (e.g., `global::System.Collections.Generic.List<int>`) is already captured by
the parameter's `TypeFullyQualified` — no extra work needed if the extractor records the closed type correctly.

### 3B. Complete EscapeJson

**File:** `SchemaEmitter.cs`

Current `EscapeJson` handles `\`, `"`, `\n`, `\r`. Missing `\t` and control characters U+0000–U+001F.

Fix: escape all control characters per RFC 8259. Handle `\t` → `\\t`, `\b` → `\\b`, `\f` → `\\f`. All other
U+0000–U+001F characters use `\\uXXXX` form.

### 3C. Fix EscapeYaml for multi-line

**File:** `SkillEmitter.cs`

Current: quotes values containing `:` or `#`. Breaks on multi-line descriptions from XML doc summaries.

Fix: if value contains `\n`, use YAML literal block scalar (`|`) with each line indented under the key. For single-line
with special chars (`:`, `#`, `"`, leading/trailing whitespace), use double-quoted scalar with proper escaping.

### 3D. OTel: two-layer telemetry model

**Files:** `OTelEmitter.cs`, `DispatchEmitter.cs`, `McpProtocolHandler.cs`

The generated telemetry has two distinct layers:

**Layer 1 — Execute-tool spans (generated by DispatchEmitter)**

The generator wraps each tool method body. This is the primary span.

- `ActivitySource("Qyl.Agents")` — instrumentation library identity, version sourced from
  `typeof({ClassName}).Assembly.GetName().Version?.ToString() ?? "0.0.0"` (not hardcoded `"1.0.0"`)
- `Meter("Qyl.Agents")` — same version strategy
- Span name: `execute_tool {tool.name}` (already correct in current code)
- `ActivityKind.Internal` (already correct)
- Required attributes on every span:
    - `gen_ai.operation.name` = `"execute_tool"` (already emitted)
    - `gen_ai.tool.name` = tool name (already emitted)
    - `gen_ai.tool.type` = `"function"` (already emitted)
    - `gen_ai.system` = `"mcp"` (**new**)
    - `server.name` = server name (**new** — from `ServerModel.ServerName`)
- On error: `error.type` = exception type FQN (already emitted)
- Metric: `gen_ai.client.operation.duration` histogram (replaces `gen_ai.server.request.duration` — correct metric for
  local tool execution per GenAI metrics spec)
- No counter metric for v1. The current `qyl.agent.tool.calls` is removed. The GenAI metrics spec defines
  `gen_ai.client.operation.duration` and `gen_ai.client.token.usage` but no standard tool-call counter. If needed later,
  add a Qyl-namespaced counter (`qyl.agents.tool.call.count`) rather than inventing a `gen_ai.*` name.

**Layer 2 — MCP transport spans (McpProtocolHandler, runtime)**

`McpProtocolHandler.HandleAsync` optionally emits transport-level spans for MCP JSON-RPC methods. These are secondary
and only enriching.

- Same `ActivitySource("Qyl.Agents")` (shared instrumentation identity)
- Span name: `{method} {target}` — e.g., `tools/call {tool.name}`, `tools/list`, `initialize`
  (method alone when there is no meaningful target)
- `ActivityKind.Server` (the protocol handler is the server side of the MCP transport)
- Attributes: `mcp.method.name` (e.g., `"tools/call"`), `jsonrpc.request.id` (from JSON-RPC `id` field)
- For `tools/call` spans: also set `gen_ai.tool.name` so the transport span links to the tool being invoked
- The execute-tool span (layer 1) becomes a child of the transport span automatically via `Activity.Current` propagation
- If no listener is registered for "Qyl.Agents", no span is created (zero overhead)

**Version strategy:** `OTelEmitter` emits the `ActivitySource` and `Meter` constructors using
`typeof({ClassName}).Assembly.GetName().Version?.ToString() ?? "0.0.0"` instead of a hardcoded version string. This ties
the instrumentation version to the package version, avoiding drift.

---

## Chunk 4: Generator Pipeline

### 4A. Second pipeline for orphaned [Tool] → QA0004

**File:** `McpServerGenerator.cs`

Add `ForAttributeWithMetadataName("Qyl.Agents.ToolAttribute", ...)` that:

1. Gets the method's containing type
2. Checks if it has `[McpServer]` attribute (via `HasAttribute`)
3. If not → emit QA0004 diagnostic

This pipeline only emits diagnostics, no source output.

---

## Chunk 5: Runtime Fixes

### 5A. Cache JSON allocations in McpProtocolHandler

**File:** `Qyl.Agents/Protocol/McpProtocolHandler.cs`

**Cached statics (class-level, shared across all handler instances):**

- `s_emptyObject`: `JsonDocument.Parse("{}").RootElement.Clone()` — used in ping response and missing-arguments
  fallback. The `Clone()` detaches the `JsonElement` from the `JsonDocument` lifetime, so the document can be collected.
- `s_defaultSchema`: `JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone()` — used in tools/list for tools
  with no schema.

**Pre-parsed tool schemas (instance-level, computed once in constructor):**

- Store `JsonElement[]` alongside `s_tools`, where each element is pre-parsed from `tool.InputSchema` bytes (or
  `s_defaultSchema` if empty). Use `JsonDocument.Parse(tool.InputSchema).RootElement.Clone()` to detach from the
  document. This avoids parsing on every `tools/list` request.

**Test strategy:** Do not test element identity (JsonElement is a struct). Instead, test that repeated `tools/list`
calls return structurally identical schema content, confirming the cached path is exercised (no per-call allocation
observable from the test, but the code path is covered).

---

## Chunk 6: Tests

### Generator Tests (McpServerGeneratorTests.cs)

Add test cases for:

- QA0002 (static class) — expects `DiagnosticSeverity.Error`, no output
- QA0003 (generic class) — expects `DiagnosticSeverity.Error`, no output
- QA0004 (tool outside McpServer) — expects diagnostic via second pipeline
- QA0005 (static method) — expects `DiagnosticSeverity.Error`
- QA0006 (generic method) — expects `DiagnosticSeverity.Error`
- QA0007 (unsupported return type) — expects `DiagnosticSeverity.Error`
- QA0008 (unsupported parameter type) — expects `DiagnosticSeverity.Error`
- QA0009 (missing description) — expects `DiagnosticSeverity.Warning`, code still `.Compiles()`
- QA0010 (no tools) — expects `DiagnosticSeverity.Warning`
- QA0011 (duplicate tool names) — expects `DiagnosticSeverity.Error`, no generated output
- Nested partial class — verifies declaration chain emission and `.Compiles()`
- Enum parameter — verifies enum values in schema JSON and `[JsonSerializable(typeof(MyEnum))]`
- Nullable parameter — verifies schema handles nullable, `[JsonSerializable(typeof(int?))]`
- DateTimeOffset/Guid/Uri parameters — verifies `format` field in schema
- Array/List parameter — verifies `[JsonSerializable]` includes the collection type
- JSON context wiring — verifies generated code references `{ClassName}JsonContext.Default` (not `s_jsonOptions`)
- Multi-line description — verifies YAML literal block scalar in SKILL.md output
- `server.name` attribute — verifies generated code includes `SetTag("server.name", ...)`

### Runtime Tests (McpProtocolEndToEndTests.cs)

Add test cases for:

- Repeated `tools/list` returns consistent schema content (pre-parsed path)
- Tool exception → `isError: true` content with error message
- OTel span attributes: assert `server.name`, `gen_ai.system` = `"mcp"` on execute-tool span
- MCP transport span: assert `mcp.method.name`, `jsonrpc.request.id` attributes on tools/call

---

## Dependency Order

```
Chunk 1 (models) → Chunk 2 (extractors) → Chunk 3 (emitters) → Chunk 4 (pipeline) → Chunk 5 (runtime) → Chunk 6 (tests)
```

Chunks 4 and 5 are independent of each other. Chunk 6 validates everything.

---

## Out of Scope

- Moving Qyl.Agents to its own repo (separate task)
- `McpHost` stream injection for testability (runtime design change, not a bug)
- `ValueStringBuilder` replacements in emitters (generator-time perf is irrelevant)
- `ToKebabCase` extraction to shared utility (stays in ToolExtractor, only used by this generator)
- `OTEL_SEMCONV_STABILITY_OPT_IN` gating (unshipped, starts fresh on current experimental shape)
- Custom task-like return types (explicitly unsupported, QA0007)
