# Sentry Seer — Competitive Intelligence for Loom

> Compiled from official Sentry documentation, Sentry Kapa.ai bot interactions, and Sentry Dashboards/OTel docs audit.
> Date: 2026-04-08 (v7 — final)

---

## 1. What Seer Is (and Sentry's Broader AI Surface)

Seer is Sentry's **closed-source** AI debugging agent and a **paid add-on** to the Sentry subscription. It accesses Sentry's telemetry (issues, traces, logs, profiles) and linked GitHub codebases **as agentic resources** — not copy-pasted context blobs, but tool-exposed data sources that Seer queries on-demand during analysis (analogous to how code search is exposed as a tool). This is a critical architectural detail: Seer's context window isn't pre-stuffed, it's dynamically populated via tool calls.

**Seer's four capabilities:**

- **Autofix** — end-to-end RCA → solution → code fix pipeline
- **PR Creation** — pushes generated fixes to GitHub (requires separate Seer GitHub app)
- **Coding Agent Delegation** — hands off to external agents (e.g. Cursor)
- **Code Review** — pre-merge error prediction on GitHub PRs

**Other AI features in Sentry (not Seer-specific, included in base product):**

- **Issue Summary** — auto-generated overview of an issue highlighting what's wrong, potential cause, and insights from trace-connected issues. Uses event + issue-level metadata. Not an agent — a single-shot summarization pass.
- **Query Assistant** — NL→query translation for traces and spans data. Users describe what they want in natural language, Seer translates to structured queries and finds relevant compute metric samples. (This partially addresses the NL→query gap noted in §12 — but only for traces/spans, not logs.)
- **AI Summaries** — summarization of User Feedback submissions and Session Replays to surface common patterns. Separate from Issue Summary.

All generative AI features (Seer + the above) can be disabled org-wide via the `Show Generative AI Features` toggle in org settings.

---

## 2. Architecture: The Autofix Pipeline

Seer's Autofix is a **three-stage** sequential pipeline:

```
┌─────────────────────┐
│  Root Cause Analysis │  ← analyzes issue + code + telemetry
├─────────────────────┤
│ Solution Identific.  │  ← proposes fix steps, user can edit/remove/add
├─────────────────────┤
│   Code Generation    │  ← generates diffs, optionally opens PR
└─────────────────────┘
```

### Loom Comparison

Loom runs a **five-stage** pipeline:

```
Context Gathering → Root Cause Analysis → Solution Planning → Diff Generation → Confidence Scoring
```

Key structural differences:

| Aspect | Seer (3-stage) | Loom (5-stage) |
|--------|---------------|----------------|
| Context gathering | Implicit (built into RCA) | Explicit first stage |
| Confidence scoring | `output_confidence_score` + `proceed_confidence_score` on steps | Dedicated final stage |
| Persistence | Sentry's internal DB, exposed via API | `AutofixStepRecord` entries in DuckDB |
| Approval flow | `stopping_point` per-request | `PolicyGate` (AutoApply/RequireReview/DryRun) |
| User interaction | Mid-flow chat + comment threads | Background-to-conversation handoff via SSE |

### Stopping Point ↔ PolicyGate Mapping

| Seer `stopping_point` | Loom `PolicyGate` equivalent |
|------------------------|------------------------------|
| `root_cause` (default) | `DryRun` |
| `solution` | `RequireReview` (partial) |
| `code_changes` | `RequireReview` (full) |
| `open_pr` | `AutoApply` |

---

## 3. Seer's Agentic Codebase Tooling

Beyond consuming Sentry telemetry, Seer has **direct agentic access to codebases** with the following confirmed capabilities:

| Tool | Description |
|------|-------------|
| Grep-like search | Executes `ripgrep`-style searches across repository files |
| Documentation parsing | Reads and interprets project documentation |
| Commit history analysis | Traces and analyzes git commit history for recent changes |
| Multi-repo breaking change detection | Examines multiple repositories to catch cross-service breaking changes |
| Direct file modification | Can modify files directly when generating fixes |

This is architecturally significant: Seer operates as a **tool-using agent** with resource access, not a context-stuffed prompt. The API response confirms this — the `Retrieve Seer Issue Fix State` endpoint shows `progress` messages like `"Looking at src/seer/automation/autofix/tools/tools.py in getsentry/seer..."`, indicating real-time tool invocations during analysis.

### Implication for Loom

Loom's MCP projection model (agent isolation via MCP tool boundaries) is the same architectural pattern. The difference: Seer's tools are proprietary and internal. Loom's tools are exposed via the qyl MCP server (54+ tools) and fully inspectable.

---

## 4. Automated Scanning & Fix Flow

Seer has two automation tiers beyond manual "Find Root Cause":

### Tier 1: Automated Issue Scanning

When enabled, Seer continuously monitors all incoming issues and:

- Evaluates actionability (fixability score)
- Augments Slack/email alerts with AI analysis summaries
- Highlights the most actionable issues to reduce alert noise

### Tier 2: Automated Fixes

When "Automated Fixes" is enabled on top of scanning:

- Seer auto-triggers full Autofix pipeline (RCA → solution → code) without manual intervention
- Drafts solutions in the background
- **Nothing merges without human approval** — the guardrail is at the PR merge step, not the analysis step

### Auto-Trigger Conditions (from docs)

- Agent configured for background handoff
- Issue has 10+ events captured
- Medium-or-above fixability score

### Real-World Evidence

Seer has been used on Sentry's own codebase. In one documented case, Seer opened a PR in the `getsentry/sentry` repo to fix an exception caused by unhandled `None` values — the team reviewed the diff and merged it. This "Seer fixing itself" anecdote is used in their marketing.

### Customer Quote (Curai)

> "It's no longer one dev, one PR. I'm running Seer across all our issues in parallel. If a fix is off, no big deal—reject it, give more context, try again. Iteration is cheap, and it's saving my team days."
> — Neil Wang, Engineering Manager at Curai

### Sweet Spot Use Cases (from Sentry marketing)

- **Quick wins:** Type errors, null dereferences, missing keys, unhandled exceptions
- **Complex cross-service:** Issues involving multiple services talking to each other (Seer's trace-awareness gives it an edge here)
- **Performance:** Slow N+1 queries, detected via spans/profiles
- **Frontend → Backend:** e.g. `TypeError` in React component traced back to missing null check in API response

---

## 5. Data Sources Seer Consumes

Seer is **trace-aware** and builds connected trees across services:

| Data Source | Details |
|-------------|---------|
| Issue details | Error messages, stack traces, event metadata |
| Tracing data | Distributed traces, span trees |
| Structured logs | Beta — trace-connected logs via OTel SDK, CLI, or log drains |
| Performance data | Profiles and performance metrics |
| Session health | Session status (healthy/crashed/errored/abnormal), user counts, release-correlated health trends |
| Web Vitals | LCP, INP, CLS, FCP, TTFB — Performance Score (0–100) per page, Opportunity scoring weighted by traffic |
| Frontend assets | JS/CSS/image/font load duration, transfer size, render-blocking status via Resource Timing API |
| MCP telemetry | Tool calls, resource access, prompt usage, transport distribution, per-client traffic breakdown |
| Codebases | Linked GitHub repos (cloud only), multi-repo for distributed services |
| User feedback | Interactive mid-flow guidance |
| Rules files | Auto-parses `.cursorrules`, Windsurf, Cline, `CLAUDE.md` |

### Architectural Note: Seer Has Zero Independent Ingestion

Seer is a **pure consumer** — it has no ingestion pipeline of its own. It sits on top of whatever Sentry's collector layer already captured and queries the unified store on-demand via tool calls during Autofix runs. Vector, Fluent Bit, SDKs, OTLP direct, log drains — Seer doesn't care how data arrived. This is both a strength (zero ingestion work for the Seer team) and a constraint: **Seer's RCA quality is capped by whatever Sentry's collector preserves.** If Sentry samples, drops, or applies retention limits to data, Seer never sees what was lost.

### Relay and PII Scrubbing

All data flowing into Sentry passes through **Relay**, which applies PII scrubbing before events are stored. Advanced Data Scrubbing rules (mask, hash, replace, or remove sensitive fields) take precedence. Seer and the MCP server only ever see the **already-scrubbed version** of events. This means:

- Seer's RCA context may be missing information that was scrubbed (e.g. request bodies, user IDs, query parameters containing PII)
- There is no documented mechanism for Seer to know *what* was scrubbed or *whether* the scrubbed content was relevant to the root cause
- Overly aggressive scrubbing rules can silently degrade RCA quality with no feedback loop

### Sentry's Flywheel Strategy

Sentry's CTO has noted that every additional type of connected data "pays huge dividends" for Seer's debugging accuracy. The strategic play: Seer's quality becomes the upsell argument for sending *all* observability data (errors + traces + logs + profiles) to Sentry, not just errors. Logs are still in beta, meaning Sentry is actively expanding the surface area Seer can draw from to tighten this lock-in loop.

### Implication for Loom

qyl owns the full stack (ingestion via OTel collector → DuckDB storage → Loom RCA), so Loom has no equivalent "collector ceiling" — it queries exactly what qyl stored, with no intermediate sampling/retention layer it doesn't control. PII handling is also under the operator's control, not a third-party scrubbing layer that silently removes RCA-relevant data.

### Ingestion Paths (How Data Reaches Sentry → Seer)

```
App → Sentry SDK → Sentry (direct — primary path for errors, traces, performance)
App → OTel SDK → Sentry OTLP endpoint (direct OTLP export)
App → OTel Collector → Sentry OTLP endpoint (collector pipeline)
App → Vector → Sentry OTLP endpoint (log/trace forwarding pipeline)
App → Fluent Bit → Sentry OTLP endpoint (log/trace forwarding pipeline)
Platform → Log & Trace Drains → Sentry (Vercel, Cloudflare, Heroku, Supabase)
```

**Fluent Bit OTLP config reference:**
```yaml
pipeline:
  outputs:
    - name: opentelemetry
      match: "*"
      host: {ORG_INGEST_DOMAIN}
      port: 443
      logs_uri: /api/{PROJECT_ID}/integration/otlp/v1/logs
      tls: on
      tls.verify: on
      header:
        - x-sentry-auth sentry sentry_key={PUBLIC_KEY}
```

Once data lands in Sentry (regardless of path), Seer automatically uses it alongside other telemetry. The richer the data, the better the RCA.

### AI-Powered Log Analysis Integration Points

- **Sentry CLI** (`sentry-cli logs`) — pipe log data into AI tools
- **Sentry MCP Server** — Model Context Protocol bridge for NL queries from Claude/Cursor/VS Code
- **Seer** — automatic consumption during Autofix

---

## 6. API Surface

Seer exposes **three endpoints** (all marked experimental):

### 6.1 Start Seer Issue Fix

```
POST /api/0/organizations/{org}/issues/{issue_id}/autofix/
```

The process **runs asynchronously** — the POST returns immediately with a `run_id`, and you poll state via the GET endpoint (§6.2). Per the official docs, the issue fix process can: identify the root cause, propose a solution, generate code changes, and create a pull request with the fix. If no `stopping_point` is provided, it defaults to `root_cause` only.

**Body parameters (all optional):**

| Parameter | Type | Purpose |
|-----------|------|---------|
| `event_id` | string | Pin analysis to specific event (defaults to "recommended event") |
| `instruction` | string | Free-text NL guidance for the fix process |
| `pr_to_comment_on_url` | string | Existing PR URL where Seer should post comments |
| `stopping_point` | enum | `root_cause` \| `solution` \| `code_changes` \| `open_pr` |

**Auth:** Bearer token, `event:write` or `event:admin`
**Response:** `202` with `{ "run_id": 12345 }`

### 6.2 Retrieve Seer Issue Fix State

```
GET /api/0/organizations/{org}/issues/{issue_id}/autofix/
```

**Auth:** Bearer token, `event:read` / `event:write` / `event:admin`

**Response shape (under `autofix`):**

- `run_id`, `status` (e.g. `COMPLETED`), `updated_at`, `last_triggered_at`, `completed_at`
- `request` — original trigger context (org/project IDs, repos, tags, `options.auto_run_source`)
- `steps[]` — each step contains:
  - `id`, `index`, `key` (e.g. `root_cause_analysis_processing`, `root_cause_analysis`)
  - `status`, `type`, `title`
  - `progress[]` — timestamped messages (INFO type)
  - `insights[]`
  - `output_confidence_score`, `proceed_confidence_score`
  - `active_comment_thread`, `agent_comment_thread`, `queued_user_messages`
- `causes[]` (on RCA step):
  - `description`, `relevant_repos[]`, `reproduction_urls[]`
  - `root_cause_reproduction[]` — timeline items with `code_snippet_and_analysis`, `relevant_code_file` (path + repo), `timeline_item_type` (`human_action` | `internal_code`), `is_most_important_event`
- `codebases` — keyed by external repo ID, with `file_changes[]`, `is_readable`, `is_writeable`
- `repositories[]` — full repo metadata (integration ID, URL, provider, default branch, read/write)
- `coding_agents` — object (empty when no delegation active)

### 6.3 List Seer AI Models

```
GET /api/0/seer/models/
```

Region-specific: `us.sentry.io` or `de.sentry.io`
Docs claim no auth required, but list bearer token auth. No response schema documented (just `.`).

---

## 7. Webhook System

Seven webhook event types via `Sentry-Hook-Resource: seer`:

### Lifecycle Events

| Phase | Started | Completed |
|-------|---------|-----------|
| Root Cause Analysis | `seer.root_cause_started` | `seer.root_cause_completed` |
| Solution Generation | `seer.solution_started` | `seer.solution_completed` |
| Code Generation | `seer.coding_started` | `seer.coding_completed` |
| PR Creation | — | `seer.pr_created` |

### Common Fields (all events)

- `action` — event type string
- `data.run_id` — correlates all events in a single run
- `data.group_id` — Sentry issue ID
- `actor` — always `{ id: "sentry", name: "Sentry", type: "application" }`
- `installation.uuid`

### Payload Shapes by Event

**`root_cause_completed`:**
```
data.root_cause.description  — string
data.root_cause.steps[]      — timeline items:
  .title
  .code_snippet_and_analysis
  .timeline_item_type          ("human_action" | "internal_code")
  .relevant_code_file          { file_path, repo_name }
  .is_most_important_event     boolean
```

**`solution_completed`:**
```
data.solution.description  — string
data.solution.steps[]      — { title } only
```

**`coding_completed`:**
```
data.changes[] —
  .repo_name, .repo_external_id
  .title, .description
  .diff                        (full diff as string)
  .branch_name
```

**`pr_created`:**
```
data.pull_requests[] —
  .pull_request   { pr_number, pr_url, pr_id }
  .repo_name
  .provider       ("github")
```

---

## 8. Code Review (Pre-Merge)

Separate from Autofix — runs on GitHub PRs:

- **Auto-trigger:** On `opened` (non-draft), `ready_for_review`, and every commit while ready
- **Manual trigger:** `@sentry review` comment on PR
- **Output:** Review comments on PR + GitHub status check
- **Status check states:** Success (no errors) | Neutral (errors found) | Error (service issue) | Cancelled (superseded by new commit)
- **Recommendation:** Keep as optional check, not required in branch protection

### Permissions Required

- Pull Requests: Read & Write
- Checks: Read & Write

---

## 9. Seer in the Sentry UI

From Sentry bot intel + dashboard docs audit:

- **Issue Details sidebar** — dedicated Seer section with AI debugging features
- **Initial Guess** — automatic pre-analysis that runs before the user clicks anything. Shown as a panel containing a starting hypothesis about the issue, with a "Find Root Cause" button beneath it. This means Seer is doing lightweight analysis on every viewed issue, not just on-demand.
- **Issue Summary** — auto-generated quick overview on issue pages (what's wrong, potential cause, trace-connected insights)
- **AI Summaries** — summarization of User Feedback and Session Replays to surface common patterns
- **"Find Root Cause" button** — manual Autofix trigger on any issue (beneath the Initial Guess panel)
- **Automated Fixes** — if enabled, Seer pre-populates root cause + fix before the user even opens the issue
- **Coding Agent Handoff** — from the Autofix panel, send root cause to external coding agents (e.g. Cursor) for implementation
- **Query Assistant** — NL→structured query for traces/spans data, finds relevant samples without manual query building
- **Experimental: `Cmd + /`** — opens Seer NL interface anywhere in Sentry for querying, investigations, and triage
- **Auto-trigger conditions:** Agent configured for background handoff + issue has 10+ events + medium-or-above fixability score
- **Slack integration (beta):** "Fix with Seer" button on issue alert messages, results posted to thread
- **AI Dashboards** — dedicated hub linking to AI Agents dashboards (agent workflows, token usage, tool calls, model costs) and MCP dashboards (see §13)
- **MCP Dashboards** — four sub-dashboards: Overview (traffic, client distribution, transport protocol), Tools (call counts, errors, p95 latency), Resources (access patterns), Prompts (usage, errors, latency)

---

## 10. Pricing, Constraints & Deployment Boundaries

### Pricing

- **Model:** Seer is a paid **add-on** to the Sentry subscription, using active contributor pricing — anyone with 2+ PRs/month in a Seer-enabled project is billed
- **Seer-enabled:** Repo connected to Sentry with any Seer feature turned on

### SCM Constraints

- **SCM:** GitHub cloud only (no self-hosted GitHub, no GitLab, no Bitbucket)

### Self-Hosted vs. SaaS Boundary

This is a critical architectural boundary:

- **Seer is closed-source and SaaS-only.** It is not available on self-hosted Sentry — not partially, not in degraded mode, not at all.
- **Sentry MCP Server works on self-hosted**, but via stdio transport only (not the hosted cloud endpoint), and **Seer skills must be explicitly disabled**:

```bash
npx @sentry/mcp-server@latest \
  --access-token=YOUR_TOKEN \
  --host=sentry.example.com \
  --disable-skills=seer
```

This means self-hosted Sentry users get MCP connectivity to their data (for use with Claude, Cursor, VS Code, etc.) but are explicitly locked out of Seer's AI debugging capabilities. The `--disable-skills=seer` flag is not optional — Seer skills will fail on self-hosted because they call SaaS-only APIs.

### Privacy & Controls

- **Privacy:** Sentry does not train generative AI models using customer data by default and without permission. AI-generated output is shown only to authorized users in the account.
- **PII:** All data passes through Relay for Advanced Data Scrubbing before storage. Seer and MCP only see already-scrubbed data (see §5).
- **Global kill switch:** `Show Generative AI Features` toggle in org settings (disables **all** generative AI features, not just Seer)
- **Granular controls:** Per-feature enable/disable per project/repo, advanced settings to block PR creation and alert augmentation

---

## 11. Loom's Competitive Wedges (Summary)

Based on all documented Seer capabilities:

| Wedge | Loom Advantage |
|-------|----------------|
| **Self-hosted** | Seer is closed-source, SaaS-only. Self-hosted Sentry users get MCP but must run `--disable-skills=seer`. Loom is fully self-hostable with zero feature degradation. |
| **Pricing** | Seer charges per active contributor (2+ PRs/month). Loom is free, MIT-licensed. |
| **SCM lock-in** | Seer requires GitHub cloud. Loom is SCM-agnostic. |
| **Backend** | Seer uses Sentry's proprietary backend. Loom uses DuckDB as single backend for traces/metrics/logs. |
| **Pipeline depth** | Seer: 3 stages. Loom: 5 stages with explicit context gathering and confidence scoring. |
| **Approval model** | Seer: per-request `stopping_point`. Loom: configurable `PolicyGate` (AutoApply/RequireReview/DryRun). |
| **Handoff UX** | Seer: in-page chat + Slack thread. Loom: background-to-conversation SSE handoff with full context hydration ("Attach & Continue Chat"). |
| **OTel-native** | Seer consumes OTel data via Sentry's collector. qyl/Loom is OTel-first with DuckDB as native OTel backend. |
| **Data ownership** | Seer has zero independent ingestion — RCA quality is capped by Sentry's sampling/retention. qyl owns the full stack (collector → DuckDB → Loom), so Loom queries exactly what was stored with no intermediate ceiling. |
| **PII transparency** | Sentry's Relay scrubs PII before storage; Seer sees already-scrubbed data with no visibility into what was removed or whether it was RCA-relevant. qyl's operator controls PII handling directly — no silent third-party scrubbing layer. |
| **Transparency** | Seer is closed-source; its API is "experimental and may change". Loom's pipeline is open-source and inspectable. |
| **OTel SDK breadth** | Sentry's OTel linking (`propagateTraceparent`, OTLP Integration) is live for Python and Ruby only; Go and PHP "coming soon"; **.NET is absent entirely**. qyl is .NET-first with native OTel instrumentation via Roslyn source generators. |
| **MCP observability depth** | Sentry's MCP dashboards show traffic/tools/resources/prompts as read-only charts. qyl's MCP server (54+ tools) exposes the same telemetry as agentic resources Loom can query during RCA — not just display. |
| **Dashboard editability** | Sentry's built-in dashboards cannot be edited (only duplicated to custom). qyl dashboards are fully open/extensible. |

---

## 12. Known Seer Gaps (Loom Opportunities)

From Autofix docs, API analysis, dashboard docs audit, and Kapa.ai bot interactions:

- No documented session management (retrieve/list/resume past Autofix runs beyond single-issue GET)
- **Partial** NL→query translation: Query Assistant handles traces/spans, but no documented NL→query for **logs** or **metrics** exploration. The `Cmd + /` experimental UI may expand this, but log-level NL querying remains a Loom opportunity.
- No documented anomaly detection or proactive alerting from AI analysis
- No documented multi-model orchestration or model selection transparency (the `/seer/models/` endpoint exists but response schema is undocumented)
- Webhook payloads are relatively flat — `solution_completed` steps only have titles, no structured rationale
- No documented confidence thresholds or automatic escalation policies
- Code Review and Autofix are separate flows with no documented cross-pollination (e.g. Code Review doesn't feed back into Autofix learning)
- **No .NET OTel integration** — Sentry's `propagateTraceparent` and OTLP Integration cover JavaScript, Python, Ruby, mobile SDKs; Go and PHP are "coming soon"; .NET is completely absent from the OTel linking story. This is a structural gap for any shop running ASP.NET Core / .NET backends with OTel instrumentation — they can't get end-to-end Sentry+OTel traces without manual workarounds.
- **MCP dashboards are display-only** — Sentry's four MCP sub-dashboards (Overview, Tools, Resources, Prompts) surface metrics like call counts, error rates, and p95 latency, but there's no documented path from MCP telemetry into Seer's RCA pipeline. MCP failures don't auto-trigger Autofix. Loom's MCP projection model feeds the same telemetry directly into the RCA context-gathering stage.
- **Dynamic Sampling opacity** — Sentry's built-in dashboards are affected by Dynamic Sampling, and there's no documented mechanism for Seer to know what data was sampled away. Seer's RCA operates on whatever survived sampling, with no visibility into what was dropped. qyl's event-sourced core stores everything with no sampling layer.
- **PII scrubbing opacity** — Relay applies Advanced Data Scrubbing before storage. Seer sees the scrubbed result with no documented mechanism to know what was removed, whether the scrubbed content was relevant to the root cause, or how scrubbing affected RCA quality. Overly aggressive scrubbing silently degrades Seer's analysis with no feedback loop. qyl's operator controls PII handling directly.
- **Session health → Seer disconnect** — Session Health dashboards track crashed/errored/abnormal sessions with release correlation, but there's no documented integration where session health regressions auto-trigger Seer investigation. The dashboards and Seer are parallel surfaces, not connected workflows.
- **Asset performance → Seer disconnect** — Frontend asset monitoring (render-blocking detection, size tracking, duration analysis) is dashboard-only. No documented path where slow/failing assets feed into Seer's RCA or trigger Autofix.
- **Web Vitals → Seer disconnect** — Performance Score (0–100), Opportunity scoring, and per-page Web Vital breakdowns are dashboard-only. Seer doesn't consume Web Vital regressions as RCA signals — a CLS spike or LCP regression won't trigger an investigation.
- **Initial Guess is lightweight only** — the automatic pre-analysis on Issue Details provides a starting hypothesis, but there's no documented path for it to feed richer context back into a full Autofix run. It appears to be a separate single-shot pass, not a warm-start for the 3-stage pipeline.

---

## 13. Sentry's Observability Surface (What Seer Draws From)

Understanding Sentry's full dashboard taxonomy reveals both the breadth of data available to Seer and the gaps where dashboards and Seer don't connect.

### Dashboard Taxonomy

**App-Wide:** Outbound API Requests (HTTP response duration, 3xx/4xx/5xx rates), Domain Details (drill-down per domain).

**Frontend (5 dashboards):** Frontend Overview (Best Page Opportunities, Most Time-Consuming Assets, p50/p75 duration), Web Vitals (Performance Score 0–100, log-normal distribution, separate desktop/mobile weight tables — LCP 30%, INP 30%, CLS 15%, FCP 15%, TTFB 10%, Opportunity scoring weighted by traffic), Assets (JS/CSS/image/font duration, size, render-blocking status, URL parameterization for grouping, drill-down Asset Summary → Sample List → Trace View), Session Health (Unhealthy Sessions, Session/User Counts by status: healthy/crashed/errored/abnormal — frontend "crashed" = unhandled errors, "errored" = handled errors, mutually exclusive).

**Backend (4 dashboards):** Backend Overview (Most Time-Consuming Queries/Domains, p50/p75), Queries (throughput, avg duration, drill-down to query summary), Caches (hit/miss rates, throughput, latency), Queues (publish/processing latency, error rates, throughput).

**Mobile (5 dashboards):** Mobile Vitals (cold/warm starts, slow/frozen frames, TTID/TTFD), Mobile Session Health (crash-free session/user rates with release annotations), App Starts, Screen Rendering (slow frames >16.7ms, frozen >700ms), Screen Loads (TTID/TTFD per screen).

**Framework-Specific:** Next.js Overview (SSR tree view, rage/dead clicks), Laravel Overview.

**AI (2 dashboards):** AI Agents (agent workflows, token usage, tool calls, model costs), MCP (Overview with traffic/client/transport distribution, Tools with call counts/errors/p95 latency, Resources, Prompts).

### Key Observation for Loom

Sentry's dashboard surface is **wide but shallow for AI integration**. The dashboards provide excellent human-facing visualization, but Seer's documented data consumption is limited to issues, traces, logs, profiles, and codebases (§5). The richer dashboard-level aggregates — Web Vital Performance Scores, asset render-blocking analysis, session health trends, MCP tool error rates, cache hit ratios — are not documented as Seer RCA inputs. They're parallel read-only surfaces.

Loom's architecture collapses this gap: because qyl stores all telemetry in DuckDB and Loom's context-gathering stage queries DuckDB directly, every metric that powers a dashboard is also available as an RCA signal. There's no "dashboard layer" that's disconnected from the RCA engine.

---

## 14. Sentry's OTel Integration Landscape

### Trace Linking (Sentry SDK ↔ OTel Backend)

For apps using Sentry SDKs on frontend/mobile with OTel-instrumented backends, `propagateTraceparent` sends the W3C `traceparent` header to link into a single distributed trace.

**Supported SDKs:** All major JavaScript frameworks (Browser JS, Angular, Astro, Ember, Gatsby, Next.js, Nuxt, React, React Router, Remix, Solid, SolidStart, Svelte, SvelteKit, Vue, Wasm), plus mobile (Android, Flutter, Native, React Native).

### OTLP Integration (Same-Service Coexistence)

For backends running both Sentry SDK and OTel instrumentation in the same process, the OTLP Integration forces shared trace IDs so Sentry errors link to OTel traces.

**Live:** Python, Ruby.
**Coming Soon:** Go, PHP.
**Absent:** .NET, Java, Rust, Elixir.

### Implication for Loom

The .NET gap is structurally significant. Any team running ASP.NET Core with OTel instrumentation (which is the standard .NET observability pattern) cannot get automatic Sentry↔OTel trace linking. They'd need to either:
1. Abandon OTel and go Sentry-SDK-only, or
2. Accept disconnected traces

qyl is .NET 10 native with Roslyn source-generated OTel instrumentation (`MeterEmitter`, `Qyl.Instrumentation.Generators`). There's no "linking" problem because qyl *is* the OTel backend — traces, metrics, and logs all land in DuckDB via the OTel collector with zero SDK-level integration needed.

---

## 15. Early Adopter Pipeline (Sentry's Near-Term Evolution)

Sentry's Early Adopter opt-in (toggled org-wide via Settings → General Settings, features roll out in waves) surfaces upcoming capabilities. Two sources give partially different snapshots:

### From docs page (2026-04-07 live fetch)

- **Issue Views** — custom issue list layouts
- **Issue Status tags** — richer triage state
- **Span Summary** — aggregated span-level insights
- **Dynamic Alerts** — adaptive alerting thresholds
- **New Trace Explorer with Span Metrics** — enhanced trace querying with metric-level drill-down
- **Size Analysis** — bundle/asset size tracking
- **Uptime Monitoring Verification** — validation layer for uptime checks

### From Kapa.ai bot (2026-04-07 query)

- **Seer Slack Workflows** — listed under AI & Automation category
- **Prebuilt Sentry Dashboards** — listed under Dashboards category

The discrepancy likely reflects wave-based rollout (the page warns features may not appear immediately) or Kapa.ai training on a different docs snapshot. Both lists are included here for completeness.

### What This Signals for Loom

**Seer Slack Workflows** (Kapa.ai) is directly relevant — it confirms Sentry is investing in Seer→Slack integration beyond the current beta "Fix with Seer" button, moving toward full workflow automation in Slack. Loom's SSE-based handoff model is a different UX paradigm but should be compared against a maturing Slack-first experience.

**New Trace Explorer with Span Metrics** (live docs) is the most strategically relevant infrastructure feature — it suggests Sentry is moving toward richer span-metric querying, which could eventually feed into Seer's context. If Seer gets access to span-level metric aggregates (not just raw spans), its RCA quality for performance issues would improve significantly. Loom already has this via DuckDB queries over OTel metric data.

**Dynamic Alerts** is also notable — adaptive thresholds are a prerequisite for "anomaly detection triggers Seer" workflows. This is currently a gap (§12), but the Early Adopter pipeline suggests Sentry is building toward it.

This list explicitly excludes alphas, closed betas, and manual-opt-in features, so there may be additional AI/Seer evolution not visible here.
