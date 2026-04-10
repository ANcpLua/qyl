# eng/go — OTel Go Instrumentation

> **Status: stub.** Go SDK codegen (like `eng/semconv/` for TypeSpec) is deferred until qyl reaches beta / 1.0. During alpha the focus is the C# platform. This file documents manual Go instrumentation for now.

Instruments Go apps to send OTLP telemetry to qyl.
Go has no auto-instrumentation agent. Instrumentation is explicit via SDK + contrib libraries.

## How it works

Go uses `go.opentelemetry.io/otel` SDK with contrib instrumentation packages.
Each framework (net/http, gRPC, database/sql) needs its own middleware/wrapper.
Env vars configure the OTLP exporter destination.

## Quick start (existing Go app)

```bash
# Add OTel dependencies
go get go.opentelemetry.io/otel \
       go.opentelemetry.io/otel/sdk \
       go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp \
       go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp

# Set env vars
export OTEL_SERVICE_NAME=my-go-service
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf

# Run
go run .
```

## Minimal setup code

```go
package main

import (
    "context"
    "go.opentelemetry.io/otel"
    "go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp"
    "go.opentelemetry.io/otel/sdk/resource"
    sdktrace "go.opentelemetry.io/otel/sdk/trace"
    semconv "go.opentelemetry.io/otel/semconv/v1.42.0"
)

func initTracer() (*sdktrace.TracerProvider, error) {
    exporter, err := otlptracehttp.New(context.Background())
    if err != nil {
        return nil, err
    }
    tp := sdktrace.NewTracerProvider(
        sdktrace.WithBatcher(exporter),
        sdktrace.WithResource(resource.NewWithAttributes(
            semconv.SchemaURL,
            semconv.ServiceNameKey.String("my-service"),
        )),
    )
    otel.SetTracerProvider(tp)
    return tp, nil
}
```

## Available contrib instrumentation

| Package | Instruments |
|---------|------------|
| `otelhttp` | `net/http` handlers and clients |
| `otelgrpc` | gRPC servers and clients |
| `otelsql` | `database/sql` queries |
| `otelgin` | Gin HTTP framework |
| `otelecho` | Echo HTTP framework |
| `otelmux` | Gorilla Mux router |
| `otelaws` | AWS SDK calls |

Install: `go get go.opentelemetry.io/contrib/instrumentation/<import-path>`

## Key difference from Java

Java: attach agent, everything instrumented automatically.
Go: add middleware per framework explicitly. No bytecode manipulation possible.

The tradeoff: more control, less magic. Every instrumented call is visible in source code.

## Env var tuning

| Env var | Effect |
|---------|--------|
| `OTEL_SDK_DISABLED=true` | Disable all telemetry |
| `OTEL_TRACES_SAMPLER=parentbased_traceidratio` | Sample traces |
| `OTEL_TRACES_SAMPLER_ARG=0.1` | 10% sampling rate |
| `OTEL_EXPORTER_OTLP_TIMEOUT=10000` | Export timeout (ms) |

## Files

| File | Purpose |
|------|---------|
| `eng/go/CLAUDE.md` | This file |

## Constraints

- Go 1.21+ required
- No auto-instrumentation — each framework needs explicit middleware
- `context.Context` must be propagated through call chains for trace correlation
- Forgetting to pass context breaks distributed tracing silently
