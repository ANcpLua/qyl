# qyl

qyl is an analytical engine built for AI agents. Services send it OpenTelemetry traces, logs and
metrics; agents query it through MCP and get typed answers: what changed, why a request failed or
was slow, whether a fix held, how an agent run, a release or a CI run behaved. qyl measures and
correlates; the agent reasons and acts. qyl ships no dashboard or other product UI.

Built over ten months by one developer, starting with Claude Opus 4.5, using Anthropic and OpenAI models to the fullest.

## Official sources

- Website: https://qyl.at
- Source: https://github.com/ANcpLua/qyl
- Hosted MCP server: https://mcp.qyl.at/mcp
- npm: [`qyl-mcp-server`](https://www.npmjs.com/package/qyl-mcp-server)
- NuGet: [`qyl`](https://www.nuget.org/packages/qyl) and [`Qyl.Api.Contracts`](https://www.nuget.org/packages/Qyl.Api.Contracts)

## Connect an agent

Add `https://mcp.qyl.at/mcp` as a remote MCP server (Streamable HTTP). Sign-in is OAuth through
qyl's Auth0 tenant `qyl-eu`. The client registers itself with a Client ID Metadata Document (CIMD)
or Dynamic Client Registration (DCR), so no client ID is needed.

| Scope | Allows |
| --- | --- |
| `qyl:read` | Every call needs it; the 20 read tools need nothing else. |
| `qyl:write` | Creating and changing annotations and saved queries, and setting a failure's status. |
| `qyl:delete` | Deleting annotations and saved queries. |

No tool changes or deletes telemetry; retention alone removes it. The server speaks MCP protocol
`2026-07-28` and serves 2025-era clients from the same endpoint, through the MCP TypeScript SDK v2
default `legacy: 'stateless'`. The [MCP server README](https://github.com/ANcpLua/qyl/blob/main/services/qyl.mcp/README.md)
lists the 24 tools, their result and error shapes and the server's configuration.

Locally, run a Collector with the `qyl` tool and give the MCP client `bunx qyl-mcp-server --stdio`
as its server command:

```bash
dotnet tool install --global qyl && qyl up
bunx qyl-mcp-server --stdio    # started by the MCP client
```

## Send telemetry

Any OpenTelemetry SDK exports to the Collector over OTLP: `/v1/traces`, `/v1/logs` and
`/v1/metrics` over HTTP (protobuf or JSON), or gRPC. `qyl up` listens on `http://127.0.0.1:4318`
(HTTP) and `127.0.0.1:4317` (gRPC) and accepts telemetry without a key. The hosted Collector takes
OTLP over HTTP at `https://api.qyl.at` with the project's key in the `x-otlp-api-key` header. An
API built with [`Qyl.Api.Sdk`](https://github.com/ANcpLua/qyl/blob/main/packages/Qyl.Api.Sdk/README.md)
exports from its first line.

## Repository

| Path | What it is |
| --- | --- |
| `services/qyl.collector*` | The Collector (C#, .NET 10): OTLP ingestion over HTTP and gRPC, DuckDB storage per project, and every analysis, behind `POST /api/v1/analysis/<tool_name>`. NativeAOT in the hosted container, framework-dependent in the `qyl` tool. |
| `services/qyl.mcp` | The MCP server (TypeScript, MCP TypeScript SDK v2, Bun), npm `qyl-mcp-server`. It validates, scopes and frames tool calls and serves the `qyl-investigate` skill; it adds no analysis of its own. |
| `contracts/` | TypeSpec for every wire shape. It generates `contracts/openapi/qyl.openapi.json`, the C# types in `internal/qyl.contracts` (NuGet `Qyl.Api.Contracts`, released with the Collector at the same version) and the MCP server's TypeScript types and tool schemas. |
| `plugins/qyl` | The plugin bundle: the hosted MCP connection and 18 skills, `qyl-investigate` among them. |
| `packages/Qyl.Cli` | The `qyl` tool. `qyl up` runs a local Collector ([README](https://github.com/ANcpLua/qyl/blob/main/packages/Qyl.Cli/README.md)). |
| `packages/Qyl.Api.Sdk` | An MSBuild SDK for NativeAOT ASP.NET Core APIs that are validated, documented and instrumented from the first line. |
| `packages/Qyl.Orchestration` | The process supervisor `qyl up` runs on, as a library. |
| `packages/Qyl.Run.Workload` | A synthetic workload that sends telemetry to a local Collector. |
| `internal/` | The generated C# contracts, the Collector's self-telemetry defaults and the source generators. |
| `samples/qyl.sample` | An API built with `Qyl.Api.Sdk`. |
| `tests/` | The .NET tests (MSTest on Microsoft.Testing.Platform). |
| `eng/` | The Fallout build (`eng/build`), scripts, smoke tools and the Auth0 tenant configuration (`eng/auth0`). |
| `docs/architecture.yaml` | The target architecture: layout, ownership, contracts. |
| `.railway/railway.ts` | The two Railway services: `qyl-collector` at `https://api.qyl.at` and `qyl-mcp` at `https://mcp.qyl.at`. |

`https://mcp.qyl.at/healthz` reports `ok`, or `degraded` while the Collector is unreachable or
serves another contract revision. `https://api.qyl.at/health` reports the Collector's health and its
`contract_revision`.

## Build, test, release

`mise install` installs the pinned dotnet, bun and node. The build is Fallout, and
`./eng/build.sh --help` lists every target.

| Command | Does |
| --- | --- |
| `./eng/build.sh Check --Configuration Release` | The gate before every push, and in CI on every push and pull request: generated contracts current, compile, .NET tests, the orchestration smoke, the Collector's contract and storage checks, and the MCP server's SDK boundary check, lint, unit tests, smokes, answer key and project isolation against the Collector built here. |
| `./eng/build.sh CheckFull --Configuration Release` | Check plus the Collector NativeAOT container smoke, the packed-tool smoke and the Qyl.Api.Sdk proof. CI runs it on `main`; the container stages need Docker. |
| `./eng/build.sh Contracts` | Regenerates the contract outputs from the TypeSpec; commit them with the TypeSpec change. |
| `./eng/build.sh GenerateCollectorSemanticAttributeCatalog` | Regenerates the Collector's semantic catalog after a SemanticConventions pin change. |
| `./release.sh` | Releases `Version.props` `QylVersion`: packs once into `artifacts/release/<version>/` with `SHA512SUMS`, publishes what nuget.org and npm lack, tags `v<version>` and creates the GitHub release. Rerun it to resume. |
| `mise run hosted-eval` | Checks the hosted MCP server's reads, scope challenges and input validation with a token from the Keychain. |

## Documentation

- [MCP server](https://github.com/ANcpLua/qyl/blob/main/services/qyl.mcp/README.md): tools, sign-in, configuration and telemetry
- [Architecture](https://github.com/ANcpLua/qyl/blob/main/docs/architecture.yaml)
- [Security policy](https://github.com/ANcpLua/qyl/blob/main/SECURITY.md)
- [Changes per version](https://github.com/ANcpLua/qyl/releases)

## License

qyl is free, under the [MIT license](https://github.com/ANcpLua/qyl/blob/main/LICENSE). That covers
the Collector, the MCP server, the contracts and every package this repository publishes: `qyl`,
`qyl.<rid>`, `Qyl.Api.Contracts`, `Qyl.Api.Sdk` and `Qyl.Orchestration` on NuGet, and
`qyl-mcp-server` on npm.

The plugin bundle in `plugins/qyl` is `MIT AND Apache-2.0`, because it carries the ancplua-dotnet
skills under Apache-2.0, with their LICENSE and NOTICE in `plugins/qyl/licenses/ancplua-dotnet/`.
Its MSTest skills are MIT, with their LICENSE in `plugins/qyl/licenses/mstest-extensions-plugin/`.
