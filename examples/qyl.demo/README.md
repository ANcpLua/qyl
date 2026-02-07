# qyl.demo - The Magic Experience

This demo shows how qyl makes observability **just work** with zero configuration.

## What You Get

| Feature                               | How                        | Code Required |
|---------------------------------------|----------------------------|---------------|
| OpenTelemetry (traces, metrics, logs) | `UseQylConventions()`      | 1 line        |
| Health checks (`/health`, `/alive`)   | `MapQylDefaultEndpoints()` | 1 line        |
| OpenAPI documentation                 | `UseQylConventions()`      | 0 lines       |
| HTTP client resilience                | `UseQylConventions()`      | 0 lines       |
| GenAI instrumentation (OTel 1.39)     | Compile-time generator     | 0 lines       |
| GitHub Copilot integration            | `AddQylCopilot()`          | 1 line        |

## Running the Demo

```bash
# Start qyl.collector (receives telemetry)
dotnet run --project src/qyl.collector

# In another terminal, start the demo
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project examples/qyl.demo

# Set up GitHub auth (choose one)
export GH_TOKEN=your_token
# or
gh auth login
```

## Try It

```bash
# Chat with Copilot
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello, how are you?"}'

# Stream response
curl -X POST http://localhost:5000/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Explain observability in 3 sentences"}'

# Health check
curl http://localhost:5000/health

# OpenAPI spec
curl http://localhost:5000/openapi/v1.json
```

## What Happens Behind the Scenes

### Compile Time (Zero Runtime Cost)

The `qyl.servicedefaults.generator` scans your code and finds:

- `AIAgent.RunAsync()` calls
- `AIAgent.RunStreamingAsync()` calls

It generates interceptors that wrap these calls with OTel spans:

```csharp
// What you write:
var response = await adapter.ChatCompleteAsync("Hello", ct: ct);

// What actually runs (generated at compile time):
using var activity = ActivitySource.StartActivity("gen_ai.chat");
activity?.SetTag("gen_ai.provider.name", "github_copilot");
activity?.SetTag("gen_ai.request.model", "copilot");
// ... your actual call ...
activity?.SetTag("gen_ai.usage.input_tokens", response.Usage.InputTokens);
activity?.SetTag("gen_ai.usage.output_tokens", response.Usage.OutputTokens);
```

### Runtime

1. **OTLP Export**: Traces, metrics, and logs flow to qyl.collector
2. **DuckDB Storage**: Efficient columnar storage for querying
3. **Dashboard**: Real-time visualization at http://localhost:5100

## What's NOT Done Yet

| Feature                            | Status      | Notes                                             |
|------------------------------------|-------------|---------------------------------------------------|
| Generator interception for Copilot | Investigate | Type pattern matching may need adjustment         |
| Workflow execution                 | TODO        | Need `.qyl/workflows/*.md` files                  |
| Token counting                     | Partial     | Depends on SDK response format                    |
| Cost tracking                      | TODO        | Needs pricing data integration                    |
| Transitive generator flow          | TODO        | Generator should flow through qyl.servicedefaults |

## Architecture

```
┌─────────────┐     OTLP      ┌─────────────────┐     HTTP     ┌─────────────┐
│  qyl.demo   │ ─────────────>│  qyl.collector  │<────────────>│  Dashboard  │
│  (your app) │   :4317       │  (backend)      │   :5100      │  (React)    │
└─────────────┘               └────────┬────────┘              └─────────────┘
                                       │
                                       v
                              ┌─────────────────┐
                              │     DuckDB      │
                              │  (columnar DB)  │
                              └─────────────────┘
```

## The Point

You write business logic. qyl handles observability.

No manual spans. No configuration. No boilerplate. It just works.