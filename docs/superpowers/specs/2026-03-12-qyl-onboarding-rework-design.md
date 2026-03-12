# qyl Onboarding Rework — Design Specification

**Version:** 0.0.1-beta
**Date:** 2026-03-12
**Status:** Approved

## Overview

Complete rework of the qyl onboarding system. Two entrances — dashboard wizard and MCP client connector — converge on a single OAuth flow through Keycloak Identity Brokering. No API keys, no PATs, no static configuration pages.

The goal: match everything Sentry does for their MCP connector that is not SaaS-specific, adapted for qyl's observability platform and Conversational Loom.

## Reference Screenshots

The following screenshots were provided inline during the design conversation and serve as the visual specification for this rework:

### BridgeMind Onboarding Flow (Reference for Dashboard Wizard)

| # | Content | Design Reference |
|---|---------|-----------------|
| 1 | BridgeMind welcome page (`app.bridgemind.ai/onboarding`) — clean "Get Started" CTA | Welcome step layout, minimal copy, single primary button |
| 2 | BridgeMind OAuth provider selection — GitHub, Azure, Google buttons | OAuth step layout (qyl uses same 3 providers via Keycloak) |
| 3 | BridgeMind feature showcase — displays platform capabilities after auth | Features step — show qyl capabilities post-login |
| 4 | BridgeMind validation/doctor step — background health checks | Doctor step pattern — run checks, show pass/fail inline |

### Sentry MCP Connector Flow (Reference for MCP Directory Integration)

| # | Content | Design Reference |
|---|---------|-----------------|
| 5 | Sentry MCP documentation page (`docs.sentry.io/ai/mcp/`) — setup instructions for all clients | Landing page at `mcp.qyl.dev` structure |
| 6 | Sentry listed in claude.ai Customize > Integrations connector directory | qyl must appear here — follow Anthropic directory submission requirements |
| 7 | Sentry connector detail modal in claude.ai — MCP URL, documentation link, tools list, privacy policy | qyl connector modal content |
| 8 | Sentry OAuth consent page (`sentry.io/oauth/authorize/`) — organization access request with permission list | Custom consent page at `mcp.qyl.dev/consent` |
| 9 | GitHub OAuth authorization for Sentry — "Authorize Sentry" via Identity Brokering | Keycloak renders GitHub/Azure/Google login buttons |
| 10 | Sentry OAuth callback landing — account association confirmation | Post-auth confirmation page |
| 11 | Sentry authorization grant page — name, email, org permissions listed | Permission disclosure pattern for consent page |
| 12 | Post-auth redirect back to claude.ai — "Connected to Sentry" confirmation | Redirect flow back to MCP client |
| 13 | Sentry MCP landing page (`mcp.sentry.dev`) — web-based test interface | Landing page at `mcp.qyl.dev` — connection test UI |
| 14 | Sentry MCP server configuration examples (JSON, CLI) | Setup instructions per client on landing page |

### Sentry Tool Permissions & Skills (Reference for Consent Page)

| # | Content | Design Reference |
|---|---------|-----------------|
| 15 | Sentry tool permissions in claude.ai — read-only (15) vs write/delete (6) toggle | qyl consent page skill checkboxes with tool counts |
| 16 | Sentry connector enabled in claude.ai Customize — post-installation state | qyl appears in user's connector list after setup |

### qyl Brand Assets

| # | Content | Reference |
|---|---------|-----------|
| 17 | qyl logo / og-image (`/Users/ancplua/qyl/images/og-image.png`) — panda mascot | Used in consent page, landing page, directory listing |
| 18 | qyl logo SVG variant (`/Users/ancplua/qyl/images/og-image.svg`) | Vector version for directory submission |

---

## Section 1: Architecture — Unified Entry

Two entrances, one OAuth flow.

### Entrance A: Dashboard Wizard

User visits `qyl.dev` → dashboard detects no session → redirects to 4-step onboarding wizard. The wizard handles first-time setup for self-hosted users who access qyl through the browser.

### Entrance B: MCP Client Connector

User adds `https://mcp.qyl.dev/mcp` in Claude Code, Cursor, claude.ai, VS Code, or any MCP-compatible client. The client discovers OAuth metadata via `/.well-known/oauth-protected-resource` (RFC 9728, served by ModelContextProtocol.AspNetCore 1.1.0), then redirects to Keycloak for authentication.

### Convergence Point: Keycloak Identity Brokering

Both entrances redirect to the same Keycloak realm. Keycloak renders a login page with three identity provider buttons:

- **GitHub** — primary for developers
- **Azure AD** — enterprise SSO
- **Google** — broad access

No username/password form. No API keys. No PATs. Keycloak handles token issuance (JWT with custom claims), session management, and identity provider federation.

### Auth Flow (MCP Client)

```
MCP Client
  → GET /.well-known/oauth-protected-resource
  ← { authorization_servers: ["https://auth.qyl.dev/realms/qyl"],
       scopes_supported: ["qyl:inspect","qyl:triage","qyl:analyze","qyl:manage"] }
  → GET https://auth.qyl.dev/realms/qyl/.well-known/openid-configuration
  ← { authorization_endpoint, token_endpoint, ... }
  → Redirect user to authorization_endpoint (+ PKCE)
  → Keycloak redirects to mcp.qyl.dev/consent (custom consent via Keycloak redirect URI override)
  → User selects skills, clicks "Authorize qyl"
  → Consent page POSTs selected scopes back to Keycloak token endpoint
  → Keycloak issues authorization code with only approved scopes
  → MCP client exchanges code for JWT
  → JWT `scope` claim contains space-separated approved scopes (e.g. "qyl:inspect qyl:triage qyl:analyze")
  → MCP client calls tools with Bearer token
```

> **New requirement (C-1):** `ProtectedResourceMetadata` in `ConfigureHttpAuthentication` must add `ScopesSupported = ["qyl:inspect", "qyl:triage", "qyl:analyze", "qyl:manage"]`. This is not yet set in the existing code.

> **JWT claim distinction (F-3):** OAuth scopes land in the JWT `scope` claim (space-separated string). The existing `qyl:admin` realm role lands in `realm_access.roles` (JSON array). The new scope-based tool filter reads from `scope`, not from `realm_access.roles`. Both mechanisms coexist.

### Auth Flow (Dashboard)

```
Browser → qyl.dev/onboarding
  → Step 1: Welcome
  → Step 2: "Sign in" button → redirect to Keycloak
  → Keycloak shows GitHub/Azure/Google buttons
  → User authenticates via identity provider
  → Keycloak redirects back to qyl.dev/onboarding?step=features
  → Step 3: Feature showcase
  → Step 4: Doctor + verification
```

### Existing Infrastructure

The auth foundation already exists in `qyl.mcp`:

- `Program.cs` → `ConfigureHttpAuthentication()` — `.AddAuthentication()` → `.AddJwtBearer()` → `.AddMcp(ForwardAuthenticate)` with `ProtectedResourceMetadata`
- `McpAuthExtensions.cs` → `AddMcpAuth()` — Keycloak client-credentials, `KeycloakTokenProvider`, `McpAdminToolFilter`
- `McpAdminToolFilter.cs` — gates tools on `qyl:admin` realm role via `realm_access.roles` JWT claim
- `Version.props` — `ModelContextProtocolVersion` = 1.1.0

The rework builds on this, not replaces it.

### Scope of Directory-Facing Tools

The initial 0.0.1-beta exposes **23 tools** through the MCP directory — the Phase 1 tools in subdirectories under `Tools/`. The existing 55+ legacy tools (in `Tools/*.cs`) remain available to authenticated users but are not part of the directory submission. Future versions may promote legacy tools to directory-facing status.

---

## Section 2: Dashboard Onboarding — 4-Step Wizard

Replaces the current 6-step wizard (`OnboardingPage.tsx`, 969 lines, steps: Welcome, GitHub, Connect, SDK Setup, Verify, Done).

### Step 1: Welcome

- qyl logo (panda mascot)
- One-liner: "AI Observability for your entire stack"
- Single "Get Started" button
- No copy wall, no feature list (that comes in Step 3)

### Step 2: OAuth (Sign In)

- "Sign in to continue" heading
- Three provider buttons: GitHub, Azure AD, Google
- Each button redirects to Keycloak with the appropriate `kc_idp_hint` parameter
- No email/password form, no API key input, no PAT configuration
- On successful auth, Keycloak redirects back with session established
- Eliminates the current GitHub PAT / Device Code step entirely

### Step 3: Features

- Shown only after successful authentication
- Grid or card layout of qyl capabilities:
  - Trace Explorer — distributed tracing visualization
  - Error Tracking — error grouping and timeline
  - GenAI Observability — LLM token usage, model comparison
  - Conversational Loom — session replay and agent handoff
  - Anomaly Detection — baseline comparison and drift alerts
  - Auto-Fix Pipeline — generate fixes, run tests, approve deployments
- Each card: icon + name + one-line description
- "Continue" button at bottom

### Step 4: Doctor + Verify (Combined)

Runs 6 background health checks while displaying results inline. Replaces the current separate "Connect", "SDK Setup", and "Verify" steps.

Progress indicator shows each check as it completes:

| Check | What it validates |
|-------|-------------------|
| Collector Reachable | `GET /api/v1/meta` returns `CollectorMeta` |
| OTLP Ingestion | Traces/logs arriving via OTLP endpoint |
| DuckDB Storage | Database accessible, schema version correct |
| Auth Token Valid | JWT not expired, required claims present |
| MCP Server | `/.well-known/oauth-protected-resource` responds |
| Dashboard API | `/api/v1/services` returns service list |

Each check shows: spinner → green checkmark / red X with error detail.

- All pass → "You're all set!" with "Go to Dashboard" button
- Partial pass → show which checks failed with actionable remediation steps
- Pattern reference: OpenClaw's `/doctor` endpoint

---

## Section 3: Custom Consent Page — `mcp.qyl.dev/consent`

When MCP clients (Claude Code, claude.ai, Cursor) initiate OAuth, the user sees a custom consent page — not Keycloak's default. This page mirrors Sentry's skill-checkbox pattern.

### Layout

```
┌──────────────────────────────────────────────┐
│  [qyl logo]                                  │
│                                              │
│  qyl is requesting access to your account    │
│  (user@example.com)                          │
│                                              │
│  ┌────────────────────────────────────────┐   │
│  │ ☑ Inspect Telemetry          12 tools │   │
│  │   Search traces, errors, logs, and    │   │
│  │   explore observability data          │   │
│  ├────────────────────────────────────────┤   │
│  │ ☑ Triage Issues               4 tools │   │
│  │   View and trigger issue triage       │   │
│  ├────────────────────────────────────────┤   │
│  │ ☑ Loom & Analyze              3 tools │   │
│  │   AI-powered session analysis, trace  │   │
│  │   analysis, and fix suggestions       │   │
│  ├────────────────────────────────────────┤   │
│  │ ☐ Manage                      4 tools │   │
│  │   Code review, auto-fix, test         │   │
│  │   generation, deployments             │   │
│  └────────────────────────────────────────┘   │
│                                              │
│  After approval, you will be redirected to:  │
│  https://claude.ai/api/mcp/auth_callback     │
│                                              │
│  [Cancel]                    [Authorize qyl]  │
│                                              │
│  Terms of Use — Privacy Policy               │
└──────────────────────────────────────────────┘
```

### Skill → Scope Mapping

Each checkbox maps to an OAuth scope. Selected scopes are written to the JWT `scope` claim (space-separated string):

| Skill | OAuth Scope | Tools | Default |
|-------|-------------|-------|---------|
| Inspect Telemetry | `qyl:inspect` | 12 | checked |
| Triage Issues | `qyl:triage` | 4 | checked |
| Loom & Analyze | `qyl:analyze` | 3 | checked |
| Manage | `qyl:manage` | 4 | unchecked |

"Manage" is unchecked by default because it includes write/delete operations. This matches Sentry's pattern of separating read-only from write/delete tools.

### Scope → QylSkillKind Mapping

Each OAuth scope gates one or more `QylSkillKind` enum values. The scope-based tool filter checks the JWT `scope` claim against this mapping:

| OAuth Scope | QylSkillKind(s) | Directory Tools |
|-------------|----------------|-----------------|
| `qyl:inspect` | `Inspect`, `Health`, `Analytics`, `Build`, `Anomaly`, `ClaudeCode` | Discovery/(3), Traces/(3), Logs/(2), Metrics/(2), Sessions/read(2) |
| `qyl:triage` | `Loom` (triage subset) | Triage/(2), Sessions/write(2) |
| `qyl:analyze` | `Agent` | Analysis/(3) |
| `qyl:manage` | `Loom` (management subset), `Apps` | Management/(4) |

### Exact Tool → Scope Assignment (23 Directory-Facing Tools)

**`qyl:inspect` (12 tools — read-only):**
`GetServiceMapTool`, `ListProjectsTool`, `ListServicesTool`, `GetTraceDetailsTool`, `SearchTracesTool`, `GetSpanTool`, `GetLogDetailsTool`, `SearchLogsTool`, `ListMetricsTool`, `QueryMetricsTool`, `GetSessionTool`, `SearchSessionsTool`

**`qyl:triage` (4 tools — read + write):**
`AnnotateTraceTool`, `MarkTraceReviewedTool`, `AnnotateSessionTool`, `UpdateSessionStatusTool`

**`qyl:analyze` (3 tools — read-only):**
`AnalyzeSessionTool`, `AnalyzeTraceTool`, `SuggestFixTool`

**`qyl:manage` (4 tools — write/delete):**
`ConfigureRetentionTool`, `CreateApiKeyTool`, `CreateProjectTool`, `UpdateProjectTool`

### Tool Permission Categories (Sentry Pattern)

Like Sentry's read-only (15) vs write/delete (6) separation, qyl tools fall into:

**Read-only tools (always allow):**
All Inspect + Triage tools — they query data but never modify state.

**Write/delete tools (require explicit consent):**
Manage tools — `ConfigureRetentionTool`, `CreateApiKeyTool`, `CreateProjectTool`, `UpdateProjectTool`.
Triage write tools — `AnnotateTraceTool`, `MarkTraceReviewedTool`, `AnnotateSessionTool`, `UpdateSessionStatusTool`.

### Implementation: Standalone ASP.NET Core Consent Page

The consent page is a standalone Razor page served by `qyl.mcp` at `mcp.qyl.dev/consent`. It is NOT a Keycloak theme — it intercepts the OAuth authorize flow via Keycloak's redirect URI configuration.

**Flow:**

1. MCP client redirects user to Keycloak's `authorization_endpoint` with `scope=qyl:inspect qyl:triage qyl:analyze qyl:manage`
2. Keycloak authenticates the user (GitHub/Azure/Google login)
3. Keycloak redirects to `mcp.qyl.dev/consent` (configured as the OAuth client's redirect URI in Keycloak)
4. The consent page reads the authenticated user's identity from the Keycloak session and the requested scopes from query parameters
5. User selects/deselects skill checkboxes
6. On "Authorize", the consent page issues a POST back to Keycloak's token endpoint with only the selected scopes
7. Keycloak issues the authorization code scoped to the user's selection
8. The consent page redirects back to the MCP client's `redirect_uri` (e.g. `https://claude.ai/api/mcp/auth_callback`) with the authorization code

**Keycloak configuration required (Chunk 1):**
- OAuth client `qyl-mcp` must have `Consent Required = ON`
- `mcp.qyl.dev/consent` must be in `Valid Redirect URIs`
- Client must have `Full Scope Allowed = OFF` with optional scopes `qyl:inspect`, `qyl:triage`, `qyl:analyze`, `qyl:manage` configured

The page uses the existing brutal theme (Tailwind) consistent with the qyl dashboard.

---

## Section 4: Doctor System

### Endpoint: `GET /api/v1/doctor`

Returns a structured health report. Called by both the dashboard wizard (Step 4) and MCP clients for runtime validation.

```json
{
  "status": "healthy",
  "version": "0.0.1-beta",
  "checks": [
    { "name": "collector", "status": "pass", "latencyMs": 12, "detail": "v0.0.1-beta" },
    { "name": "otlp_ingestion", "status": "pass", "latencyMs": 45, "detail": "3 spans in last 60s" },
    { "name": "duckdb", "status": "pass", "latencyMs": 8, "detail": "schema v4" },
    { "name": "auth", "status": "pass", "latencyMs": 3, "detail": "JWT valid, expires 2026-03-12T18:00:00Z" },
    { "name": "mcp_server", "status": "pass", "latencyMs": 5, "detail": "MCP 1.1.0, {N} tools registered" },
    { "name": "dashboard_api", "status": "pass", "latencyMs": 15, "detail": "4 services discovered" }
  ],
  "timestamp": "2026-03-12T14:30:00Z"
}
```

### Health Checks Detail

| Check | Implementation | Pass Condition |
|-------|---------------|----------------|
| Collector Reachable | `GET /api/v1/meta` on collector | Returns `CollectorMeta` with status 200 |
| OTLP Ingestion | Query DuckDB for recent spans | At least 1 span in the last 60 seconds |
| DuckDB Storage | `SELECT 1` + schema version check | Query succeeds, schema matches expected version |
| Auth Token Valid | Validate JWT claims and expiry | Token not expired, required claims present |
| MCP Server | `GET /.well-known/oauth-protected-resource` | Returns valid protected resource metadata |
| Dashboard API | `GET /api/v1/services` | Returns service list without error |

### Context Check (Anthropic Requirement)

Per Anthropic's MCP directory submission requirements, the doctor endpoint also validates:

- **Safety annotations** — all tools have `readOnlyHint` / `destructiveHint` set
- **Token budget** — no tool response exceeds 25,000 tokens
- **OAuth metadata** — `/.well-known/oauth-protected-resource` serves valid RFC 9728 response
- **Privacy policy** — `mcp.qyl.dev/privacy` returns 200

---

## Section 5: MCP Directory Submission

### Connector Metadata

For qyl to appear in claude.ai's Connector directory (like Sentry in screenshot #6), the following must be submitted to Anthropic:

| Field | Value |
|-------|-------|
| Name | qyl |
| Version | 0.0.1-beta |
| MCP URL | `https://mcp.qyl.dev/mcp` |
| Logo | `og-image.png` (panda mascot) |
| Description | AI observability for traces, errors, GenAI metrics, and Conversational Loom |
| Privacy Policy | `https://mcp.qyl.dev/privacy` |
| Documentation | `https://mcp.qyl.dev/docs` |
| OAuth Callback | `https://claude.ai/api/mcp/auth_callback` (Anthropic's standard callback) |
| Scopes | `qyl:inspect`, `qyl:triage`, `qyl:analyze`, `qyl:manage` |
| Skills | Inspect Telemetry (12 tools), Triage Issues (4 tools), Loom & Analyze (3 tools), Manage (4 tools) — 23 directory-facing tools total |
| Safety | All tools annotated with `readOnlyHint`, `destructiveHint`, `idempotentHint` |
| Token Limit | All responses < 25,000 tokens |

### Landing Page: `mcp.qyl.dev`

A public page with:

1. **Hero** — qyl logo, one-liner, "Connect" CTA
2. **Setup per client** — tabbed instructions for:
   - Claude Code: `claude mcp add --transport http qyl https://mcp.qyl.dev/mcp`
   - Cursor: MCP settings JSON
   - claude.ai: Settings → Profile → Integrations → Add More
   - VS Code / GitHub Copilot: Command Palette → MCP: Add Server
   - Codex: `codex mcp add qyl --url https://mcp.qyl.dev/mcp`
   - Windsurf, Warp, Amp, other clients: standard MCP URL
3. **Tool list** — grouped by skill (Inspect, Triage, Loom & Analyze, Manage) with descriptions
4. **Test connection** — web-based interface to authenticate and test tools (like Sentry's `mcp.sentry.dev`)
5. **Footer** — Terms of Use, Privacy Policy, GitHub link

### Hosting: `mcp.qyl.dev`

All pages are served by `qyl.mcp` — the same ASP.NET Core process that serves `/mcp`. Add Razor Pages and static file middleware to serve the landing page, consent page, docs, and privacy policy alongside the MCP endpoint. No separate project needed.

### Required Pages

| Path | Purpose |
|------|---------|
| `mcp.qyl.dev/` | Landing page with setup instructions |
| `mcp.qyl.dev/consent` | Custom OAuth consent page (standalone Razor page) |
| `mcp.qyl.dev/privacy` | Privacy policy |
| `mcp.qyl.dev/docs` | Tool documentation |
| `mcp.qyl.dev/mcp` | MCP server endpoint (Streamable HTTP) |

### `/docs` Content Specification

The tool documentation page contains:

1. **Tool inventory by skill** — 4 sections (Inspect, Triage, Loom & Analyze, Manage), each listing tool name, description, parameters, and return type
2. **OAuth setup reference** — how to authenticate, available scopes, JWT claims
3. **Example prompts** — like Sentry's "Tell me about the issues in my project" examples, adapted for qyl:
   - "Show me the last 10 traces for my service"
   - "Analyze the errors in session X and suggest a fix"
   - "What's the token usage trend for the last 7 days?"
4. **Troubleshooting** — common connection issues, reauthentication steps per client

---

## Section 6: Summary

### Deliverables

1. **Dashboard wizard rewrite** — 4 steps (Welcome, OAuth, Features, Doctor), replaces current 6-step `OnboardingPage.tsx`
2. **Keycloak realm configuration** — GitHub, Azure AD, Google identity providers, custom OAuth scopes (`qyl:inspect/triage/analyze/manage`), scope negotiation enabled, redirect URIs for both dashboard (`qyl.dev/onboarding*`) and consent page (`mcp.qyl.dev/consent`)
3. **Custom consent page** — standalone Razor page at `mcp.qyl.dev/consent` with skill checkboxes and tool counts
4. **Scope-based tool filter** — new `McpScopeToolFilter` (analogous to existing `McpAdminToolFilter`) that reads JWT `scope` claim and blocks tools whose scope was not granted. Enforced via ASP.NET Core authorization policies, one per scope.
5. **`ScopesSupported` on ProtectedResourceMetadata** — add `["qyl:inspect","qyl:triage","qyl:analyze","qyl:manage"]` to the existing `ProtectedResourceMetadata` in `ConfigureHttpAuthentication`
6. **Doctor endpoint** — `GET /api/v1/doctor` with 6 health checks
7. **Landing page** — `mcp.qyl.dev` with per-client setup instructions, served by `qyl.mcp` (Razor pages + static files alongside the MCP endpoint)
8. **Safety annotations** — audit remaining tool classes; Phase 1 tools (subdirectory tools) already have `ReadOnly`/`Destructive`/`Idempotent` set. Remaining work: verify legacy `Tools/*.cs` classes have annotations where they will be directory-facing in future versions.
9. **Directory submission package** — metadata, logo, privacy policy for Anthropic MCP directory
10. **Privacy policy page** — `mcp.qyl.dev/privacy`
11. **Tool documentation page** — `mcp.qyl.dev/docs` with tools grouped by skill, OAuth setup reference, and per-tool descriptions

### Implementation Order

These deliverables form a dependency chain. Build in this order:

| Chunk | What | Why First |
|-------|------|-----------|
| 1 | Keycloak realm + identity providers + scope config | Everything depends on auth working. Includes: 3 IdPs, 4 OAuth scopes, scope negotiation enabled, redirect URIs for dashboard (`qyl.dev/onboarding*`) and consent page (`mcp.qyl.dev/consent`). |
| 2 | Scope-based tool filter + `ScopesSupported` + safety annotations | Server-side scope enforcement must exist before exposing tools. New `McpScopeToolFilter` reads JWT `scope` claim. |
| 3 | Doctor endpoint | Validates infrastructure, required for directory. Reports dynamic tool count. |
| 4 | Dashboard wizard rewrite | User-facing onboarding flow, depends on Keycloak (Chunk 1) being live. |
| 5 | Consent page + landing page + docs + privacy policy | External-facing pages served by `qyl.mcp` as Razor pages. Depends on scope filter (Chunk 2) and doctor (Chunk 3). |
| 6 | Directory submission | Requires all of the above to be live and verified. |

### What We Match from Sentry (Non-SaaS)

| Sentry Feature | qyl Equivalent |
|----------------|---------------|
| OAuth via Identity Brokering (GitHub) | Keycloak with GitHub, Azure AD, Google |
| Skill-checkbox consent page | Custom consent at `mcp.qyl.dev/consent` |
| Read-only vs write/delete tool separation | `qyl:inspect`+`qyl:analyze` (read-only) vs `qyl:triage`+`qyl:manage` (write) |
| Per-client setup instructions | Landing page at `mcp.qyl.dev` |
| Listed in claude.ai Connector directory | Directory submission with Anthropic |
| `mcp.sentry.dev` test interface | `mcp.qyl.dev` test interface |
| Safety annotations on all tools | `readOnlyHint`, `destructiveHint`, `idempotentHint` |
| Privacy policy link | `mcp.qyl.dev/privacy` |

### What We Do NOT Match (SaaS-Specific)

| Sentry Feature | Why Not |
|----------------|---------|
| Seer AI debugger integration | qyl has Conversational Loom instead |
| Organization/project multi-tenancy | qyl is self-hosted, single-tenant |
| `sentry-cli` integration | Not applicable |
| Release management tools | qyl tracks observability, not releases |

### What qyl Adds Beyond Sentry

| qyl Feature | Description |
|-------------|-------------|
| Conversational Loom | Session replay, agent handoff, conversation threading |
| GenAI Observability | LLM token usage, model comparison, cost tracking |
| Auto-Fix Pipeline | Generate fixes, run tests, approve via MCP |
| Doctor Endpoint | Comprehensive self-hosted health validation |
