# Routine Test Run — 2026-05-16

## Status: STOPPED (pre-existing environment blocker)

## What was broken on arrival

The remote execution container does not have the `dotnet` CLI installed, and the
network policy blocks downloading it:

```
$ curl -L "https://dot.net/v1/dotnet-install.sh"
Host not in allowlist
```

`./eng/build.sh Ci` exits with code 22 (curl HTTP error) before any .NET code
is compiled or tested. This is a container/environment configuration issue, not
a code defect.

## Actions taken

- Read CLAUDE.md / AGENTS.md to understand repo conventions.
- Confirmed `dotnet` is absent from all standard paths (`/usr/bin`, `/usr/local/bin`,
  `/opt`, etc.).
- Confirmed the `dot.net` install URL is blocked by the environment's network
  allowlist.
- No code was modified.

## What needs to happen for the next run

The execution environment must either:
1. Have the .NET 10 SDK pre-installed (matching `global.json`: `10.0.203`,
   `rollForward: latestFeature`, `allowPrerelease: true`), or
2. Have `https://dot.net` added to the network allowlist so `eng/build.sh`
   can auto-install it on first run.

## What would be targeted next (test gaps)

Once the environment is fixed, priority targets are:
- `services/qyl.collector/Storage/` — DuckDB read/write path integration tests.
- `services/qyl.collector/Errors/` — error ingestion endpoint unit tests.
- `services/qyl.mcp/` — MCP tool registration and telemetry unit tests.
