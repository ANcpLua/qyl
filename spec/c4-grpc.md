# C4: qyl.grpc

> OTLP/gRPC receiver with protobuf conversion and event broadcasting

## Overview

The gRPC component implements OpenTelemetry Protocol receivers for traces, metrics, and logs. It converts protobuf
messages to internal models and raises events for downstream processing.

## Key Classes/Modules

| Class                  | Purpose                    | Location                                    |
|------------------------|----------------------------|---------------------------------------------|
| `OtlpTraceService`     | gRPC trace receiver        | `Services/OtlpTraceService.cs`              |
| `OtlpMetricsService`   | gRPC metrics receiver      | `Services/OtlpMetricsService.cs`            |
| `OtlpLogsService`      | gRPC logs receiver         | `Services/OtlpLogsService.cs`               |
| `OtlpConverter`        | Protobuf → internal models | `Protocol/OtlpConverter.cs`                 |
| `OtlpHttpHandler`      | OTLP/HTTP endpoint         | `Protocol/OtlpHttpHandler.cs`               |
| `SpanModel`            | Internal span record       | `Models/SpanModel.cs`                       |
| `LogModel`             | Internal log record        | `Models/LogModel.cs`                        |
| `MetricModel`          | Internal metric record     | `Models/MetricModel.cs`                     |
| `ResourceModel`        | Resource attributes        | `Models/ResourceModel.cs`                   |
| `TelemetryBroadcaster` | Event broadcasting         | `Streaming/TelemetryBroadcaster.cs`         |
| `ResourceAttributes`   | Semconv constants          | `SemanticConventions/ResourceAttributes.cs` |

## Dependencies

**Internal:** None (base library)

**External:** Grpc.AspNetCore 2.71, Google.Protobuf 3.33, OpenTelemetry.Proto

## Data Flow

```
gRPC Export Request (protobuf)
    ↓
OtlpTraceService.Export()
    ↓
OtlpConverter.ConvertResourceSpans()
    ↓
SpanModel[] ──→ SpansReceived event
    ↓
Subscribers (collector, broadcaster)
```

## Patterns Used

- **Observer**: Services raise events (SpansReceived, MetricsReceived, LogsReceived)
- **Adapter**: OtlpConverter bridges protobuf ↔ internal models
- **Template Method**: All 3 services follow identical Export() structure
- **Sealed Classes**: All services sealed for performance
