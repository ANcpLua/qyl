# qyl gRPC Implementation Analysis

> **Generated:** 2026-01-23
> **Purpose:** Dokumentation des aktuellen gRPC-Implementierungsstatus für zukünftige Referenz
> **Sources:** Microsoft gRPC Docs, LightProto (dameng324), Native AOT Docs

---

## 0. Executive Summary - Moderne Alternativen

### qyl's Ansatz vs. moderne Alternativen

| Aktuell (qyl)                      | Alternative                  | Trade-off                                             |
|------------------------------------|------------------------------|-------------------------------------------------------|
| Custom `ref struct ProtobufReader` | **LightProto** (dameng324)   | Gleiche AOT-Vorteile, weniger Maintenance, Source-Gen |
| Manuell implementierte OTLP Types  | LightProto + `.proto` Import | Automatische Updates bei OTLP Spec Changes            |
| SSE für Real-Time                  | **gRPC-Web**                 | gRPC-native Clients, aber SSE ist browser-kompatibler |
| Separate HTTP/gRPC Endpoints       | **gRPC JSON Transcoding**    | Einheitliche Endpoints, weniger Code                  |
| REST Health Checks                 | **gRPC Health Protocol**     | K8s-native Probes                                     |

### Serialisierungs-Entscheidungsmatrix (.NET 10)

| Szenario                                        | Lösung                                      |
|-------------------------------------------------|---------------------------------------------|
| REST API, Browser-Clients                       | `System.Text.Json` + Source Gen             |
| Interne Service-zu-Service, Max Performance     | **MemoryPack**                              |
| Interop mit anderen Sprachen (Go, Java, Python) | **LightProto** oder protobuf-net            |
| gRPC Services                                   | `Grpc.AspNetCore` + contract-first          |
| Bestehende .proto Files                         | `protobuf-net.BuildTools` oder `Grpc.Tools` |
| Greenfield, Performance-kritisch, .NET-only     | **MemoryPack**                              |
| Greenfield, Protobuf-kompatibel, AOT-kritisch   | **LightProto**                              |

---

## 1. Aktuelle Implementierung

### 1.1 Proto Files

**Status:** Keine `.proto` Dateien vorhanden. gRPC-Services sind manuell implementiert ohne Code-Generierung.

**Begründung:** Native AOT-Kompatibilität - keine Reflection, volle Kontrolle über Serialisierung.

### 1.2 gRPC Service Implementation

**TraceServiceImpl** (`src/qyl.collector/Grpc/TraceServiceImpl.cs`)

- Erbt von `TraceServiceBase` (custom base class, nicht Grpc.Core generated)
- Implementiert: `Export(ExportTraceServiceRequest request, ServerCallContext context)`
- Service Path: `opentelemetry.proto.collector.trace.v1.TraceService`

**Verantwortlichkeiten:**

- Akzeptiert OTLP Trace-Daten via gRPC (Port 4317)
- Konvertiert Proto-Format zu Storage-Rows via `OtlpConverter`
- Pusht zu In-Memory Ring Buffer für Real-Time Queries
- Enqueued Batch zu DuckDB Store für Persistenz
- Broadcastet zu SSE Subscribers
- Gibt `ExportTraceServiceResponse` zurück

**Error Handling:**

```csharp
OperationCanceledException → StatusCode.Cancelled
ObjectDisposedException   → StatusCode.Unavailable
Exception (generic)       → StatusCode.Internal
```

### 1.3 gRPC Service Infrastructure

**TraceServiceMethodProvider** (`src/qyl.collector/Grpc/OtlpProtoTypes.cs`)

- Implementiert `IServiceMethodProvider<TraceServiceImpl>`
- Registriert gRPC Method Discovery
- Verwendet `MethodType.Unary` für Export
- Custom Marshallers für Request/Response Serialisierung

**Message Types (manuell implementiert):**

- `ExportTraceServiceRequest` - Server-side only Deserialisierung
- `ExportTraceServiceResponse` - Empty Response (per OTLP Spec)
- OTLP Proto Hierarchy Types:
    - `OtlpResourceSpansProto`
    - `OtlpSpanProto`
    - `OtlpKeyValueProto`
    - etc.

### 1.4 gRPC Konfiguration

**Program.cs Setup (Zeilen 41-46):**

```csharp
builder.Services.AddGrpc(options =>
{
    options.ResponseCompressionLevel = CompressionLevel.Optimal;
    options.ResponseCompressionAlgorithm = "gzip";
});
builder.Services.AddSingleton<IServiceMethodProvider<TraceServiceImpl>, TraceServiceMethodProvider>();
```

**Kestrel Konfiguration (Zeilen 31-38):**

- Dual Endpoint Setup:
    - **HTTP Endpoint** (Port 5100): HTTP/1 + HTTP/2 für REST API, HTTP OTLP, Static Files
    - **gRPC Endpoint** (Port 4317): HTTP/2 only für gRPC TraceService
- Environment Variables:
    - `QYL_PORT` (default: 5100) - HTTP
    - `QYL_GRPC_PORT` (default: 4317) - gRPC

### 1.5 Compression & Decompression

**Response Compression:**

- gRPC: Optimal gzip compression in AddGrpc konfiguriert
- HTTP: Pre-computed gzip für Dashboard Assets

**Request Decompression:**

```csharp
builder.Services.AddRequestDecompression();
app.UseRequestDecompression();
```

- Handled gzip/deflate compressed Payloads von OTLP Clients
- Positioniert vor Request Body Reading in Middleware Chain

### 1.6 Protobuf Parsing Infrastructure

**Custom ProtobufReader** (`ref struct` in OtlpProtoTypes.cs):

- Zero-Allocation Protobuf Reader für OTLP Messages
- Implementiert Wire Type Handling (Varint, Fixed64, LengthDelimited, etc.)

**Methoden:**

| Methode                          | Beschreibung                                       |
|----------------------------------|----------------------------------------------------|
| `ReadVarint()`                   | Variable-length Integer Decoding                   |
| `ReadSignedVarint()`             | ZigZag decoded signed Integers                     |
| `ReadFixed64()` / `ReadDouble()` | Fixed-size Values                                  |
| `ReadString()` / `ReadBytes()`   | Length-delimited Data                              |
| `ReadBytesAsHex()`               | Converts Bytes to Hex String (für TraceId, SpanId) |
| `SkipField()`                    | Forward-compatible Field Skipping                  |

**IProtobufParseable Interface:**

- Alle OTLP Message Types implementieren: `void MergeFrom(ProtobufReader reader, int length)`
- Ermöglicht rekursives Parsing von Nested Messages

### 1.7 Client Usage

**gRPC Client Usage** (`src/qyl.mcp/Client.cs`):

- MCP Server kommuniziert mit Collector via **HTTP only**, nicht gRPC
- Verwendet Standard HttpClient mit 60-Sekunden Timeout
- Ruft REST Endpoints auf: `/api/v1/sessions`, `/api/v1/traces`, etc.
- Keine direkte gRPC Client Usage (Kommunikation entkoppelt)

### 1.8 Health Checks

**Health Check Konfiguration** (`src/qyl.collector/Health/HealthExtensions.cs`):

**Tagging System für Kubernetes Probes:**

| Tag       | Checks                                               |
|-----------|------------------------------------------------------|
| `"live"`  | ApplicationLifecycleHealthCheck, ResourceUtilization |
| `"ready"` | DuckDbHealthCheck, ResourceUtilization               |

**Endpoints:**

- `/health` - Only "live" Checks (process running?)
- `/ready` - All Health Checks (service ready for traffic?)

**Features:**

- Published als OTel Metrics via `AddTelemetryHealthCheckPublisher()`
- Resource Monitoring auf Linux/Windows (OS-aware)

### 1.9 Authentication/Authorization

**OTLP-specific Middleware:**

| Middleware             | Funktion                                                                      |
|------------------------|-------------------------------------------------------------------------------|
| `OtlpCorsMiddleware`   | CORS für `/v1/*` OTLP Paths only                                              |
| `OtlpApiKeyMiddleware` | API Key Validation via `x-otlp-api-key` Header                                |
| `TokenAuthMiddleware`  | Dashboard Auth (excluded für `/health`, `/ready`, `/v1/traces`, `/api/login`) |

**OtlpApiKeyMiddleware Details:**

- Supports Primary und Secondary Keys
- Verwendet `CryptographicOperations.FixedTimeEquals()` für Timing-safe Comparison

### 1.10 Dependencies

**NuGet Packages** (`src/qyl.collector/qyl.collector.csproj`):

- `Grpc.AspNetCore` - ASP.NET Core gRPC Hosting
- `Grpc.Tools` - Code Generation (nicht aktiv genutzt für .proto)
- `Google.Protobuf` - Protobuf Runtime
- `OpenTelemetry.Api` - OTel Instrumentation
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` - OTLP Export Support

### 1.11 Streaming Implementations

**NICHT via gRPC Streaming implementiert:**

- Real-time Telemetry verwendet **Server-Sent Events (SSE)** stattdessen
- Endpoint: `/api/v1/live` mit `text/event-stream`
- Verwendet Bounded Channels für Backpressure (`BoundedChannelFullMode.DropOldest`)
- SSE ist browser-kompatibler und firewall-freundlicher

### 1.12 W3C Trace Context Parser

**TraceContextParser** (in TraceServiceImpl.cs):

- Zero-Allocation Parser für W3C traceparent Headers
- Format: `{version}-{trace-id}-{parent-id}-{flags}`
- Sowohl char als auch UTF-8 byte span Varianten
- Verwendet für Extracting Trace Context aus HTTP Headers

---

## 2. Architektur-Entscheidungen

| Entscheidung                        | Begründung                                                  | Trade-off                                 |
|-------------------------------------|-------------------------------------------------------------|-------------------------------------------|
| Manual Protobuf Implementation      | AOT-Kompatibilität, keine Runtime-Codegen                   | Mehr Maintenance-Aufwand                  |
| Dual Protocol Support (gRPC + HTTP) | Flexibilität für verschiedene Clients                       | Zwei Code-Pfade zu maintainen             |
| No Streaming RPCs                   | SSE für Browser-Kompatibilität                              | gRPC-native Clients nicht optimal bedient |
| Decoupled Client Communication      | MCP via HTTP für Entkopplung                                | Keine gRPC-Performance-Vorteile           |
| AOT-Ready Design                    | `ref struct ProtobufReader`, Source-Gen JSON, no Reflection | Komplexere Implementierung                |
| REST-only Health Checks             | Einfachheit, HTTP-Kompatibilität                            | Kein gRPC Health Checking Protocol        |

---

## 3. Was fehlt (Gap Analysis)

### 3.1 Hohe Priorität (P1)

**gRPC Health Checking Protocol**

- Aktuell: REST-only (`/health`, `/ready`)
- Empfohlen: `Grpc.AspNetCore.HealthChecks`
- Vorteil: Kubernetes-native gRPC Health Probes

```csharp
// Empfohlene Implementierung:
builder.Services.AddGrpcHealthChecks()
    .AddCheck("duckdb", () => /* ... */);

app.MapGrpcHealthChecksService();
```

### 3.2 Mittlere Priorität (P2)

**gRPC Interceptors**

- Aktuell: Keine Interceptors außer Compression
- Empfohlen: Logging, Metrics, OTel Tracing

```csharp
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<MetricsInterceptor>();
});
```

**Deadline/Timeout Konfiguration**

- Aktuell: Keine explizite Deadline-Konfiguration
- Empfohlen: Upper Limit für Call Duration

### 3.3 Niedrige Priorität (P3)

| Feature               | Status              | Relevanz                            |
|-----------------------|---------------------|-------------------------------------|
| Retry Policies        | Nicht implementiert | Server-seitig weniger relevant      |
| Keep-Alive Pings      | Nicht konfiguriert  | Optional für langlebige Connections |
| Flow Control Tuning   | Default Settings    | Bei hoher Last relevant             |
| gRPC-JSON Transcoding | Separate Endpoints  | Alternative vorhanden               |

---

## 4. Datei-Referenzen

| Datei                                                         | Inhalt                       |
|---------------------------------------------------------------|------------------------------|
| `src/qyl.collector/Grpc/TraceServiceImpl.cs`                  | Core gRPC Service            |
| `src/qyl.collector/Grpc/OtlpProtoTypes.cs`                    | Proto Types & ProtobufReader |
| `src/qyl.collector/Program.cs` (12-46, 114-124)               | Konfiguration                |
| `src/qyl.collector/Health/HealthExtensions.cs`                | Health Checks                |
| `src/qyl.collector/Ingestion/OtlpCorsMiddleware.cs`           | CORS Middleware              |
| `src/qyl.collector/Ingestion/OtlpApiKeyMiddleware.cs`         | API Key Middleware           |
| `tests/qyl.collector.tests/Integration/OtlpIngestionTests.cs` | Integration Tests            |

---

## 5. Limitationen

- ❌ Keine gRPC Streaming Implementations
- ❌ Keine Interceptors außer Compression
- ❌ Keine Load Balancing Konfiguration
- ❌ Keine Retry Policy auf gRPC Level (nur async Backoff auf Storage Layer)
- ❌ Keine Connection Pooling Options exposed
- ❌ Kein gRPC-JSON Transcoding (separate Endpoints)
- ❌ Kein gRPC Health Checking Protocol

---

## 6. Empfohlene nächste Schritte

1. **gRPC Health Checks implementieren** für K8s-native Deployments
2. **OTel Interceptor hinzufügen** für Meta-Observability
3. **Deadline Configuration** für Ressourcenschutz
4. **Flow Control Tuning** evaluieren bei Performance-Tests

---

## 7. Neue Erkenntnisse aus Microsoft Docs (2026-01)

### 7.1 LightProto als Alternative zu Custom ProtobufReader

**LightProto** (https://github.com/dameng324/LightProto) ist ein Source-Generator-basierter Protobuf-Ersatz:

**Features:**

- AOT-friendly by design, no IL warnings
- Source generator–powered serializers/parsers at compile time
- 20-50% schneller als protobuf-net
- `netstandard2.0`, `net8.0`, `net9.0`, `net10.0` Support
- Zero third-party dependencies

**Migration von qyl's Custom Implementation:**

```csharp
// Aktuell (qyl): Manual ref struct
public ref struct ProtobufReader { ... }

// LightProto Alternative:
[ProtoContract]
public partial class OtlpSpan
{
    [ProtoMember(1)] public string TraceId { get; set; }
    [ProtoMember(2)] public string SpanId { get; set; }
    // ... Source Generator generiert Parser
}
```

**Trade-off für qyl:**

| Aspekt             | Custom ProtobufReader      | LightProto                          |
|--------------------|----------------------------|-------------------------------------|
| AOT-Kompatibilität | ✅ Manuell sichergestellt   | ✅ By design                         |
| Maintenance        | ❌ Hoch (OTLP Spec Changes) | ✅ Niedrig                           |
| Performance        | ✅ Optimiert für OTLP       | ✅ 20-50% schneller als protobuf-net |
| Kontrolle          | ✅ Vollständig              | 🟡 Generator-basiert                |

**Empfehlung:** Evaluation für zukünftige OTLP Spec Updates, aber aktueller Ansatz ist valide.

### 7.2 gRPC-Web für Browser-Clients

**Aktuell:** qyl nutzt SSE (`/api/v1/live`) für Real-Time Telemetrie.

**Alternative:** gRPC-Web ermöglicht gRPC-Calls direkt aus dem Browser:

```csharp
// Server-Setup
app.UseGrpcWeb();
app.MapGrpcService<TraceServiceImpl>().EnableGrpcWeb();
```

**gRPC-Web Limitationen:**

- ❌ Client Streaming nicht supported
- ❌ Bidirectional Streaming nicht supported
- ✅ Unary RPCs supported
- ✅ Server Streaming supported (mit `grpcwebtext` Mode)

**Vergleich für qyl:**

| Aspekt              | SSE (aktuell)  | gRPC-Web               |
|---------------------|----------------|------------------------|
| Browser Support     | ✅ Alle Browser | ✅ Alle Browser         |
| Protokoll           | HTTP/1.1       | HTTP/1.1 oder HTTP/2   |
| Bidirectional       | ❌ Nein         | ❌ Nein                 |
| Firewall-freundlich | ✅ Sehr         | 🟡 Proxy nötig (Envoy) |
| Komplexität         | ✅ Einfach      | 🟡 Höher               |

**Empfehlung:** SSE bleibt besser für qyl's Dashboard-Use-Case.

### 7.3 gRPC JSON Transcoding

**Aktuell:** qyl hat separate Endpoints:

- `/v1/traces` (HTTP/JSON)
- Port 4317 (gRPC/Protobuf)

**Alternative:** gRPC JSON Transcoding ermöglicht einheitliche Endpoints:

```protobuf
service TraceService {
    rpc Export (ExportTraceServiceRequest) returns (ExportTraceServiceResponse) {
        option (google.api.http) = {
            post: "/v1/traces"
            body: "*"
        };
    }
}
```

**Trade-off:**

| Aspekt             | Separate Endpoints (aktuell) | JSON Transcoding    |
|--------------------|------------------------------|---------------------|
| Flexibilität       | ✅ Hoch                       | 🟡 An gRPC gebunden |
| Code Duplication   | 🟡 Zwei Pfade                | ✅ Ein Pfad          |
| AOT Kompatibilität | ✅ Voll                       | ✅ Voll              |

**Empfehlung:** Aktueller Ansatz bleibt valide, Transcoding nur bei Neuentwicklung evaluieren.

### 7.4 Native AOT - qyl ist bereits optimal

**qyl's AOT-Features (bereits implementiert):**

- ✅ `ref struct ProtobufReader` (Zero-Allocation)
- ✅ Source-Generated JSON Serialization
- ✅ No Reflection
- ✅ `WebApplication.CreateSlimBuilder` equivalent

**Microsoft ASP.NET Core Native AOT Kompatibilität:**

| Feature       | Status                |
|---------------|-----------------------|
| gRPC          | ✅ Fully Supported     |
| Minimal APIs  | ✅ Partially Supported |
| Health Checks | ✅ Fully Supported     |
| Static Files  | ✅ Fully Supported     |
| MVC           | ❌ Not Supported       |
| Blazor Server | ❌ Not Supported       |

**qyl nutzt nur AOT-kompatible Features!**

### 7.5 Inter-Process Communication (IPC)

**Neue Optionen für lokale Kommunikation:**

| Transport           | Use Case                 | Performance                  |
|---------------------|--------------------------|------------------------------|
| TCP (aktuell)       | Netzwerk                 | Standard                     |
| Unix Domain Sockets | Same-machine Linux/macOS | Schneller                    |
| Named Pipes         | Same-machine Windows     | Schneller + Windows Security |

**Für qyl relevant:** MCP-Server kommuniziert via HTTP. Bei Performance-Problemen könnte UDS evaluiert werden.

### 7.6 gRPC Health Checking Protocol (FEHLEND)

**Microsoft empfiehlt:**

```csharp
builder.Services.AddGrpcHealthChecks()
    .AddCheck("duckdb", () => HealthCheckResult.Healthy());

app.MapGrpcHealthChecksService();
```

**Vorteile:**

- Kubernetes kann gRPC-native Health Probes verwenden
- Standard-Protocol (`grpc.health.v1.Health`)
- Integriert mit ASP.NET Core Health Checks

**Empfehlung:** P1 - Sollte implementiert werden für K8s Deployments.

---

## 8. Package-Empfehlungen für .NET 10

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>

<ItemGroup>
<!-- JSON - built-in, nur Source Gen Context definieren -->

<!-- Binary High-Perf (intern) - Optional -->
<PackageReference Include="MemoryPack" Version="1.21.*"/>

<!-- Protobuf Wire-Format - EINE von beiden: -->
<!-- Option A: Neuer, schneller, AOT-first -->
<PackageReference Include="LightProto" Version="*"/>
<!-- Option B: Etabliert, größeres Ecosystem -->
<PackageReference Include="protobuf-net" Version="3.*"/>

<!-- gRPC -->
<PackageReference Include="Grpc.AspNetCore" Version="2.*"/>

<!-- gRPC Health Checks -->
<PackageReference Include="Grpc.AspNetCore.HealthChecks" Version="2.*"/>
</ItemGroup>
```

---

## 9. Aktualisierte Prioritäten

| Priorität | Feature                  | Aufwand | Impact                |
|-----------|--------------------------|---------|-----------------------|
| **P1**    | gRPC Health Checks       | Niedrig | K8s-native Probes     |
| **P2**    | gRPC Interceptors (OTel) | Mittel  | Meta-Observability    |
| **P2**    | Deadline Configuration   | Niedrig | Ressourcenschutz      |
| **P3**    | LightProto Evaluation    | Hoch    | Maintenance-Reduktion |
| **P4**    | gRPC-Web Evaluation      | Hoch    | Nur für neue Clients  |
| **P5**    | JSON Transcoding         | Mittel  | Code-Reduktion        |

---

## 10. Fazit

**qyl's aktuelle Architektur ist gut:**

- AOT-optimiert ✅
- Zero-Allocation Protobuf ✅
- Dual Protocol (gRPC + HTTP) ✅

**Was wirklich fehlt:**

1. **gRPC Health Checking Protocol** - P1, niedrig Aufwand, hoher K8s-Benefit
2. **gRPC Interceptors** - P2, für Meta-Observability

**Was NICHT nötig ist:**

- LightProto Migration (aktueller Ansatz funktioniert)
- gRPC-Web (SSE ist besser für Dashboard)
- JSON Transcoding (separate Endpoints sind flexibler)
