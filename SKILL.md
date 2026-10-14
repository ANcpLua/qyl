# qyl: release and pins

One agent per qyl-dev repo (qyl, qyl.mcp, qyl-api-schema, Qyl.Telemetry.AutoInstrumentation, Qyl.Telemetry.SemanticConventions, qyl.at), each on its own `main`; agent, user and owner are one word. No PR and no publishing workflow; CI only reports: releases run on this machine with the keys in Vitals (`vitals keys`).

## This repository

- Publishes one NuGet release set: the `qyl` tool, its six runtime packages `qyl.<rid>` and `Qyl.Api.Sdk`.
- Version source: `Version.props` `QylVersion`, bumped in its own commit before the first package-affecting change after a release. The README release lines are rendered from `Version.props` by `./eng/build.sh Check` (target `RenderReadmeVersions`): after a bump or a pin, commit the README it rewrote.
- Bump: Check rewrites `PublicAPI.txt` beside `packages/Qyl.Cli` and the two libraries Qyl.Api.Sdk ships (`packages/Qyl.Api.Sdk/Qyl.Xml`, `Qyl.Api`) to their public API and notes `api: +N -M changed` or `api: unchanged`; it informs, never blocks. Read `git diff v<last release> -- '*PublicAPI.txt'`: a removed or changed line is major, only added lines minor, none patch.
- Key: Vitals entry "qyl-dev manual publish and release (NuGet)", `~/.config/vitals/nuget-qyl-manual-publish-key`, read by the script. Never print, paste, commit or log it. Entry missing or note expired: stop and tell the owner.
- Upstream pins: `Version.props` `QylApiContractsVersion` (qyl-api-schema), `QylSemanticConventionsVersion` (SemanticConventions), `QylTelemetryVersion` (AutoInstrumentation); `services/qyl.dashboard/package.json` and its `bun.lock` (`@ancplua/qyl-api-schema`).
- A `release.sh` change keeps `python3 eng/scripts/test-release-publish-order.py` green.

## Stale-version guard, before any work

1. `git pull --ff-only origin main`, `mise install`, `bun install --frozen-lockfile` in `services/qyl.dashboard`, `dotnet restore`.
2. Compare every upstream pin with the index: `npm view <package> version`; NuGet `curl -fsSL https://api.nuget.org/v3-flatcontainer/<id, lowercase>/index.json` (last entry). If the index is ahead, re-pin first.

## Release

1. Land the `QylVersion` bump on `main`.
2. `./release.sh <x.y.z>` (`--dry-run` first only when a step is new or unsure; the script refuses before publishing when anything is off). A dry run pushes nothing: it proves the NuGet key for each of the eight ids through NuGet's package-verification handshake (a one-use key minted and verified against the latest published version, three attempts); an existing tag or published version only warns. The runtime packages are indexed before the tool pointer publishes (in the foreground, up to 20 min); it returns once qyl and Qyl.Api.Sdk are accepted and the tag (on the commit the checks and the pack ran on) and GitHub release exist. A detached job runs `./eng/build.sh VerifyRelease --published-version <x.y.z>` (index wait for all eight, the published tool through QylToolSmoke) and then the snapshot refresh (`published-versions.yaml` in qyl.at), logging to `artifacts/release-<x.y.z>-verify.log`. Do not run the snapshot refresh by hand.
3. `./release.sh --wait <x.y.z>` blocks until that log ends in VERIFIED or FAILED; notify no consumer before. Exit 3 = not indexed in time, `./release.sh --verify <x.y.z>` checks again; any other FAILED after publication is a patch release.
4. VERIFIED: notify the consumers.

## Notify

| Consumer | Re-pins |
|---|---|
| qyl.at | `src/data/site.ts` (`qyl`, `Qyl.Api.Sdk`) |

One message per consumer through the session's agent-messaging tool (Claude Code: `ListAgents`, then `SendMessage`):

```
RELEASE qyl + qyl.<rid> + Qyl.Api.Sdk <version> indexed <UTC time>
repo: qyl.at
re-pin: src/data/site.ts
then: regenerate the lockfile, run the gate, commit, push main
```

## Re-pin when an upstream release lands

Act at once, before your own work:

1. Edit the pin files the message names.
2. Lockfile: `bun install` (not frozen, it rewrites `bun.lock`); `dotnet restore` (`--force-evaluate` where a `packages.lock.json` exists).
3. `mise run check`; commit `chore: pin <package> <version>` with the rendered README; push `main` (deploys the Collector).
4. If the pin changes what ships, bump `QylVersion` and release: one release per repo per wave.

## Order rules

- Contract-revision change: qyl re-pins and pushes first; qyl.mcp re-pins only after `https://api.qyl.at/health` serves the new revision (mcp.qyl.at refuses a Collector on another revision).
- Deployment state: `railway deployment list --json` in this clone (project qyl, service `qyl-collector`, environment `production`): the newest entry shows `status: SUCCESS` and `meta.commitHash` = the pushed commit.
- SemanticConventions and AutoInstrumentation both feed qyl: wait until both are indexed, re-pin them in one commit.
