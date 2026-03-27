# Changelog

## Unreleased

### Changed

- **Shared Loom types moved to qyl.contracts**: Moved `FixPolicy`, `PolicyGate`, `FixRunRecord`,
  `AutofixStepRecord`, `ConfidenceResult`, `TriageResult`, `LlmTriageResponse`, `IssueSummary`,
  `IssueStatus`, and `IssueEvent` from `qyl.collector` to `Qyl.Contracts.Loom` namespace in
  `src/qyl.contracts/Loom/`. This enables both collector and standalone Loom to share these types
  without a ProjectReference dependency between them.

### Removed

- **Dead qyl.loom HTTP mirror cluster deleted**: Removed the unused `qyl.loom` endpoint/code-review/investigation surface
  that duplicated live collector-owned Loom routes. Deleted `LoomEndpoints`, `LoomSettingsEndpoints`, the old coding-agent and
  code-review endpoint files, the duplicate `LoomInsight`/explorer model stack, and their `qyl.loom.csproj` compile metadata.
- **Phosphor icons eliminated**: Replaced all `@phosphor-icons/react` usage with `lucide-react` across 25+ dashboard
  files. Removed `@phosphor-icons/react` from package.json. Lucide is now the sole icon library.
- **Loom import cleanup (Hades)**: Removed 21 unused PackageReferences and 1 unused ProjectReference (qyl.instrumentation)
  from `qyl.loom.csproj`. Replaced `ContainsIgnoreCase`/`StartsWithIgnoreCase`/`StartsWithOrdinal` extension methods with
  standard BCL `Contains`/`StartsWith` + `StringComparison`.
- **Dead code cleanup (Hades audit)**: Deleted 8 files (~26,427 lines) confirmed unused by grep across entire repo.
  `Clear.cs` (26K stale diff in qyl.mcp), `WorkspaceContext.cs` (orphaned, zero refs), `PolicyGate.cs` (empty stub),
  `AnomalyTypes.cs` (empty), `AutofixArtifacts.cs` x2 and `AutofixConstants.cs` x2 (unused types in both collector and
  loom), `UnixNano` struct from `CollectorTypes.cs` (never instantiated, raw ulong used everywhere).

### Fixed

- **Generator build chain restored**: `qyl.instrumentation.generators` now imports
  `ANcpLua.Roslyn.Utilities`, `ANcpLua.Roslyn.Utilities.Models`, and `ANcpLua.Roslyn.Utilities.Matching`
  explicitly, `qyl.collector.storage.generators` imports `ANcpLua.Roslyn.Utilities`, and `SpanStorageRow`
  is now `partial` so the generated DuckDB mapper methods bind again. This restores the full collector build chain.
- **Collector Loom route ownership completed**: `qyl.collector` now maps coding-agent and org-scoped Loom settings
  endpoints on the active host. `DuckDbStore` no longer hardcodes `'default'` when reading Loom settings, so
  `/api/v1/loom/settings/{orgId}` now reflects the requested org instead of a fake singleton record.
- **MCP code review route alignment**: `qyl.trigger_code_review` and `qyl.get_code_review` now split `owner/repo`
  into the collector's `{owner}/{repo}` route shape instead of sending an escaped full name that could not match the
  active endpoint surface.
- **SEC-001 XSS fix**: Replaced `dangerouslySetInnerHTML` in `text-visualizer.tsx` with tokenized React element rendering.
- **SEC-002 type safety**: Replaced `as unknown as SpanRecord` in `use-telemetry.ts` with proper typed construction.
- **A11Y-001/002**: Added keyboard support (role, tabIndex, onKeyDown) to DashboardCard and PerformancePage service rows.
- **A11Y-003**: Replaced all `outline-none` with `outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2`.
- **UX-001**: Added React ErrorBoundary component wrapping routes in App.tsx.
- **TASTE-002**: Replaced 60+ raw Tailwind colors with semantic tokens (signal-red, signal-green, etc.) across dashboard.
- **TASTE-003**: Replaced hardcoded HSL/oklch chart colors with CSS variable theme tokens.

### Changed

- **Dependency baseline updated**: .NET runtime packages `10.0.4` → `10.0.5`, `Microsoft.Extensions.AI*`
  `10.4.0` → `10.4.1`, `OpenTelemetry.Instrumentation.AspNetCore` `1.15.0` → `1.15.1`,
  `Microsoft.IdentityModel.JsonWebTokens` `8.16.0` → `8.17.0`, `ANcpLua.NET.Sdk*` `2.25.4` → `2.25.5`,
  and the MSBuild-pinned `ANcpLua.Roslyn.Utilities*` versions `1.47.0` → `1.48.0`.
- **Solution file duplicate removed**: `qyl.slnx` no longer declares `eng/loom-requirements-registry.png` twice, which
  fixes MSBuild solution parsing and unblocks `dotnet clean`.
- **Loom identity clarified**: `specs/loom.md` now maps Loom 1:1 to Sentry's Seer product model. The spec now explicitly
  distinguishes the observability substrate (`qyl.collector` / dashboard / instrumentation), the MCP access surface (
  `qyl.mcp`), and Loom as the standalone Seer-equivalent intelligence plane with Autofix, PR creation, coding-agent
  delegation, and code review.
- **MAF hosting guidance corrected**: `AGENTS.md` now records the verified rc4/preview.260311.1 reality: `AddAIAgent()`
  returns `IHostedAgentBuilder`, hosted durability uses `AgentSessionStore` via `WithSessionStore(...)` /
  `WithInMemorySessionStore()`, `AIAgent` exposes `CreateSessionAsync` + `RunAsync` / `RunStreamingAsync`, and shared
  `conversationId` does not create implicit cross-agent Loom memory.
- **MAF Loom sample rewritten**: `samples/maf-agent-qyl` is now a lean one-file Loom subsystem showcase instead of the
  old prompt-truncation demo. It registers bounded agents through `AddAIAgent(...)`, attaches durability with
  `WithInMemorySessionStore()`, exercises `WithAITool(...)` for PR creation, and keeps Loom's cross-agent handoff state
  explicit instead of treating shared conversation IDs as magical shared memory.

### Added

- **Qyl.Agents Loom migration slice**: `qyl.loom` now contains a `Qyl.Agents`-backed MCP server surface
  (`LoomGodAnalyzerServer`) plus ASP.NET/stdin hosting helpers. The new slice exposes issue insight, fix-run launch,
  PR review, and a reusable "god analyzer" prompt without reintroducing the dead HTTP mirror layer or depending on the
  currently unstable upstream source generator path.
- **Cost engine**: DuckDB `model_pricing` + `model_pricing_tiers` tables via migration V20260322. Pre-aggregated
  `cost_by_model_hourly` view. `ModelPricingService` seeds from `data/model-pricing.json` (30 models across OpenAI,
  Anthropic, Google, Meta, Mistral, DeepSeek) on first boot. Server-side cost computation at ingestion time — both
  gRPC and HTTP OTLP paths enrich spans with `gen_ai_cost_usd` from pricing lookup. SDK-reported costs are preserved.
  REST API: `GET /api/v1/cost/by-session`, `/by-service`, `/by-model`, `/timeseries`, `/budget`;
  `POST /cost/sync-pricing`; `PUT /cost/pricing/{provider}/{model}`.
- **CostPage**: New `/cost` route with ECharts timeseries (hourly cost by model), TanStack Table breakdown
  (sort/filter/paginate by model), KPI cards (spend today, top model, budget status), and per-service cost summary.
- **ServicesPage**: New `/services` route with sortable service table showing span counts, error counts, version,
  health status (healthy/degraded based on recent errors).
- **Telemetry Intelligence types**: TypeSpec definitions in `core/specs/intelligence/` (8 files) + C# types in
  `src/qyl.contracts/Intelligence/` (7 types, 3 static registries). 10 diagnostic patterns, 6 causal rules,
  4 investigation strategies — all seed data from spec §5. Compile-time collections, zero I/O.
- **Pattern engine**: `IPatternEngine` + `PatternEngine` in `src/qyl.collector/Intelligence/`. Pure computation:
  `Evaluate()` matches observed signals against diagnostic patterns via type-coerced comparison, `BuildCausalGraph()`
  traverses causal rules to identify root causes, `SelectStrategy()` resolves investigation strategies by pattern ID
  then category fallback. No I/O, no LLM, no DI — deterministic same-input-same-output.
- **Intelligence REST API**: 4 endpoints in `src/qyl.collector/Intelligence/IntelligenceEndpoints.cs` —
  `GET /api/v1/intelligence/evaluate` (extract signals from trace/issue spans, run pattern matching),
  `/causal-chain` (build causal graph from pattern IDs), `/strategy` (select investigation strategy),
  `/execute-step` (execute strategy step DuckDB query with template substitution). `IPatternEngine` registered
  as singleton in DI with static registries.
- **Intelligence + cost test suite**: 62 tests in `tests/qyl.collector.tests/Intelligence/` and `Cost/`.
  PatternEngineTests (31): every seed pattern positive/negative match, causal graph building, root cause
  identification, strategy selection. CostComputationTests (10): pricing lookup, cost formula verification,
  unknown model handling, batch enrichment. InvestigationQueryValidationTests (21): validates all DuckDB queries
  from investigation strategies against live schema — caught `code_filepath` column missing from spans.
- **MCP intelligence tools**: 5 new tools in `src/qyl.mcp/Tools/Intelligence/IntelligenceTools.cs` —
  `list_diagnostic_patterns` (static registry), `evaluate_patterns`, `explain_causal_chain`,
  `suggest_investigation`, `execute_investigation_step`. All ReadOnly, structured output.
- **MCP structured output**: `StructuredResponse` envelope in `ResponseFormatter.cs` — separates `facts`,
  `analysis`, `actions`, `pagination`, `evidence` per spec section 5.2 and tool contract section 8.
- **Telemetry Intelligence Model spec**: `specs/telemetry-intelligence.md` — canonical reasoning model over telemetry
  data. Defines diagnostic patterns, causal rules, and investigation strategies as TypeSpec → generated C# types.
  Completes the deterministic stack: emit → store → group → **reason** → investigate.
- **API Contract spec**: `specs/api.md` — single source of truth for response envelope, error model, status codes,
  pagination, timestamps, and entity IDs. Referenced by collector and MCP specs.
- **Cost Engine spec**: `specs/cost.md` — extracted from architecture spec. Adds normative cost formula, time
  aggregation rules, and consistency guarantees.
- **Spec restructure**: Slimmed `00-architecture.md` from 750→563 lines (kernel only). Added ownership headers to all 12
  specs. Deduplicated boundary statements in loom.md and mcp.md. Code context SSOT assigned to `instrumentation.md`
  section 5.1.
- **MAF rc4 upgrade**: Microsoft.Agents.AI packages bumped from rc3/preview.260304.1 to rc4/preview.260311.1.
- **OTel source name fix**: Added `Experimental.Microsoft.Agents.AI` to `SGenAiActivitySources` and
  `SGenAiMeterNames` — MAF's `UseOpenTelemetry()` spans were silently dropped.
- **CapabilityEmitter restored**: Fixed pre-existing empty emitter file that broke the instrumentation generators build.
- **CodingAgent types relocated**: `CodingAgentProvider`, `CodingAgentRunRecord`, `LoomSettingsRecord` moved from
  `Qyl.Collector.CodingAgent` to `Qyl.Contracts.Loom` for cross-project sharing.
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
- Analytics hook (`use-analytics`)
- Span clusters DuckDB schema and store

### Changed

- **Loom exploration facade**: Collector `/api/v1/loom/{issueId}/explore` now runs through `LoomOrchestrator`, which
  delegates root-cause investigation to `LoomDiagnostician` and solution planning to `LoomStrategist` via keyed DI.
  Added an in-memory `LoomSessionStore` so the strategist can reuse diagnostician output without rebuilding prompts
  manually.
- **API contract ownership tightened**: Rewrote `specs/api.md` to own only cross-cutting HTTP invariants
  (errors, pagination, timestamps, IDs, auth, serialization), removed the hand-maintained route inventory
  approach from that spec, and documented runtime endpoint verification as the enforcement direction.
- **Spec alignment sweep**: Reworked `specs/api.md`, `specs/contracts.md`, `specs/cost.md`, `specs/dashboard.md`,
  `specs/issue-fingerprinting.md`, `specs/loom.md`, `specs/mcp.md`, `specs/00-architecture.md`, and the
  `loom-standalone` / `maf-native-migration` / `no-helicone` / `no-proxy` ADRs into implementation-grade documents with
  explicit migration sequences, validation hooks, and clearer current-state vs target-state boundaries.
- **No Helicone ADR made mechanical**: Rewrote `specs/decisions/no-helicone.md` as an implementation-grade ADR. It now
  defines the exact boundary between allowed deprecated OTel semconv normalization and forbidden
  Helicone/OpenLLMetry compatibility, names the collector/spec/test files that must change, sets `llm.*` ingest
  policy to raw overflow only, and specifies regression coverage for promotion, cost, and proxy/sidecar behavior.
- **Fix generation RCA budget**: `qyl.generate_fix` Phase 1 now allows up to 16 tool calls instead of 8, with the
  runtime invocation limit and prompt text sharing the same source of truth.
- **WriteJob boilerplate consolidation**: Converted all 48 write methods across 13 DuckDbStore partial files to use the
  existing `ExecuteWriteAsync` / `ExecuteWriteAsync<T>` helpers instead of manually constructing `WriteJob<int>` +
  `_jobs.Writer.WriteAsync` + `job.Task`. Net reduction of ~800 lines. `new WriteJob` now only appears in the two
  helper definitions in DuckDbStore.cs.
- **TypedResults migration**: Migrated all 29 non-Mcp `*Endpoints.cs` files in `qyl.collector` from `Results.*` to
  `TypedResults.*`. Added explicit `Task<IResult>` return type annotations to inline lambdas with mixed result types
  to satisfy delegate inference. `TypedResults.Accepted((string?)null)` used where `Results.Accepted()` had no args.
- **Dead using cleanup**: Removed unused `global using Qyl.Collector.Contracts;` from
  `src/qyl.loom/Identity/GlobalUsings.cs`.
  Investigation confirmed Contracts.cs DTOs (SpanDto, SessionDto, etc.) are only used within collector — qyl.mcp defines
  its own independent DTOs for HTTP deserialization, qyl.loom does not use them at all. Move to qyl.contracts skipped
  per policy.
- **Artifact export CLI**: `tools/export-artifact.ts` now reads the artifact API base URL from `QYL_URL` (or legacy
  `QYL_COLLECTOR_URL`) and no longer embeds a collector-specific host default.
- **Analyzer pre-filter tightened**: `AgentCallSiteAnalyzer.CouldBeAgentInvocation` now uses `HashSet<string>` method
  name lookup instead of generic `CouldBeInvocation`, avoiding expensive `GetSymbolInfo` calls on non-matching nodes.
- **Dead code removed from analyzers**: `TryFindAttributeData` replaced with `method.GetAttribute()` extension,
  `GenAiCallSiteAnalyzer.TryExtractModelName` uses `TryGetStringArgument` helper, `TracedCallSiteAnalyzer` uses
  `is not {}` pattern match, `ServiceDefaultsSourceGenerator` uses `IsMethodNamed` helper.
- **Dead NuGet package removed**: `Microsoft.AspNetCore.Authentication.JwtBearer` — zero usage in collector (auth uses
  `IdentityModel` directly).
- **Guard.NotNull consistency**: `SpanRingBuffer.PushRange` now uses `Guard.NotNull(spans)` instead of
  `ArgumentNullException.ThrowIfNull`.
- **ParseNullableLong consolidated**: Duplicate definitions in `OtlpConverter` and `CodexTelemetryMapper` now delegate
  to shared `AttributeParsing.ParseNullableLong` in `Mapping/Mappers.cs`.
- **Expression-bodied methods and ternary cleanup** across collector, copilot, mcp, and watch projects.
- **qyl documentation agents**: Added `qyl-diagram-agent.md` and `qyl-ecosystem-scout-SKILL.md` as
  reusable prompts/assets for Mermaid architecture diagrams and 5-domain ecosystem scouting.
- **Architecture docs corrected**: README and v2/spec index now describe the acyclic split of `qyl.web`,
  `qyl.collector`, `qyl.agents`, `qyl.mcp`, `qyl.infrastructure`, and `qyl.core` instead of the old collector/loom
  split.
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

### Fixed

- **qyl.loom compile blocker**: Removed stale `Qyl.Collector.ConsoleBridge` global using from
  `src/qyl.loom/Identity/GlobalUsings.cs` after the MCP console bridge cleanup, restoring solution compile.
- **GenAiCallSiteAnalyzer AzureOpenAIClient dead entry**: Removed `AzureOpenAIClient` entry with empty methods array
  that produced zero matchers. Azure.AI.OpenAI's new pattern uses `OpenAI.Chat.ChatClient` via `GetChatClient()` and
  is already covered by the OpenAI SDK entries.
- **AgentCallSiteAnalyzer invariant undocumented**: Added XML doc comment documenting that `GenAiCallSiteAnalyzer`
  handles RunAsync/RunStreamingAsync while `AgentCallSiteAnalyzer` handles InvokeAsync on the same
  `Microsoft.Agents.AI` types (disjoint method sets, overlapping types).
- **MeterAnalyzer CouldBeMeterClass undocumented**: Added remarks documenting why the predicate is kept despite being
  largely redundant with `ForAttributeWithMetadataName` (public pipeline API stability).
- **MCP safety annotations**: Applied `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` annotations to all MCP tools
  (required for Anthropic Connectors Directory). Corrected `Destructive=true` → `false` on 5 additive tools
  (CreateProjectTool, CreateApiKeyTool, AnnotateSessionTool, AnnotateTraceTool, MarkTraceReviewedTool). Removed
  unnecessary null suppression in `McpHostOptions.ResolvePublicMcpUrl`. Fixed nullability mismatch in
  `RiderMcpProxy.CallToolAsync`. Made lambdas `static` in `TraceExplorerTools`.
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

- **Dead dashboard pages**: Deleted 11 pages per architecture kill list — `BotPage`, `BotConversationDetailPage`,
  `BotUserJourneyPage`, `CodeReviewPage`, `IssueTriagePage`, `IssueFixRunsPage`, `LoomDashboardPage`, `AgentsPage`,
  `AgentRunDetailPage`, `InsightsOverviewPage`, `ResourcesPage`. Removed routes from `App.tsx`, nav items from
  `Sidebar.tsx`, exports from `pages/index.ts`. AI nav section removed entirely.
- **DomainContracts.g.cs**: Deleted unreferenced generated file and empty `Generated/` directory from
  `qyl.instrumentation.generators`. The `Qyl.Contracts.Generated` namespace had zero consumers.
- **Dead Mcp*Endpoints files**: Deleted 6 unreferenced files from `src/qyl.collector/Endpoints/` —
  `McpApiKeyEndpoints.cs`, `McpLogEndpoints.cs`, `McpMetricEndpoints.cs`, `McpSessionEndpoints.cs`,
  `McpTraceEndpoints.cs`, `McpQueryBuilder.cs`. None were registered in endpoint routing. Directory removed.
- **qyl.agents project**: Deleted. `QylAgentBuilder`, `QylCopilotAdapter`, `CopilotAdapterFactory`,
  `LlmProviderFactory`, `TrackModeRouter`, `ChunkingPipeline` — all shim layers over MAF native APIs
  (`AddAIAgent()`, `AgentWorkflowBuilder`, `MapAGUI()`). `InstrumentedChatClient` and `InstrumentedAIFunction`
  to be moved to `qyl.instrumentation` SDK.
- **qyl.workflows project**: Deleted. `DeclarativeEngine`, `WorkflowParser`, `WorkflowEngine` — shim layers
  over MAF native `AgentWorkflowBuilder` and `DeclarativeWorkflowBuilder`.
- **Collector kill-list directories**: `Copilot/`, `ClaudeCode/`, `CodingAgent/`, `Workflow/` removed from
  server per v2 architecture. Server has zero LLM dependencies.
- **DuckDbStore.ClaudeCode.cs**: Orphaned storage partial (no surviving consumer).
- **Dead MCP tools**: Deleted `BuildTools.cs`, `ConsoleTools.cs`, `AgentHandoffTools.cs` from `src/qyl.mcp/Tools/`.
  Removed service registrations from `Program.cs`, tool entries from `McpToolRegistry.cs`, and skill wiring from
  `SkillRegistrationExtensions.cs`. JSON serializer contexts (`BuildJsonContext`, `ConsoleJsonContext`) removed from
  resolver chain.
- **DuckDbExecutionStore.cs**: Implemented deleted `IExecutionStore` from qyl.workflows.
- **MCP tools referencing deleted collector endpoints**: Deleted `CopilotTools.cs`, `ClaudeCodeTools.cs`,
  `InvestigateTools.cs`, `HttpAgentProvider.cs`, `IAgentProvider.cs`, `ObservabilitySystemPrompt.cs`; removed `Copilot`
  and `ClaudeCode` skill kinds; cleaned up DI registrations, JSON contexts, and skill wiring in `Program.cs`,
  `McpToolRegistry.cs`, `SkillRegistrationExtensions.cs`, `QylSkillKind.cs`; excluded `Clear.cs` (diff scratch file)
  from compilation.
- **Dashboard**: Copilot panel/button/suggestions components, workflow pages, claude-code hooks,
  LLM status hook, related settings sections.
- **Browser extension scaffold**: Chrome extension with content script (AI text toolbar with undo toast), popup, service
  worker, shared AI client config, and Vite build pipeline (`vite.extension.config.ts`).
- **Itemized undo toast**: Browser extension undo toast shows per-change items with text previews and individual undo
  buttons (`undoByIndex()` for selective reversal).
- **Semconv comparison docs**: JS vs .NET OTel semantic convention attribute comparison (`eng/semconv/js vs net/`).
- Stray `docs/plans/index.md` (unrelated Copilot Studio localization content).
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
