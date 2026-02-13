# qyl.watch - Live Terminal Observability

Terminal surface of qyl. Real-time span viewer — same kernel data as dashboard and MCP, rendered as a live TUI.

## Role in Architecture

One of three shells (browser, terminal, IDE). `qyl-watch` is the `htop` of observability — always-on, zero-config, streams from the collector's SSE endpoint. Developers who live in the terminal never need to open a browser.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| Tool | `qyl-watch` (dotnet tool) |
| Dependency | Spectre.Console 0.54.0 |

## Usage

```bash
qyl-watch                              # All spans from localhost:5100
qyl-watch --url http://host:5100       # Custom collector
qyl-watch --errors                     # Errors only
qyl-watch --slow [ms]                  # Slow spans (default >200ms)
qyl-watch --service my-api             # Filter by service
qyl-watch --genai                      # GenAI spans only
qyl-watch --session abc123             # Filter by session
```

## Keyboard

| Key | Action |
|-----|--------|
| `q` / `Ctrl+C` | Quit |
| `c` | Clear screen |
| `e` | Toggle errors-only filter |
| `f` | Cycle service filter |

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — SSE stream loop, keyboard handler |
| `CliConfig.cs` | CLI argument parsing, mutable runtime filters |
| `SseClient.cs` | SSE client with Channel-based reconnection |
| `SseEvent.cs` | Parsed SSE event record |
| `SpanBatchDto.cs` | DTOs — `SpanBatchDto` + `SpanDto` with computed properties |
| `SpanRenderer.cs` | Span tree rendering with color coding |
| `HeaderRenderer.cs` | Rolling stats header (req/s, error%, p95) |
| `Filters.cs` | Runtime filter logic |

## Architecture

```
Collector SSE (/api/v1/live)
    |
    v
SseClient (HttpClient + Channel<SseEvent>)
    |── reconnect with exponential backoff (1s → 30s max)
    |── parses event:/data: SSE format
    v
Program.cs
    |── ProcessSpanEvent: deserialize → filter → render → stats
    |── HandleKeyboard: toggle filters at runtime
    v
SpanRenderer (tree view)       HeaderRenderer (stats bar)
    |── group by traceId            |── 10s rolling window
    |── parent/child tree            |── req/s, error%, p95
    |── color by type/status         |── service breakdown
```

## Span Rendering

| Span Type | Detection | Display |
|-----------|-----------|---------|
| GenAI | `GenAiProviderName` or `GenAiRequestModel` set | Model, tokens, cost, tool |
| Database | `db.system.name` attribute | System:operation |
| HTTP | `http.request.method` attribute | Method, path, status code |
| Generic | Fallback | Name, duration |

Color coding: green (<200ms), yellow (200-500ms), red (>500ms or error).

## SpanDto Computed Properties

Attributes are stored as JSON string (`AttributesJson`). Computed properties parse on demand:
- `DurationMs` — from `DurationNs / 1_000_000.0`
- `IsGenAi` — provider or model present
- `IsError` — `StatusCode == 2`
- `HttpStatusCode`, `HttpMethod`, `HttpRoute` — from attributes
- `DbSystem`, `DbOperation` — from attributes

## Rules

- Uses `TimeProvider.System.GetUtcNow()` (not `DateTime.Now`)
- Uses `Lock` (not `object _lock`)
- SSE reconnection is automatic — never crashes on connection loss
- Keyboard runs on background thread, does not block SSE processing
