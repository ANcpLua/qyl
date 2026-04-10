# eng/java — OTel Java Agent Integration

> **Status: stub.** Java SDK codegen (like `eng/semconv/` for TypeSpec) is deferred until qyl reaches beta / 1.0. During alpha the focus is the C# platform. This file documents manual Java instrumentation for now.

Instruments Java apps to send OTLP telemetry to qyl. Zero code changes required.

## How it works

The OpenTelemetry Java Agent attaches to the JVM via `-javaagent:` flag.
It instruments bytecode at runtime: Spring Boot, JDBC, Kafka, gRPC, HTTP clients.
Traces, metrics, and logs emit as OTLP to the qyl collector.

## Quick start

```bash
nuke JavaAgent --instrument-path /path/to/java-project
source qyl-otel.env
mvn spring-boot:run   # or: java -jar app.jar
```

## What `nuke JavaAgent` does

1. Downloads `opentelemetry-javaagent.jar` (v2.26.0) if not present
2. Generates `qyl-otel.env` with:
   - `JAVA_TOOL_OPTIONS=-javaagent:./opentelemetry-javaagent.jar`
   - `OTEL_SERVICE_NAME=<project-dir-name>`
   - `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318`
   - `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`
   - `OTEL_TRACES_EXPORTER=otlp`
   - `OTEL_METRICS_EXPORTER=otlp`
   - `OTEL_LOGS_EXPORTER=otlp`
3. Adds `opentelemetry-javaagent.jar` and `qyl-otel.env` to `.gitignore`

## Parameters

| Parameter | Default | What it sets |
|-----------|---------|-------------|
| `--instrument-path` | repo root | Target Java project directory |
| `--otlp-endpoint` | `http://localhost:4318` | qyl collector address |
| `--service-name` | directory name | `OTEL_SERVICE_NAME` value |

## Docker usage

```yaml
services:
  my-java-app:
    build: .
    env_file: [qyl-otel.env]
    volumes:
      - ./opentelemetry-javaagent.jar:/opentelemetry-javaagent.jar
```

## Maven `.mvn/jvm.config`

```
-javaagent:./opentelemetry-javaagent.jar
```

This persists the agent across `mvn` invocations without env vars.

## Env var tuning

| Env var | Effect |
|---------|--------|
| `OTEL_JAVAAGENT_ENABLED=false` | Disable agent without removing it |
| `OTEL_INSTRUMENTATION_[NAME]_ENABLED=false` | Disable specific instrumentation |
| `OTEL_TRACES_SAMPLER=parentbased_traceidratio` | Sample traces |
| `OTEL_TRACES_SAMPLER_ARG=0.1` | 10% sampling rate |

## Files

| File | Purpose |
|------|---------|
| `eng/build/BuildInstrument.cs` | NUKE target implementation |
| `eng/java/CLAUDE.md` | This file |

## Constraints

- Java 8+ required (agent supports 8-21+)
- Agent JAR is ~30MB — do not commit to git
- `JAVA_TOOL_OPTIONS` is read by ALL JVMs on the machine — scope with Docker or per-terminal
