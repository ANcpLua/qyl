qyl is one schema-owned modular platform: one dependency graph, one verification
pipeline, and multiple independently shipped artifacts — MCP, CLI, instrumentation,
conventions, collector, Workbench, and Runner each keep one clear responsibility
without owning duplicate contracts. Qyl.Telemetry.SemanticConventions defines the
vocabulary qyl emits, Qyl.Telemetry provides its telemetry primitives and explicit
instrumentation API, Qyl.Telemetry.AutoInstrumentation implements automatic capture,
and Qyl.Telemetry.Hosting composes the complete producer pipeline inside an
application up to the OTLP exporter. Qyl.Collector begins where that exporter ends:
it is a standalone service that receives telemetry, validates it against the shared
registry, stores it, and exposes it through the collector API.
`qyl/packages/Qyl.Cli` is a client of that API — not the API itself — and brings up
the local stack, including the collector on `:5100`, diagnostics on `:5200`, and
OTLP ingestion on `:4318`, with `Qyl.Run.Workload` as the Runner supervising those
local processes. `qyl.dashboard` presents product telemetry over HTTP. On top of the
stored telemetry, the `qyl.mcp` server projection is a closed-world MCP server that
exposes the known qyl model as MCP tools and runs as the `qyl-mcp` Node service on
Railway at `mcp.qyl.at`; its tool shapes are generated from the `qyl-api-schema`
contract while tool curation is authored — never a second contract source, with a
fail-closed contract-revision handshake against the collector. Separately, the
`qyl.mcp` client runtime is an open-world MCP client that connects to, inspects, and
tests arbitrary external MCP servers through a local loopback process on `:18888` —
deliberately outside both loops. The `qyl.mcp` dashboard is the browser-based
workbench UI: a Vite bundle and MCP App served by the server, through which
developers discover capabilities, invoke tools, and inspect runtime behavior.
One graph, one truth, many artifacts. Normative source: ARCHITECTURE-1.0.0.md —
on conflict, the MD wins.
