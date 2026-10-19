# qyl

qyl is an analytical engine for AI agents. The Collector (C#; NativeAOT in the hosted container,
framework-dependent in the local `qyl` tool) ingests OpenTelemetry traces, logs and metrics, stores them per project in DuckDB and answers typed analysis questions:
triage, failures and their context, comparisons, fix verification, aggregates, agent runs,
releases, service maps and telemetry health. Agents use it through the MCP server
(TypeScript SDK v2, protocol `2026-07-28`, 2025-era clients negotiated down).
[docs/architecture.yaml](docs/architecture.yaml) is the architecture.

## Endpoints

| Endpoint | Serves |
| --- | --- |
| `https://mcp.qyl.at/mcp` | Hosted MCP server. OAuth 2.1 (Auth0 issuer `https://qyl-eu.eu.auth0.com/`), scopes `qyl:read`, `qyl:write`, `qyl:delete`; an unauthenticated request answers `401` with `resource_metadata`. `/healthz` is its health: `degraded` names both contract revisions while the Collector serves another, and tools answer `collector_contract_mismatch` until both match. |
| `https://api.qyl.at` | Hosted Collector: read API under `/api/v1/`, analysis under `POST /api/v1/analysis/<tool_name>`, OTLP under `/v1/traces`, `/v1/logs`, `/v1/metrics`. `/health` carries `contract_revision`. |
| `bunx qyl-mcp-server --stdio` | Local MCP over stdio; `bunx qyl-mcp-server` serves HTTP on `127.0.0.1:3001`. Point it at a Collector with `QYL_COLLECTOR_URL` and, if secured, `QYL_API_KEY`. |
| `dotnet tool install --global qyl && qyl up` | Local Collector API on `127.0.0.1:5100`, diagnostics on `:5200`, OTLP on `:4318` (HTTP) and `:4317` (gRPC), runner API on `:18889`; data under `~/.qyl/`. |

Applications send telemetry with `Qyl.Telemetry.Hosting` (`builder.AddQyl()`); APIs built by
`Qyl.Api.Sdk` (`<Project Sdk="Qyl.Api.Sdk/<version>">`) are instrumented from their first line.

## Layout

```text
contracts/                 TypeSpec contract, emitters, generation scripts; openapi/qyl.openapi.json
services/qyl.collector*/   Collector: host and analysis, OTLP ingestion, DuckDB storage
services/qyl.mcp/          MCP server (npm qyl-mcp-server): src/, skills/, eval/, smokes
internal/                  generated C# contracts (qyl.contracts), Collector self-telemetry defaults, source generators
packages/                  qyl tool (Qyl.Cli), Qyl.Orchestration, Qyl.Api.Sdk, Qyl.Run.Workload
plugins/qyl/               plugin bundles and connector listing texts
samples/qyl.sample/        API built by Qyl.Api.Sdk
tests/                     .NET tests
eng/                       Fallout build (eng/build), scripts, smoke tools, Auth0 tenant config
docs/                      architecture, configuration, attribute persistence, MCP threat model
.railway/railway.ts        Railway services qyl-collector and qyl-mcp
```

## Build, test, release

`mise install` installs the pinned dotnet, bun and node.

| Command | Does |
| --- | --- |
| `./eng/build.sh Check --Configuration Release` (`mise run check`) | `VerifyContractsCurrent` (regenerates the contract outputs and fails on any difference), compile, .NET tests, orchestration smoke, `Verify` (routes, key roles and limits against the OpenAPI contract, generated semantic catalog, project-scoped reads, span and log storage identity, instrumentation redaction), `McpBuild` and `McpTest` (MCP lint, unit tests, smokes, answer key and project isolation against the Collector built here). Runs on every push and pull request. |
| `./eng/build.sh CheckFull --Configuration Release` (`mise run check-full`) | Check plus the Collector NativeAOT container smoke, the packed-tool smoke and the Qyl.Api.Sdk proof. Runs on `main`, and on `gh workflow run check.yml --ref <branch>` to verify a commit before `main` moves; the container stages need Docker. |
| `./eng/build.sh Contracts` | Regenerate `contracts/openapi`, `internal/qyl.contracts` and `services/qyl.mcp/src/contracts/generated` from the TypeSpec; commit them with the TypeSpec change. |
| `./eng/build.sh GenerateCollectorSemanticAttributeCatalog` | Regenerate the Collector's semantic catalog after a SemanticConventions pin change. |
| `./release.sh` | Release `Version.props` `QylVersion`: pack once into `artifacts/release/<version>/` with `SHA512SUMS`, publish what nuget.org and npm lack, tag `v<version>`, push the tag. Rerun to resume. |

`./eng/build.sh --help` lists every target. A push to `main` deploys both Railway services when
their watched paths change.

## License

MIT
