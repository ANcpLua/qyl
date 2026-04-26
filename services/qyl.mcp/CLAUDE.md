@import ../../CLAUDE.md

# qyl.mcp

**Type:** Application  
**Framework:** net10.0  
**Language:** C# latest  
**SDK:** ANcpLua.NET.Sdk

## Banned APIs

- Use `TimeProvider` instead of current-time APIs
- Use `Lock` instead of `object` for locking
- Use `System.Text.Json` instead of Newtonsoft

## Result Cap Coaching

Tools that return lists/search results use `ResponseFormatter.AppendResultCap` to coach the LLM when the result cap is
hit. Constraints:

- **No shared `RESULT_LIMIT` constant.** Tools have legitimately different defaults (25 for search, 50 for errors, 100
  for spans).
- **No migration of ad-hoc tools to `FormatPagedList`.** Different tools need different formats.
- **No filter-level enforcement.** Coaching is explicit and context-aware at the tool level.
- **Fixed-scope tools skip coaching.** `list_trace_logs`, `get_error_issue` events, `export_for_agent`.

## Generator

`internal/qyl.mcp.generators/` emits `QylToolManifest` with `ToolTypes[]`, `ToolDescriptors[]`, `RegisterTools(...)`,
`RegisterServices(...)`, and `CreateTools(...)`. It is the **single** MCP tool-discovery generator in qyl — the legacy
`Qyl.Agents.Generator` / `Qyl.Agents.Abstractions` / `Qyl.Agents` triad was removed 2026-04-20. Both `qyl.mcp` and
`qyl.loom` stand on the official `ModelContextProtocol` SDK for protocol + dispatch; `qyl.mcp.generators` layers
skill-aware manifest emission on top.

### Meta-attachment emission (2026-04-21 refactor)

`RegisterTools(IMcpServerBuilder, SkillConfiguration, JsonSerializerOptions)` emits **explicit per-method**
`McpServerTool.Create(methodInfo, targetFactory, new McpServerToolCreateOptions { ..., Meta = ... })` registrations
with `qyl.skill` + `qyl.capabilities.starting` + `qyl.capabilities.followUp` attached via `JsonObject`. The previous
`.WithTools<T>()` reflection path is gone — not for AOT reasons, but because `WithTools<T>()` has no per-tool `Meta`
attachment point. Skill-gating is inline: one `if (skills.IsEnabled(QylSkillKind.X))` per owning class.

`CreateTools(IServiceProvider, Func<Type, bool>)` emit is **preserved** — two load-bearing consumers
(`Tools/UseQylTools.cs` + `Tools/RcaTools.cs`) materialize `List<AIFunction>` for embedded-agent
`UseFunctionInvocation` loops. Second dispatch path parallel to the MCP protocol surface.

### QylToolManifest emits two dispatch surfaces

| Surface                                                             | Consumer                                                | Purpose                                                                |
|---------------------------------------------------------------------|---------------------------------------------------------|------------------------------------------------------------------------|
| `RegisterTools(builder, skills, jsonOpts)` + `McpServerTool.Create` | `QylMcpServerRegistration.Configure` → MCP `tools/call` | External protocol surface, skill-gated, carries `_meta`                |
| `CreateTools(services, predicate) → List<AIFunction>`               | `UseQylTools`, `RcaTools`                               | Embedded-agent `UseFunctionInvocation` loop (inside one MCP tool call) |

Both load-bearing. Deleting `CreateTools()` silently breaks the meta-tools — `tools/list._meta` can't bridge an
inside-a-tool-call dispatch path.

### Meta-tool invariants

Tools that materialize other tools (`UseQylTools`, `RcaTools`) MUST pass a predicate to
`QylToolManifest.CreateTools(services, predicate)` that excludes their own type
(`static type => type != typeof(UseQylTools)`). Without this guard the meta-tool recursively discovers itself and
the embedded agent loops until the spawn budget trips. `InvestigationLineage.TryEnter()` is a backstop, not a
substitute.

### Retired — `qyl://manifest`

Deleted on the 2026-04-21 destruction pass. `QylMcpManifestBuilder` + `QylMcpMetadataCatalog` + `/mcp.json` HTTP
endpoint are gone. Skill/capability tagging now flows through `tools/list._meta`; server-internal counts come from
`QylToolManifest.ToolDescriptors.Length`.

### Emitter style

Uses `IndentedStringBuilder.BeginBlock()` pattern (not `Indent()/Outdent()` which are internal).

## Agent-layer telemetry

Every `AIAgent` constructed in this project uses `.AsBuilder().UseQylAgentTelemetry().Build()` at the composition
root. Helper lives in `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs` and bundles
`UseOpenTelemetry("qyl.agent")` + `UseLogging()`. Diagnostic `QYL0135` (`AgentCompositionRootAnalyzer`) fires if any
construction site misses the wrap — symbol-based detection, rename- and namespace-collision-safe. Canonical shape
in the MAF-qyl overlay skill.

## TaskSupport classification

`TaskSupport` on `[McpServerTool(...)]` (MCP SDK experimental; `<NoWarn>MCPEXP001</NoWarn>` scoped to this csproj):

- **Required** — `qyl.approve_fix_run`, `qyl.generate_fix` (start async pipelines with side effects).
- **Optional** — agent-invoking meta-tools (`qyl.use_qyl`, `qyl.root_cause_analysis`, `qyl.summarize_*`) and
  long-form searches (`qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything >10k rows).
- **Forbidden** (default — omit the attribute) — fast reads (`qyl.get_*_by_id`, `qyl.health_check`,
  `qyl.list_services`, single-record fetches).

## MCP prompt registration

Only `CodeReviewPrompt` (lives in `services/qyl.loom/CodeReview/`) is promoted to `[McpServerPromptType]` — real
template with `Build(prTitle, diffContent, knownErrorPatterns)`. Registered in `services/qyl.loom/Program.cs` via
`.WithPrompts<CodeReviewPrompt>()`. The four summary prompt constants under `Agents/` (`RcaPrompt.Prompt`,
`ErrorSummaryPrompt.Prompt`, `TraceSummaryPrompt.Prompt`, `SessionSummaryPrompt.Prompt`) stay as internal
`static const string` — system-prompt constants consumed via `ChatOptions.Instructions`, not MCP protocol templates.
Grep `\.Build(` (not `*Prompt`) when deciding what belongs in `prompts/list`.

## Task store

`InMemoryMcpTaskStore` registered singleton in `QylMcpServerRegistration` with `defaultTtl: 1h, maxTtl: 6h,
pollInterval: 1s, maxTasks: 500` and wired onto `McpServerOptions.TaskStore` via DI. Required for tools declared with
`TaskSupport = Required`.
