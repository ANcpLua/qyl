# Changelog

## Unreleased

### Added
- **Playwright E2E tests**: smoke test suite (`e2e/smoke.spec.ts`) covering health endpoint, sidebar navigation, time range selector, theme toggle, search input, and settings page; `npm run e2e` / `npm run e2e:ui` scripts
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
