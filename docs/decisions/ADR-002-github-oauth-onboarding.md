# ADR-002: GitHub OAuth Onboarding

Status: Done
Date: 2026-02-26
Depends-On: ADR-001

## Context

Users need to configure qyl manually: set OTLP endpoints, add NuGet packages, modify code. This is the same friction every observability tool has. Sentry requires DSN + signup. Aspire requires Azure + CLI.

## Decision

qyl uses **GitHub OAuth** as its only authentication mechanism. Like GitHub Copilot, the user connects their GitHub account. No signup, no separate account, no API keys to manage.

### Flow

```
1. User opens localhost:5100 (first time)
2. Dashboard shows: "Connect GitHub to get started"
3. User clicks → GitHub OAuth flow (Device Flow for CLI, Web Flow for browser)
4. qyl receives GitHub token
5. Token stored in container (or volume mount for persistence)
6. Dashboard unlocks: repo discovery, PR creation, Copilot agent
```

### GitHub App vs Personal Token

| Approach | Pros | Cons |
|----------|------|------|
| GitHub App | Fine-grained permissions, org install | Requires app registration |
| OAuth App | Simple, standard flow | Broader scopes |
| Personal Token | Zero setup | User manages token manually |

Decision: Support all three. Dashboard offers OAuth flow (recommended), also accepts `QYL_GITHUB_TOKEN` env var for CI/headless.

## Constraints

- No GitHub Enterprise requirement — works with github.com free tier
- No Azure AD, no Sentry auth, no proprietary auth
- Token never leaves the container (or user's volume mount)
- Without GitHub auth: qyl still works for OTLP ingestion + dashboard, just no repo discovery / PR creation

## Acceptance Criteria

```gherkin
GIVEN a running qyl container with no GitHub token
WHEN  user opens localhost:5100
THEN  dashboard shows onboarding screen with "Connect GitHub" button
AND   OTLP ingestion on :4317 still works (no auth required for telemetry)

GIVEN a valid GitHub token (env var or OAuth)
WHEN  qyl authenticates
THEN  GET /api/v1/github/repos returns the user's repositories
AND   token is persisted to volume mount (if configured)

GIVEN a running qyl container
WHEN  container is removed
THEN  no GitHub tokens remain on host (unless volume-mounted)
```

## Verification Steps (Agent-Executable)

1. Start container without token → assert dashboard shows onboarding
2. Set `QYL_GITHUB_TOKEN=<test-token>` → restart
3. `curl http://localhost:5100/api/v1/github/repos` → assert returns repo list
4. Send OTLP span without token → assert span appears in dashboard (telemetry has no auth gate)

## Implementation Status

### Shipped

| Component | Status | Details |
|-----------|--------|---------|
| DuckDB token persistence | Done | `github_tokens` table, upsert/get/delete via channel-buffered writes |
| GitHubService | Done | Mutable token with `Lock`, 3 auth sources (runtime > env > none), `InitializeAsync` at startup |
| Device Flow | Done | `StartDeviceFlowAsync`/`PollDeviceFlowAsync`, requires `QYL_GITHUB_CLIENT_ID` |
| PAT submission | Done | `POST /api/v1/github/token` validates against GitHub `/user` before persisting |
| Token disconnect | Done | `DELETE /api/v1/github/token` clears DuckDB, reverts to env var if present |
| API endpoints (7) | Done | status, token CRUD, device flow (start/poll/available), repos |
| Onboarding wizard | Done | 6-step wizard with 3-tab GitHub auth (Device Flow / PAT / Env Var) |
| Settings > Integrations | Done | GitHub connection status, auth method badge, disconnect button |
| `/health/ui` endpoint | Done | `HealthUiService` registered, returns DuckDB/disk/memory/ingestion health |
| OTLP without auth | Done | `/v1/traces` has no auth gate (always worked) |
| FirstVisitGate | Done | `App.tsx:FirstVisitGate` queries `/api/v1/github/status`, redirects to `/onboarding` when not configured |
| Auth unification | Done | Removed LoginPage + cookie login. GitHub OAuth is user identity; `QYL_TOKEN` kept for MCP/CLI only |
| Copilot token bridge | Done | `CopilotAuthOptions.ExternalTokenProvider` → `GitHubService.GetToken()` via DI |
| Device Flow setup docs | Done | "Device Flow Setup" section in this ADR |

### Remaining

| Task | Priority | Details |
|------|----------|---------|
| ~~Fix FirstVisitGate~~ | ~~P0~~ | Done — queries `/api/v1/github/status`, redirects to `/onboarding` when `configured: false` |
| ~~Auth unification~~ | ~~P1~~ | Done — removed LoginPage, cookie login/logout, `/api/auth/check`. GitHub OAuth is now the only user identity mechanism. `QYL_TOKEN` kept for MCP/CLI infrastructure auth only. |
| ~~Copilot token bridge~~ | ~~P2~~ | Done — `CopilotAuthOptions.ExternalTokenProvider` delegate resolves `GitHubService.GetToken()` at runtime. Copilot inherits GitHub token from onboarding automatically. |
| ~~Device Flow setup docs~~ | ~~P2~~ | Done — see "Device Flow Setup" section below |

## Device Flow Setup

To enable the GitHub Device Flow tab in the onboarding wizard:

### 1. Register a GitHub OAuth App

1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Click **New OAuth App**
3. Fill in:
   - **Application name**: `qyl` (or any name)
   - **Homepage URL**: `http://localhost:5100`
   - **Authorization callback URL**: `http://localhost:5100` (not used for Device Flow, but required)
4. Click **Register application**
5. Copy the **Client ID** (you do NOT need the client secret for Device Flow)

### 2. Enable Device Flow

On the OAuth App settings page, check **Enable Device Flow** under the "General" section.

### 3. Configure qyl

```bash
# Docker
docker run -d \
  -e QYL_GITHUB_CLIENT_ID=Ov23li... \
  -p 5100:5100 -p 4317:4317 \
  ghcr.io/ancplua/qyl

# Direct
QYL_GITHUB_CLIENT_ID=Ov23li... dotnet run --project src/qyl.collector
```

### 4. Verify

Open `http://localhost:5100` → Onboarding → GitHub step → "Device Flow" tab should be visible.

Without `QYL_GITHUB_CLIENT_ID`, the Device Flow tab is hidden. Users can still authenticate via PAT or `QYL_GITHUB_TOKEN` env var.

## Consequences

- GitHub becomes the identity provider (free, ubiquitous)
- Copilot integration gets GitHub token for free (already authenticated)
- PR-based instrumentation possible (ADR-004)
- Users without GitHub can still use qyl for OTLP collection (degraded mode)
