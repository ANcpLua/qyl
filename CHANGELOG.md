# Automation Changelog

This file tracks the last 10 cron automation material changes. It is for automated runners, not developer release notes.

## 2026-05-09T19:20:02Z - branch-hygiene-sweep

- Merged qyl PR #301 (`renovate/react-monorepo`) after live green CI, CodeQL, stability-days, and Claude review evidence; merge commit `76fc2e61`.
- Advanced qyl PR #300 review fixes on `dev/forgejo-summary-research`: tracked-only local corpus collection, full-text redaction before excerpts, shared credential patterns, TRX test dependency, safer runner token/capacity handling, pinned runner images, and daily research schedule.
- Verified qyl PR #300 locally with `dotnet test --project tests/qyl.mcp.tests/qyl.mcp.tests.csproj -- --report-trx --results-directory artifacts/test-results/qyl-mcp` and `node eng/forgejo/research-forgejo-docs.mjs --no-refresh`.
- Enabled auto-merge for ANcpLua/ancplua-claude-plugins PR #242 after green checks; GitHub branch policy still reports `mergeStateStatus=BLOCKED`.
- Fast-forwarded local ANcpLua/ancplua-claude-plugins PR #241 worktree to remote head `4b03fbb`; PR remains blocked by live `CHANGES_REQUESTED`.
- Configured repo paths for Arqio, ANcpLua.Roslyn.Utilities, ANcpLua.NET.Sdk, ANcpLua.Analyzers, ANcpLua.Agents, ErrorOrX, typespec-otel-semconv, and renovate-config were missing locally, so no branch action was possible there.

## 2026-05-09T10:30:00Z - branch-hygiene-sweep

- Pruned merged local branches across Arqio, qyl, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX after live merged-PR evidence.
- Fast-forwarded local main branches for Arqio, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX.
- qyl PR #290 was already merged; qyl PRs #288, #297, and #299 were merged after live green checks.
- Fixed and pushed qyl `dev/forgejo-summary-research` commit `c09b8519` after focused SummaryCredentialRedactor tests passed.
- Opened qyl PR #300 for `dev/forgejo-summary-research`; checks are pending after push.
- Opened ANcpLua.NET.Sdk PR #130 from `fix/refix-codestyle-policy-pass`; local lint is clean and checks are pending after push.
- ANcpLua.Agents PR #67 is closed unmerged; the repo reread is clean on `main`.
