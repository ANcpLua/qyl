# Changelog

## Unreleased

### Added
- **AG-UI copilot endpoint**: `MapQylAguiChat("/api/v1/copilot/chat")` serves any `AIAgent` over AG-UI SSE protocol (CopilotKit-compatible). `AddQylAgui()` service registration, SSE streaming with `RUN_STARTED`/`TEXT_MESSAGE_*`/`RUN_FINISHED` events, `RUN_ERROR` for stream errors.
- **QylAgentBuilder**: Fluent factory for `AIAgent` instances — `FromCopilotAdapter()` (GitHub Copilot path) and `FromChatClient()` (provider-agnostic path with `InstrumentedChatClient` OTel wrapping).
- **DeclarativeEngine**: YAML `AdaptiveDialog` workflow executor using `DeclarativeWorkflowBuilder`, streaming `IAsyncEnumerable<StreamUpdate>`. Sits alongside markdown `WorkflowEngine` with same contract. In-memory `ChatClientResponseAgentProvider` bridges `IChatClient` → `ResponseAgentProvider`.
- **Browser extension scaffold**: Chrome extension with content script (AI text toolbar with undo toast), popup, service worker, shared AI client config, and Vite build pipeline (`vite.extension.config.ts`).
- **Itemized undo toast**: Browser extension undo toast shows per-change items with text previews and individual undo buttons (`undoByIndex()` for selective reversal).
- **Semconv comparison docs**: JS vs .NET OTel semantic convention attribute comparison (`eng/semconv/js vs net/`).

### Changed
- **Analyzer pre-filter tightened**: `AgentCallSiteAnalyzer.CouldBeAgentInvocation` now uses `HashSet<string>` method name lookup instead of generic `CouldBeInvocation`, avoiding expensive `GetSymbolInfo` calls on non-matching nodes.
- **Dead code removed from analyzers**: `TryFindAttributeData` replaced with `method.GetAttribute()` extension, `GenAiCallSiteAnalyzer.TryExtractModelName` uses `TryGetStringArgument` helper, `TracedCallSiteAnalyzer` uses `is not {}` pattern match, `ServiceDefaultsSourceGenerator` uses `IsMethodNamed` helper.
- **Expression-bodied methods and ternary cleanup** across collector, copilot, mcp, and watch projects.

### Fixed
- **AG-UI plan docs restored**: PR #89 accidentally overwrote AG-UI design/impl docs with Seer content; originals restored from git history.
- **Metrics doc cleaned**: Removed accidental Seer spec appendix from `andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`.
- **Missing parameter validation** in identity endpoints.

### Removed
- Stray `docs/plans/index.md` (unrelated Copilot Studio localization content).

### Added
- **Coding Agent Provider system**: Pluggable coding agent backends for autofix pipeline (Seer, Cursor, GitHub Copilot, Claude Code). Enum, DuckDB schema (`coding_agent_runs`, `seer_settings` tables), REST endpoints (`/api/v1/fix-runs/{id}/coding-agents`, `/api/v1/seer/settings`), React Query hooks, 3 UI components (`ClaudeCodeIntegrationCta`, `CodingAgentResultCard`, `SeerSettingsSection`), Seer tab in Settings, coding agent section in Issue Detail page. No `branch_name` display, provider-aware button text ("Open in Cursor" / "Open in Claude Code")
- **Playwright E2E tests**: smoke test suite (`e2e/smoke.spec.ts`) covering health endpoint, sidebar navigation, time range selector, theme toggle, search input, and settings page; `npm run e2e` / `npm run e2e:ui` scripts
- **Expanded dashboard smoke coverage**: compatibility endpoint reachability (`/api/v1/traces`, `/api/v1/genai/*`, `/api/v1/search/query`), onboarding verify endpoint assertions, `/api/v1/logs/live` stream availability, keyboard shortcut navigation/modal checks, and external APIs header/collapse semantics
- **Browser → Server trace correlation via `session.id`**: every OTLP payload from the browser SDK includes a `session.id` resource attribute (32-char hex, generated once per `init()`), grouping all telemetry from one browser tab without forcing a single mega-trace
- Per-interaction trace model: each web vital, navigation, click, and fetch gets its own trace; `session.id` on the resource handles session grouping
- Error logs now include `traceId` and `spanId` for correlation
- `/v1/logs` added to auth exclusion list for browser log ingestion
- CORS defaults to `*` when `QYL_OTLP_CORS_ALLOWED_ORIGINS` is not set
- **ADR-002 GitHub OAuth onboarding**: DuckDB token persistence, GitHub Device Flow + PAT + env var auth, 7 API endpoints, 6-step onboarding wizard, Copilot token bridge, `/health/ui` endpoint
- ADR documentation (`docs/decisions/ADR-001` through `ADR-005`)
- Embedding cluster background worker (`EmbeddingClusterWorker`)
- Bot pages: `BotPage`, `BotConversationDetailPage`, `BotUserJourneyPage`
- Analytics hook (`use-analytics`)
- Span clusters DuckDB schema and store

### Changed
- **Microsoft.Agents.AI upgrade**: `Microsoft.Agents.AI.Abstractions` → `1.0.0-rc2`, `Microsoft.Agents.AI.GitHub.Copilot` → `1.0.0-preview.260225.1` (split version variable for independent cadence)
- **Anthropic default model**: `claude-sonnet-4-20250514` → `claude-sonnet-4-6` in `LlmProviderFactory`
- **Dashboard keyboard shortcuts aligned with UI hints**: `S` now routes to Seer (instead of structured logs alias), and shortcut docs/modal reflect runtime behavior
- **Live stream status robustness**: dashboard now parses both typed SSE envelope payloads and legacy event payloads for `/api/v1/live`, restoring reliable `LIVE`/connection status transitions
- **Seer API resilience**: `/api/v1/regressions` mapping is enabled and Seer feed endpoints return `200` with empty payloads when optional storage tables are not initialized
- Auth unification: GitHub is now the only identity provider (removed cookie-based login)
- StartupBanner shows GitHub connect hint instead of login token
- Browser SDK `context.ts` now manages session context (`initSessionContext`, `getSessionId`)
- Browser SDK `transport.ts` constructor accepts `sessionId` parameter

### Removed
- `WorkflowEngine.GetExecutions()` and `GetExecution(string)` sync methods (backward-compat shims — only async variants remain)
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
