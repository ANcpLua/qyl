# Seer Knowledge Base

Extracted from Sentry's public Loom plugin, MCP server, and Seer backend.
Source material: `src/loom/` (deleted after extraction), `~/sentry-seer-sourcepack/` (external).

## Companion Documents (archived)

Deep-dive references were extracted from `~/sentry-seer-sourcepack/` and later consolidated into this file.
The individual companion files (`seer-explorer-tools.md`, `seer-autofix-pipeline.md`, `seer-code-review.md`,
`seer-algorithms.md`, `seer-api-surface.md`) were deleted during the 2026-03-15 repo cleanup.
Key information is preserved in the sections below.

## 1. MCP Tool Catalog

Sentry's MCP server exposes tools grouped into 5 skills. qyl.mcp should implement equivalent coverage.

### Skill: inspect (16 tools, default enabled)

| Tool | Purpose | Scopes |
|------|---------|--------|
| `find_organizations` | List/search orgs (max 25) | org:read |
| `find_projects` | List/search projects (max 25) | project:read |
| `find_releases` | List releases, find recent versions | project:read |
| `find_teams` | List/search teams | team:read |
| `get_event_attachment` | Download event attachments or list them | event:read |
| `get_issue_details` | Full issue details by ID, URL, or event ID | event:read |
| `get_issue_tag_values` | Tag value distribution for an issue (url, browser, os, env, release) | event:read |
| `get_qyl_resource` | Generic resource fetch by URL or type+ID, supports breadcrumbs sub-resource | event:read |
| `get_trace_details` | Trace overview by trace ID (32-char hex) | event:read |
| `list_events` | Direct query syntax search across datasets (errors, logs, spans) | event:read |
| `list_issue_events` | Direct query syntax search within a specific issue | event:read |
| `list_issues` | Direct query syntax issue search | event:read |
| `search_events` | NL→query translation for events AND aggregations (counts, sums, avgs) | event:read |
| `search_issue_events` | NL→query for events within a specific issue | event:read |
| `search_issues` | NL→query for grouped issue lists (NOT counts) | event:read |
| `whoami` | Authenticated user identity | (none) |

### Skill: loom (9 tools, default enabled)

| Tool | Purpose | Scopes |
|------|---------|--------|
| `analyze_issue_with_loom` | AI root cause analysis + code fixes (polling state machine, 5min timeout) | (none) |
| `find_organizations` | (shared) | org:read |
| `find_projects` | (shared) | project:read |
| `get_issue_details` | (shared) | event:read |
| `list_events` | (shared) | event:read |
| `list_issues` | (shared) | event:read |
| `search_events` | (shared) | event:read |
| `search_issues` | (shared) | event:read |
| `whoami` | (shared) | (none) |

### Skill: docs (5 tools, default disabled)

| Tool | Purpose | Scopes |
|------|---------|--------|
| `search_docs` | Search SDK documentation (POST /api/search, 15s timeout) | (none) |
| `get_doc` | Fetch specific doc page by path | (none) |
| `find_organizations` | (shared) | org:read |
| `find_projects` | (shared) | project:read |
| `whoami` | (shared) | (none) |

### Skill: triage (6 tools, default disabled)

| Tool | Purpose | Scopes |
|------|---------|--------|
| `update_issue` | Resolve, assign, comment on issues | event:write |
| `find_organizations` | (shared) | org:read |
| `find_projects` | (shared) | project:read |
| `get_issue_details` | (shared) | event:read |
| `search_issues` | (shared) | event:read |
| `whoami` | (shared) | (none) |

### Skill: project-management (5 tools, default disabled)

| Tool | Purpose | Scopes |
|------|---------|--------|
| `create_project` | Create new project in org | project:write |
| `create_team` | Create new team in org | team:write |
| `create_dsn` | Create DSN for a project | project:write |
| `find_dsns` | List DSNs for a project | project:read |
| `update_project` | Modify project settings | project:write |

### Meta-tool: use_qyl (agent-only)

Embedded AI agent that chains multiple MCP tools via in-memory transport.
- Creates `InMemoryTransport.createLinkedPair()` for client↔server
- Excludes itself (anti-recursion) and simple replacement tools
- Returns text result + optional tool call trace
- Pattern: build internal MCP server → connect client → get tools → run agent → cleanup

### Tool Annotations (Directory Requirements)

Every tool must declare:
```json
{
  "readOnlyHint": true|false,
  "destructiveHint": true|false,
  "openWorldHint": true|false
}
```

## 2. Seer API Surface

Sentry's Seer is a separate Python service. These are the API endpoints it exposes.
qyl runs algorithms locally — no separate service needed — but the API surface documents capabilities.

### Autofix & Analysis

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/v1/automation/summarize/trace` | POST | AI trace summarization (7-day cache) |
| `/v1/automation/summarize/issue` | POST | AI issue summarization |
| `/v1/automation/codegen/unit-tests` | POST | Automated test generation |
| `/v1/automation/explorer/index` | POST | Explorer context indexing |
| `/v1/automation/explorer/index/org-project-knowledge` | POST | Project knowledge indexing |

### Similarity & Grouping

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/v0/issues/supergroups` | POST | Supergroups embedding trigger |
| (configurable SEER_SIMILAR_ISSUES_URL) | POST | Similar issues via embedding distance |

### Anomaly Detection

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/detect-anomalies` | POST | Time-series anomaly detection |
| `/v1/workflows/compare/cohort` | POST | Distribution comparison (baseline vs outlier) |

### Assisted Query

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/v1/assisted-query/translate` | POST | NL → query syntax translation |
| `/v1/assisted-query/translate-agentic` | POST | Agentic NL translation |
| `/v1/assisted-query/start` | POST | Start search agent run |
| `/v1/assisted-query/state` | POST | Get search agent state |
| `/v1/assisted-query/create-cache` | POST | Create query cache |

### Infrastructure

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/v1/models` | GET | List available LLM models |
| `/v1/llm/generate` | POST | Generic LLM generate (provider, model, prompt, schema) |
| `/v1/project-preference/remove-repository` | POST | Remove repository from project |
| `/v1/explorer/service-map/update` | POST | Update service topology map |
| `/trends/breakpoint-detector` | POST | Statistical breakpoint detection in time series |

### Authentication Pattern

```
HMAC-SHA256 request signing:
  signature = hmac(shared_secret, body, sha256).hexdigest()
  Authorization: Rpcsignature rpc0:{signature}

Viewer context (optional):
  X-Viewer-Context: json({organization_id, user_id})
  X-Viewer-Context-Signature: hmac(shared_secret, context_bytes, sha256)
```

## 3. Algorithms Not Yet in qyl.loom

### 3.1 Stacktrace Processing Pipeline

From `similarity/utils.py` (678 lines). Extracts normalized stacktrace strings for embedding similarity.

**Algorithm:**
1. Reverse-iterate exceptions (prioritize recent), limit 30 exceptions, 30 frames/exception
2. Filter by `contributes` flag from grouping info
3. Strip base64-encoded frames, compiler-generated code, minified HTML
4. Platform-specific rules: JS, Python, PHP bypass frame-count filters for backfill compat
5. If fully minified → truncate to 20 frames (high token density)
6. Two-stage token counting: fast string-length check → expensive tokenizer if long
7. Tokenizer: `jina-embeddings-v2-base-en`, thread-safe lazy singleton (double-check locking)

**qyl equivalent needed:** Stacktrace normalization for embedding-based issue similarity.
Current `ErrorFingerprinter.cs` does SHA-256 fingerprinting but not embedding-ready normalization.

### 3.2 Circuit Breaker for Similarity Requests

From `similarity/similar_issues.py`. Pattern for resilient embedding lookups.

**Algorithm:**
1. `CircuitBreaker` wraps Seer embedding API calls
2. Configurable retries (default 2, 0.5s backoff)
3. On `TimeoutError`/`MaxRetryError` → increment circuit breaker, return empty
4. Race-condition handling: if hash lookup fails because group was deleted during ingest:
   - Check hash age (60s threshold)
   - If stale → trigger async hash deletion
   - Track outcome metrics (similar_groups_found, matching_group_found, error, redirect)

**qyl equivalent needed:** `EmbeddingClusterWorker.cs` does greedy cosine similarity but lacks circuit breaker and race-condition recovery.

### 3.3 Breakpoint Detection

From `breakpoints.py`. Statistical change-point detection in time series.

**Data model:**
```python
BreakpointData:
  project: str
  transaction: str  # function or transaction name
  aggregate_range_1: float  # before breakpoint
  aggregate_range_2: float  # after breakpoint
  unweighted_t_value: float
  unweighted_p_value: float
  trend_percentage: float
  absolute_percentage_change: float
  trend_difference: float
  breakpoint: int  # timestamp
```

**qyl equivalent needed:** Currently regression detection uses deployment-based checks.
Breakpoint detection would add statistical time-series change detection independent of deployment events.

### 3.4 Assisted Query Translation

From `assisted_query/` (3 files, ~1000 lines). Translates natural language to platform search syntax.

**Key patterns:**
- Static value sets: IS (resolved/unresolved/archived), PRIORITY, FIXABILITY, CATEGORIES
- Event context fields: device class, error type, timestamps, memory usage
- Dynamic field value discovery: fetches unique values for categorical fields
- Semantic field mapping: maps common user queries to internal schema
- Transaction/metric exploration: builds queries for performance data with breakdown dimensions

**qyl equivalent needed:** `qyl.mcp` natural language → DuckDB query translation.
This is the most complex untranspiled feature.

### 3.5 Trace Summary Caching

From `trace_summary.py`. AI trace summarization with cache.

**Pattern:**
- Cache key: `ai-trace-summary:{traceId}`
- TTL: 7 days
- Response: snake_case → camelCase conversion
- Feature-gated per organization

**qyl equivalent:** Loom already does trace-level analysis but doesn't cache AI summaries.
Simple addition to `LoomExplorerService.cs`.

### 3.6 Supergroups / Issue Grouping via Embeddings

From `supergroups.py` + similarity pipeline. Groups similar issues by embedding distance.

**Pattern:**
1. On new issue → trigger embedding request to Seer
2. Seer computes embedding → finds similar groups by distance
3. Distance threshold: `SEER_MAX_GROUPING_DISTANCE` (configurable)
4. Results sorted by descending stacktrace similarity

**qyl equivalent:** `EmbeddingClusterWorker.cs` does GenAI span clustering.
Issue-level grouping by embedding similarity is not yet implemented.

## 4. Seer Request/Response Types

Key TypedDicts from `signed_seer_api.py` for qyl contract reference:

```
SummarizeTraceRequest { trace_id, only_transaction, trace: { trace_id, trace[] } }
SummarizeIssueRequest { group_id, issue, trace_tree?, organization_slug, organization_id, project_id }
SupergroupsEmbeddingRequest { organization_id, group_id, artifact_data }
ServiceMapUpdateRequest { organization_id, nodes[], edges[] }
UnitTestGenerationRequest { repo, pr_id }
TranslateQueryRequest { org_id, org_slug, project_ids[], natural_language_query }
SearchAgentStartRequest { org_id, org_slug, project_ids[], natural_language_query, strategy, user_email?, timezone?, options? }
CompareDistributionsRequest { baseline[], outliers[], total_baseline, total_outliers, config, meta }
LlmGenerateRequest { provider, model, referrer, prompt, system_prompt, temperature, max_tokens, response_schema? }
OrgProjectKnowledgeProjectData { project_id, slug, sdk_name, error_count, transaction_count, instrumentation[], top_transactions[], top_span_operations[] }
```

## 5. Autofix Status State Machine

Terminal states (no more updates expected):
- `COMPLETED`, `FAILED`, `ERROR`, `CANCELLED`
- `NEED_MORE_INFORMATION`, `WAITING_FOR_USER_RESPONSE` (human intervention)

Non-terminal states:
- `PENDING`, `PROCESSING`, `IN_PROGRESS`

Polling: 5s interval, 5min timeout, 3 max retries with exponential backoff (1s initial).
Retry policy: retry on 5xx or network errors, skip on 4xx client errors.

## 6. Anomaly Detection Types

```
AnomalyType: HIGH_CONFIDENCE | LOW_CONFIDENCE | NONE | NO_DATA
Sensitivity: LOW | MEDIUM | HIGH
Seasonality: AUTO | HOURLY | DAILY | WEEKLY | HOURLY_DAILY | HOURLY_WEEKLY | DAILY_WEEKLY | HOURLY_DAILY_WEEKLY
Direction: ABOVE | BELOW | ABOVE_AND_BELOW

TimeSeriesPoint { timestamp, value, anomaly?, yhat_lower?, yhat_upper? }
AnomalyDetectionConfig { time_period, sensitivity, direction, expected_seasonality, aggregate? }
```

## 7. Architecture Patterns Worth Adopting

### In-Memory MCP Transport for Embedded Agent

Sentry creates a linked pair of in-memory transports so an embedded AI agent can call MCP tools
without network overhead. Pattern:

```
[clientTransport, serverTransport] = InMemoryTransport.createLinkedPair()
server = buildServer({ context, tools: filteredTools })
server.connect(serverTransport)
mcpClient = createMCPClient({ transport: clientTransport })
tools = mcpClient.tools()
result = runAgent({ request, tools })
```

qyl equivalent: `qyl.mcp` already uses `ModelContextProtocol.AspNetCore`. Consider adding
in-process tool invocation for `qyl.loom` → MCP tool access without HTTP.

### Skills-Based Authorization

5 skill categories control tool visibility:
- `inspect` (default) — read-only issue/event/trace exploration
- `loom` (default) — AI analysis
- `docs` (default) — documentation search
- `triage` (opt-in) — issue state changes
- `project-management` (opt-in) — project/team/DSN management

Each tool declares which skills enable it. Tool is visible if ANY of its skills are active.
qyl.mcp already has this concept.

### OTel Semantic Namespace Data

80+ JSON files with OTel semantic conventions bundled at build time.
Used by embedded AI agent for NL→query translation.
Structure: `{ namespace, description, attributes: { "attr.name": { type, description, examples } } }`

qyl already has semconv data via `qyl.protocol/Attributes/Generated/`.
Consider bundling a JSON export for MCP tool guidance.

## 8. Undocumented UI API Surface (from network waterfall analysis)

Extracted from Sentry performance JSON exports (18 files, 3 capture sessions).
Noise domains stripped (pendo, amplitude, stripe, statuspage, zendesk, ingest).
22 unique Sentry API endpoints found, 11 already documented above, 11 new.

### High-value gaps (relevant to qyl)

| Endpoint | Latency | What it is | qyl equivalent |
|----------|---------|-----------|----------------|
| `/api/0/organizations/{org}/integrations/coding-agents/` | 1341ms | Coding agent integration registry | qyl.loom `AutofixAgentService` — missing integration listing |
| `/api/0/assistant/` | 425ms | AI assistant API (chat-style) | qyl copilot — already implemented differently |
| `/api/0/organizations/{org}/dashboards/` | 169ms | Saved dashboard CRUD | No saved-dashboard API in qyl.collector |
| `/api/0/organizations/{org}/repos/` | 214ms | Repository listing for org | qyl.loom needs for code review context |
| `/api/0/organizations/{org}/config/integrations/` | 460ms | Available integration catalog | Integration registry pattern — not in qyl |
| `/api/0/organizations/{org}/group-search-views/starred/` | 153ms | Saved/starred issue search views | Bookmarked queries — not in qyl |

### SaaS-only (not relevant to qyl)

| Endpoint | Latency | Purpose |
|----------|---------|---------|
| `/api/0/organizations/{org}/pendo-details/` | 959ms | Pendo tracking integration |
| `/api/0/organizations/{org}/promotions/trigger-check/` | 266ms | Marketing promotion eligibility |
| `/api/0/organizations/{org}/prompts-activity/` | 152ms | Onboarding prompt state |
| `/api/0/organizations/{org}/broadcasts/` | 1441ms | System-wide announcements |
| `/api/0/organizations/{org}/forwarding/` | 142ms | Event forwarding config |

### Source data

- Raw: `/Users/ancplua/Downloads/seer/{1,2,3}/*.json`
- Deduplicated: `/Users/ancplua/Downloads/seer/sentry_api_surface.csv`
- Gaps only: `/Users/ancplua/Downloads/seer/sentry_feature_gaps.csv`

## 9. Autofix Pipeline Prompts & Schemas

### Step Pipeline

5 sequential steps, each producing a structured artifact:

1. **ROOT_CAUSE** → `RootCauseArtifact { one_line_description, five_whys[], reproduction_steps[] }`
2. **SOLUTION** → `SolutionArtifact { one_line_summary, steps[{ title, description }] }`
3. **CODE_CHANGES** → No artifact (reads file_patches from Explorer state)
4. **IMPACT_ASSESSMENT** → `ImpactAssessmentArtifact { one_line_description, impacts[{ label, rating, impact_description, evidence }] }`
5. **TRIAGE** → `TriageArtifact { suspect_commit?, suggested_assignee? }`

### Prompt Templates

**Root Cause:** Analyze issue, ask "why" repeatedly, output one_line_description (<30 words) + five_whys + reproduction_steps (<15 words each).

**Solution:** Review root cause, pick most pragmatic approach, do NOT include testing/implementation. Output: one_line_summary + ordered steps.

**Code Changes:** Review root cause + solution, use code editing tools. Minimal and focused.

**Impact Assessment:** Check upstream/downstream dependencies, metrics, connected issues. Output: one_line_description + impacts[{ label, rating(low/medium/high), impact_description, evidence }].

**Triage:** Look at recent commits, code ownership. Output: suspect_commit { sha(7), repo, message, author, date } + suggested_assignee { name, email, why }.

### Fixability Score Thresholds

| Level | Score |
|-------|-------|
| SUPER_HIGH | >= 0.76 |
| HIGH | >= 0.66 |
| MEDIUM | >= 0.40 |
| LOW | >= 0.25 |
| SUPER_LOW | >= 0.0 |

### Automation Tuning

Per-project: `off` | `medium` | `always` (super_low/low/high deprecated).
Stopping points: `code_changes` | `open_pr` (root_cause/solution deprecated).

### Autofix Settings API

```
GET  /organizations/{org}/autofix/automation-settings/  → paginated, searchable
POST /organizations/{org}/autofix/automation-settings/  → bulk update (max 1000)
```

### Autofix Referrers

`api.group_ai_autofix`, `issue_summary.fixability`, `issue_summary.alert_fixability`, `issue_summary.post_process_fixability`, `slack`, `unknown`.
