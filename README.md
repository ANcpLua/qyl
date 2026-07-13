# qyl

qyl is an OpenTelemetry-compatible observability product for ingesting,
investigating, and visualizing telemetry. The product is the collector, DuckDB
storage, public investigation API, dashboard, and local distributed-app host.
OpenTelemetry supplies the ingestion protocol and telemetry vocabulary; it is not
Qyl's product API.

qyl is pre-beta. The current repository favors a small, executable product surface
over compatibility layers for unreleased experiments.

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
qyl.collector                            OTLP ingest -> DuckDB -> product API
        |
        v
qyl.dashboard                            investigation UI
```

The main components are:

| Path | Responsibility |
| --- | --- |
| `services/qyl.collector` | Official OTLP wire ingestion, internal storage, and the generated-contract product API |
| `services/qyl.dashboard` | React investigation interface backed by generated client contracts |
| `internal/qyl.instrumentation` | Qyl service defaults and Qyl-specific telemetry |
| `packages/Qyl.Host` | Unpublished local distributed-app runner |
| `packages/Qyl.Host.Mcp` | Optional MCP hosting integration for the runner |
| `eng/build` | Build, generation, verification, and packaging gates |

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

## Run locally

Requirements: the .NET SDK selected by `global.json` and Node.js 20 or later.

Start the collector:

```bash
QYL_BIND_ADDRESS=127.0.0.1 QYL_OTLP_AUTH_MODE=Unsecured \
  dotnet run --project services/qyl.collector
```

Start the dashboard in another terminal:

```bash
cd services/qyl.dashboard
npm install
npm run dev
```

The browser dashboard is intentionally a loopback development surface and never
stores the ingest-capable API key. `ApiKey` deployments expose only the generated
product API and OTLP endpoints; use a generated client rather than the dashboard.

Or run the local host, which composes the collector and its isolated diagnostics
collector:

```bash
dotnet run --project packages/Qyl.Run.Host
```

Point an OTLP exporter at the collector:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:5100
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

| Port | Purpose |
| --- | --- |
| `5100` | Product API and OTLP/HTTP; embedded dashboard only in local `Unsecured` mode |
| `4317` | OTLP/gRPC receiver |
| `4318` | Dedicated OTLP/HTTP receiver |

## Verify

Run the repository gate before pushing:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

Generated files are outputs, not editing surfaces. Change the owning TypeSpec,
protobuf input, generator, or manifest and regenerate in the same commit.

## License

MIT
