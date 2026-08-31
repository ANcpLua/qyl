# qyl

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/ANcpLua/qyl/badge)](https://scorecard.dev/viewer/?uri=github.com/ANcpLua/qyl)
[![OpenSSF Criticality](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FANcpLua%2Fqyl%2Fbadge%2Fcriticality.json)](docs/criticality.md)

A local OpenTelemetry investigation stack for .NET. Instrument an application with one
line, run the collector on your own machine, and read the traces and logs back through
the embedded dashboard, the collector API, or MCP.

The latest public qyl release is 1.1.8; the `main` branch is the 1.2.0 source line. The
site and documentation are at [qyl.at](https://qyl.at/). The other two hosted surfaces
are endpoints rather than pages: `https://api.qyl.at` serves the collector read API and
OTLP ingest under their route prefixes, and `https://mcp.qyl.at/mcp` is the MCP endpoint,
which answers `401` to anything without an OAuth 2.1 bearer token.

## Run the stack

```bash
dotnet tool install --global qyl
qyl up
```

`qyl up` starts the collector and its embedded dashboard on `127.0.0.1:5100`, diagnostics
on `:5200`, OTLP ingestion on `:4318` and `:4317`, and the runner API on `:18889`.
Telemetry is stored under `~/.qyl/`, never in the working directory. All five ports are
checked up front, so a conflict fails the command instead of leaving a half-bound stack.

## Observe Codex workflows

`qyl codex` owns the Codex app-server connection so the workflow journal is factual
rather than inferred from generic telemetry:

```bash
export QYL_API_KEY='your-qyl-ingest-key'
qyl codex --
```

It verifies the installed Codex app-server schema, starts an authenticated loopback
server and the normal Codex TUI, and records immutable runs, attempts, collaboration,
tools, content references, and controls. Uploads are idempotent. Network failures leave
an encrypted spool under `~/.qyl/codex/` for retry instead of losing or rewriting
events. The bundled Observe Graph plugin discovers the current run through the local
`qyl observer-bridge`; that bridge is modern-only MCP `2026-07-28`.

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

## Artifacts and release lines

qyl is one dependency graph with several independently released packages. Each line
carries its own version — the 1.0.0 launch is an event, not a number every package
adopts. The versions below are the source and dependency lines targeted by `main`, not a
claim that every target has already been published. Package registries are authoritative
for public availability.

| Package | `main` / release target | Repository |
| --- | --- | --- |
| `qyl` (dotnet tool) | 1.2.0 (latest public: 1.1.8) | this one |
| `Qyl.Telemetry.Hosting`, `Qyl.Telemetry.AutoInstrumentation*` | 9.1.0 | [Qyl.OpenTelemetry.AutoInstrumentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) |
| `Qyl.Telemetry.SemanticConventions*` | 6.0.0 | [Qyl.OpenTelemetry.SemanticConventions](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions) |
| `Qyl.Api.Contracts`, `@ancplua/qyl-api-schema` | 7.3.0 | [qyl-api-schema](https://github.com/ANcpLua/qyl-api-schema) |
| `qyl-mcp-server` | 1.1.3 | [qyl.mcp](https://github.com/ANcpLua/qyl.mcp) |

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
