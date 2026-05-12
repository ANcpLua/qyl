# ANcpLua Auto-Merge App — Design

Status: design-stage. Bulldozer (`palantirtechnologies/bulldozer:1.19.3`) is the
six-month bridge. This document is the long-term contract for the self-built
replacement that takes over the same GitHub App registration, webhook secret,
and Railway service slot once Bulldozer's limitations bite.

## Why build it

The bridge is necessary because no off-the-shelf App is the right shape:

| Predecessor | Steal | Drop |
| --- | --- | --- |
| Mergify (SaaS) | `extends: <central>` inheritance — one config repo, every consumer references it by name. Speculative-execution merge queue. | SaaS dependency. Per-repo stub still mandatory. No admin-bypass for branch protection. |
| Kodiak (self-host) | Branch-protection-native simplicity — no parallel admin tier, the App does what the protection rules already say. | No central / org config; every repo needs its own `.kodiak.toml`. Recreates the boilerplate we're killing. |
| Bulldozer (Palantir, self-host) | Zero-touch `.github` org fallback — install once, every repo inherits unless it opts out. Single Go binary, MIT, runs anywhere. | Slow release cadence (last tag 2024-06). No author allow-list as a first-class trigger. No admin override for sticky `CHANGES_REQUESTED` reviews. |

Best-of-three target: **Bulldozer's org-fallback shape + Mergify's `extends` semantics + Kodiak's branch-protection-native posture, plus a first-class trusted-author allow-list and an opt-in admin tier for repos that explicitly need it.**

## Surface

### Config resolution order

For repo `<owner>/<repo>`, the App resolves config in this order and stops at the first hit:

1. `<owner>/<repo>:.github/auto-merge.yml` — per-repo override.
2. `<owner>/.github:.github/auto-merge.yml` — org / user-account fallback. Uses GitHub's standard `.github`-repo discovery (works for Orgs *and* User namespaces; this is the divergence from Bulldozer that gets `ANcpLua/*` user-account repos covered without per-repo files).
3. An `extends:` chain — any config above can reference shared rule packs by repo+ref, Mergify-style:
   ```yaml
   extends:
     - ANcpLua/automerge-rules@main:trusted-bots.yml
     - O-ANcppLua/.github@main:branch-protection-friendly.yml
   ```
4. Built-in default — no auto-merge, behaviour is identical to "App not installed".

### Trigger surface

```yaml
version: "1"

merge:
  trigger:
    labels: ["auto-merge"]
    auto_merge: true             # GitHub native auto-merge toggle counts
    comment_substrings: ["==MERGE_PR=="]
    authors:                     # NEW: first-class allow-list, missing in Bulldozer
      - ANcpLua
      - "renovate[bot]"
      - "dependabot[bot]"
      - "copilot[bot]"
      - "coderabbitai[bot]"
      - "jules[bot]"
      - "claude-code[bot]"
      - "github-actions[bot]"

  ignore:
    labels: ["do-not-merge", "wip", "blocked", "needs-review"]
    draft: true

  method: squash
  delete_after_merge: true

  # Branch protection is the source of truth — same Kodiak posture.
  required_statuses: from_branch_protection
  allow_merge_with_no_checks: false

  # OPT-IN admin tier — off by default. When `true`, the App auto-approves
  # sticky CHANGES_REQUESTED reviews from the configured bots and uses the
  # GraphQL `enablePullRequestAutoMerge(mergeMethod: SQUASH, authorEmail: …,
  # commitHeadline: …)` mutation. The App installation must hold admin on
  # the repo for this to work — same constraint as the legacy
  # `destructive-auto-merge.yml` workflow.
  admin_override:
    enabled: false
    override_sticky_changes_requested_from:
      - "coderabbitai[bot]"
      - "copilot[bot]"
```

### Webhook event subscription

Same set Bulldozer subscribes to, plus `installation_repositories` so we don't need a manual sync when a repo is added to the App:

- `pull_request`
- `pull_request_review`
- `pull_request_review_comment`
- `issue_comment`
- `check_run`
- `status`
- `push`
- `commit_comment`
- `installation_repositories`

## Architecture

```
GitHub App  ──webhook──>  Railway service (ANcpLua.AutoMerge)
                              │
                              ├─> Config resolver (in-memory cache, 5-min TTL)
                              │     fetches `.github/auto-merge.yml` from the
                              │     resolution chain via REST + GraphQL
                              │
                              ├─> Trigger evaluator (pure function over PR + config)
                              │
                              ├─> Action executor
                              │     - GraphQL `enablePullRequestAutoMerge`
                              │       (default path; branch-protection-native)
                              │     - `gh pr review --approve` + REST PUT
                              │       /repos/.../pulls/.../merge with `merge_method=squash`
                              │       (admin tier; only if admin_override.enabled)
                              │
                              └─> OTel exporter → qyl-collector
                                    (every webhook → span; every merge decision → event;
                                     spans tagged with `automerge.repo`, `automerge.pr`,
                                     `automerge.trigger`, `automerge.outcome`)
```

Service shape mirrors the qyl services in the same Railway project:
`ANcpLua.AutoMerge/Dockerfile`, `railway.toml`, `/health` endpoint, structured logs.
Language: C# / .NET 10 to share `Qyl.OpenTelemetry.Extensions` + `Qyl.Telemetry`
out of the box — no new SDK in the family.

## Migration shape

Bulldozer → ANcpLua.AutoMerge is a **drop-in swap** by design:

1. The new App registration is the **same** App ID and private key Bulldozer is currently using. The Railway service is rebuilt from the new image; webhook URL is updated on github.com to point at the new service path.
2. Existing `bulldozer.yml` files map automatically: same trigger semantics (labels / `auto_merge` / comment-substrings / safety labels) and same `version: "1"` shape. The new App reads them as a back-compat fallback so a flag-day swap stays safe.
3. Repos can adopt the new `.github/auto-merge.yml` schema (with `authors:` and `admin_override:`) at their own pace.

## Non-goals

- Merge queue with speculative execution. Mergify's killer feature, but irrelevant for a solo dev with <20 active repos; can be added later as `queue: serial` (the only mode that ships before merge-queue work begins on actual demand).
- Multi-tenant SaaS. The App is single-tenant (Alexander) and self-hosted on Railway; multi-tenant requires PostgreSQL state, per-installation isolation, and a billing surface that doesn't exist.
- Replacing branch protection. The App reads it, doesn't replace it.
- Replacing `anti-slop` / `refix`. Those run as workflows in each repo; this App only handles the merge decision, not the gating or fix-up.

## Open questions for the build phase

1. Should the `extends:` chain hot-reload when an upstream config repo updates, or only on the next webhook for the consumer? (Lean: webhook-only — simpler invalidation, acceptable lag.)
2. Should the OTel exporter be a service-level env var on the Railway deployment (e.g. `AUTOMERGE_OTEL_ENABLED=true`, `AUTOMERGE_OTEL_ENDPOINT=https://qyl-api-production.up.railway.app/v1/traces`) rather than a per-installation YAML field? (Lean: yes — telemetry is a deployment concern, not a per-repo policy; the YAML stays focused on merge rules. The ANcpLua deployment sets the env var on; an open-sourced fork starts with it unset and the exporter no-ops.)
3. How is the admin private key stored in Railway? (Lean: Railway shared variable `AUTOMERGE_APP_PRIVATE_KEY`, same name the existing destructive workflow uses, so swapping is one env-var move.)

## Related

- `docs/automation.md` — current per-repo workflow inventory; this App replaces `destructive-auto-merge.yml` in that table.
- `O-ANcppLua/.github:bulldozer.yml` — the bridge config; the App reads it as a back-compat source.
- `.agents/HANDOFF.md` (gitignored) — the original five-step migration checklist.
