# qyl

The qyl tool runs the qyl Collector on your machine, so an AI agent can investigate your local
services' telemetry through MCP. qyl is an analytical engine built for AI agents; it ships no
dashboard or other product UI.

```bash
dotnet tool install --global qyl
qyl up
```

`qyl up` starts the Collector API on `127.0.0.1:5100`, with OTLP on `127.0.0.1:4318` (HTTP) and
`127.0.0.1:4317` (gRPC), a diagnostics Collector on `:5200` for qyl's own telemetry, and the runner
API on `:18889`. It checks all five ports first and stops if one is taken. Every listener binds to
loopback and accepts requests without a key. Telemetry is stored under `~/.qyl/`, never in the
working directory.

The tool and the Collector it starts are framework-dependent .NET 10 applications: the `qyl`
package selects the `qyl.<rid>` package for Linux, macOS or Windows on x64 or arm64.

## Send telemetry

Point any OpenTelemetry SDK's OTLP exporter at the Collector, for example
`OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318` for OTLP over HTTP or `http://127.0.0.1:4317`
for gRPC. The runner (service `qyl-runner`) and the Collector send their own telemetry to the
diagnostics Collector on `:5200`, never to the one on `:5100`.

## Investigate

An agent reaches the Collector through the qyl MCP server, npm
[`qyl-mcp-server`](https://www.npmjs.com/package/qyl-mcp-server):

```bash
bunx qyl-mcp-server --stdio
```

Its default `QYL_COLLECTOR_URL` is this Collector. The Collector's own API is under `/api/v1/`;
`POST /api/v1/analysis/<tool_name>` answers the same analysis questions the MCP tools ask.

## More

- Website: https://qyl.at
- Source: https://github.com/ANcpLua/qyl
- Hosted MCP server: https://mcp.qyl.at/mcp
- Changes per version: [CHANGELOG](https://github.com/ANcpLua/qyl/blob/main/CHANGELOG.md)

## License

qyl is free, under the MIT license
([LICENSE](https://github.com/ANcpLua/qyl/blob/main/LICENSE)).
