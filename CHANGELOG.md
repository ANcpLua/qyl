# Changelog

## Unreleased

### Added

- **Telemetry Intelligence Model spec**: `specs/telemetry-intelligence.md` — canonical reasoning model over telemetry data. Defines diagnostic patterns, causal rules, and investigation strategies as TypeSpec → generated C# types. Completes the deterministic stack: emit → store → group → **reason** → investigate.
- **API Contract spec**: `specs/api.md` — single source of truth for response envelope, error model, status codes, pagination, timestamps, and entity IDs. Referenced by collector and MCP specs.
- **Cost Engine spec**: `specs/cost.md` — extracted from architecture spec. Adds normative cost formula, time aggregation rules, and consistency guarantees.
- **Spec restructure**: Slimmed `00-architecture.md` from 750→563 lines (kernel only). Added ownership headers to all 12 specs. Deduplicated boundary statements in loom.md and mcp.md. Code context SSOT assigned to `instrumentation.md` section 5.1.
- **MAF rc4 upgrade**: Microsoft.Agents.AI packages bumped from rc3/preview.260304.1 to rc4/preview.260311.1.
- **OTel source name fix**: Added `Experimental.Microsoft.Agents.AI` to `SGenAiActivitySources` and
  `SGenAiMeterNames` — MAF's `UseOpenTelemetry()` spans were silently dropped.
- **CapabilityEmitter restored**: Fixed pre-existing empty emitter file that broke the instrumentation generators build.
- **CodingAgent types relocated**: `CodingAgentProvider`, `CodingAgentRunRecord`, `LoomSettingsRecord` moved from
  `Qyl.Collector.CodingAgent` to `Qyl.Contracts.Loom` for cross-project sharing.

### Removed

- **qyl.agents project**: Deleted. `QylAgentBuilder`, `QylCopilotAdapter`, `CopilotAdapterFactory`,
  `LlmProviderFactory`, `TrackModeRouter`, `ChunkingPipeline` — all shim layers over MAF native APIs
  (`AddAIAgent()`, `AgentWorkflowBuilder`, `MapAGUI()`). `InstrumentedChatClient` and `InstrumentedAIFunction`
  to be moved to `qyl.instrumentation` SDK.
- **qyl.workflows project**: Deleted. `DeclarativeEngine`, `WorkflowParser`, `WorkflowEngine` — shim layers
  over MAF native `AgentWorkflowBuilder` and `DeclarativeWorkflowBuilder`.
- **Collector kill-list directories**: `Copilot/`, `ClaudeCode/`, `CodingAgent/`, `Workflow/` removed from
  server per v2 architecture. Server has zero LLM dependencies.
- **DuckDbStore.ClaudeCode.cs**: Orphaned storage partial (no surviving consumer).
- **DuckDbExecutionStore.cs**: Implemented deleted `IExecutionStore` from qyl.workflows.
- **MCP tools**: `CopilotTools`, `ClaudeCodeTools`, `InvestigateTools`, `HttpAgentProvider`,
  `IAgentProvider`, `ObservabilitySystemPrompt` — called deleted collector endpoints.
- **Dashboard**: Copilot panel/button/suggestions components, workflow pages, claude-code hooks,
  LLM status hook, related settings sections.
- **Browser extension scaffold**: Chrome extension with content script (AI text toolbar with undo toast), popup, service
  worker, shared AI client config, and Vite build pipeline (`vite.extension.config.ts`).
- **Itemized undo toast**: Browser extension undo toast shows per-change items with text previews and individual undo
  buttons (`undoByIndex()` for selective reversal).
- **Semconv comparison docs**: JS vs .NET OTel semantic convention attribute comparison (`eng/semconv/js vs net/`).

### Changed

- **Artifact export CLI**: `tools/export-artifact.ts` now reads the artifact API base URL from `QYL_URL` (or legacy `QYL_COLLECTOR_URL`) and no longer embeds a collector-specific host default.

- **Analyzer pre-filter tightened**: `AgentCallSiteAnalyzer.CouldBeAgentInvocation` now uses `HashSet<string>` method
  name lookup instead of generic `CouldBeInvocation`, avoiding expensive `GetSymbolInfo` calls on non-matching nodes.
- **Dead code removed from analyzers**: `TryFindAttributeData` replaced with `method.GetAttribute()` extension,
  `GenAiCallSiteAnalyzer.TryExtractModelName` uses `TryGetStringArgument` helper, `TracedCallSiteAnalyzer` uses
  `is not {}` pattern match, `ServiceDefaultsSourceGenerator` uses `IsMethodNamed` helper.
- **Expression-bodied methods and ternary cleanup** across collector, copilot, mcp, and watch projects.
- **qyl documentation agents**: Added `qyl-diagram-agent.md` and `qyl-ecosystem-scout-SKILL.md` as
  reusable prompts/assets for Mermaid architecture diagrams and 5-domain ecosystem scouting.
- **Architecture docs corrected**: README and v2/spec index now describe the acyclic split of `qyl.web`,
  `qyl.collector`, `qyl.agents`, `qyl.mcp`, `qyl.infrastructure`, and `qyl.core` instead of the old collector/loom split.

### Fixed

- **AG-UI plan docs restored**: PR #89 accidentally overwrote AG-UI design/impl docs with Loom content; originals
  restored from git history.
- **Metrics doc cleaned**: Removed accidental Loom spec appendix from
  `andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`.
- **Missing parameter validation** in identity endpoints.
- **MCP tool consolidation** (`f7403d5`): removed deprecated `qyl.investigate`, `qyl.list_console_errors`, and
  `qyl.summarize_session`; deleted `InvestigateTools.cs`, `IAgentProvider.cs`, `HttpAgentProvider.cs`,
  `ObservabilitySystemPrompt.cs`; updated `ReplayTools`, `SummaryTools`, `UseQylSystemPrompt`, `ConsoleTools`, and
  telemetry prompts to reflect local MCP-agent telemetry and OTLP flow.

### Removed

- **MCP tools referencing deleted collector endpoints**: Deleted `CopilotTools.cs`, `ClaudeCodeTools.cs`,
  `InvestigateTools.cs`, `HttpAgentProvider.cs`, `IAgentProvider.cs`, `ObservabilitySystemPrompt.cs`; removed `Copilot`
  and `ClaudeCode` skill kinds; cleaned up DI registrations, JSON contexts, and skill wiring in `Program.cs`,
  `McpToolRegistry.cs`, `SkillRegistrationExtensions.cs`, `QylSkillKind.cs`; excluded `Clear.cs` (diff scratch file)
  from compilation.
- Stray `docs/plans/index.md` (unrelated Copilot Studio localization content).

### Added

- **Coding Agent Provider system**: Pluggable coding agent backends for autofix pipeline (Loom, Cursor, GitHub Copilot,
  Claude Code). Enum, DuckDB schema (`coding_agent_runs`, `Loom_settings` tables), REST endpoints (
  `/api/v1/fix-runs/{id}/coding-agents`, `/api/v1/Loom/settings`), React Query hooks, 3 UI components (
  `ClaudeCodeIntegrationCta`, `CodingAgentResultCard`, `LoomSettingsSection`), Loom tab in Settings, coding agent
  section in Issue Detail page. No `branch_name` display, provider-aware button text ("Open in Cursor" / "Open in Claude
  Code")
- **Playwright E2E tests**: smoke test suite (`e2e/smoke.spec.ts`) covering health endpoint, sidebar navigation, time
  range selector, theme toggle, search input, and settings page; `npm run e2e` / `npm run e2e:ui` scripts
- **Expanded dashboard smoke coverage**: compatibility endpoint reachability (`/api/v1/traces`, `/api/v1/genai/*`,
  `/api/v1/search/query`), onboarding verify endpoint assertions, `/api/v1/logs/live` stream availability, keyboard
  shortcut navigation/modal checks, and external APIs header/collapse semantics
- **Browser → Server trace correlation via `session.id`**: every OTLP payload from the browser SDK includes a
  `session.id` resource attribute (32-char hex, generated once per `init()`), grouping all telemetry from one browser
  tab without forcing a single mega-trace
- Per-interaction trace model: each web vital, navigation, click, and fetch gets its own trace; `session.id` on the
  resource handles session grouping
- Error logs now include `traceId` and `spanId` for correlation
- `/v1/logs` added to auth exclusion list for browser log ingestion
- CORS defaults to `*` when `QYL_OTLP_CORS_ALLOWED_ORIGINS` is not set
- **ADR-002 GitHub OAuth onboarding**: DuckDB token persistence, GitHub Device Flow + PAT + env var auth, 7 API
  endpoints, 6-step onboarding wizard, Copilot token bridge, `/health/ui` endpoint
- ADR documentation (`docs/decisions/ADR-001` through `ADR-005`)
- Embedding cluster background worker (`EmbeddingClusterWorker`)
- Bot pages: `BotPage`, `BotConversationDetailPage`, `BotUserJourneyPage`
- Analytics hook (`use-analytics`)
- Span clusters DuckDB schema and store

### Changed

- **Microsoft.Agents.AI upgrade**: `Microsoft.Agents.AI.Abstractions` → `1.0.0-rc2`,
  `Microsoft.Agents.AI.GitHub.Copilot` → `1.0.0-preview.260225.1` (split version variable for independent cadence)
- **Anthropic default model**: `claude-sonnet-4-20250514` → `claude-sonnet-4-6` in `LlmProviderFactory`
- **Dashboard keyboard shortcuts aligned with UI hints**: `S` now routes to Loom (instead of structured logs alias), and
  shortcut docs/modal reflect runtime behavior
- **Live stream status robustness**: dashboard now parses both typed SSE envelope payloads and legacy event payloads for
  `/api/v1/live`, restoring reliable `LIVE`/connection status transitions
- **Loom API resilience**: `/api/v1/regressions` mapping is enabled and Loom feed endpoints return `200` with empty
  payloads when optional storage tables are not initialized
- Auth unification: GitHub is now the only identity provider (removed cookie-based login)
- StartupBanner shows GitHub connect hint instead of login token
- Browser SDK `context.ts` now manages session context (`initSessionContext`, `getSessionId`)
- Browser SDK `transport.ts` constructor accepts `sessionId` parameter

### Removed

- `WorkflowEngine.GetExecutions()` and `GetExecution(string)` sync methods (backward-compat shims — only async variants
  remain)
- `.codex/` skills directory (unused Codex agent definitions)
- `examples/` directory (AgentsGateway, qyl.demo)
- `qyl.cli` project (ADR-004)
- `qyl.watchdog` project (consolidated into collector)
- `qyl.Analyzers` + `qyl.Analyzers.CodeFixes` (migrated to ANcpLua.Analyzers)
- `LoginPage`, `use-auth.ts`, cookie auth endpoints
- `AuthCheckResponse` record
- `COMPARISON.md`, `QYL-VS-ENTERPRISE.md`, `ROADMAP.md`, `AGENTS.md` (root)
- `docs/policies/catalog-format-policy.md`, `docs/prds/observability-enhancements-v1.0-prd.md`
- `hooks/dotnet-build-capture.sh`, `hooks/install-dotnet-build-capture.sh`
