---
paths:
  - "tests/**"
---

# Testing Rules

## Framework
- xUnit v3 with Microsoft Testing Platform (MTP)
- Use `run-tests` skill (NUKE build system), never raw `dotnet test`
- Prefer `nuke` (default test entry), `nuke UnitTests`, or `nuke IntegrationTests`; avoid explicit `nuke Test` to stay aligned with repo guidance
- MTP filter syntax: `--filter-method "*MethodName"`, `--filter-class "*ClassName"`

## MTP Exit Codes
| Exit | Meaning |
|------|---------|
| 0 | All passed |
| 2 | Test(s) failed |
| 8 | Zero tests ran (filter matched nothing — check spelling) |

## Conventions
- E2E tests via Copilot SDK harness (requires COPILOT_AGENT=true)
- Coverage via Microsoft.Testing.Extensions.CodeCoverage
- TRX reports via Microsoft.Testing.Extensions.TrxReport

## Dashboard Tests
- Unit: Vitest (`npm run test` in src/qyl.dashboard/)
- E2E: Playwright (`npm run e2e`)
