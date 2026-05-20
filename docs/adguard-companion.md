# qyl AdGuard Native Messaging Companion

The companion is a local Chrome/Chromium native messaging host plus an unpacked
MV3 extension. AdGuard remains the blocking layer. The companion adds local
diagnosis: DNS posture, browser-surface summaries, blocked-request evidence,
copyable AdGuard user-rule suggestions, and optional qyl OTLP telemetry.

There is no HTTP server. Chrome talks to the host over native messaging
(`stdio`) only.

## Layout

- Host: `services/qyl.adguard.companion/`
- Extension: `extensions/qyl-adguard-companion/`
- Native host name: `dev.qyl.adguard_companion`

## Build And Install

Publish the native host for this Mac:

```bash
dotnet publish services/qyl.adguard.companion/qyl.adguard.companion.csproj -c Release -r osx-arm64
```

Load the extension unpacked in Chrome:

1. Open `chrome://extensions`.
2. Enable developer mode.
3. Load `extensions/qyl-adguard-companion/`.
4. Copy the generated extension id.

Install the user-level native messaging manifest:

```bash
artifacts/publish/qyl.adguard.companion/release_osx-arm64/qyl-adguard-companion \
  install \
  --browser chrome \
  --extension-id <chrome-extension-id>
```

The installer writes:

```text
~/Library/Application Support/Google/Chrome/NativeMessagingHosts/dev.qyl.adguard_companion.json
```

Use `--browser chromium` for Chromium, or `--host-path <absolute-path>` when
installing a binary from another location.

## Doctor

Run the command-line doctor after installation:

```bash
artifacts/publish/qyl.adguard.companion/release_osx-arm64/qyl-adguard-companion \
  doctor \
  --browser chrome \
  --extension-id <chrome-extension-id>
```

The popup also has a Doctor button. The native response reports the manifest
file, executable path, allowed extension origin, protocol readiness, and qyl
telemetry state.

## Native Messaging API

Requests use a length-prefixed JSON envelope:

```json
{
  "id": "request-id",
  "schemaVersion": 1,
  "method": "hello",
  "params": {}
}
```

Supported methods:

- `hello` returns host version, capabilities, and qyl telemetry availability.
- `dns.status` runs a read-only macOS DNS posture check.
- `page.snapshot` summarizes the current browser page surface.
- `network.batch` aggregates browser network errors such as
  `ERR_BLOCKED_BY_CLIENT`.
- `rule.suggest` returns copyable AdGuard user-rule suggestions.
- `qyl.flush` flushes optional OTLP telemetry when configured.
- `get_stats` returns a thread-safe in-memory counter snapshot covering uptime,
  total requests, network batches, observed events, and blocked-by-client
  decisions since the host started. The popup auto-refreshes a Live-Stats panel
  from this method on open.
- `doctor` verifies the native messaging installation.

### `get_stats` response shape

```json
{
  "schemaVersion": 1,
  "startedAtUtc": "2026-05-20T17:03:01.657056+00:00",
  "nowUtc": "2026-05-20T17:03:01.700669+00:00",
  "uptimeSeconds": 0,
  "requestsTotal": 1,
  "requestsFailed": 0,
  "networkBatches": 0,
  "networkEvents": 0,
  "blockedByClient": 0,
  "lastActivityUtc": "2026-05-20T17:03:01.695859+00:00"
}
```

Counters reset on host restart — Chrome relaunches the native host process per
extension session, so the snapshot reflects the current session only.

Responses echo `id` and return either `ok: true` with `result`, or `ok: false`
with `error`.

## Privacy Model

The companion does not mutate AdGuard settings and does not read AdGuard
configuration. It only receives summarized browser signals from the extension.

It does not send raw page text to the native host. The content script reports
counts and URL-level metadata: script/image/iframe counts, title length, and
coarse blocked-placeholder signals. Rule suggestions are copyable text; the
user decides whether to paste them into AdGuard.

Telemetry is off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. When enabled, the
host emits companion activity summaries to the configured OpenTelemetry
endpoint and exposes `qyl.flush` to flush the provider before Chrome tears down
the native messaging process.

## Related Services

### `qyl.nextdns.ingester`

Sibling service that polls the NextDNS public Logs API and emits each decision
as an OpenTelemetry span tagged with `qyl.tracker.source=dns` and
`qyl.tracker.host=<domain>`. Spans land in qyl.collector via the same OTLP
pipeline the companion uses, so DNS-layer and browser-layer decisions share a
shape and are queryable from the same MCP tools.

Required environment:

| Variable | Purpose |
|----------|---------|
| `NEXTDNS_API_KEY` | NextDNS account key. Required, otherwise the ingester refuses to start. |
| `NEXTDNS_PROFILE_ID` | Profile id whose logs are ingested. Required. |
| `NEXTDNS_POLL_INTERVAL_SECONDS` | Optional poll cadence, default `60`, clamped to `[5, 3600]`. |
| `NEXTDNS_BASE_URL` | Optional override, defaults to `https://api.nextdns.io`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where to send tracker-decision spans. |
| `NEXTDNS_DRY_RUN` | When `true`, decisions are logged but no spans are emitted — useful for first-run shape verification. |

The poller uses cursor-based pagination, exponential backoff `[5s, 60min]` on
transient errors, and `TimeProvider.System` for cancellation-aware delays. The
cursor is in-memory only: after a restart, NextDNS naturally surfaces recent
rows, and downstream deduplication on the collector side keeps the ingest
idempotent.

### MCP tools `qyl.tracker_stats_*`

`services/qyl.mcp/Tools/TrackerStatsTools.cs` registers two MCP tools that
surface tracker evidence from both layers via qyl.collector HTTP:

- `qyl.tracker_stats_top` — top trackers grouped by host across a since-window,
  optionally filtered by `source` (`browser` for companion data, `dns` for
  NextDNS data).
- `qyl.tracker_stats_for_site` — per-site decision timeline.

The HTTP endpoint contract on qyl.collector is documented in
`services/qyl.mcp/Tools/HttpTrackerStatsStore.cs`. The store gracefully degrades
to empty results (with a hint surfaced to the LLM) when the collector route is
not yet wired, so the tools register safely even before the data layer ships.
