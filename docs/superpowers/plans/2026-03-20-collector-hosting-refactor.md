# Collector Hosting Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:
> executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the 896-line `Program.cs` into a thin entry point + modular hosting extensions, add protobuf log
ingestion support, and harden OTLP auth for production.

**Architecture:** 5 extension classes in `Hosting/` own distinct concerns (services, Kestrel, initialization,
middleware, endpoints). `Program.cs` becomes ~30 lines of `await`-based pipeline. A new `CollectorPortOptions` record
resolves port config once. Protobuf logs support mirrors the existing `/v1/traces` protobuf path using the same
`ProtobufReader` infrastructure. OTLP auth defaults to `ApiKey` in non-Development environments.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core minimal APIs, gRPC, DuckDB, OTLP protobuf (hand-rolled reader)

---

## File Structure

| File                                           | Responsibility                                                                                      |
|------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| `Hosting/CollectorPortOptions.cs`              | Port configuration record, resolved once from `IConfiguration`                                      |
| `Hosting/CollectorServiceRegistration.cs`      | All `builder.Services.*` registrations (DuckDB, SSE, Loom, search, etc.)                            |
| `Hosting/CollectorKestrelExtensions.cs`        | Kestrel multi-port setup (HTTP, OTLP, gRPC)                                                         |
| `Hosting/CollectorInitializationExtensions.cs` | Post-build initialization: migrations, GitHub init, metrics callbacks                               |
| `Hosting/CollectorMiddlewareExtensions.cs`     | Middleware pipeline: OTLP CORS/auth, token auth, decompression, exceptions, telemetry, static files |
| `Hosting/CollectorEndpointExtensions.cs`       | All inline endpoint lambdas moved to named methods + route groups                                   |
| `Grpc/OtlpLogProtoTypes.cs`                    | Protobuf log message types (mirrors trace proto types)                                              |
| `Ingestion/OtlpLogProtobufParser.cs`           | Protobuf parser for `/v1/logs` (mirrors `OtlpProtobufParser`)                                       |
| `Ingestion/OtlpConverter.cs`                   | Add `ConvertProtoLogsToStorageRows` method                                                          |
| `Program.cs`                                   | Thin ~30-line entry point with top-level `await`                                                    |

## Existing files preserved (not touched unless noted)

All existing `Map*Endpoints()` extension methods, `StartupBanner`, `OtlpApiKeyOptions`, `OtlpCorsOptions`,
`TokenAuthOptions` stay as-is. The `ExceptionHandlerLog` and `OtlpLogsLog` partial log classes move into
`CollectorMiddlewareExtensions.cs` and `CollectorEndpointExtensions.cs` respectively.

---

### Task 1: Create `CollectorPortOptions`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorPortOptions.cs`

- [ ] **Step 1: Create the port options record**

```csharp
namespace Qyl.Collector.Hosting;

/// <summary>
///     Resolves collector port configuration once at startup.
///     Injected as singleton — no repeated config lookups.
/// </summary>
public sealed record CollectorPortOptions
{
    /// <summary>Dashboard + REST API + SSE port.</summary>
    public required int Http { get; init; }

    /// <summary>OTLP HTTP ingestion port (0 = disabled, falls back to Http).</summary>
    public required int OtlpHttp { get; init; }

    /// <summary>gRPC OTLP ingestion port (0 = disabled).</summary>
    public required int Grpc { get; init; }

    /// <summary>DuckDB file path.</summary>
    public required string DataPath { get; init; }

    /// <summary>Dashboard auth token.</summary>
    public required string Token { get; init; }

    public static CollectorPortOptions FromConfiguration(IConfiguration config)
    {
        var port = config.GetValue<int?>("QYL_PORT")
                   ?? config.GetValue<int?>("PORT")
                   ?? 5100;

        var dataPath = config["QYL_DATA_PATH"] ?? "qyl.duckdb";
        var dataDir = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        return new CollectorPortOptions
        {
            Http = port,
            OtlpHttp = config.GetValue("QYL_OTLP_PORT", 4318),
            Grpc = config.GetValue("QYL_GRPC_PORT", 4317),
            DataPath = dataPath,
            Token = config["QYL_TOKEN"] ?? TokenGenerator.Generate()
        };
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj --no-restore -v:q 2>&1 | tail -5`

- [ ] **Step 3: Commit**

```
feat(collector): add CollectorPortOptions for centralized port config
```

---

### Task 2: Create `CollectorKestrelExtensions`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorKestrelExtensions.cs`

- [ ] **Step 1: Create Kestrel configuration extension**

```csharp
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Qyl.Collector.Hosting;

public static class CollectorKestrelExtensions
{
    public static WebApplicationBuilder ConfigureCollectorKestrel(
        this WebApplicationBuilder builder,
        CollectorPortOptions ports)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(ports.Http, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.OtlpHttp > 0 && ports.OtlpHttp != ports.Http)
                options.ListenAnyIP(ports.OtlpHttp, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);

            if (ports.Grpc > 0)
                options.ListenAnyIP(ports.Grpc, lo => lo.Protocols = HttpProtocols.Http2);
        });

        return builder;
    }
}
```

- [ ] **Step 2: Verify it compiles**

- [ ] **Step 3: Commit**

```
feat(collector): add CollectorKestrelExtensions
```

---

### Task 3: Create `CollectorServiceRegistration`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorServiceRegistration.cs`

This is the largest extension — all `builder.Services.*` calls from Program.cs move here. Key changes:

- OTLP auth defaults to `ApiKey` in non-Development, `Unsecured` in Development
- Throws at startup if `ApiKey` mode and no keys configured

- [ ] **Step 1: Create service registration extension**

Move all service registrations from Program.cs lines 30-263 into
`AddCollectorServices(this WebApplicationBuilder, CollectorPortOptions)`. The method returns the builder for chaining.

Key behavior change for requirement #8:

```csharp
// OTLP auth: fail closed in production
var isDevelopment = builder.Environment.IsDevelopment();
var configuredAuthMode = builder.Configuration["QYL_OTLP_AUTH_MODE"];
var otlpApiKeyOptions = new OtlpApiKeyOptions
{
    AuthMode = configuredAuthMode ?? (isDevelopment ? "Unsecured" : "ApiKey"),
    PrimaryApiKey = builder.Configuration["QYL_OTLP_PRIMARY_API_KEY"],
    SecondaryApiKey = builder.Configuration["QYL_OTLP_SECONDARY_API_KEY"]
};

if (otlpApiKeyOptions.IsApiKeyMode
    && string.IsNullOrWhiteSpace(otlpApiKeyOptions.PrimaryApiKey)
    && string.IsNullOrWhiteSpace(otlpApiKeyOptions.SecondaryApiKey))
{
    throw new InvalidOperationException(
        "OTLP auth mode is 'ApiKey' but no keys are configured. " +
        "Set QYL_OTLP_PRIMARY_API_KEY or QYL_OTLP_SECONDARY_API_KEY, " +
        "or set QYL_OTLP_AUTH_MODE=Unsecured to disable authentication.");
}
```

All singleton registrations move verbatim. `CollectorPortOptions` and `OtlpApiKeyOptions` and `OtlpCorsOptions` are
registered as singletons.

- [ ] **Step 2: Verify it compiles**

- [ ] **Step 3: Commit**

```
feat(collector): add CollectorServiceRegistration with fail-closed auth
```

---

### Task 4: Create `CollectorInitializationExtensions`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorInitializationExtensions.cs`

- [ ] **Step 1: Create initialization extension**

Post-`Build()` initialization — async with top-level await (requirement #3):

```csharp
namespace Qyl.Collector.Hosting;

public static class CollectorInitializationExtensions
{
    public static async Task InitializeCollectorAsync(this WebApplication app)
    {
        // Register storage size metrics callback
        var duckDbStore = app.Services.GetRequiredService<DuckDbStore>();
        QylMetrics.RegisterStorageSizeCallback(duckDbStore.GetStorageSizeBytes);

        // Apply pending DuckDB schema migrations
        var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
        var migrationDirectory = Path.Combine(app.Environment.ContentRootPath, "Storage", "Migrations");
        const int collectorSchemaVersion = 20260214;
        migrationRunner.ApplyPendingMigrations(duckDbStore.Connection, collectorSchemaVersion, migrationDirectory);

        // Initialize GitHub service: load persisted token from DuckDB (ADR-002)
        await app.Services.GetRequiredService<GitHubService>().InitializeAsync().ConfigureAwait(false);
    }
}
```

This replaces the sync-over-async `.GetAwaiter().GetResult()` on line 278.

- [ ] **Step 2: Verify it compiles**

- [ ] **Step 3: Commit**

```
feat(collector): add async CollectorInitializationExtensions
```

---

### Task 5: Create `CollectorMiddlewareExtensions`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorMiddlewareExtensions.cs`

- [ ] **Step 1: Create middleware pipeline extension**

```csharp
using Microsoft.AspNetCore.Diagnostics;

namespace Qyl.Collector.Hosting;

public static class CollectorMiddlewareExtensions
{
    public static WebApplication UseCollectorMiddleware(this WebApplication app)
    {
        var otlpCorsOptions = app.Services.GetRequiredService<OtlpCorsOptions>();
        var otlpApiKeyOptions = app.Services.GetRequiredService<OtlpApiKeyOptions>();
        var tokenAuthOptions = app.Services.GetRequiredService<TokenAuthOptions>();

        // OTLP middleware (before token auth — OTLP has its own auth)
        if (otlpCorsOptions.IsEnabled)
            app.UseMiddleware<OtlpCorsMiddleware>(otlpCorsOptions);

        app.UseMiddleware<OtlpApiKeyMiddleware>(otlpApiKeyOptions);
        app.UseMiddleware<TokenAuthMiddleware>(tokenAuthOptions);

        // Request decompression before endpoints that read request body
        app.UseRequestDecompression();

        // Global exception handler: structured JSON errors with trace correlation
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Qyl.Collector.ExceptionHandler");
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (exceptionFeature?.Error is { } error)
                    ExceptionHandlerLog.UnhandledException(logger, context.Request.Method,
                        context.Request.Path, context.Request.QueryString.ToString(), error);

                await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error", traceId });
            });
        });

        // .NET 10 telemetry middleware
        app.UseQylTelemetry();

        // Dashboard static files
        var hasEmbeddedDashboard = EmbeddedDashboardExtensions.HasEmbeddedDashboard();
        if (hasEmbeddedDashboard)
            app.UseEmbeddedDashboard();
        else
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        return app;
    }
}

internal static partial class ExceptionHandlerLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}{Query}")]
    public static partial void UnhandledException(ILogger logger, string method, string path, string query,
        Exception error);
}
```

- [ ] **Step 2: Verify it compiles**

- [ ] **Step 3: Commit**

```
feat(collector): add CollectorMiddlewareExtensions
```

---

### Task 6: Add protobuf log types + parser (requirement #11-14)

**Files:**

- Create: `src/qyl.collector/Grpc/OtlpLogProtoTypes.cs`
- Create: `src/qyl.collector/Ingestion/OtlpLogProtobufParser.cs`
- Modify: `src/qyl.collector/Ingestion/OtlpConverter.cs` — add `ConvertProtoLogsToStorageRows`

This fixes the protocol mismatch: `/v1/traces` supports protobuf but `/v1/logs` only supported JSON. After this task,
both endpoints handle `application/x-protobuf` and JSON.

- [ ] **Step 1: Create OTLP log protobuf types**

Following the OTLP proto spec (`logs_service.proto`, `logs.proto`), create the protobuf message types that mirror the
trace types in `OtlpProtoTypes.cs`. The field numbers match the official proto definitions.

```csharp
// src/qyl.collector/Grpc/OtlpLogProtoTypes.cs
namespace Qyl.Collector.Grpc;

/// <summary>
///     Request message for LogsService.Export (protobuf wire format).
///     Proto: opentelemetry.proto.collector.logs.v1.ExportLogsServiceRequest
/// </summary>
public sealed class ExportLogsServiceRequestProto
{
    public List<OtlpResourceLogsProto> ResourceLogs { get; } = [];

    public void MergeFrom(ReadOnlySequence<byte> data)
    {
        var reader = new ProtobufReader(data);
        while (reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // resource_logs
                    var resourceLogs = new OtlpResourceLogsProto();
                    reader.ReadMessage(resourceLogs);
                    ResourceLogs.Add(resourceLogs);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>ResourceLogs from OTLP proto.</summary>
public sealed class OtlpResourceLogsProto : IProtobufParseable
{
    public OtlpResourceProto? Resource { get; set; }
    public List<OtlpScopeLogsProto> ScopeLogs { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // resource
                    Resource = new OtlpResourceProto();
                    reader.ReadMessage(Resource);
                    break;
                case 2: // scope_logs
                    var scopeLogs = new OtlpScopeLogsProto();
                    reader.ReadMessage(scopeLogs);
                    ScopeLogs.Add(scopeLogs);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>ScopeLogs from OTLP proto.</summary>
public sealed class OtlpScopeLogsProto : IProtobufParseable
{
    public List<OtlpLogRecordProto> LogRecords { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 2: // log_records
                    var logRecord = new OtlpLogRecordProto();
                    reader.ReadMessage(logRecord);
                    LogRecords.Add(logRecord);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     LogRecord from OTLP proto.
///     Proto field numbers: opentelemetry.proto.logs.v1.LogRecord
/// </summary>
public sealed class OtlpLogRecordProto : IProtobufParseable
{
    public ulong TimeUnixNano { get; set; }
    public ulong ObservedTimeUnixNano { get; set; }
    public int SeverityNumber { get; set; }
    public string? SeverityText { get; set; }
    public OtlpAnyValueProto? Body { get; set; }
    public List<OtlpKeyValueProto>? Attributes { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // time_unix_nano (fixed64)
                    TimeUnixNano = reader.ReadFixed64();
                    break;
                case 11: // observed_time_unix_nano (fixed64)
                    ObservedTimeUnixNano = reader.ReadFixed64();
                    break;
                case 2: // severity_number (enum)
                    SeverityNumber = (int)reader.ReadVarint();
                    break;
                case 3: // severity_text
                    SeverityText = reader.ReadString();
                    break;
                case 5: // body (AnyValue)
                    Body = new OtlpAnyValueProto();
                    reader.ReadMessage(Body);
                    break;
                case 6: // attributes
                    Attributes ??= [];
                    var attr = new OtlpKeyValueProto();
                    reader.ReadMessage(attr);
                    Attributes.Add(attr);
                    break;
                case 9: // trace_id (bytes)
                    TraceId = reader.ReadBytesAsHex();
                    break;
                case 10: // span_id (bytes)
                    SpanId = reader.ReadBytesAsHex();
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}
```

- [ ] **Step 2: Create log protobuf parser**

```csharp
// src/qyl.collector/Ingestion/OtlpLogProtobufParser.cs
using Qyl.Collector.Grpc;

namespace Qyl.Collector.Ingestion;

/// <summary>
///     Parses OTLP ExportLogsServiceRequest from HTTP protobuf payloads.
///     Mirrors OtlpProtobufParser for traces.
/// </summary>
public static class OtlpLogProtobufParser
{
    public static ExportLogsServiceRequestProto Parse(ReadOnlyMemory<byte> data) =>
        Parse(new ReadOnlySequence<byte>(data));

    public static ExportLogsServiceRequestProto Parse(ReadOnlySequence<byte> data)
    {
        var request = new ExportLogsServiceRequestProto();
        request.MergeFrom(data);
        return request;
    }

    public static async Task<ExportLogsServiceRequestProto> ParseFromRequestAsync(
        HttpRequest request,
        CancellationToken ct = default)
    {
        request.EnableBuffering();
        await using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
        return Parse(ms.ToArray().AsMemory());
    }
}
```

- [ ] **Step 3: Add `ConvertProtoLogsToStorageRows` to `OtlpConverter`**

Add a new method to `OtlpConverter.cs` that converts proto log types to `LogStorageRow`, mirroring
`ConvertLogsToStorageRows` but taking `ExportLogsServiceRequestProto` input. Reuses the existing `CreateLogStorageRow`
helper by mapping proto types to the JSON DTO equivalents, or by extracting the shared logic.

The simplest approach: map proto log records to the existing `OtlpLogRecord` JSON DTO, then call the existing
conversion. This avoids duplicating the log-to-storage logic:

```csharp
public static List<LogStorageRow> ConvertProtoLogsToStorageRows(ExportLogsServiceRequestProto proto)
{
    var jsonDto = MapProtoLogsToJsonDto(proto);
    return ConvertLogsToStorageRows(jsonDto);
}

private static OtlpExportLogsServiceRequest MapProtoLogsToJsonDto(ExportLogsServiceRequestProto proto)
{
    return new OtlpExportLogsServiceRequest
    {
        ResourceLogs = proto.ResourceLogs.Select(rl => new OtlpResourceLogs
        {
            Resource = rl.Resource is not null ? MapProtoResource(rl.Resource) : null,
            ScopeLogs = rl.ScopeLogs.Select(sl => new OtlpScopeLogs
            {
                LogRecords = sl.LogRecords.Select(lr => new OtlpLogRecord
                {
                    TimeUnixNano = lr.TimeUnixNano,
                    ObservedTimeUnixNano = lr.ObservedTimeUnixNano,
                    SeverityNumber = lr.SeverityNumber,
                    SeverityText = lr.SeverityText,
                    Body = lr.Body is not null ? MapProtoAnyValue(lr.Body) : null,
                    Attributes = lr.Attributes?.Select(MapProtoKeyValue).ToList(),
                    TraceId = lr.TraceId,
                    SpanId = lr.SpanId
                }).ToList()
            }).ToList()
        }).ToList()
    };
}
```

Note: `MapProtoResource`, `MapProtoAnyValue`, `MapProtoKeyValue` — check if these already exist from the trace
conversion. If not, add minimal mappers.

- [ ] **Step 4: Verify it compiles**

- [ ] **Step 5: Commit**

```
feat(collector): add protobuf log ingestion support matching /v1/traces
```

---

### Task 7: Create `CollectorEndpointExtensions`

**Files:**

- Create: `src/qyl.collector/Hosting/CollectorEndpointExtensions.cs`

- [ ] **Step 1: Create endpoint mapping extension**

All inline lambdas from Program.cs move to named static methods. Route groups for `/api/v1` and `/v1` (requirement #5).
The `/v1/logs` endpoint now handles protobuf (requirement #13).

Structure:

```csharp
namespace Qyl.Collector.Hosting;

public static class CollectorEndpointExtensions
{
    public static WebApplication MapCollectorEndpoints(this WebApplication app)
    {
        app.MapGrpcService<TraceServiceImpl>();

        var otlp = app.MapGroup("/v1");
        otlp.MapPost("/traces", IngestOtlpTracesAsync);
        otlp.MapPost("/logs", IngestOtlpLogsAsync);

        var api = app.MapGroup("/api/v1");
        api.MapGet("/sessions", GetSessionsAsync);
        api.MapGet("/sessions/{sessionId}", GetSessionByIdAsync);
        api.MapGet("/sessions/{sessionId}/spans", SpanEndpoints.GetSessionSpansAsync);
        api.MapGet("/traces", GetTracesAsync);
        api.MapGet("/traces/{traceId}", SpanEndpoints.GetTraceAsync);
        api.MapGet("/logs", GetLogsAsync);
        api.MapGet("/logs/live", StreamLogsLiveAsync);
        api.MapGet("/genai/stats", GetGenAiStatsAsync);
        api.MapGet("/genai/spans", GetGenAiSpansAsync);
        api.MapPost("/ingest", IngestNativeAsync);
        api.MapPost("/console", IngestConsoleAsync);
        api.MapGet("/console", GetConsoleAsync);
        api.MapGet("/console/errors", GetConsoleErrorsAsync);
        api.MapGet("/console/live", StreamConsoleLiveAsync);
        api.MapDelete("/telemetry", ClearTelemetryAsync);
        api.MapGet("/telemetry/stats", GetTelemetryStatsAsync);
        api.MapGet("/meta", GetMeta);

        // Existing Map*Endpoints extension methods
        app.MapServiceEndpoints();
        app.MapSseEndpoints();
        app.MapSpanMemoryEndpoints();
        app.MapInsightsEndpoints();
        app.MapDashboardEndpoints();
        app.MapAnalyticsEndpoints();
        app.MapObserveEndpoints();
        app.MapQylHealthChecks();
        app.MapSchemaEndpoints();
        app.MapSearchEndpoints();
        app.MapSearchDocumentEndpoints();
        app.MapErrorEndpoints();
        app.MapIdentityEndpoints();
        app.MapGitHubEndpoints();
        app.MapProvisioningEndpoints();
        app.MapIssueEndpoints();
        app.MapIssueAnalyticsEndpoints();
        app.MapAnomalyEndpoints();
        app.MapAutofixEndpoints();
        app.MapRegressionEndpoints();
        app.MapAgentHandoffEndpoints();
        app.MapCodeReviewEndpoints();
        app.MapGitHubWebhookEndpoints();
        app.MapLoomEndpoints();
        app.MapTriageEndpoints();
        app.MapAgentRunEndpoints();
        app.MapAgentInsightsEndpoints();
        app.MapArtifactEndpoints();
        app.MapQueryEndpoints();
        app.MapLogSummaryEndpoints();

        var buildFailureCaptureEnabled = app.Configuration.GetValue("QYL_BUILD_FAILURE_CAPTURE_ENABLED", true);
        if (buildFailureCaptureEnabled)
            app.MapBuildFailureEndpoints();

        // Browser SDK script
        app.MapGet("/qyl.js", () =>
            Results.File(Path.Combine(app.Environment.WebRootPath, "qyl.js"), "application/javascript"));

        // SPA fallback
        app.MapFallback(FallbackHandler);

        return app;
    }

    // Each inline lambda becomes a named static method.
    // IngestOtlpLogsAsync now handles both protobuf and JSON:
    //
    // if (OtlpProtobufParser.IsProtobufContentType(contentType))
    //     logs = OtlpConverter.ConvertProtoLogsToStorageRows(
    //         await OtlpLogProtobufParser.ParseFromRequestAsync(request, ct));
    // else
    //     ... existing JSON path ...
}

internal static partial class OtlpLogsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process OTLP logs payload")]
    public static partial void FailedToProcessPayload(ILogger logger, Exception ex);
}
```

- [ ] **Step 2: Verify it compiles**

- [ ] **Step 3: Commit**

```
feat(collector): add CollectorEndpointExtensions with route groups
```

---

### Task 8: Rewrite `Program.cs`

**Files:**

- Modify: `src/qyl.collector/Program.cs` (full rewrite)

- [ ] **Step 1: Rewrite to thin entry point**

```csharp
using Qyl.Collector;
using Qyl.Collector.Hosting;
using Qyl.Instrumentation.Instrumentation;

Console.WriteLine($"[qyl] Process starting at {TimeProvider.System.GetUtcNow():O}");

var builder = WebApplication.CreateSlimBuilder(args);

var ports = CollectorPortOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(ports);

// Startup diagnostics
Console.WriteLine($"[qyl] Starting on port {ports.Http} (QYL_PORT={Environment.GetEnvironmentVariable("QYL_PORT")}, PORT={Environment.GetEnvironmentVariable("PORT")})");
Console.WriteLine($"[qyl] gRPC port: {ports.Grpc} (0=disabled)");
Console.WriteLine($"[qyl] OTLP HTTP port: {ports.OtlpHttp} (0=disabled, falls back to {ports.Http})");

builder
    .ConfigureCollectorKestrel(ports)
    .AddCollectorServices(ports);

var app = builder.Build();

await app.InitializeCollectorAsync().ConfigureAwait(false);
app.UseCollectorMiddleware();
app.MapCollectorEndpoints();

var otlpCorsOptions = app.Services.GetRequiredService<OtlpCorsOptions>();
var otlpApiKeyOptions = app.Services.GetRequiredService<OtlpApiKeyOptions>();
StartupBanner.Print($"http://localhost:{ports.Http}", ports.Http, ports.Grpc, ports.OtlpHttp, otlpCorsOptions, otlpApiKeyOptions);

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"[qyl] Application started and listening on port {ports.Http}"));

await app.RunAsync().ConfigureAwait(false);
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj --no-restore -v:q 2>&1 | tail -5`

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/qyl.collector.tests/qyl.collector.tests.csproj --no-restore -v:q 2>&1 | tail -10`

- [ ] **Step 4: Commit**

```
refactor(collector): thin Program.cs with modular hosting extensions

BREAKING: OTLP auth defaults to ApiKey in non-Development environments.
Set QYL_OTLP_AUTH_MODE=Unsecured to opt out.
```

---

### Task 9: Verify end-to-end

- [ ] **Step 1: Build entire solution**

Run: `dotnet build qyl.sln --no-restore -v:q 2>&1 | tail -5`

- [ ] **Step 2: Run collector tests**

Run: `dotnet test tests/qyl.collector.tests/ -v:q 2>&1 | tail -10`

- [ ] **Step 3: Verify no compiler warnings**

Run: `dotnet build src/qyl.collector/ -warnaserror 2>&1 | tail -10`

---

## Verification Checklist

- [ ] Program.cs is ≤35 lines of application code
- [ ] All existing endpoints still mapped (no behavioral changes except auth default)
- [ ] `/v1/logs` accepts both `application/x-protobuf` and `application/json`
- [ ] `/v1/traces` continues to accept both formats
- [ ] Non-Development environments default to ApiKey auth mode
- [ ] Startup throws if ApiKey mode is active with no keys
- [ ] No sync-over-async (`GetAwaiter().GetResult()`) in startup
- [ ] Route groups used for `/api/v1` and `/v1`
- [ ] `CollectorPortOptions` injected as singleton
- [ ] StartupBanner still prints
- [ ] Tests pass
