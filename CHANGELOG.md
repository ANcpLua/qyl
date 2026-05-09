# Automation Changelog

This file tracks the last 10 cron automation material changes. It is for automated runners, not developer release notes.

## 2026-05-09T19:20:02Z - branch-hygiene-sweep

- qyl PR #301 (`renovate/react-monorepo`) was already merged upstream after live green CI, CodeQL, stability-days, and Claude review evidence; `origin/main` advanced to `76fc2e61`.
- Advanced qyl PR #300 review fixes on `dev/forgejo-summary-research`: tracked-only local corpus collection, full-text redaction before excerpts, shared credential patterns, TRX test dependency, safer runner token/capacity handling, and daily research schedule.
- Verified qyl PR #300 locally with `node --check eng/forgejo/research-forgejo-docs.mjs`, `node eng/forgejo/research-forgejo-docs.mjs --no-refresh`, `bash -n eng/forgejo/run-local-runner.sh`, and `dotnet test --project tests/qyl.mcp.tests/qyl.mcp.tests.csproj --no-restore --filter-method '*SummaryCredentialRedactorTests*'`.
- ANcpLua/ancplua-claude-plugins PR #242 was inspected: green checks, `mergeStateStatus=BLOCKED`, no local branch change.
- ANcpLua/ancplua-claude-plugins PR #241 was inspected: remote head `4b03fbb`, `reviewDecision=CHANGES_REQUESTED`, no local branch change.
- Configured repo paths for Arqio, ANcpLua.Roslyn.Utilities, ANcpLua.NET.Sdk, ANcpLua.Analyzers, ANcpLua.Agents, ErrorOrX, typespec-otel-semconv, and renovate-config were missing locally, so no branch action was possible there.

## 2026-05-09T10:30:00Z - branch-hygiene-sweep

- Pruned merged local branches across Arqio, qyl, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX after live merged-PR evidence.
- Fast-forwarded local main branches for Arqio, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX.
- qyl PR #290 was already merged; qyl PRs #288, #297, and #299 were merged after live green checks.
- Fixed and pushed qyl `dev/forgejo-summary-research` commit `c09b8519` after focused SummaryCredentialRedactor tests passed.
- Opened qyl PR #300 for `dev/forgejo-summary-research`; checks are pending after push.
- Opened ANcpLua.NET.Sdk PR #130 from `fix/refix-codestyle-policy-pass`; local lint is clean and checks are pending after push.
- ANcpLua.Agents PR #67 is closed unmerged; the repo reread is clean on `main`.
