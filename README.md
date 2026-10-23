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
| `contracts/` | TypeSpec for every wire shape, constraint and encoding. It generates the OpenAPI, C# types and source-generated serializers in `internal/qyl.contracts`, and TypeScript types and MCP tool schemas. Both runtimes validate the same generated `contract-schemas.json`. |
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
| `./release.sh --plan` | Shows the release phases without reading credentials, publishing packages or changing Git. |
| `./release.sh` | Releases `Version.props` `QylVersion` from one sealed artifact set. Publishes libraries, runtimes, the tool pointer and npm in order; waits for registry visibility and package download availability before advancing. Then tags the release and advances production. Rerun it to resume. |
| `mise run hosted-eval` | Checks the hosted MCP server's reads, scope challenges and input validation with a token from the Keychain. |

Registry polling uses bounded backoff; `QYL_RELEASE_READY_TIMEOUT` sets the wait limit in seconds (1800 by default). An accepted upload or duplicate response does not bypass the availability check. Installed clients still need to update to the new version; local contract project references use the source on the next build.

## Documentation

- [MCP server](https://github.com/ANcpLua/qyl/blob/main/services/qyl.mcp/README.md): tools, sign-in, configuration and telemetry
- [Architecture](https://github.com/ANcpLua/qyl/blob/main/docs/architecture.yaml)
- [Security policy](https://github.com/ANcpLua/qyl/blob/main/SECURITY.md)
- [Changes per version](https://github.com/ANcpLua/qyl/releases)

## Contract parity

`ContractJson` in `Qyl.Api.Contracts` validates JSON before deserialization and validates the serialized result before it leaves the Collector. The TypeScript `checkContract` and MCP tool schemas use the same normalized TypeSpec schemas. A new schema assertion must be supported by both runtimes; generation fails for unknown assertions.

`Timestamp` is wire text in both languages. It preserves RFC 3339 spelling, offset and fractional precision instead of passing through `DateTimeOffset`. The Collector converts a timestamp to an instant only when executing a query; its storage range and 100 ns execution precision remain execution constraints. Numeric JSON counters use `JsonInteger`, limited to exactly representable integers. OTLP 64-bit integers and nanoseconds remain canonical decimal strings across their full ranges. String enums do not accept numeric ordinals, and MCP arguments are validated without quote coercion. Generated `OptionalValue<T>` members preserve the difference between absence and an explicit null where TypeSpec permits both.

C# consumers use `ContractJson.Serialize(value)` and `ContractJson.Deserialize<T>(json)` for contract bodies, including paginated responses. HTTP parameter bounds and the analysis operation map are generated with the models. `default(OptionalValue<T>)` means absent; constructing it with a value, including null, means present. Trace comparison preserves that distinction before serialization. The telemetry test consumers reference the local contracts project so they cannot silently validate an older published model.

`Qyl.Telemetry.Tests/Qyl.Telemetry.IntegrationTests/Contracts`, included in `Qyl.Telemetry.Tests.slnx`, runs the shared fixtures against the actual C# validator/serializers and the actual TypeScript validator under Bun. It also checks that both generated schema artifacts are byte-identical. From `Qyl.Telemetry.Tests`, run:

```sh
dotnet test --project Qyl.Telemetry.IntegrationTests/Qyl.Telemetry.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Qyl.Telemetry.IntegrationTests.Contracts"
```

The integration harness runs under the managed Microsoft.Testing.Platform runner; that does not establish NativeAOT test execution. Bun and the MCP workspace dependencies must be installed. `contracts/scripts/lib/encoded-integers.test.mjs` checks narrower and exclusive integer bounds during every contract generation.

## License

qyl is free, under the [MIT license](https://github.com/ANcpLua/qyl/blob/main/LICENSE). That covers
the Collector, the MCP server, the contracts and every package this repository publishes: `qyl`,
`qyl.<rid>`, `Qyl.Api.Contracts`, `Qyl.Api.Sdk` and `Qyl.Orchestration` on NuGet, and
`qyl-mcp-server` on npm.

The plugin bundle in `plugins/qyl` is `MIT AND Apache-2.0`, because it carries the ancplua-dotnet
skills under Apache-2.0, with their LICENSE and NOTICE in `plugins/qyl/licenses/ancplua-dotnet/`.
Its MSTest skills are MIT, with their LICENSE in `plugins/qyl/licenses/mstest-extensions-plugin/`.
