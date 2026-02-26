# ADR-004: Remove qyl.cli

Status: Accepted
Date: 2026-02-26
Depends-On: ADR-003

## Context

qyl.cli (`qyl init`) does three things:
1. Detect stack (csproj, docker-compose, package.json)
2. Add PackageReference to csproj
3. Insert code into Program.cs

All three are replaced by the NuGet-first approach (ADR-003): `dotnet add package qyl.servicedefaults` + one line of code. No CLI needed.

## Decision

Remove `src/qyl.cli/` from the repository. Everything it does is replaced by a standard NuGet install.

### What Replaces qyl.cli

| qyl.cli Feature | Replacement |
|-----------------|-------------|
| Stack detection | Source generators auto-detect at compile time |
| Add PackageReference | `dotnet add package qyl.servicedefaults` |
| Insert code into Program.cs | User adds `builder.AddQylServiceDefaults()` |
| Docker-compose editing | User adds qyl service manually (documented in dashboard) |

No code migration needed — the source generators already handle everything qyl.cli did, but better (compile-time, incremental, zero runtime overhead).

## Acceptance Criteria

```gherkin
GIVEN the qyl repository
WHEN  src/qyl.cli/ is removed
THEN  qyl.slnx has no reference to qyl.cli
AND   dotnet build qyl.slnx succeeds
AND   CLAUDE.md has no mention of qyl.cli
AND   README.md has no mention of qyl.cli
```

## Verification Steps (Agent-Executable)

1. Remove `src/qyl.cli/` directory
2. Remove from `qyl.slnx`
3. Remove from `CLAUDE.md`, `README.md`, `.github/copilot-instructions.md`
4. `dotnet build qyl.slnx` → assert success
5. `dotnet test` → assert all tests pass
6. Grep for `qyl.cli` in repo → assert zero matches (except CHANGELOG historical)

## Consequences

- One less project to maintain
- Spectre.Console dependency removed from qyl (only qyl.watch keeps it)
- YamlDotNet dependency removed (was only used by ComposeEditor)
- Users who want CLI-based setup use `dotnet add package qyl.servicedefaults` directly
