---
name: qyl-otel-exporter-setup
description: Configure the OpenTelemetry Collector to route traces/metrics/logs to the qyl collector. Use when setting up OTel with qyl, configuring collector pipelines, or instrumenting any OTLP-capable service to send telemetry to qyl.
---

# qyl OTLP Exporter Setup

qyl is OTLP-native — there is no vendor SDK and no custom exporter. The user runs `otelcol-contrib`, configures the upstream `otlp` exporter pointed at a qyl collector, and ships traces/metrics/logs over standard OTLP (gRPC 4317 or HTTP 4318).

**Terminology**: call this the "qyl OTLP exporter" (meaning the upstream `otlp` exporter configured for qyl). Do not invent a "qyl Exporter" component — none exists.

## Setup Overview

Copy this checklist to track progress:

```
qyl OTLP Exporter Setup:
- [ ] Step 1: Check for existing collector config
- [ ] Step 2: Check collector version and install if needed
- [ ] Step 3: Confirm qyl collector URL and auth mode
- [ ] Step 4: Write collector config
- [ ] Step 5: Add environment variable placeholders
- [ ] Step 6: Run the collector
- [ ] Step 7: Verify setup
- [ ] Step 8: Wire application telemetry for trace connectedness
```

## Step 1: Check for Existing Configuration

Search for existing OpenTelemetry Collector configs — YAML files containing `receivers:`, or files named `otel-collector-config.*`, `collector-config.*`, `otelcol.*`.

**If an existing config is found**, ask the user:
- **Modify existing config**: add the qyl OTLP exporter to the existing file (recommended — avoids duplicate collectors listening on 4317/4318).
- **Create separate config**: leave the existing file untouched and create a new one for testing.

**Wait for the answer and record their choice** before continuing. The rest of the workflow depends on it.

**If no config exists**, note that you will create `collector-config.yaml` in Step 4, then proceed.

## Step 2: Check Collector Version

The qyl OTLP exporter requires **otelcol-contrib v0.145.0 or later** (pinned for parity with other OTLP-native vendors; the upstream `otlp` exporter itself is stable much earlier).

### Check for existing collector

1. Run `which otelcol-contrib` to check PATH; also check for `./otelcol-contrib` in the project.
2. If found, print the version and parse it.
3. **Record the collector path** (`otelcol-contrib` on PATH, or `./otelcol-contrib` local) for Steps 5 and 6.

| Existing Version | Action |
|------------------|--------|
| ≥ 0.145.0 | Skip to Step 3 |
| < 0.145.0 | Install below |
| Not installed | Install below |

### Installation

Ask the user: **Binary** (download from GitHub) or **Docker** (container).

### Binary Installation

Fetch the latest tag:

```bash
curl -s https://api.github.com/repos/open-telemetry/opentelemetry-collector-releases/releases/latest | grep '"tag_name"' | cut -d'"' -f4
```

The GitHub tag is `vX.Y.Z`, but the download filename and Docker tag are the numeric `X.Y.Z` without the `v`. Detect platform with `uname -s` / `uname -m`, then map:

- Darwin + arm64 → `darwin_arm64`
- Darwin + x86_64 → `darwin_amd64`
- Linux + x86_64 → `linux_amd64`
- Linux + aarch64 → `linux_arm64`

```bash
curl -LO https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v<numeric_version>/otelcol-contrib_<numeric_version>_<os>_<arch>.tar.gz
tar -xzf otelcol-contrib_<numeric_version>_<os>_<arch>.tar.gz
chmod +x otelcol-contrib
```

Perform these commands for the user — do not just show them. Ask whether to delete the tarball (~50MB); only delete on explicit yes.

### Docker Installation

```bash
docker --version
docker pull otel/opentelemetry-collector-contrib:<numeric_version>
```

The `docker run` command comes in Step 6 after the config exists.

## Step 3: Confirm qyl Collector URL and Auth Mode

Ask the user two questions:

1. **Where is the qyl collector reachable?**
   - Hosted qyl: the URL assigned to their workspace (something like `https://collect.qyl.app` — confirm with the qyl dashboard, do not hard-code a domain).
   - Self-hosted: whatever URL/port their `qyl.collector` instance is bound to. Default local dev is `http://localhost:4318` (HTTP) or `http://localhost:4317` (gRPC).

2. **Which auth mode is the qyl collector running in?**
   - **ApiKey** — the qyl collector validates an `x-otlp-api-key` header against its configured keys. Requires the user to paste the key they provisioned in the qyl dashboard / in their collector's `OtlpApiKey__PrimaryApiKey` env var.
   - **Unsecured** — the collector accepts unauthenticated OTLP. Only legitimate for local dev.

Record both values. They drive Step 4 (whether to emit the header) and Step 5 (which placeholders to add).

## Step 4: Write Collector Config

**Use the decision from Step 1.** Edit the existing config if that was chosen; otherwise create `collector-config.yaml`. Record the path for Steps 5 and 6.

The qyl collector speaks standard OTLP — no custom exporter, no vendor attribute mapping. The upstream `otlp` exporter from `otelcol-contrib` is all that is required.

### If editing an existing config

Add one new exporter (`otlp/qyl`) and include it in the `traces`, `metrics`, and `logs` pipelines. Do not remove or modify existing exporters.

### If creating a new config

Create `collector-config.yaml`:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

exporters:
  otlp/qyl:
    endpoint: "${env:QYL_COLLECTOR_URL}"
    # Omit the headers block entirely when the qyl collector runs in Unsecured mode.
    headers:
      x-otlp-api-key: "${env:QYL_API_KEY}"
  debug:
    verbosity: detailed

service:
  pipelines:
    traces:  { receivers: [otlp], exporters: [otlp/qyl, debug] }
    metrics: { receivers: [otlp], exporters: [otlp/qyl, debug] }
    logs:    { receivers: [otlp], exporters: [otlp/qyl, debug] }
```

Notes:

- **`endpoint`**: for gRPC, use `host:port` (`collect.qyl.app:4317`); for HTTP, use the full URL. The `otlp` exporter defaults to gRPC — for HTTP, switch the type to `otlphttp`, rest is identical.
- **`x-otlp-api-key`** is the header qyl's `OtlpApiKeyMiddleware` checks. Do **not** use `Authorization: Bearer <token>` — qyl does not parse it and returns 401.
- **`debug` exporter** is for first-run verification. Remove it from the pipelines once Step 7 passes.

## Step 5: Add Environment Variable Placeholders

The qyl OTLP exporter needs up to two env vars. You are adding **placeholders** that the user fills in — never real credentials.

**Language constraint**: NEVER say "add credentials", "add environment variables", or "add the key" without the word **placeholder**. Always clarify the user fills them in later.

DO NOT say:
- "Let me add the environment variables"
- "I'll add the API key to your .env"
- "Adding the qyl API key"

SAY INSTEAD:
- "I'll add placeholder environment variables for you to fill in"
- "Adding placeholder values — you will replace these with your actual qyl collector URL and API key"
- "I'll set up the env var keys with placeholder values"

Glob for existing `.env` files: `**/.env`. **Always ask which file to use** — never infer. Present each discovered path plus "Create new at root". Wait for explicit selection. Record the path.

Add these placeholders:

```bash
QYL_COLLECTOR_URL=your-qyl-collector-url
QYL_API_KEY=your-qyl-api-key
```

Tell the user where to get the real values:

1. **`QYL_COLLECTOR_URL`**: the URL of the qyl collector serving their workspace. For hosted qyl, find it in the qyl dashboard onboarding panel. For self-hosted, it's whatever URL their `services/qyl.collector` instance is bound to (default `http://localhost:4318` for OTLP HTTP, `http://localhost:4317` for gRPC).
2. **`QYL_API_KEY`**: only required when the qyl collector runs in `ApiKey` auth mode. For hosted qyl, copy from the dashboard. For self-hosted, the operator sets it via `OtlpApiKey__PrimaryApiKey` (env) or `OtlpApiKey:PrimaryApiKey` (appsettings.json). Omit both the env var and the `headers:` block in the config when running the collector in `Unsecured` mode.

Ensure the chosen `.env` is in `.gitignore`.

### Wait for user to set values

Ask: **Yes, values are set** → proceed. **Not yet** → wait and ask again. Do not proceed to Step 6 until confirmed.

### Validate config

Once values are set, validate:

#### Binary

```bash
set -a && source "<env_file>" && set +a && "<collector_path>" validate --config "<config_file>"
```

#### Docker

Docker volume mounts require absolute paths — prefix relative paths with `$(pwd)/`.

```bash
docker run --rm \
  -v "<config_file>":/etc/otelcol-contrib/config.yaml \
  --env-file "<env_file>" \
  otel/opentelemetry-collector-contrib:<numeric_version> \
  validate --config /etc/otelcol-contrib/config.yaml
```

If validation fails, read the error, fix the config, re-run. Repeat until green.

Once validation passes, ask: **Yes, run it now** → Step 6. **Not yet** → wait.

## Step 6: Run the Collector

**Only after the user confirms they're ready.** Provide the command; do not auto-execute.

Use the paths recorded in Steps 1, 2, 5.

### Binary

```bash
set -a && source "<env_file>" && set +a && "<collector_path>" --config "<config_file>"
```

### Docker

```bash
docker stop otel-collector 2>/dev/null; docker rm otel-collector 2>/dev/null
docker run -d --name otel-collector \
  -p 4317:4317 -p 4318:4318 -p 13133:13133 \
  -v "<config_file>":/etc/otelcol-contrib/config.yaml \
  --env-file "<env_file>" \
  otel/opentelemetry-collector-contrib:<numeric_version>
```

## Step 7: Verify Setup

1. Check collector logs for clean startup — no errors about invalid config, no 401 loops against the qyl endpoint.
2. Send test telemetry from any OTLP-capable service pointed at the collector's `4317` (gRPC) or `4318` (HTTP).
3. Confirm the `debug` exporter logs spans/metrics/logs to stdout, then confirm they appear in qyl within ~60 seconds.

Docker logs: `docker logs -f otel-collector`.

Once verified, remove `debug` from the pipelines and restart the collector.

## Step 8: Wire Application Telemetry for Trace Connectedness

qyl is OTLP-native: any OTel SDK pointed at this collector will connect traces, logs, and metrics by `trace_id` automatically — there is no vendor SDK integration to install. Point the app's OTel SDK at the collector and you're done.

Ask: **What stack is the instrumented app?**

### .NET (qyl's native stack)

For services inside the qyl repo, use the telemetry primitives shipped here — do not roll OTel wiring by hand.

In `Program.cs`:

```csharp
builder.AddQylServiceDefaults(options =>
{
    options.ServiceName = "your-service";
});

// …

app.UseQylTelemetry();
```

`AddQylServiceDefaults` registers the OTel SDK with OTLP export already wired, and `UseQylTelemetry` wires the ASP.NET request pipeline instrumentation. The app will read `OTEL_EXPORTER_OTLP_ENDPOINT` and ship to whatever collector the env points at — set it to the collector from Step 4 (typically `http://localhost:4318`).

For standalone .NET apps outside this repo, use the stock OTel SDK: `OpenTelemetry.Exporter.OpenTelemetryProtocol` + `OpenTelemetry.Extensions.Hosting`, configured with `AddOtlpExporter()` and `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318`.

### Python / Node / Go / Java / Ruby

Install the standard OTel SDK for the language and set these env vars on the app process (not the collector):

```bash
OTEL_SERVICE_NAME=your-service
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

That is the whole integration. No qyl SDK, no vendor-specific integration, no DSN. Trace-connectedness works because the collector forwards the traces, logs, and metrics pipelines to qyl with their original `trace_id` intact.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| 401 from qyl collector on every export | qyl collector is in `ApiKey` mode; header missing or wrong scheme | Add `x-otlp-api-key: ${env:QYL_API_KEY}` to the exporter `headers:`. Do not use `Authorization: Bearer` — qyl's middleware does not read it. |
| 401 only some of the time | Key rotation mid-export (primary vs secondary) | Confirm you're using the current `PrimaryApiKey`; `SecondaryApiKey` is for rotation overlap only. |
| Validation fails with env var errors | `.env` not loaded before `validate` / `run` | Prefix the command with `set -a && source "<env_file>" && set +a`. |
| `connection refused` on 4317/4318 | Collector not running, or another process bound the port | `lsof -i :4317` / `lsof -i :4318`; stop the other process or change the receiver port. |
| Spans appear in `debug` exporter but never in qyl | `QYL_COLLECTOR_URL` wrong, or the exporter is using the `otlp` type (gRPC) against an HTTP endpoint | For an HTTP URL, switch the exporter type to `otlphttp`. For gRPC, use host:port form. |
| `container name already in use` | Previous Docker run still registered | `docker stop otel-collector && docker rm otel-collector` |
| App ships traces but no logs/metrics reach qyl | App SDK only configured for traces | Add OTLP log/metric exporters in the app SDK — the collector pipelines are ready, the app has to emit each signal. |

**One honest note**: qyl's OTLP ingestion accepts the header name configured on the collector (`OtlpApiKeyOptions.HeaderName`, default `x-otlp-api-key`). If a qyl operator has overridden that default, this config is wrong — ask the operator for the actual header name. Everything else in this skill (port 4317/4318, `/v1/traces` `/v1/logs` `/v1/metrics` paths, OTLP-native ingest) is verified against `services/qyl.collector/Ingestion/`.
