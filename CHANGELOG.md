# Automation Changelog

This file tracks the last 10 cron automation material changes. It is for automated runners, not developer release notes.

## 2026-05-09T10:30:00Z - branch-hygiene-sweep

- Pruned merged local branches across Arqio, qyl, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX after live merged-PR evidence.
- Fast-forwarded local main branches for Arqio, ANcpLua.Analyzers, ANcpLua.Agents, and ErrorOrX.
- qyl PR #290 was already merged; qyl PR #288 has commit `93b9b389` with checks still running; qyl PR #297 has regen commit `c7340407` with checks rerun and pending.
- Fixed and pushed qyl `dev/forgejo-summary-research` commit `c09b8519` after focused SummaryCredentialRedactor tests passed.
- Opened ANcpLua.NET.Sdk PR #130 from `fix/refix-codestyle-policy-pass`; checks are pending after push.
- ANcpLua.Agents PR #67 remains open with auto-merge enabled but blocked by branch policy.
