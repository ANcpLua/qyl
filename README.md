# qyl

A local OpenTelemetry investigation stack for .NET. Instrument an application with one
line, run the collector on your own machine, and read the traces and logs back through
the embedded dashboard, the collector API, or MCP.

qyl 1.0.0 is released. The hosted surfaces are [qyl.at](https://qyl.at/) for the site and
documentation, [api.qyl.at](https://api.qyl.at/) for the collector read API and OTLP
ingest, and [mcp.qyl.at/mcp](https://mcp.qyl.at/mcp) for the MCP endpoint (OAuth 2.1).

## Run the stack

```bash
dotnet tool install --global qyl
qyl up
```

`qyl up` starts the collector and its embedded dashboard on `127.0.0.1:5100`, diagnostics
on `:5200`, OTLP ingestion on `:4318` and `:4317`, and the runner API on `:18889`.
Telemetry is stored under `~/.qyl/`, never in the working directory. All five ports are
checked up front, so a conflict fails the command instead of leaving a half-bound stack.

## Send telemetry from an application

```bash
dotnet add package Qyl.Telemetry.Hosting
```

```csharp
using Qyl;

builder.AddQyl();
```

`AddQyl()` is what wires the pipeline: it activates automatic instrumentation, registers
the qyl activity sources and meters, and exports traces, metrics, and logs over OTLP — to
`OTEL_EXPORTER_OTLP_ENDPOINT` when set, otherwise `QYL_ENDPOINT`, otherwise a collector
discovered on localhost. Environment variables on their own export nothing; without the
call there is no exporter to configure.

Metrics are accepted at the wire and acknowledged with `partial_success`, then discarded.
qyl stores and serves traces and logs.

## The shipped artifacts

qyl is one dependency graph with several independently released packages. Each line
carries its own version — the 1.0.0 launch is an event, not a number every package
adopts.

| Package | Line | Repository |
| --- | --- | --- |
| `qyl` (dotnet tool) | 1.0.0 | this one |
| `Qyl.Telemetry.Hosting`, `Qyl.Telemetry.AutoInstrumentation*` | 9.0.1 | [Qyl.OpenTelemetry.AutoInstrumentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) |
| `Qyl.Telemetry.SemanticConventions*` | 1.0.0 | [Qyl.OpenTelemetry.SemanticConventions](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions) |
| `Qyl.Api.Contracts`, `@ancplua/qyl-api-schema` | 4.0.0 | [qyl-api-schema](https://github.com/ANcpLua/qyl-api-schema) |
| `qyl-mcp-server` | 1.0.0 | [qyl.mcp](https://github.com/ANcpLua/qyl.mcp) |

`Qyl.Sdk` and the `Qyl.OpenTelemetry.*` package IDs are retired. They stop at their last
published versions and receive no further releases; the table above lists their
successors.

## Architecture

`ARCHITECTURE-1.0.0.md` is the normative document: component taxonomy, boundary law, the
dependency-edge list, the generated loops, and the gates. `docs/component-taxonomy.html`
is a view of the same content. On conflict the Markdown wins.

One graph, one truth, many artifacts.

## Build and verify

Requires the .NET SDK pinned in `global.json`.

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

`Ci` builds and tests the backend, builds and tests the dashboard, runs its Release-product
Playwright smoke, verifies the generated contract package, and checks the collector
semantic catalog.

## License

MIT
