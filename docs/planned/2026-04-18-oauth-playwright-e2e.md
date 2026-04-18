# PLANNED — OAuth end-to-end Playwright harness

**Status:** Planned. **Blocked on** `.claude/handoffs/2026-04-17-spec2-ops-followup.md` items 1–11 — requires `mcp.qyl.info` to be live with a real Keycloak realm, test user, and green deploy.
**Predecessor:** Spec 2 code landed on 2026-04-17 in `main` (guard + logging + deploy skeleton).
**Unlocks:** Regression-proofs the Claude-Code → Keycloak → qyl.mcp OAuth dance. Required before MCP-registry listing.

## Outcome

CI job runs on every push to `main` (or nightly) in a fresh container. From zero credentials:

1. `claude mcp add --transport http qyl https://mcp.qyl.info/mcp`
2. Scripted browser completes the Keycloak PKCE flow with `test@qyl.info`
3. Tokens flow back, stored client-side
4. `claude` invokes 3 canonical tools (e.g. `qyl.list_services`, `qyl.get_trace`, `qyl.list_error_issues`) and asserts non-empty responses
5. Invalid-token request returns proper RFC-9728 `401 + WWW-Authenticate: Bearer resource_metadata=...`
6. After access-token TTL + 10 s, next call silently refreshes and succeeds

Pass → badge green. Fail → block the MCP-registry listing / alert ops.

## Scope

Net-new test project. Does not modify runtime code.

- **New project:** `tests/qyl.mcp.e2e/qyl.mcp.e2e.csproj` — xUnit v3 + MTP + `Microsoft.Playwright`
- **New:** `tests/qyl.mcp.e2e/OAuthDanceTests.cs` — the 6 assertions above as distinct `[Fact]` methods
- **New:** `tests/qyl.mcp.e2e/DevcontainerFixture.cs` — xUnit fixture that boots a fresh `mcr.microsoft.com/devcontainers/base:ubuntu` container via Testcontainers, installs `claude-code`, exposes the CLI to the test
- **New workflow:** `.github/workflows/oauth-e2e.yml` — triggers on `push` to `main` and nightly cron; needs `QYL_E2E_KEYCLOAK_TEST_USER_PW` secret
- **Optional:** `tests/qyl.mcp.e2e/README.md` with "how to run locally" — requires `docker` + `dotnet` only

Do **not** touch: runtime `src/qyl.mcp/**`, `Hosting/QylMcpHttpHost.cs`, or any Keycloak config (those are in Spec 2 ops work, already landed or handed off).

## Prerequisites — hard gates

Cannot start until **all** of the following are ✅ (cross-reference `.claude/handoffs/2026-04-17-spec2-ops-followup.md`):

- [ ] Keycloak realm `qyl` provisioned with `qyl-mcp` public PKCE client + audience mapper
- [ ] `mcp.qyl.info` resolves, TLS cert valid, `/healthz` returns 200
- [ ] Test user `test@qyl.info` exists in Keycloak with known password stored as GH secret
- [ ] Deploy workflow's `if: false` gate is flipped and the container is actually serving

Do **not** scaffold the E2E before these are green — you'll write against assumptions and the test will need a rewrite after real deployment.

## Execute-ready prompt

```
You are implementing the OAuth E2E Playwright harness for qyl.mcp.

## Workspace
- Create a worktree: `git worktree add .worktrees/oauth-e2e -b oauth-e2e main`
- `cd .worktrees/oauth-e2e && git status` (expect clean).

## Read first (absolute paths)
- /Users/ancplua/qyl/docs/planned/2026-04-18-oauth-playwright-e2e.md (this file)
- /Users/ancplua/qyl/.claude/handoffs/2026-04-17-spec2-ops-followup.md (ops prereqs)
- /Users/ancplua/qyl/docs/mcp-oauth-runbook.md (expected URLs, failure modes)

## Hard gates — verify BEFORE writing any code
1. `curl -fsI https://mcp.qyl.info/healthz` → 200
2. `curl -fs https://mcp.qyl.info/.well-known/oauth-protected-resource | jq .` → valid JSON with authorization_servers
3. Keycloak test-user credential is in the repo's GH secrets
If any fails → STOP, do not proceed. Ops work per spec2-ops-followup is not complete.

## Baseline
`dotnet build tests/qyl.mcp.tests/qyl.mcp.tests.csproj --tl:off` must be green (establishes the test-infra baseline).

## Steps
1. Create `tests/qyl.mcp.e2e/qyl.mcp.e2e.csproj` using xUnit v3 + MTP (follow the shape of tests/qyl.mcp.tests.csproj). Add PackageReferences for Microsoft.Playwright, Testcontainers. CPM via Directory.Packages.props — never inline versions.
2. Install Playwright browsers during build (`playwright install chromium` in a PostBuild target).
3. DevcontainerFixture: spin up an Ubuntu container, install claude-code via its official install script, expose a bound mount for artifacts.
4. OAuthDanceTests: 6 facts mapping to the 6 DoD assertions (steps 1-6 of the Outcome section).
5. Secrets: wire `QYL_E2E_KEYCLOAK_TEST_USER_PW` through Testcontainers env.
6. GitHub workflow `oauth-e2e.yml` — runs on push to main + nightly cron; uses the secret.
7. Smoke locally first (against real mcp.qyl.info) BEFORE wiring CI.

## Repo rules
MAF wins. No suppression. No git stash. UTF-8 BOM on new .cs. C# 14 preview.
Tests use FakeChatClient only if mocking IChatClient — not relevant here.
Never push. Never merge main.

## DoD
- All 6 facts pass against live mcp.qyl.info from a clean machine.
- Invalid-token test asserts presence of `WWW-Authenticate: Bearer resource_metadata="..."` (RFC 9728).
- Refresh test waits TTL + 10s, verifies silent recovery.
- Workflow runs green in CI at least once.
- Report: test output, CI run URL, any flakes observed.
```

## DoD checklist

- [ ] All 6 DoD assertions passing on a fresh machine (`docker run --rm …`)
- [ ] CI workflow green nightly for 7 consecutive days before wire-up to release gating
- [ ] Runbook `docs/mcp-oauth-runbook.md` updated with "if the E2E fails, check these lines first" section

## Risks / non-goals

- **Playwright browser install adds ~200 MB to CI runtime** — acceptable; gates a release, not a dev loop
- **Flakes from Keycloak latency** — add reasonable retry budgets; 3× retry on the browser-fill step
- **Do not** re-test MCP protocol conformance here — that's covered by `tests/qyl.mcp.tests`
- **Do not** embed real secrets in the repo — secrets come from GH Actions env only
- **Do not** scaffold before ops prereqs land — dead code that needs rewriting
