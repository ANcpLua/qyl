# qyl

A local OpenTelemetry investigation stack for .NET. Run the collector on your own machine,
instrument an application with one line, and read the traces, logs and metrics back through the
embedded dashboard, the collector API, or MCP.

```bash
dotnet tool install --global qyl
qyl up
```

`qyl up` starts the collector and its embedded dashboard on `127.0.0.1:5100`, diagnostics on
`:5200`, OTLP ingestion on `:4318` and `:4317`, and the runner API on `:18889`. Telemetry is
stored under `~/.qyl/`, never in the working directory. All five ports are checked up front, so a
conflict fails the command instead of leaving a half-bound stack.

The tool is a NativeAOT binary per platform: the `qyl` package selects the matching
`qyl.<rid>` package for linux, macOS and Windows on x64 and arm64.

## Send telemetry to it

```bash
dotnet add package Qyl.Telemetry.Hosting
```

```csharp
using Qyl;

builder.AddQyl();
```

`AddQyl()` activates the automatic instrumentation and exports traces, metrics and logs over OTLP,
discovering the running collector on localhost. Environment variables alone export nothing;
without the call there is no exporter to configure.

## Read it back

- The dashboard at `http://127.0.0.1:5100`.
- The collector API under `/api/v1/`: traces, logs, and metrics aggregated server-side into
  buckets by name, attribute matchers, time range and step.
- MCP, for an agent: [`qyl-mcp-server`](https://www.npmjs.com/package/qyl-mcp-server) over
  stdio locally, or the hosted endpoint at `https://mcp.qyl.at/mcp`.

## More

- Site and documentation: [qyl.at](https://qyl.at/)
- Building an API that is instrumented from the first line:
  [`Qyl.Api.Sdk`](https://www.nuget.org/packages/Qyl.Api.Sdk)
- Source, samples and the release lines:
  [github.com/ANcpLua/qyl](https://github.com/ANcpLua/qyl)
- Changes per version:
  [CHANGELOG](https://github.com/ANcpLua/qyl/blob/main/CHANGELOG.md)
