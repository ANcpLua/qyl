---
name: loom-review-bot-pr
description: Review and resolve qyl review-bot PR comments on GitHub (qyl[bot], qyl-review[bot], or any extra bot logins passed via additionalBotLogins). Use when the user asks to address qyl bot feedback on a pull request, triage bot comments, or verify which bot-flagged issues still apply in the current code. Parses the markdown body deterministically and separates resolved vs skipped vs manual-review with reasons.
---

# loom-review-bot-pr — Process qyl review-bot PR comments

Loom's PR review-bot workflow. Filters to qyl review bots by default (extensible to others via `additionalBotLogins`), parses the markdown body, and drives each comment to a deterministic outcome (resolved / skipped-already-fixed / skipped-false-positive / manual-review).

## Invoke this skill when
- The user mentions `qyl[bot]`, `qyl-review[bot]`, or "qyl review-bot comments".
- The user shares a PR URL or number and mentions qyl bot feedback.
- The user asks to "address qyl review" / "resolve bot findings".
- `loom-workflow` routed to `ReviewBotPrComments`.

## Scope — qyl review bots (extensible)

The parser matches authors whose login:
- Exactly equals `qyl[bot]` or `qyl-review[bot]`, **or**
- Starts with `qyl` (case-insensitive), **or**
- Appears in the `additionalBotLogins` list the caller passes (when processing PRs that also carry foreign review bots).

Every other author (`cursor[bot]`, `dependabot[bot]`, `copilot[bot]`, human reviewers) is silently dropped. Do not process them here.

## How to run this skill

### Step 1 — Fetch PR comments via GitHub CLI

```bash
gh api repos/{owner}/{repo}/pulls/{prNumber}/comments --paginate \
  --jq '[.[] | {author: .user.login, file: .path, line: .line, body: .body}]'
```

### Step 2 — Parse the batch

Call MCP tool `loom_parse_review_bot_comments(commentsJson)` with the raw GitHub JSON. It filters to qyl review bots by default, strips HTML, and extracts for each surviving comment:
- `bug` — the `**Bug:**` header
- `severity` + `severityText` — parsed label and verbatim text
- `confidence` — 0–1
- `detailedAnalysis` — plain-text `🔍 Detailed Analysis` section
- `suggestedFix` — plain-text `💡 Suggested Fix` section
- `agentPrompt` — plain-text `🤖 Prompt for AI Agent` section

The tool also returns a pre-formatted `summary` ordered by severity / confidence.

### Step 3 — Fetch the workflow prompt

Call MCP prompt `qyl.loom.review_bot_pr(repoFullName, prNumber, parsedSummary)` with the summary from Step 2. The prompt returns the full workflow directive with the classification matrix baked in.

### Step 4 — Run the classification + fix loop

For each comment:

| Severity + confidence | Action |
|---|---|
| `Critical` or `High`, `confidence ≥ 0.8` | Act unless already fixed or clearly false positive |
| `Medium`, `confidence ≥ 0.7` | Act unless context contradicts |
| `Low` / `Info` / `confidence < 0.5` | Manual review; do not auto-apply |
| `Unknown` severity | Treat as `Medium` |

Then:
1. Read the referenced file at the referenced line.
2. Confirm the problematic code is still there (later pushes may have already fixed it).
3. Apply the minimal **root-cause** fix, starting from the `suggestedFix` section.
4. Respect the surrounding codebase style; no refactors outside the flagged lines.

### Step 5 — Report

Emit the structured report from the workflow prompt:

```markdown
## Review-Bot Pass: {repo} PR #{n}

### Resolved
| File:Line | Severity | Confidence | Fix Applied |

### Skipped
| File:Line | Severity | Reason |

### Summary
- Resolved / Skipped (already fixed) / Skipped (false positive) / Manual review required
```

Never silently skip — every skipped item records **why**.

## MCP surface this skill uses

| Tool | Purpose |
|---|---|
| `loom_parse_review_bot_comments` | Deterministic markdown/HTML parser for bot review comments. |

| Prompt | Purpose |
|---|---|
| `qyl.loom.review_bot_pr` | Full workflow prompt — severity matrix, fix rules, report shape. |

## Hard rules

- **qyl bot filter by default.** `cursor[bot]`, `dependabot[bot]`, human reviewers — silently dropped. Pass `additionalBotLogins` to process foreign review bots explicitly.
- **Verify every item.** A later commit may have already fixed the issue. Mark those "skipped (already fixed)" with the commit sha when identifiable; otherwise "manual review required".
- **Root cause, not silencer.** If the bot flagged a missing null-check, add the null-check — do not wrap in try/catch to make the warning go away.
- **Do not fabricate commit hashes.** If you cannot identify why a fix appears already applied, mark it "manual review required" instead.

## Troubleshooting

| Issue | Fix |
|---|---|
| Parser returns 0 comments but PR has bot feedback | Verify the bot login starts with `qyl` or is listed in `additionalBotLogins`. Run `gh api .../comments --jq '[.[] | .user.login] \| unique'` to inspect all author logins. |
| Severity is `Unknown` | Bot didn't emit a `<sub>Severity: …</sub>` line. Treat as `Medium` or surface to the user. |
| `agentPrompt` is empty | Bot did not include the `🤖 Prompt for AI Agent` section. Use `suggestedFix` as the primary input. |
| Fix conflicts with project conventions | Apply the **spirit** of the fix, not the letter; note the deviation in the report. |