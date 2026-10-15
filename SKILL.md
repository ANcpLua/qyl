# qyl: release and pins

One agent per qyl-dev repo (qyl, qyl.mcp, qyl-api-schema, Qyl.Telemetry.AutoInstrumentation, Qyl.Telemetry.SemanticConventions, qyl.at), each on its own `main`; agent, user and owner are one word. No PR and no publishing workflow; CI only reports: releases run on this machine with the keys in Vitals (`vitals keys`).

## This repository

- Publishes one NuGet release set: the `qyl` tool, its six runtime packages `qyl.<rid>`, `Qyl.Api.Sdk` and `Qyl.Orchestration`.
- Version source: `Version.props` `QylVersion`, bumped in its own commit before the first package-affecting change after a release. The README release lines are rendered from `Version.props` by `./eng/build.sh Check` (target `RenderReadmeVersions`): after a bump or a pin, commit the README it rewrote.
- Bump: Check rewrites `PublicAPI.txt` beside `packages/Qyl.Cli`, `packages/Qyl.Orchestration` and the two libraries Qyl.Api.Sdk ships (`packages/Qyl.Api.Sdk/Qyl.Xml`, `Qyl.Api`) to their public API and notes `api: +N -M changed` or `api: unchanged`; it informs, never blocks. Read `git diff v<last release> -- '*PublicAPI.txt'`: a removed or changed line is major, only added lines minor, none patch.
- Key: Vitals entry "qyl-dev manual publish and release (NuGet)", `~/.config/vitals/nuget-qyl-manual-publish-key`, read by the script. Never print, paste, commit or log it. Entry missing or note expired: stop and tell the owner.
- Upstream pins: `Version.props` `QylApiContractsVersion` (qyl-api-schema), `QylSemanticConventionsVersion` (SemanticConventions), `QylTelemetryVersion` (AutoInstrumentation); `services/qyl.dashboard/package.json` and its `bun.lock` (`@ancplua/qyl-api-schema`).
- A `release.sh` change keeps `python3 eng/scripts/test-release-publish-order.py` green; a quiet-runner change uses `python3 eng/scripts/test-quiet-runner.py`. Index-probe changes can run `./eng/build.sh VerifyReleaseIndex --published-version <published version>` without repeating the unchanged package smokes.

## Refresh changed inputs

1. Pull before starting when the tree is clean and no other writer owns it. Install missing tool versions; restore the dashboard lockfile or .NET packages when their project/package/lockfile inputs change.
2. Compare upstream pins with the registries once per release wave. Re-pin when this clone's next change lands, grouping SemanticConventions and AutoInstrumentation updates. Reuse that comparison and passing checks until their inputs change.

## Release

1. Land the `QylVersion` bump on `main`.
2. Run `./release.sh <x.y.z>` directly as the main agent, without a shell fallback or a waiting subagent. One invocation checks, packs, smokes, publishes, tags the packed commit, uploads a GitHub draft, runs `./eng/build.sh VerifyRelease --published-version <x.y.z>`, publishes the verified GitHub release, and refreshes `published-versions.yaml` in qyl.at with the expected package version. Runtime packages must index before the tool pointer publishes (up to 20 min); verification waits for all nine (up to 45 min), then installs the published tool with an isolated packages folder and `--no-http-cache` and runs QylToolSmoke plus the standalone Qyl.Orchestration package consumer. GitHub upload failures retry at most three times, only after NuGet publication succeeds. Registry probes bypass stale CDN entries; the status distinguishes runtime indexing from library/tool uploads. Do not create tags/releases or refresh the snapshot by hand.
3. Require exit 0 **and VERIFIED** before notifying consumers. Every attempt preserves its complete log and JSON status under `artifacts/release-<version>-<attempt>/`; `artifacts/release-<version>.status.json` names the latest attempt and records its phase, commit, PID and exit code. `./release.sh --status <version>` returns that result even if an outer shell masked the original exit: 4 means still running, 1 means interrupted without a final result. Exit 3 with PENDING means indexing is unresolved. A dry run returns DRY_RUN, never VERIFIED; `--dry-run` is optional and still probes the NuGet key without publishing.
4. Resume published verification with `./release.sh --verify <version>`; it never pushes NuGet packages, checks that the tag, GitHub assets and registry package set exist, then runs verification, publishes a matching GitHub draft if needed, and refreshes the snapshot in the foreground. If only runtime packages were pushed, rerun `./release.sh <version>` to continue its duplicate-safe push. Other failures stop immediately: inspect the attempt's log and fix the reported cause; a broken immutable package needs a patch release. There is no detached verifier or separate `--wait` command.
5. VERIFIED: notify the consumers.

## Notify

| Consumer | Re-pins |
|---|---|
| qyl.at | `src/data/site.ts` (`qyl`, `Qyl.Api.Sdk`, `Qyl.Orchestration`) |

One message per consumer through the session's agent-messaging tool (Claude Code: `ListAgents`, then `SendMessage`):

```
RELEASE qyl + qyl.<rid> + Qyl.Api.Sdk + Qyl.Orchestration <version> indexed <UTC time>
repo: qyl.at
re-pin: src/data/site.ts
then: regenerate the lockfile, run the gate, commit, push main
```

## Re-pin when an upstream release lands

Re-pin when this clone's next change lands:

1. Edit the pin files the message names.
2. Lockfile: `bun install` (not frozen, it rewrites `bun.lock`); `dotnet restore` (`--force-evaluate` where a `packages.lock.json` exists).
3. `mise run check`; commit `chore: pin <package> <version>` with the rendered README; push `main` (deploys the Collector).
4. If the pin changes what ships, bump `QylVersion` and release: one release per repo per wave.

## Order rules

- Contract-revision change: qyl re-pins and pushes first; qyl.mcp re-pins only after `https://api.qyl.at/health` serves the new revision (mcp.qyl.at refuses a Collector on another revision).
- Deployment state for watched source/build changes: `railway deployment list --json` in this clone (project qyl, service `qyl-collector`, environment `production`) must show `status: SUCCESS` and `meta.commitHash` = the pushed commit. Root documentation and tests alone do not trigger a deployment; reuse the existing deployed-runtime evidence for those changes.
- SemanticConventions and AutoInstrumentation both feed qyl: wait until both are indexed, re-pin them in one commit.
