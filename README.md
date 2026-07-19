# qyl

qyl is an OpenTelemetry-compatible observability product for ingesting,
investigating, and visualizing telemetry across all four OTLP signals — traces,
logs, metrics, and profiles.
The product is the collector, DuckDB storage, public investigation API,
dashboard, and local distributed-app host. OpenTelemetry supplies the ingestion
protocol and telemetry vocabulary; it is not Qyl's product API.

qyl is pre-beta. Unpublished surfaces converge directly on a small, executable
product; compatibility requires a proven consumer.

## Architecture

```text
OpenTelemetry semantic conventions
        |
        v
Qyl.OpenTelemetry.SemanticConventions    typed vocabulary
        |
        v
qyl-api-schema                           TypeSpec product contract
        |
        +----> Qyl.Api.Contracts         generated .NET contracts
        +----> generated TS contracts    dashboard and other consumers
        |
        v
qyl.collector                            OTLP ingest (traces, logs, metrics,
        |                                profiles) -> DuckDB -> product API
        v
qyl.dashboard                            investigation UI
```

The main components are:

| Path | Responsibility |
| --- | --- |
| `services/qyl.collector` | Official OTLP wire ingestion (all four signals), DuckDB storage, and the generated-contract product API |
| `services/qyl.dashboard` | React investigation interface backed by generated client contracts |
| `internal/qyl.instrumentation` | Qyl service defaults and Qyl-specific telemetry |
| `internal/qyl.instrumentation.generators`, `internal/qyl.collector.storage.generators` | Compile-time source generators for instrumentation and storage |
| `packages/Qyl.Host` | Published distributed-app runner library with subprocess orchestration and deferred endpoint resolution, no Aspire dependencies |
| `packages/Qyl.Host.Console` | Host console frontend consuming the generated TypeScript contracts (build/typecheck-gated) |
| `packages/Qyl.Host.Mcp` | Optional MCP hosting integration for the runner (stdio/HTTP resources, OTLP export of the MCP SDK ActivitySource) |
| `packages/Qyl.Run.Host` | The `qyl` dotnet tool; packages the collector, embedded dashboard, and isolated diagnostics collector used by `qyl up` |
| `packages/Qyl.Run.Workload` | Synthetic GenAI workload emitter for local end-to-end exercise |
| `eng/build` | Build, generation, verification, and packaging gates |

## Ingestion and product API

OTLP ingestion speaks the official protobuf contract on gRPC (`:4317` —
trace, logs, metrics, and profiles services) and HTTP (`POST /v1/traces`,
`/v1/logs`, `/v1/metrics`, plus `POST /v1development/profiles` for the
development profiles signal).

The generated product API is served under `/api/v1`:

| Route group | Purpose |
| --- | --- |
| `/sessions`, `/sessions/stats`, `/sessions/{id}`, `/sessions/{id}/traces` | Session inventory, statistics, and session-scoped traces |
| `/traces`, `/traces/{id}`, `/traces/{id}/spans` | Trace inventory and full span data |
| `/logs`, `/stream/logs` | Log search and live log streaming |
| `/profiles`, `/profiles/{id}`, `/profiles/by-trace/{id}`, `/profiles/by-span/{id}` | Profile inventory and correlation lookups |

## Contract boundaries

Every client-visible Qyl request, response, stream event, and error is authored in
the external [`qyl-api-schema`](https://github.com/ANcpLua/qyl-api-schema) TypeSpec
repository. Qyl consumes the generated `Qyl.Api.Contracts` package and generated
TypeScript contract artifacts; it does not maintain a second public DTO model in the collector.

OTLP endpoints use the official OpenTelemetry protobuf contract. Collector storage
rows, ingest batches, query models, and internal projections remain implementation
details and are explicitly mapped to generated product contracts before crossing an
HTTP, gRPC, MCP, streaming, or client boundary.

## Instrumentation

[`Qyl.OpenTelemetry.AutoInstrumentation`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation)
provides the managed compile-time instrumentation substrate. It uses Roslyn source
interceptors, build assets, BCL telemetry primitives, and public diagnostic hooks;
it does not use a CLR profiler or runtime IL rewriting. Its generated
[`coverage-matrix.md`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation/blob/main/docs/coverage-matrix.md)
is the detailed capability and evidence record. Qyl does not duplicate that matrix
or turn configuration-only rows into runtime-coverage claims.

Applications onboard through the `Qyl.Sdk` package published from that repository:
`builder.AddQyl()` activates the instrumentation, wires the OpenTelemetry SDK, and
exports to a qyl collector in one call.

## Run qyl locally

Install the prerelease dotnet tool and start the complete local product:

```bash
dotnet tool install --global qyl --prerelease
qyl up
```

`qyl up` is self-contained at the package level: its RID-specific tool package
contains the collector and embedded dashboard it launches. It does not clone qyl,
look for repository-relative projects, or require Node.js. The supported runtime
identifiers are `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, and
`win-arm64`; the .NET 10 runtime is required.

Open `http://127.0.0.1:5100` for the dashboard.

Instrument a .NET application with the first-party SDK:

```bash
dotnet add package Qyl.Sdk
```

```csharp
builder.AddQyl();
```

`AddQyl()` boots the compile-time auto-instrumentation, registers the ASP.NET Core,
HttpClient, and GenAI trace sources, propagates `session.id` across each trace, and
exports over OTLP to a locally discovered `qyl up` collector — no environment
variables required. Standard `OTEL_*` variables take precedence when set.

Any other OTLP-compliant source works by pointing its exporter at the collector:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

The runner binds only to loopback and starts a second isolated collector for the
product collector's own telemetry. `qyl up` fails before launch when one of its
fixed ports is already occupied; it never attaches to an unrelated process.

| Port | Purpose |
| --- | --- |
| `5100` | Product API and embedded dashboard in local `Unsecured` mode |
| `4317` | OTLP/gRPC receiver |
| `4318` | Dedicated OTLP/HTTP receiver |
| `5200` | Isolated diagnostics collector API |
| `18888` | Loopback runner resource API |

### Attaching an MCP server

`qyl up` can supervise one MCP server alongside the stack and project it onto the
runner API (`/runner/mcp/mcp/tools`, `/tools/call`, `/resources/read`):

```bash
qyl up --mcp-stdio <command> [args...]   # launch a child over stdio
qyl up --mcp-http <url>                  # attach over Streamable HTTP
```

Everything after `--mcp-stdio` is the child command line. The child inherits this
process's environment plus `QYL_COLLECTOR_URL=http://127.0.0.1:5100` and
`QYL_OTLP_ENDPOINT=http://127.0.0.1:4318`, so a qyl-aware MCP server reads from
and exports telemetry to this stack with no configuration —
[qyl.mcp](https://github.com/ANcpLua/qyl.mcp)'s published server composes as:

```bash
qyl up --mcp-stdio npx -y qyl-mcp-server --stdio
``` The attachment waits for the collector, then
readiness is a completed MCP handshake (initialize plus `tools/list`); a failed
attachment marks only the `mcp` resource failed and never blocks the collector
or dashboard.

### Native AOT publish

Native AOT is the collector's publish contract: `QylAot` defaults on, so
`dotnet publish services/qyl.collector` produces a native binary, and the
Dockerfile builds on the AOT SDK image and runs on `runtime-deps`. Publish with
`-p:QylAot=false` for a JIT diagnostic build with full analyzer enforcement.
The native lane's executable owner is `eng/scripts/collector-aot-smoke.sh`.

## Verify

Run the repository gate before pushing:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

To prove the distributable itself, build the frontends, pack every published
library plus all RID-specific `qyl` tool packages, install the current platform's
tool into a clean directory, ingest a real OTLP trace, read it back through the
product API, and verify Ctrl-C tears down every child:

```bash
dotnet run --project eng/build/build.csproj -- PackSmoke
```

Generated files are outputs, not editing surfaces. Change the owning TypeSpec,
protobuf input, generator, or manifest and regenerate in the same commit.

## License

MIT
