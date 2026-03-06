---
name: run-tests
description: Run qyl tests with correct MTP syntax via NUKE. Use when running backend tests, filtering by class/method, or debugging test failures.
---

# Run Tests

Tests use xUnit v3 on Microsoft Testing Platform. Always run through NUKE.

## Quick Commands

```bash
# Run all tests
nuke Test

# Unit tests only
nuke UnitTests

# Integration tests (needs Docker)
nuke IntegrationTests
```

## MTP Filter Syntax

xUnit v3 uses query syntax, not the old VSTest `--filter` flag. Pass via NUKE parameter:

```bash
# Filter by namespace pattern
nuke Test --IQylTest.TestFilter "/*/*/Unit/*"

# Filter by class
nuke Test --IQylTest.TestFilter "/*/CloneTests/*"

# Filter by specific method
nuke Test --IQylTest.TestFilter "/*/*/SkillsTests/TestSkillExecution"

# Stop on first failure + live output
nuke Test --IQylTest.StopOnFail --IQylTest.LiveOutput

# Combined
nuke Test --IQylTest.TestFilter "/*/RpcTests/*" --IQylTest.StopOnFail --IQylTest.LiveOutput
```

## MTP Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All passed |
| 2 | Test(s) failed |
| 8 | Zero tests ran (filter matched nothing — NUKE ignores this automatically) |

## Wrong (never do this)

```bash
# Wrong: VSTest filter syntax
dotnet test --filter "FullyQualifiedName~CloneTests"

# Wrong: raw MTP flags without NUKE
dotnet test -- --filter-namespace "*.Unit.*" --report-trx

# Wrong: missing -- separator
dotnet test --filter-class "*RpcTests*"
```

## Frontend Tests

```bash
# Vitest unit tests
cd src/qyl.dashboard && npm run test -- --run

# Type checking
cd src/qyl.dashboard && npm run typecheck

# Lint
cd src/qyl.dashboard && npm run lint
```

For browser interaction, use Playwright MCP — prefer MCP tools over writing new `.spec.ts` files.

## E2E Tests

E2E tests use the Copilot SDK harness. Requires:

```bash
export COPILOT_AGENT=true
```

No other env vars. Integration tests with persistent storage need Testcontainers (Docker).
