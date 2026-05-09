# Automation Changelog

This file tracks the last 10 cron automation material changes. It is for automated runners, not developer release notes.

## 2026-05-09T10:30:00Z - branch-hygiene-sweep

- Pruned merged local branches across Arqio, qyl, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX after live merged-PR evidence.
- Fast-forwarded local main branches for Arqio, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX.
- qyl PR #290 was already merged; qyl PRs #288, #297, and #299 were merged after live green checks.
- Fixed and pushed qyl `dev/forgejo-summary-research` commit `c09b8519` after focused SummaryCredentialRedactor tests passed.
- Opened qyl PR #300 for `dev/forgejo-summary-research`; checks are pending after push.
- Opened ANcpLua.NET.Sdk PR #130 from `fix/refix-codestyle-policy-pass`; local lint is clean and checks are pending after push.
- ANcpLua.Agents PR #67 is closed unmerged; the repo reread is clean on `main`.
