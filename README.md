# qyl

qyl is an OTLP-native, DuckDB-backed observability product for ingesting,
investigating, and visualizing traces and logs. Metrics are accepted for wire
compatibility and discarded. Profiles are not supported.
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
qyl.collector                            OTLP traces + logs -> DuckDB -> product API
        |                                OTLP metrics -> discard acknowledgement
        v
qyl.dashboard                            investigation UI
```

The main components are:

| Path | Responsibility |
| --- | --- |
| `services/qyl.collector` | Official OTLP trace/log ingestion and DuckDB storage, metrics discard acknowledgement, and the generated-contract product API |
| `services/qyl.dashboard` | React investigation interface backed by generated client contracts |
| `internal/qyl.instrumentation` | Qyl service defaults and Qyl-specific telemetry |
| `internal/qyl.instrumentation.generators`, `internal/qyl.collector.storage.generators` | Compile-time source generators for instrumentation and storage |
| `packages/Qyl.Host` | Published distributed-app runner library with subprocess orchestration and deferred endpoint resolution, no Aspire dependencies |
| `packages/Qyl.Host.Console` | Host console frontend consuming the generated TypeScript contracts (build/typecheck-gated) |
| `packages/Qyl.Run.Host` | The `qyl` dotnet tool; packages the collector, embedded dashboard, and isolated diagnostics collector used by `qyl up` |
| `packages/Qyl.Run.Workload` | Synthetic GenAI workload emitter for local end-to-end exercise |
| `eng/build` | Build, generation, verification, and packaging gates |

## Ingestion and product API

OTLP ingestion speaks the official protobuf contract on gRPC (`:4317`) and HTTP
(`:4318`). Trace and log exports are stored. Metric exports are counted,
discarded, and acknowledged with an OTLP `partial_success` response. HTTP accepts
binary protobuf or JSON, each with optional gzip compression.

The generated product API is served under `/api/v1`:

| Route group | Purpose |
| --- | --- |
| `/sessions`, `/sessions/stats`, `/sessions/{id}`, `/sessions/{id}/traces` | Session inventory, statistics, and session-scoped traces |
| `/traces`, `/traces/{id}`, `/traces/{id}/spans` | Trace inventory and full span data |
| `/logs`, `/stream/logs` | Log search and live log streaming |

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

### Tested SDK pairing

The compatibility claim is the one exact `<QylVersion>` ↔ `<QylSdkVersion>` pair
owned by [`Version.props`](./Version.props), not a version range. The Native AOT
conformance lane restores that released `Qyl.Sdk` package, executes `builder.AddQyl()`,
and proves trace and log readback through the product API on every push. No other
qyl ↔ `Qyl.Sdk` pairing is claimed.

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

### Data retention

The collector retains traces and logs for 30 days by default. Set `QYL_RETENTION_DAYS`
to change the age bound (`0` disables retention),
`QYL_RETENTION_INTERVAL_MINUTES` to change the hourly cleanup interval, and
`QYL_STORAGE_MIN_FREE_MB` to change the 2048 MB threshold below which `/health`
reports degraded storage.

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
| `18889` | Loopback host resource API |

MCP connection management, local MCP process supervision, and the `/runner` MCP
workbench are owned by the sibling
[`qyl.mcp`](https://github.com/ANcpLua/qyl.mcp) product. It is the sole default
listener on `18888`; `qyl up` no longer accepts MCP attachment flags.

### Native AOT publish

Native AOT is the collector's publish contract: `QylAot` defaults on, so
`dotnet publish services/qyl.collector` produces a native binary, and the
Dockerfile builds on the AOT SDK image and runs on `runtime-deps`. Publish with
`-p:QylAot=false` for a JIT diagnostic build with full analyzer enforcement.
The native lane's executable owner is `eng/scripts/collector-aot-smoke.sh`.

## Configuration

This is the complete `QYL_*` environment contract bound by product, developer,
acceptance, and CI code in this repository. Standard `OTEL_*`, `PORT`, and `VITE_*`
settings retain their upstream semantics and are not duplicated here.

| Variable | Scope | Default and behavior |
| --- | --- | --- |
| `QYL_BASE_URL` | Dashboard Playwright | Unset starts the embedded product at `http://127.0.0.1:5100`; a value targets an already-running product. |
| `QYL_BIND_ADDRESS` | Collector | IP address literal for every listener; defaults to `127.0.0.1`. |
| `QYL_CI_API_KEY` | NuGet consumer smoke | Optional `x-otlp-api-key` used only when CI telemetry export is enabled. |
| `QYL_CI_LEG` | NuGet consumer smoke | CI telemetry leg name; defaults to the current runtime identifier. |
| `QYL_CI_OTLP_ENDPOINT` | NuGet consumer smoke | OTLP/HTTP base URL for smoke telemetry; unset makes the emitter inert. |
| `QYL_CI_RUN_ID` | NuGet consumer smoke | CI telemetry session id; defaults to `local-<machine>`. |
| `QYL_DATA_PATH` | Collector | DuckDB path; defaults to `qyl.duckdb` (`qyl up` assigns per-collector files under `~/.qyl`). |
| `QYL_DB_MEMORY_LIMIT` | Collector | Optional DuckDB `memory_limit` value; unset leaves the engine default. |
| `QYL_DB_TEMP_DIR` | Collector | Optional DuckDB `temp_directory`; unset leaves the engine default. |
| `QYL_DB_THREADS` | Collector | Optional positive DuckDB worker count; unset leaves the engine default. |
| `QYL_ENDPOINT` | Automatic instrumentation | Qyl-specific OTLP discovery fallback after `OTEL_EXPORTER_OTLP_ENDPOINT`; unset probes the standard local endpoints. |
| `QYL_GRPC_PORT` | Collector | OTLP/gRPC listener; defaults to `4317`, and `0` disables it. |
| `QYL_OTLP_AUTH_MODE` | Collector | `Unsecured` or `ApiKey`; defaults to `Unsecured` only in Development and to `ApiKey` otherwise. |
| `QYL_OTLP_CORS_ALLOWED_HEADERS` | Collector | Optional comma-separated additions to `content-type` and `x-otlp-api-key`. |
| `QYL_OTLP_CORS_ALLOWED_ORIGINS` | Collector | Comma-separated OTLP/HTTP browser origins (`*` allowed); unset disables OTLP CORS. |
| `QYL_OTLP_PORT` | Collector | OTLP/HTTP listener; defaults to `4318`, and `0` disables it. |
| `QYL_OTLP_PRIMARY_API_KEY` | Collector | Primary `x-otlp-api-key`; at least one key is required in `ApiKey` mode. |
| `QYL_OTLP_SECONDARY_API_KEY` | Collector | Optional rotation key accepted alongside the primary key. |
| `QYL_PORT` | Collector | Product API/dashboard listener; falls back to `PORT`, then `5100`. |
| `QYL_RETENTION_DAYS` | Collector | Trace/log age bound in days; defaults to `30`, and `0` disables retention. |
| `QYL_RETENTION_INTERVAL_MINUTES` | Collector | Retention and disk-pressure check interval; defaults to `60`. |
| `QYL_RUNNER_ORIGIN` | Host console Vite server | Host resource API proxy target; defaults to `http://127.0.0.1:18889`. |
| `QYL_SMOKE_API_PORT` | NativeAOT smoke | Host product-API port; defaults to `5199`. |
| `QYL_SMOKE_GRPC_PORT` | NativeAOT smoke | Host OTLP/gRPC port; defaults to `4317`. |
| `QYL_SMOKE_OTLP_HTTP_PORT` | NativeAOT smoke | Host OTLP/HTTP port; defaults to `4318`. |
| `QYL_SMOKE_PLATFORM` | NativeAOT smoke | Docker build/run platform; defaults to `linux/amd64`. |
| `QYL_STORAGE_MIN_FREE_MB` | Collector | Free-space threshold for degraded `/health`; defaults to `2048`, and `0` disables the threshold. |
| `QYL_WORKLOAD_ONESHOT` | Synthetic workload | `1` emits one acceptance turn and exits; unset runs continuously. |

## Verify

Run the repository gate before pushing:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

GitHub Actions classifies documentation-only pushes before scheduling build
lanes; required build jobs report skipped rather than disappearing. Ambiguous
paths always retain the full pipeline.

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
