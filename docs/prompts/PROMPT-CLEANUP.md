# Prompt: Project Cleanup — qyl Observability Platform

## Goal

Single commit. Stage all deletions, fix all stale references, remove dead docs. Zero functional changes.

## 1. Stage Existing Deletions (already deleted from disk)

These directories were removed in prior sessions but never staged:

```bash
git add -u docs/prompts/PROMPT-ADR-IMPLEMENTATION.md
git add -u docs/prompts/PROMPT-AGENTS-DASHBOARD.md
```

Both replaced by `docs/prompts/PROMPT-REMAINING-WORK.md` (already tracked).

## 2. Delete Obsolete ADRs

Two pre-numbered ADRs are superseded by the canonical `ADR-00x` series:

| Old File | Superseded By |
|----------|---------------|
| `docs/adrs/001-observability-enhancements.md` | `ADR-001-docker-first-distribution.md` + CLAUDE.md "Observability Enhancements v1.0" section |
| `docs/adrs/002-copilot-integration.md` | `ADR-005-agent-framework-copilot.md` |

Delete both. No other file references them.

## 3. Delete Stale Docs

| File | Reason |
|------|--------|
| `docs/sentry/qyl-sentry.md` | Competitive comparison — point-in-time snapshot, not actionable |
| `docs/sentry/sentry.md` | Sentry feature registry — stale catalog, no downstream consumers |
| `docs/aspire/qyl-aspire.md` | Aspire comparison — point-in-time snapshot, not actionable |
| `docs/plans/2026-02-15-ai-chat-analytics-design.md` | Bot analytics design doc — already implemented in AgentsPage + BotPage |
| `docs/architecture/semconv-pipeline.md` | Describes pipeline already built — move any useful bits to `core/CLAUDE.md` if missing, then delete |
| `docs/verify-catalog-docs.sh` | Verification script for deleted catalogs |

After deleting, remove empty directories: `docs/sentry/`, `docs/aspire/`, `docs/plans/`, `docs/architecture/`.

## 4. Fix Stale CLAUDE.md References

### Root `CLAUDE.md`

Remove these lines/sections:

```
# Line ~138 — references deleted file
- For catalog/registry/matrix/taxonomy/inventory requests, follow `@catalog-format-v1` from:
    - `/Users/ancplua/qyl/docs/policies/catalog-format-policy.md`
- Default artifact set:
    - `<slug>-catalog.csv`
    - `<slug>-abstractions.csv`
    - `<slug>-catalog.json`
- Keep IDs stable once assigned and use commit-pinned source links when requested.
```

Replace these lines:

```
# Line ~162-163 — point to deleted files
Dashboard spec: `docs/prompts/PROMPT-AGENTS-DASHBOARD.md` (Agents Overview with 6 panels, trace list, trace detail).
Implementation prompt: `docs/prompts/PROMPT-ADR-IMPLEMENTATION.md` (team orchestration for all ADRs).
```

With:

```
Remaining work: `docs/prompts/PROMPT-REMAINING-WORK.md` (3 items: browser correlation, copilot verification, visual verification).
```

### `src/qyl.dashboard/CLAUDE.md`

Replace line 15:

```
Full spec: `PROMPT-AGENTS-DASHBOARD.md` — pixel-level reference for the `/agents` route.
```

With:

```
Agents dashboard: implemented (AgentsPage.tsx, use-agent-insights.ts, AgentTraceTree.tsx). Remaining visual verification in `docs/prompts/PROMPT-REMAINING-WORK.md`.
```

### `src/qyl.collector/CLAUDE.md`

Find and update the reference at line ~50:

```
**Agents Analytics** (ADR implementation — see `PROMPT-AGENTS-DASHBOARD.md`):
```

Replace with:

```
**Agents Analytics** (implemented — 10 endpoints, see AgentInsightsEndpoints.cs):
```

## 5. Verify

```bash
# No broken references remain
grep -r "PROMPT-ADR-IMPLEMENTATION\|PROMPT-AGENTS-DASHBOARD\|catalog-format-policy\|001-observability\|002-copilot-integration" --include="*.md" --include="*.cs" --include="*.ts" .

# Solution builds
dotnet build qyl.slnx

# Empty dirs gone
ls docs/sentry/ docs/aspire/ docs/plans/ docs/architecture/ 2>&1 | grep "No such file"
```

## Rules

- One commit, message: `chore: remove obsolete docs and fix stale references`
- Do NOT touch source code (`.cs`, `.ts`, `.tsx`, `.csproj`)
- Do NOT delete any ADR-00x files (those are canonical)
- Do NOT delete `docs/prompts/PROMPT-REMAINING-WORK.md` (that's the active prompt)
- If `docs/architecture/semconv-pipeline.md` contains information not already in `core/CLAUDE.md`, merge it there first
