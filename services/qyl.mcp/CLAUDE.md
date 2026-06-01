@import ../../CLAUDE.md

# qyl.mcp

**Type:** Application  
**Framework:** net10.0  
**Language:** C# latest  
**SDK:** ANcpLua.NET.Sdk

The whole service already runs on the official `ModelContextProtocol` SDK 1.3.0 — there is **no hand-rolled
protocol plumbing** (no manual `tools/list`, `CallToolResult`, JSON-RPC, or schema building). Everything below is
either a real correctness invariant or a simplification target. Nothing here is "load-bearing by tradition" — if a
section reads like it's protecting code, simplify the code and update the section.

## Banned APIs

- `TimeProvider` instead of current-time APIs
- `Lock` instead of `object` for locking
- `System.Text.Json` instead of Newtonsoft

## Result cap coaching

List/search tools call `ResponseFormatter.AppendResultCap` so the LLM knows a cap truncated the output. Caps are
per-tool (25 search / 50 errors / 100 spans), so there is no shared `RESULT_LIMIT`. Fixed-scope tools
(`list_trace_logs`, `get_error_issue` events, `export_for_agent`) return everything and skip it. This is the current
shape, not a mandate — if `FormatPagedList` or a filter cleans it up, use it.

## Tool generator

`internal/qyl.mcp.generators/` emits `QylToolManifest` (`ToolTypes[]`, `ToolDescriptors[]`, `RegisterTools(...)`,
`RegisterServices(...)`, `CreateTools(...)`) — the single MCP tool-discovery generator. It only layers skill-aware
manifest emission on top of the SDK's dispatch. Emitter uses `IndentedStringBuilder.BeginBlock()` (the
`Indent()/Outdent()` pair is internal).

`RegisterTools(...)` emits per-method `McpServerTool.Create(...)` carrying a `Meta` `JsonObject` (`qyl.skill`,
`qyl.capabilities.starting`, `qyl.capabilities.followUp`). It is per-method **only** because `WithTools<T>()` has no
per-tool `Meta` hook — if the SDK adds one, collapse back to `WithTools<T>()`.

### Two dispatch surfaces — both wired, not redundant

| Surface | Consumer | Note |
|---|---|---|
| `RegisterTools(builder, skills, jsonOpts)` → `McpServerTool.Create` | MCP `tools/call` | external protocol, skill-gated, carries `_meta` |
| `CreateTools(services, predicate)` → `List<AIFunction>` | `UseQylTools`, `RcaTools` | embedded-agent `UseFunctionInvocation`, inside one tool call |

These are independent paths: `tools/list._meta` cannot bridge an inside-a-tool-call dispatch, so deleting
`CreateTools` breaks the meta-tools. Keep both until the embedded-agent loop itself is removed.

### Meta-tool recursion guard

`UseQylTools` and `RcaTools` materialize other tools and MUST exclude their own type from
`CreateTools(services, predicate)` (`static type => type != typeof(UseQylTools)`). Without it the meta-tool
discovers itself and the embedded agent loops until the spawn budget trips. `InvestigationLineage.TryEnter()` is a
backstop, not a substitute.

## Capability metadata — mostly load-bearing (verified repo-wide)

`Capabilities/` is **not** dead weight; the generator depends on most of it. Three tiers:

- **`QylCapabilityAttribute` (`[QylCapability(id, role)]`)** — tags **74 tool methods**. `qyl.mcp.generators`
  reads it to emit each tool's `tools/list._meta` (`qyl.capabilities.starting` / `qyl.capabilities.followUp`,
  `ToolManifestEmitter.cs:280-281`). Deleting it breaks the 74 tools, the generator, and the `_meta`.
- **`QylCapabilityDefinitionAttribute` + `QylCapabilityDefinitions` (16 defs) + `QylCapabilityDescriptor`** — the
  generator turns these into `QylToolManifest.Capabilities[]`. They are the **source** of the capability hints,
  not a duplicate of `_meta`. Load-bearing.
- **`CapabilityTools` (`qyl.list_capabilities`, `qyl.get_capability_guide`) + `QylCapabilityCatalog` (~190 LOC)** —
  the only optional part. Two LLM-facing introspection tools with no *internal* consumers; removing them drops a
  client-discovery affordance but breaks nothing else. A deliberate product call, not dead-code cleanup.

## Agent-layer telemetry

Every `AIAgent` is built with `.AsBuilder().UseQylAgentTelemetry().Build()` at the composition root
(`internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`, bundling `UseOpenTelemetry("qyl.agent")`
+ `UseLogging()`). Diagnostic `QYL0135` (`AgentCompositionRootAnalyzer`) fires on any construction site that misses
the wrap. Canonical shape lives in the MAF-qyl overlay skill.

## MCP-server telemetry

The filter stack lives in `internal/qyl.instrumentation/Instrumentation/Mcp/QylMcpServerInstrumentation.cs`. The
composition root calls `.UseQylMcpInstrumentation(TelemetryConstants.ActivitySource)` on the `IMcpServerBuilder`
immediately after the transport; the facade registers the JSON-RPC envelope filters and the per-primitive request
filters that emit one OTel span plus the `gen_ai.execute_tool {name}` child per invocation. `qyl.mcp` and `qyl.loom`
share this one facade.

- **One facade, no inline filters.** A parallel `WithMessageFilters` / `AddCallToolFilter` block emitting its own
  activity is the drift `[QYL0135]` catches at the agent layer — same rule, MCP layer.
- **PII is opt-in.** `RecordInputs` / `RecordOutputs` are off by default; enable per call site only when the tool
  surface is known not to carry credentials or customer data.
- **`IsError` is handled centrally.** `CallToolResult.IsError = true` → `ActivityStatusCode.Error` in the facade;
  don't duplicate it per tool.

The downstream business filter (admin denial, scope injection, anthropic max-result-size meta) chains after the
facade in `QylMcpServerRegistration` and carries zero telemetry concerns.

## TaskSupport classification

`TaskSupport` on `[McpServerTool(...)]` (SDK-experimental; `<NoWarn>MCPEXP001</NoWarn>` scoped to this csproj):

- **Required** — side-effecting async pipelines (`qyl.approve_fix_run`, `qyl.generate_fix`).
- **Optional** — agent-invoking meta-tools (`qyl.use_qyl`, `qyl.root_cause_analysis`, `qyl.summarize_*`) and
  long-form searches (`qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything >10k rows).
- **Forbidden** (omit the attribute) — fast reads (`qyl.get_*_by_id`, `qyl.health_check`, `qyl.list_services`).

## MCP prompt registration

Only `CodeReviewPrompt` (`services/qyl.loom/CodeReview/`) is a real `[McpServerPromptType]` — `Build(prTitle,
diffContent, knownErrorPatterns)`, registered via `.WithPrompts<CodeReviewPrompt>()` in `qyl.loom/Program.cs`. The
four `Agents/` summary constants (`RcaPrompt.Prompt`, etc.) are `static const string` consumed via
`ChatOptions.Instructions`, not MCP templates. Grep `\.Build(` (not `*Prompt`) to decide what belongs in
`prompts/list`.

## Task store

`InMemoryMcpTaskStore` is registered singleton in `QylMcpServerRegistration` (`defaultTtl: 1h, maxTtl: 6h,
pollInterval: 1s, maxTasks: 500`) and wired onto `McpServerOptions.TaskStore` via DI. Required for
`TaskSupport = Required` tools.
