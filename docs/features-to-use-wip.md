# Features to Use - WIP

> Gesammelte Code-Snippets und Patterns für qyl
> Stand: 2025-12-15

---

## 1. HTTP Logging + Redaction (Microsoft.Extensions.Compliance)

### Packages

```
Microsoft.Extensions.Http.Diagnostics
Microsoft.Extensions.Telemetry
Microsoft.Extensions.Compliance.Abstractions
Microsoft.Extensions.Compliance.Redaction
Microsoft.AspNetCore.Diagnostics.Middleware
```

### QylDataClasses Taxonomy

```csharp
public static class QylDataClasses
{
    public static readonly DataClassification Secret = new("qyl", "secret");
    public static readonly DataClassification Pii    = new("qyl", "pii");
    public static readonly DataClassification UserId = new("qyl", "id.user");
    public static readonly DataClassification Tenant = new("qyl", "id.tenant");
}
```

**Taxonomy-Mapping:**

- `qyl.secret` → Erase (Tokens, Authorization, Cookies, API Keys)
- `qyl.pii` → HMAC (E-Mail, Phone, Names; korrelierbar ohne Klartext)
- `qyl.id.user` / `qyl.id.tenant` → HMAC (IDs sind "PII-adjacent", super für Korrelation)
- `qyl.meta` → none/allow (harmlos, low risk)

### Redaction Setup

```csharp
builder.Services.AddRedaction(r =>
{
    // Secrets: immer weg
    r.SetRedactor<ErasingRedactor>(QylDataClasses.Secret);

    // PII/IDs: pseudonymisieren
    r.SetHmacRedactor(builder.Configuration.GetSection("Redaction:Hmac"),
        QylDataClasses.Pii, QylDataClasses.UserId, QylDataClasses.Tenant);

    // Fallback: sicherer Default
    r.SetFallbackRedactor<ErasingRedactor>();
});
```

### Incoming Requests (ASP.NET Core)

```csharp
builder.Services.AddHttpLoggingRedaction(o =>
{
    o.RequestHeadersDataClasses["Authorization"] = QylDataClasses.Secret;
    o.RequestHeadersDataClasses["Cookie"]        = QylDataClasses.Secret;

    o.RouteParameterDataClasses["tenantId"] = QylDataClasses.Tenant;
    o.RouteParameterDataClasses["userId"]   = QylDataClasses.UserId;
});

// In pipeline:
app.UseHttpLogging();
```

### Outgoing Requests (HttpClient)

```csharp
builder.Services.AddLatencyContext();
builder.Services.AddHttpClientLatencyTelemetry(); // optional, aber nice

builder.Services.AddHttpClient("default")
    .AddExtendedHttpClientLogging(o =>
    {
        // Path: lieber "structured" (Route params separat)
        o.RequestPathLoggingMode = OutgoingPathLoggingMode.Structured;

        // Route params: STRICT = alles ist sensibel, außer du klassifizierst explizit
        o.RequestPathParameterRedactionMode = HttpRouteParameterRedactionMode.Strict;
        o.RouteParameterDataClasses["tenantId"] = QylDataClasses.Tenant;
        o.RouteParameterDataClasses["userId"]   = QylDataClasses.UserId;

        // Headers (nur was du hier einträgst wird geloggt – mit Redaction)
        o.RequestHeadersDataClasses["Authorization"] = QylDataClasses.Secret;
        o.RequestHeadersDataClasses["X-Api-Key"]     = QylDataClasses.Secret;

        // Query params
        o.RequestQueryParametersDataClasses["api_key"] = QylDataClasses.Secret;
        o.RequestQueryParametersDataClasses["userId"]  = QylDataClasses.UserId;
    });
```

### Architektur-Note

⚠️ `qyl.protocol` ist LEAF/BCL-only → `DataClassification` gehört NICHT dorthin!

**Platzierung:**

```
src/qyl.collector/Compliance/QylDataClasses.cs
src/qyl.collector/Compliance/QylRedactionConfig.cs
```

---

## 2. Telemetry Enrichment + Redaction + Complex Object Logging

### Packages

```
Microsoft.Extensions.Telemetry                    # 10.1.0
Microsoft.Extensions.Telemetry.Abstractions       # 10.1.0
Microsoft.Extensions.Compliance.Abstractions      # 10.1.0
Microsoft.Extensions.Compliance.Redaction         # 10.1.0
Microsoft.AspNetCore.Diagnostics.Middleware       # 10.1.0
```

### Wer macht was?

| Package                                       | Features                                                     |
|-----------------------------------------------|--------------------------------------------------------------|
| `Microsoft.Extensions.Telemetry`              | Enrichment, Sampling, Latency                                |
| `Microsoft.Extensions.Telemetry.Abstractions` | `[LogProperties]`, `[TagName]`, erweiterter Source Generator |
| `Microsoft.Extensions.Compliance.*`           | DataClassification, Redactors                                |
| `Microsoft.AspNetCore.Diagnostics.Middleware` | HTTP Logging, per-request Buffer                             |

### DI Setup

```csharp
builder.Services.AddRedaction(); // Grundregistrierung

builder.Logging.EnableEnrichment();                // Log enrichment aktivieren
builder.Services.AddServiceLogEnricher(o =>
{
    o.ApplicationName = true;   // service.name
    o.EnvironmentName = true;   // deployment.environment  
    o.BuildVersion = true;      // service.version
});
// ⚠️ NOTE: AddServiceLogEnricher is OBSOLETE in .NET 10!
// Use AddApplicationLogEnricher() instead (same options)
builder.Logging.EnableRedaction();                 // Redaction in Logs

// Optional: Probabilistic Sampling (10% der Logs)
// builder.Logging.AddRandomProbabilisticSampler(0.1);
```

### Complex Object Logging + Stable Tag Keys

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Telemetry.Logging;

internal static partial class QylLog
{
    [LoggerMessage(EventId = 11001, Level = LogLevel.Information, Message = "Span ingested")]
    public static partial void SpanIngested(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] in SpanRecord span,   // Alle Props als Tags
        [TagName("qyl.session.id")] string sessionId);                  // Stabiler Tag-Name
}
```

**Features:**

- `[LogProperties]` → Alle Properties eines Objekts als strukturierte Log-Tags
- `[LogProperties(OmitReferenceName = true)]` → Ohne Prefix (nicht `span.TraceId` sondern `TraceId`)
- `[TagName("...")]` → Expliziter stabiler Tag-Name (OTel-freundlich)
- `EventId` → Explizite ID für Filtering/Alerting

### EventId Convention für qyl

| Range       | Bereich      |
|-------------|--------------|
| 11000-11099 | Ingestion    |
| 11100-11199 | Storage      |
| 11200-11299 | Query        |
| 11300-11399 | SSE/Realtime |
| 11400-11499 | MCP Tools    |

---

## 3. Source-Generated Metrics with Strongly-Typed Tags (.NET 10.2+)

### Package

```
Microsoft.Extensions.Telemetry.Abstractions  # 10.1.0+
```

### Warum?

- **Compile-time safety** für Tag-Namen
- **Kein Boilerplate** - Generator erzeugt alles
- **Shared Tags** über mehrere Metrics hinweg
- **Performance** - Structs vermeiden Heap-Allocations

### Example 1: Basic Counter with Single Tag

```csharp
public struct RequestTags
{
    public string Region { get; set; }
}

public static partial class QylMetrics
{
    [Counter<int>(typeof(RequestTags))]
    public static partial RequestCount CreateRequestCount(Meter meter);
}
```

**Usage:**

```csharp
Meter meter = new("qyl.collector", "1.0");
RequestCount requestCountMetric = QylMetrics.CreateRequestCount(meter);

var tags = new RequestTags { Region = "eu-west-1" };
requestCountMetric.Add(1, tags);
```

### Example 2: Nested Tags + Custom Tag Names

```csharp
public class SpanMetricTags : BaseMetricTags
{
    [TagName("gen_ai.provider.name")]
    public string? Provider;                          // Custom OTel-konformer Name
    
    public string? Operation { get; set; }            // Default: "Operation"
    public SpanChildTags? Details { get; set; }
}

public class BaseMetricTags
{
    [TagName("service.name")]
    public string? ServiceName { get; set; }
}

public class SpanChildTags
{
    public string? Model { get; set; }                // Default: "Model"
}

public static partial class QylMetrics
{
    [Histogram<long>(typeof(SpanMetricTags), Unit = "ms")]
    public static partial SpanLatency CreateSpanLatency(Meter meter);

    [Counter<long>(typeof(SpanMetricTags), Unit = "tokens")]
    public static partial TokenUsage CreateTokenUsage(Meter meter);

    [Counter<int>(typeof(SpanMetricTags), Unit = "spans")]
    public static partial SpanCount CreateSpanCount(Meter meter);
}
```

**Usage:**

```csharp
internal class IngestionMetrics
{
    private readonly SpanLatency _latency;
    private readonly TokenUsage _tokens;
    private readonly SpanCount _count;

    public IngestionMetrics(Meter meter)
    {
        _latency = QylMetrics.CreateSpanLatency(meter);
        _tokens = QylMetrics.CreateTokenUsage(meter);
        _count = QylMetrics.CreateSpanCount(meter);
    }

    public void RecordSpan(SpanRecord span, TimeSpan elapsed)
    {
        var tags = new SpanMetricTags
        {
            ServiceName = span.ServiceName,
            Provider = span.GenAiData?.ProviderName,
            Operation = span.GenAiData?.OperationName,
            Details = new SpanChildTags
            {
                Model = span.GenAiData?.RequestModel
            }
        };

        _latency.Record(elapsed.Milliseconds, tags);
        _count.Add(1, tags);
        
        if (span.GenAiData?.InputTokens is { } inputTokens)
        {
            _tokens.Add(inputTokens, tags);
        }
    }
}
```

### Units (ab .NET 10.2)

```csharp
[Histogram<long>(typeof(SpanMetricTags), Unit = "ms")]      // Milliseconds
[Counter<long>(typeof(SpanMetricTags), Unit = "tokens")]    // Token count
[Counter<int>(typeof(SpanMetricTags), Unit = "requests")]   // Request count
[Counter<int>(typeof(SpanMetricTags), Unit = "By")]         // Bytes (UCUM)
```

### Generator Requirements

| Requirement      | Details                   |
|------------------|---------------------------|
| Method signature | `public static partial`   |
| Return type      | Unique per method         |
| First param      | Must be `Meter`           |
| Tag properties   | Only `string` or `enum`   |
| No generics      | Methods cannot be generic |

### Performance Tip

```csharp
// ✅ Struct = keine Heap-Allocation bei high-frequency metrics
public struct HighFrequencyTags
{
    public string Region { get; set; }
    public string Operation { get; set; }
}

// ❌ Class = Heap-Allocation pro Recording
public class HighFrequencyTags { ... }
```

---

## 4. Health Check Telemetry Publisher

### Package

```
Microsoft.Extensions.Diagnostics.HealthChecks  # 10.0.0
```

### Setup

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DuckDbHealthCheck>("duckdb")
    .AddCheck<OtlpHealthCheck>("otlp_receiver");

// Telemetry als Metrics publishen
builder.Services.AddTelemetryHealthCheckPublisher();
```

### Emitted Metrics

| Metric                                 | Type    | Unit                | Description            |
|----------------------------------------|---------|---------------------|------------------------|
| `dotnet.health_check.reports`          | Counter | `{report}`          | Health status reported |
| `dotnet.health_check.unhealthy_checks` | Counter | `{unhealthy_check}` | Unhealthy checks       |

**Tags:**

- `dotnet.health_check.status` → `Healthy`, `Degraded`, `Unhealthy`
- `dotnet.health_check.name` → Check name (z.B. `duckdb`)

### Health Check Varianten

```csharp
builder.Services.AddHealthChecks()
    // Typed check (DI-aware)
    .AddCheck<DuckDbHealthCheck>("duckdb", 
        failureStatus: HealthStatus.Degraded,
        tags: ["database", "critical"],
        timeout: TimeSpan.FromSeconds(5))
    
    // Type-activated mit Constructor args
    .AddTypeActivatedCheck<ConnectionStringHealthCheck>("postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database"],
        args: new object[] { "Host=localhost;Database=qyl" })
    
    // Inline async check
    .AddAsyncCheck("external_api", async ct =>
    {
        var response = await httpClient.GetAsync("/health", ct);
        return response.IsSuccessStatusCode 
            ? HealthCheckResult.Healthy() 
            : HealthCheckResult.Degraded("API slow");
    }, tags: ["external"])
    
    // Application Lifecycle (ready/live)
    .AddApplicationLifecycleHealthCheck(tags: ["ready"])
    
    // Manual control (für graceful shutdown)
    .AddManualHealthCheck(tags: ["live"])
    
    // Resource Utilization (CPU/Memory thresholds)
    .AddResourceUtilizationHealthCheck(o =>
    {
        o.CpuThresholds = new ResourceUsageThresholds
        {
            DegradedUtilizationPercentage = 80,
            UnhealthyUtilizationPercentage = 95
        };
        o.MemoryThresholds = new ResourceUsageThresholds
        {
            DegradedUtilizationPercentage = 85,
            UnhealthyUtilizationPercentage = 95
        };
    }, tags: ["resources"])
    
    // Oder via Config
    .AddResourceUtilizationHealthCheck(
        config.GetSection("HealthChecks:Resources"), 
        tags: ["resources", "live"])
    
    // Oder einfach mit Tags (default thresholds)
    .AddResourceUtilizationHealthCheck("resources", "live");
```

**appsettings.json:**

```json
{
  "HealthChecks": {
    "Resources": {
      "CpuThresholds": {
        "DegradedUtilizationPercentage": 80,
        "UnhealthyUtilizationPercentage": 95
      },
      "MemoryThresholds": {
        "DegradedUtilizationPercentage": 85,
        "UnhealthyUtilizationPercentage": 95
      }
    }
  }
}
```

### Für qyl.collector

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DuckDbHealthCheck>("duckdb", tags: ["database", "ready"])
    .AddResourceUtilizationHealthCheck(tags: ["resources", "live"])
    .AddApplicationLifecycleHealthCheck(tags: ["ready"]);

builder.Services.AddTelemetryHealthCheckPublisher();

// Endpoints
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new() { Predicate = c => c.Tags.Contains("live") });
```

---

## 5. Resource Monitoring Metrics (Container-aware)

### Package

```
Microsoft.Extensions.Diagnostics.ResourceMonitoring  # 8.8.0+
```

### Setup

```csharp
builder.Services.AddResourceMonitoring();
```

### Emitted Metrics

| Metric                                      | Type          | Description                  |
|---------------------------------------------|---------------|------------------------------|
| `container.cpu.limit.utilization`           | Gauge         | CPU vs limit [0,1]           |
| `container.cpu.request.utilization`         | Gauge         | CPU vs request [0,1] (Linux) |
| `container.cpu.time`                        | Counter       | CPU time (seconds)           |
| `container.memory.limit.utilization`        | Gauge         | Memory vs limit [0,1]        |
| `container.memory.usage`                    | UpDownCounter | Memory bytes                 |
| `process.cpu.utilization`                   | Gauge         | Process CPU [0,1]            |
| `dotnet.process.memory.virtual.utilization` | Gauge         | Memory [0,1]                 |
| `system.network.connections`                | UpDownCounter | Network connections by state |

**Ideal für qyl.collector im Container!**

---

## 6. AsyncState + Auto-Activation

### Packages

```
Microsoft.Extensions.AsyncState                              # 10.0.0
Microsoft.Extensions.DependencyInjection.AutoActivation      # 10.0.0
```

### AsyncState - Scoped State über async Boundaries

```csharp
builder.Services.AddAsyncState();
```

**Was es macht:**

- `IAsyncState` / `IAsyncContext<T>` - State der über `await` hinweg erhalten bleibt
- Ähnlich wie `AsyncLocal<T>` aber mit DI-Integration
- Perfekt für Request-scoped Context in async Code

```csharp
public class IngestionContext
{
    public string? SessionId { get; set; }
    public string? ServiceName { get; set; }
}

// In DI:
builder.Services.AddAsyncState();

// Usage:
public class SpanProcessor
{
    private readonly IAsyncContext<IngestionContext> _context;
    
    public SpanProcessor(IAsyncContext<IngestionContext> context)
    {
        _context = context;
    }
    
    public async Task ProcessAsync(SpanRecord span)
    {
        _context.Set(new IngestionContext 
        { 
            SessionId = span.SessionId,
            ServiceName = span.ServiceName 
        });
        
        await DoWorkAsync(); // Context bleibt erhalten!
    }
}
```

### Auto-Activation - Singletons beim Start initialisieren

```csharp
// Statt lazy activation beim ersten Request...
builder.Services.AddSingleton<DuckDbStore>();

// ...sofort beim App-Start aktivieren:
builder.Services.AddActivatedSingleton<DuckDbStore>();

// Oder nachträglich markieren:
builder.Services.AddSingleton<DuckDbStore>();
builder.Services.ActivateSingleton<DuckDbStore>();
```

**Warum?**

- **Cold-Start vermeiden** - DB-Connection schon beim Start
- **Fail-fast** - Fehler sofort beim Start statt beim ersten Request
- **Predictable Latency** - Kein "first request is slow"

**Für qyl.collector ideal:**

```csharp
// DuckDB sofort beim Start öffnen
builder.Services.AddActivatedSingleton<DuckDbStore>();

// Channel für SSE Broadcasting sofort starten
builder.Services.AddActivatedSingleton<SpanBroadcaster>();

// Schema-Migration beim Start
builder.Services.AddActivatedSingleton<DuckDbMigrator>();
```

### Keyed Services + Auto-Activation

```csharp
builder.Services.AddActivatedKeyedSingleton<ISpanStore, DuckDbStore>("duckdb");
builder.Services.AddActivatedKeyedSingleton<ISpanStore, InMemoryStore>("memory");
```

---

## 7. Microsoft.Extensions.AI Integration

### Package

```
Microsoft.Extensions.AI  # 10.0.0
```

### ChatClient DI Registration

```csharp
// Direct instance
builder.Services.AddChatClient(new OpenAIChatClient(...));

// Factory pattern
builder.Services.AddChatClient(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new OpenAIChatClient(config["OpenAI:ApiKey"]!);
});

// Keyed services (multiple providers)
builder.Services.AddKeyedChatClient("openai", new OpenAIChatClient(...));
builder.Services.AddKeyedChatClient("anthropic", new AnthropicChatClient(...));
```

### EmbeddingGenerator DI Registration

```csharp
builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new OpenAIEmbeddingGenerator(config["OpenAI:ApiKey"]!);
});
```

**Returns `EmbeddingGeneratorBuilder<TInput, TEmbedding>`** für Pipeline-Konfiguration!

### Für qyl.mcp (AI Agent Tools)

```csharp
// In qyl.mcp/Program.cs
builder.Services.AddKeyedChatClient("analysis", sp =>
{
    var client = new OpenAIChatClient(config["OpenAI:ApiKey"]!);
    return new ChatClientBuilder(client)
        .UseLogging()
        .UseFunctionInvocation()
        .Build();
});
```

---

## 8. Custom Log Enrichers

### Package

```
Microsoft.Extensions.Telemetry  # 10.0.0
```

### ILogEnricher Interface

```csharp
public class QylLogEnricher : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add("qyl.version", "1.0.0");
        collector.Add("qyl.instance", Environment.MachineName);
    }
}
```

### Registration

```csharp
// Generic registration
builder.Services.AddLogEnricher<QylLogEnricher>();

// Instance registration
builder.Services.AddLogEnricher(new QylLogEnricher());
```

### Beispiel: Span-Context Enricher

```csharp
public class SpanContextEnricher : ILogEnricher
{
    private readonly IAsyncContext<IngestionContext> _context;
    
    public SpanContextEnricher(IAsyncContext<IngestionContext> context)
    {
        _context = context;
    }
    
    public void Enrich(IEnrichmentTagCollector collector)
    {
        var ctx = _context.Get();
        if (ctx is not null)
        {
            collector.Add("qyl.session.id", ctx.SessionId ?? "unknown");
            collector.Add("qyl.service.name", ctx.ServiceName ?? "unknown");
        }
    }
}

// Registration
builder.Services.AddAsyncState();
builder.Services.AddLogEnricher<SpanContextEnricher>();
```

**Kombiniert mit AsyncState = Request-scoped Tags in allen Logs!**

---

## 9. Exception Summarization

### Package

```
Microsoft.Extensions.Diagnostics.ExceptionSummarization  # 10.0.0
```

### Was es macht

Erzeugt **sichere, redacted Exception-Summaries** für Logs/Telemetry - keine Stack Traces mit sensiblen Daten!

### Setup

```csharp
builder.Services.AddExceptionSummarizer();

// Mit custom Summarizers:
builder.Services.AddExceptionSummarizer(b =>
{
    b.AddHttpProvider();      // HTTP exceptions
    b.AddSocketsProvider();   // Socket exceptions
});
```

### Usage

```csharp
public class SpanProcessor
{
    private readonly IExceptionSummarizer _summarizer;
    private readonly ILogger<SpanProcessor> _logger;
    
    public SpanProcessor(IExceptionSummarizer summarizer, ILogger<SpanProcessor> logger)
    {
        _summarizer = summarizer;
        _logger = logger;
    }
    
    public async Task ProcessAsync(SpanRecord span)
    {
        try
        {
            await StoreAsync(span);
        }
        catch (Exception ex)
        {
            // Sichere Summary statt voller Exception
            var summary = _summarizer.Summarize(ex);
            _logger.LogError("Failed to store span: {Summary}", summary.Description);
            
            // summary.ExceptionType = "DuckDBException"
            // summary.Description = "Database error" (keine Connection Strings!)
        }
    }
}
```

### Warum?

- **Keine sensiblen Daten in Logs** - Connection Strings, Credentials, etc.
- **Konsistente Exception-Kategorisierung**
- **OTel-ready** - Summary passt in Span-Attributes

---

## 10. Testing Utilities

### Packages

```
Microsoft.Extensions.Compliance.Testing      # 10.0.0 - Fake Redaction
Microsoft.Extensions.Diagnostics.Testing     # 10.0.0 - Fake TimeProvider, etc.
Microsoft.Extensions.TimeProvider.Testing    # 10.0.0 - FakeTimeProvider
```

### Fake Redaction (für Tests)

```csharp
// In Test Setup - ersetzt echte Redaction mit Fakes
services.AddFakeRedaction();

// Mit Options:
services.AddFakeRedaction(o =>
{
    o.RedactionFormat = "<REDACTED:{0}>";  // Sichtbar was redacted wurde
});
```

**Test-Beispiel:**

```csharp
public class RedactionTests
{
    [Fact]
    public void ShouldRedactSecrets()
    {
        var services = new ServiceCollection();
        services.AddRedaction(r => r.SetRedactor<ErasingRedactor>(QylDataClasses.Secret));
        
        // Für Tests: Fake Redaction die sichtbar macht WAS redacted wurde
        services.AddFakeRedaction(o => o.RedactionFormat = "[REDACTED:{0}]");
        
        var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactorProvider>();
        
        var result = redactor.GetRedactor(QylDataClasses.Secret).Redact("my-api-key");
        Assert.Equal("[REDACTED:qyl.secret]", result);
    }
}
```

### FakeTimeProvider (für Time-abhängige Tests)

```csharp
public class LatencyTests
{
    [Fact]
    public async Task ShouldMeasureLatency()
    {
        var fakeTime = new FakeTimeProvider();
        
        var sut = new LatencyTracker(fakeTime);
        
        sut.Start();
        fakeTime.Advance(TimeSpan.FromMilliseconds(150));
        var elapsed = sut.Stop();
        
        Assert.Equal(150, elapsed.TotalMilliseconds);
    }
}
```

**Kombiniert mit `TimeProvider.System` im Produktionscode:**

```csharp
public class LatencyTracker
{
    private readonly TimeProvider _timeProvider;
    private long _startTimestamp;
    
    public LatencyTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
    
    public void Start() => _startTimestamp = _timeProvider.GetTimestamp();
    public TimeSpan Stop() => _timeProvider.GetElapsedTime(_startTimestamp);
}
```

---

## 11. HttpClient Diagnostics (Latency + Extended Logging)

### Package

```
Microsoft.Extensions.Http.Diagnostics  # 10.0.0
```

### Global HttpClient Latency Telemetry

```csharp
// Für ALLE HttpClients
builder.Services.AddHttpClientLatencyTelemetry();

// Mit Options:
builder.Services.AddHttpClientLatencyTelemetry(o =>
{
    o.EnableDetailedLatencyBreakdown = true;
});
```

### Extended HttpClient Logging

**Global (alle Clients):**

```csharp
builder.Services.AddExtendedHttpClientLogging(o =>
{
    o.RequestPathLoggingMode = OutgoingPathLoggingMode.Structured;
    o.LogRequestStart = true;
    o.LogBody = true;
    o.BodySizeLimit = 32 * 1024; // 32KB
});
```

**Per-Client:**

```csharp
builder.Services.AddHttpClient("qyl-collector")
    .AddExtendedHttpClientLogging(o =>
    {
        o.RequestPathLoggingMode = OutgoingPathLoggingMode.Structured;
        o.RequestPathParameterRedactionMode = HttpRouteParameterRedactionMode.Strict;
        
        // Route params redaction
        o.RouteParameterDataClasses["sessionId"] = QylDataClasses.UserId;
        
        // Header redaction
        o.RequestHeadersDataClasses["Authorization"] = QylDataClasses.Secret;
        o.RequestHeadersDataClasses["X-Api-Key"] = QylDataClasses.Secret;
        
        // Query param redaction
        o.RequestQueryParametersDataClasses["api_key"] = QylDataClasses.Secret;
    });
```

### Custom HttpClient Log Enricher

```csharp
public class QylHttpClientLogEnricher : IHttpClientLogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector, 
        HttpRequestMessage request, 
        HttpResponseMessage? response, 
        Exception? exception)
    {
        collector.Add("qyl.request.id", request.Headers.GetValues("X-Request-Id").FirstOrDefault());
        
        if (response is not null)
        {
            collector.Add("qyl.response.size", response.Content.Headers.ContentLength ?? 0);
        }
    }
}

// Registration
builder.Services.AddHttpClientLogEnricher<QylHttpClientLogEnricher>();
```

### Downstream Dependency Metadata

```csharp
public class CollectorDependencyMetadata : IDownstreamDependencyMetadata
{
    public string DependencyName => "qyl-collector";
    public ISet<string> UniqueHostNameSuffixes => new HashSet<string> { "collector.qyl.local" };
    public ISet<RequestMetadata> RequestMetadata => new HashSet<RequestMetadata>
    {
        new("/api/v1/spans", "GET", "GetSpans"),
        new("/api/v1/sessions", "GET", "GetSessions"),
        new("/v1/traces", "POST", "IngestTraces")
    };
}

// Registration
builder.Services.AddDownstreamDependencyMetadata<CollectorDependencyMetadata>();
```

### Für qyl.mcp (HTTP zu Collector)

```csharp
// In qyl.mcp/Program.cs
builder.Services.AddLatencyContext();
builder.Services.AddHttpClientLatencyTelemetry();

builder.Services.AddHttpClient("collector", client =>
{
    client.BaseAddress = new Uri(config["Collector:BaseUrl"]!);
})
.AddExtendedHttpClientLogging(o =>
{
    o.RequestPathLoggingMode = OutgoingPathLoggingMode.Structured;
    o.LogBody = false; // Spans können groß sein
});

builder.Services.AddDownstreamDependencyMetadata<CollectorDependencyMetadata>();
```

---

## 12. Latency Context (Detailed Performance Tracking)

### Package

```
Microsoft.Extensions.Telemetry              # 10.0.0
Microsoft.Extensions.Telemetry.Abstractions # 10.0.0
```

### Was es macht

Strukturiertes Performance-Tracking mit:

- **Checkpoints** - Zeitpunkte markieren (z.B. "after_validation", "before_db")
- **Measures** - Dauern messen (z.B. "db_query_time", "serialization_time")
- **Tags** - Kontext-Tags (z.B. "operation_type", "batch_size")

### Setup

```csharp
builder.Services.AddLatencyContext(o =>
{
    o.ThrowOnUnregisteredNames = true; // Fail-fast bei Typos
});

// Checkpoint/Measure/Tag Namen registrieren (compile-time safety)
builder.Services.RegisterCheckpointNames(
    "request_received",
    "validation_complete", 
    "db_query_start",
    "db_query_end",
    "response_sent"
);

builder.Services.RegisterMeasureNames(
    "validation_duration",
    "db_duration",
    "serialization_duration"
);

builder.Services.RegisterTagNames(
    "operation_type",
    "batch_size",
    "cache_hit"
);
```

### Usage

```csharp
public class SpanIngestionService
{
    private readonly ILatencyContextProvider _latencyProvider;
    
    public SpanIngestionService(ILatencyContextProvider latencyProvider)
    {
        _latencyProvider = latencyProvider;
    }
    
    public async Task IngestAsync(SpanRecord[] spans)
    {
        var ctx = _latencyProvider.CreateContext();
        
        // Checkpoint: Request empfangen
        ctx.AddCheckpoint("request_received");
        
        // Tag setzen
        ctx.SetTag("batch_size", spans.Length.ToString());
        
        // Validation messen
        var validationStart = TimeProvider.System.GetTimestamp();
        ValidateSpans(spans);
        ctx.RecordMeasure("validation_duration", 
            TimeProvider.System.GetElapsedTime(validationStart));
        ctx.AddCheckpoint("validation_complete");
        
        // DB Operation messen
        ctx.AddCheckpoint("db_query_start");
        var dbStart = TimeProvider.System.GetTimestamp();
        await _store.InsertAsync(spans);
        ctx.RecordMeasure("db_duration",
            TimeProvider.System.GetElapsedTime(dbStart));
        ctx.AddCheckpoint("db_query_end");
        
        ctx.AddCheckpoint("response_sent");
        
        // Context wird automatisch zu Logs/Traces hinzugefügt
    }
}
```

### Für qyl.collector

```csharp
// In Program.cs
builder.Services.AddLatencyContext();

builder.Services.RegisterCheckpointNames(
    "otlp_received",
    "genai_extracted",
    "spans_converted",
    "db_inserted",
    "sse_broadcast"
);

builder.Services.RegisterMeasureNames(
    "extraction_duration",
    "conversion_duration", 
    "db_insert_duration",
    "total_ingestion_duration"
);

builder.Services.RegisterTagNames(
    "service_name",
    "span_count",
    "has_genai_data"
);
```

### Kombination mit HttpClient Latency

```csharp
// HttpClient Latency nutzt denselben LatencyContext
builder.Services.AddLatencyContext();
builder.Services.AddHttpClientLatencyTelemetry();

// Jetzt werden HttpClient-Calls automatisch im LatencyContext erfasst!
```

### No-Op für Tests/Deaktivierung

```csharp
// In Tests oder wenn Latency-Tracking nicht gewünscht
builder.Services.AddNullLatencyContext();

// Alle ILatencyContextProvider Calls werden zu No-Ops
// Kein Overhead, keine Daten gesammelt
```

**Use Cases:**

- Unit Tests ohne Latency-Overhead
- Feature-Toggle für Produktion
- Einfache Deployments ohne Telemetry

---

## 13. Object Pooling mit DI Integration

### Package

```
Microsoft.Extensions.ObjectPool.DependencyInjection  # 10.0.0
```

### Was es macht

- Object Pooling direkt in DI integriert
- Scoped instances aus Pool statt neue Allokationen
- Perfekt für high-throughput, kurzlebige Objekte

### Setup

```csharp
// Einfach: Service wird gepoolt
builder.Services.AddPooled<SpanConverter>();

// Mit Interface + Implementation
builder.Services.AddPooled<ISpanConverter, SpanConverter>();

// Mit Options
builder.Services.AddPooled<SpanConverter>(o =>
{
    o.Capacity = 256;  // Default: 1024
});

// Pool-Konfiguration nachträglich
builder.Services.ConfigurePool<SpanConverter>(o =>
{
    o.Capacity = 512;
});

// Globale Pool-Konfiguration via Config
builder.Services.ConfigurePools(config.GetSection("ObjectPools"));
```

### Usage

```csharp
public class OtlpReceiver
{
    private readonly ObjectPool<SpanConverter> _converterPool;
    
    public OtlpReceiver(ObjectPool<SpanConverter> converterPool)
    {
        _converterPool = converterPool;
    }
    
    public async Task<SpanRecord[]> ProcessAsync(ExportTraceServiceRequest request)
    {
        // Converter aus Pool holen
        var converter = _converterPool.Get();
        try
        {
            return converter.Convert(request);
        }
        finally
        {
            // Zurück in Pool
            _converterPool.Return(converter);
        }
    }
}
```

### Für qyl.collector (High-Throughput OTLP Ingestion)

```csharp
// SpanConverter wird häufig gebraucht, kurzlebig
builder.Services.AddPooled<SpanConverter>(o => o.Capacity = 256);

// GenAiExtractor für jeden Span
builder.Services.AddPooled<GenAiExtractor>(o => o.Capacity = 256);

// StringBuilder für JSON Serialization
builder.Services.AddPooled<StringBuilder>(o => o.Capacity = 128);
```

### Wann verwenden?

| Szenario                  | Pool?  | Warum                        |
|---------------------------|--------|------------------------------|
| High-frequency, kurzlebig | ✅ Ja   | Weniger GC Pressure          |
| Teure Initialisierung     | ✅ Ja   | Einmal erstellen, oft nutzen |
| Stateful über Request     | ❌ Nein | State-Konflikte              |
| Singleton-Services        | ❌ Nein | Nur eine Instanz nötig       |
| Sehr kleine Objekte       | ❌ Nein | Pool-Overhead > Allokation   |

### Kombination mit IResettable

```csharp
public class SpanConverter : IResettable
{
    private List<SpanRecord> _buffer = new();
    
    public SpanRecord[] Convert(ExportTraceServiceRequest request)
    {
        // ... conversion logic using _buffer
    }
    
    // Wird aufgerufen bevor Objekt zurück in Pool geht
    public bool TryReset()
    {
        _buffer.Clear();
        return true; // true = kann wiederverwendet werden
    }
}
```

---

## 14. HTTP Resilience (Polly Integration)

### Package

```
Microsoft.Extensions.Http.Resilience  # 10.0.0
Microsoft.Extensions.Resilience       # 10.0.0
```

### Standard Resilience Handler (empfohlen)

One-liner mit Best-Practice Defaults:

```csharp
builder.Services.AddHttpClient("collector")
    .AddStandardResilienceHandler();
```

**Was ist drin?**

- Rate Limiter
- Total Request Timeout
- Retry (mit Backoff)
- Circuit Breaker
- Attempt Timeout

### Mit Custom Options

```csharp
builder.Services.AddHttpClient("collector")
    .AddStandardResilienceHandler(o =>
    {
        // Retry
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.Delay = TimeSpan.FromMilliseconds(200);
        o.Retry.BackoffType = DelayBackoffType.Exponential;
        
        // Circuit Breaker
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.MinimumThroughput = 10;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
        
        // Timeouts
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    });

// Oder via Config
builder.Services.AddHttpClient("collector")
    .AddStandardResilienceHandler(config.GetSection("HttpResilience:Collector"));
```

### Custom Resilience Pipeline

```csharp
builder.Services.AddHttpClient("collector")
    .AddResilienceHandler("custom-pipeline", pipeline =>
    {
        pipeline
            .AddTimeout(TimeSpan.FromSeconds(10))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode is HttpStatusCode.ServiceUnavailable 
                    or HttpStatusCode.TooManyRequests)
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(5)
            });
    });
```

### Hedging (Parallel Requests)

Für kritische Calls - schickt parallel Requests und nimmt ersten erfolgreichen:

```csharp
builder.Services.AddHttpClient("collector")
    .AddStandardHedgingHandler(routing =>
    {
        routing.ConfigureOrderedGroups(groups =>
        {
            groups.Add(new UriEndpointGroup
            {
                Endpoints = 
                {
                    new() { Uri = new Uri("https://collector-1.qyl.local") },
                    new() { Uri = new Uri("https://collector-2.qyl.local") }
                }
            });
        });
    });
```

### Resilience Enricher (für Logs/Metrics)

```csharp
builder.Services.AddResilienceEnricher();

// Fügt zu Logs hinzu:
// - polly.outcome
// - polly.retry.attempts
// - polly.circuit_breaker.state
```

### Für qyl.mcp (HTTP zu Collector)

```csharp
// In qyl.mcp/Program.cs
builder.Services.AddResilienceEnricher();

builder.Services.AddHttpClient("collector", client =>
{
    client.BaseAddress = new Uri(config["Collector:BaseUrl"]!);
})
.AddStandardResilienceHandler(o =>
{
    // Collector kann kurz offline sein bei Restart
    o.Retry.MaxRetryAttempts = 5;
    o.Retry.Delay = TimeSpan.FromSeconds(1);
    
    // Circuit Breaker für längere Ausfälle
    o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    
    // Großzügiges Timeout für große Span-Batches
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
})
.AddExtendedHttpClientLogging();
```

### Resilience Handler entfernen (Tests)

```csharp
// In Tests - Resilience deaktivieren
builder.Services.AddHttpClient("collector")
    .RemoveAllResilienceHandlers();  // [Experimental]
```

---

## 15. Microsoft.Extensions.AI - Speech-to-Text (Experimental)

### Package

```
Microsoft.Extensions.AI  # 10.0.0
```

### Setup

```csharp
// Direct instance
builder.Services.AddSpeechToTextClient(new WhisperSpeechToTextClient(...));

// Factory pattern
builder.Services.AddSpeechToTextClient(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new AzureSpeechToTextClient(config["Azure:SpeechKey"]!);
});

// Keyed services (multiple providers)
builder.Services.AddKeyedSpeechToTextClient("whisper", new WhisperClient(...));
builder.Services.AddKeyedSpeechToTextClient("azure", new AzureSpeechClient(...));
```

> ⚠️ **Experimental** - Markiert mit `[Experimental("MEAI001")]`

---

## 16. Distributed Caching

### Packages

```
Microsoft.Extensions.Caching.SqlServer           # 10.0.0
Microsoft.Extensions.Caching.StackExchangeRedis  # 10.0.0
```

### SQL Server Distributed Cache

```csharp
builder.Services.AddDistributedSqlServerCache(o =>
{
    o.ConnectionString = config.GetConnectionString("CacheDb");
    o.SchemaName = "dbo";
    o.TableName = "Cache";
    o.DefaultSlidingExpiration = TimeSpan.FromMinutes(30);
});
```

**SQL Server Table erstellen:**

```sql
CREATE TABLE dbo.Cache (
    Id NVARCHAR(449) NOT NULL PRIMARY KEY,
    Value VARBINARY(MAX) NOT NULL,
    ExpiresAtTime DATETIMEOFFSET NOT NULL,
    SlidingExpirationInSeconds BIGINT NULL,
    AbsoluteExpiration DATETIMEOFFSET NULL
);
CREATE INDEX IX_Cache_ExpiresAtTime ON dbo.Cache(ExpiresAtTime);
```

### Redis Distributed Cache

```csharp
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = config.GetConnectionString("Redis");
    o.InstanceName = "qyl:";
});
```

### Usage (IDistributedCache)

```csharp
public class SessionCache
{
    private readonly IDistributedCache _cache;
    
    public SessionCache(IDistributedCache cache)
    {
        _cache = cache;
    }
    
    public async Task<SessionSummary?> GetSessionAsync(string sessionId)
    {
        var bytes = await _cache.GetAsync($"session:{sessionId}");
        if (bytes is null) return null;
        
        return JsonSerializer.Deserialize<SessionSummary>(bytes);
    }
    
    public async Task SetSessionAsync(SessionSummary session)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(session);
        await _cache.SetAsync($"session:{session.SessionId}", bytes, new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        });
    }
}
```

### Für qyl.collector (Session-Caching)

```csharp
// In-Memory für Single-Instance
builder.Services.AddDistributedMemoryCache();

// Oder Redis für Multi-Instance
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = config.GetConnectionString("Redis");
    o.InstanceName = "qyl:collector:";
});

// Session-Aggregationen cachen
builder.Services.AddSingleton<SessionCache>();
```

---

## 17. TCP Endpoint Probes (Kubernetes-ready)

### Package

```
Microsoft.Extensions.Diagnostics.Probes  # 10.0.0-preview
```

### Was es macht

Exposiert Health Checks über **TCP Port** statt HTTP - perfekt für Kubernetes Probes ohne HTTP Overhead!

### Setup

```csharp
// Default Port
builder.Services.AddTcpEndpointProbe();

// Mit Options
builder.Services.AddTcpEndpointProbe(o =>
{
    o.TcpPort = 8081;
    o.FilterByName = "ready";  // Nur bestimmte Health Checks
});

// Via Config
builder.Services.AddTcpEndpointProbe(config.GetSection("Probes:Tcp"));

// Named Probes (mehrere Ports)
builder.Services.AddTcpEndpointProbe("liveness", o => o.TcpPort = 8081);
builder.Services.AddTcpEndpointProbe("readiness", o => o.TcpPort = 8082);
```

### Kubernetes Deployment

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: qyl-collector
    livenessProbe:
      tcpSocket:
        port: 8081
      initialDelaySeconds: 5
      periodSeconds: 10
    readinessProbe:
      tcpSocket:
        port: 8082
      initialDelaySeconds: 5
      periodSeconds: 5
```

### Für qyl.collector

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DuckDbHealthCheck>("duckdb", tags: ["ready"])
    .AddApplicationLifecycleHealthCheck(tags: ["live"]);

// TCP Probes für K8s
builder.Services.AddTcpEndpointProbe("liveness", o =>
{
    o.TcpPort = 8081;
    o.FilterByName = "live";
});

builder.Services.AddTcpEndpointProbe("readiness", o =>
{
    o.TcpPort = 8082;
    o.FilterByName = "ready";
});
```

**Vorteile gegenüber HTTP:**

- Kein HTTP Overhead
- Schnellere Response
- Weniger CPU/Memory
- Ideal für High-Frequency Probes

---

## 18. Validation Services (.NET 10)

### Package

```
Microsoft.Extensions.Validation  # 10.0.0
```

### Setup

```csharp
builder.Services.AddValidation(o =>
{
    // Options konfigurieren
});
```

### Für qyl (Request Validation)

```csharp
// In qyl.collector für OTLP Request Validation
builder.Services.AddValidation();

// Usage mit Minimal APIs
app.MapPost("/v1/traces", async (
    [FromBody] ExportTraceServiceRequest request,
    IValidator<ExportTraceServiceRequest> validator) =>
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid)
    {
        return Results.ValidationProblem(result.ToDictionary());
    }
    
    // Process request...
});
```

---

## 19. QYL Enterprise Telemetry Stack (Komplett-Beispiel)

### Packages (alle zusammen)

```
Microsoft.Extensions.Telemetry                    # Enrichment/Redaction/Sampling
Microsoft.Extensions.Telemetry.Abstractions       # [LogProperties], [TagName]
Microsoft.Extensions.Compliance.Abstractions      # DataClassification
Microsoft.Extensions.Compliance.Redaction         # ErasingRedactor, HmacRedactor
Microsoft.Extensions.Http.Diagnostics             # Extended HttpClient logging
Microsoft.AspNetCore.Diagnostics.Middleware       # HTTP logging, per-request buffering
Microsoft.Extensions.Diagnostics.Buffering        # PerRequestLogBuffer
```

### QylDataClasses - Compliance Taxonomy

```csharp
// src/qyl.collector/Compliance/QylDataClasses.cs
using Microsoft.Extensions.Compliance.Classification;

namespace Qyl.Collector.Compliance;

public static class QylDataClasses
{
    public static readonly DataClassification Secret = new("qyl", "secret");   // erase
    public static readonly DataClassification Pii    = new("qyl", "pii");      // hmac
    public static readonly DataClassification UserId = new("qyl", "id.user");  // hmac
    public static readonly DataClassification Tenant = new("qyl", "id.tenant");// hmac
}
```

### QylRedactionConfig - Redaction Setup

```csharp
// src/qyl.collector/Compliance/QylRedactionConfig.cs
using Microsoft.Extensions.Compliance.Redaction;

namespace Qyl.Collector.Compliance;

public static class QylRedactionConfig
{
    public static IServiceCollection AddQylRedaction(
        this IServiceCollection services, 
        IConfiguration config)
    {
        services.AddRedaction(r =>
        {
            // Secrets: immer weg
            r.SetRedactor<ErasingRedactor>(QylDataClasses.Secret);

            // PII/IDs: pseudonymisieren (korrelierbar ohne Klartext)
            r.SetHmacRedactor(config.GetSection("Redaction:Hmac"),
                QylDataClasses.Pii, QylDataClasses.UserId, QylDataClasses.Tenant);

            // Fallback: sicherer Default
            r.SetFallbackRedactor<ErasingRedactor>();
        });
        
        return services;
    }
}
```

### QylHttpPolicy - Centralized Redaction Policy

```csharp
// src/qyl.collector/Compliance/QylHttpPolicy.cs
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Http.Logging;

namespace Qyl.Collector.Compliance;

internal static class QylHttpPolicy
{
    private static readonly IReadOnlyDictionary<string, DataClassification> Headers =
        new Dictionary<string, DataClassification>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = QylDataClasses.Secret,
            ["Cookie"]        = QylDataClasses.Secret,
            ["Set-Cookie"]    = QylDataClasses.Secret,
            ["X-Api-Key"]     = QylDataClasses.Secret,
            ["X-Tenant-Id"]   = QylDataClasses.Tenant,
            ["X-User-Id"]     = QylDataClasses.UserId,
        };

    private static readonly IReadOnlyDictionary<string, DataClassification> RouteParams =
        new Dictionary<string, DataClassification>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenantId"]  = QylDataClasses.Tenant,
            ["userId"]    = QylDataClasses.UserId,
            ["sessionId"] = QylDataClasses.UserId,
        };

    private static readonly IReadOnlyDictionary<string, DataClassification> Query =
        new Dictionary<string, DataClassification>(StringComparer.OrdinalIgnoreCase)
        {
            ["api_key"]      = QylDataClasses.Secret,
            ["access_token"] = QylDataClasses.Secret,
            ["userId"]       = QylDataClasses.UserId,
            ["tenantId"]     = QylDataClasses.Tenant,
        };

    internal static void Apply(LoggingRedactionOptions o)
    {
        foreach (var kv in Headers)     o.RequestHeadersDataClasses[kv.Key] = kv.Value;
        foreach (var kv in RouteParams) o.RouteParameterDataClasses[kv.Key] = kv.Value;
    }

    internal static void Apply(LoggingOptions o)
    {
        foreach (var kv in Headers)     o.RequestHeadersDataClasses[kv.Key] = kv.Value;
        foreach (var kv in RouteParams) o.RouteParameterDataClasses[kv.Key] = kv.Value;
        foreach (var kv in Query)       o.RequestQueryParametersDataClasses[kv.Key] = kv.Value;
    }
}
```

### QylEnterpriseTelemetryExtensions - All-in-One Setup

```csharp
// src/qyl.collector/Observability/QylEnterpriseTelemetryExtensions.cs
using Microsoft.Extensions.Http.Logging;

namespace Qyl.Collector.Observability;

public static class QylEnterpriseTelemetryExtensions
{
    public static WebApplicationBuilder AddQylEnterpriseTelemetry(this WebApplicationBuilder builder)
    {
        // 1) Compliance/Redaction baseline
        builder.Services.AddQylRedaction(builder.Configuration);

        // 2) Logging pipeline: Enrichment + Redaction
        builder.Logging.EnableEnrichment();
        builder.Logging.EnableRedaction();
        builder.Services.AddApplicationLogEnricher(o =>  // ⚠️ NOT AddServiceLogEnricher (obsolete!)
        {
            o.ApplicationName = true;
            o.EnvironmentName = true;
            o.BuildVersion = true;
        });

        // 3) Trace-aware Sampling (koppelt an Activity sampling)
        builder.Logging.AddTraceBasedSampler();

        // 4) Per-request buffer (für "log-on-failure / log-on-sampled-trace")
        builder.Logging.AddPerIncomingRequestBuffer(LogLevel.Information);

        // 5) Incoming HTTP logs: redaction/enrichment policy-driven
        builder.Services.AddHttpLogging();
        builder.Services.AddHttpLoggingRedaction(o => QylHttpPolicy.Apply(o));

        // 6) Outgoing HTTP logs: HttpClient diagnostics + policy-driven redaction
        builder.Services.AddLatencyContext();
        builder.Services.AddHttpClientLatencyTelemetry();
        builder.Services.AddHttpClient("qyl-default")
            .AddExtendedHttpClientLogging(o =>
            {
                o.RequestPathLoggingMode = OutgoingPathLoggingMode.Structured;
                o.RequestPathParameterRedactionMode = HttpRouteParameterRedactionMode.Strict;
                QylHttpPolicy.Apply(o);
            });

        return builder;
    }
}
```

### QylTraceAwareLogFlushMiddleware - Flush on Error/Sampled

```csharp
// src/qyl.collector/Observability/QylTraceAwareLogFlushMiddleware.cs
using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.Buffering;

namespace Qyl.Collector.Observability;

public static class QylTraceAwareLogFlushMiddleware
{
    private const string ForceFlushTag = "qyl.logs.force_flush";

    public static IApplicationBuilder UseQylTraceAwareLogFlush(this IApplicationBuilder app)
        => app.Use(async (ctx, next) =>
        {
            try
            {
                await next();

                var sampled = Activity.Current?.Recorded == true;
                var failed  = ctx.Response.StatusCode >= 500;

                if (sampled || failed)
                {
                    Activity.Current?.SetTag(ForceFlushTag, true);
                    ctx.RequestServices.GetService<PerRequestLogBuffer>()?.Flush();
                }
            }
            catch
            {
                Activity.Current?.SetTag(ForceFlushTag, true);
                ctx.RequestServices.GetService<PerRequestLogBuffer>()?.Flush();
                throw;
            }
        });
}
```

### QylLog - Source-Generated Logging

```csharp
// src/qyl.collector/Observability/QylLog.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Telemetry.Logging;

namespace Qyl.Collector.Observability;

internal static partial class QylLog
{
    [LoggerMessage(EventId = 11001, Level = LogLevel.Information, Message = "Span ingested")]
    public static partial void SpanIngested(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true, SkipNullProperties = true)] in SpanRecord span,
        [TagName("qyl.session.id")] string sessionId);
    
    [LoggerMessage(EventId = 11002, Level = LogLevel.Warning, Message = "Span validation failed")]
    public static partial void SpanValidationFailed(
        this ILogger logger,
        [TagName("qyl.trace.id")] string traceId,
        [TagName("qyl.error.reason")] string reason);
    
    [LoggerMessage(EventId = 11101, Level = LogLevel.Error, Message = "DuckDB insert failed")]
    public static partial void DuckDbInsertFailed(
        this ILogger logger,
        Exception exception,
        [TagName("qyl.span.count")] int spanCount);
}
```

### Program.cs - Alles zusammen

```csharp
// src/qyl.collector/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Enterprise Telemetry Stack (one-liner!)
builder.AddQylEnterpriseTelemetry();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DuckDbHealthCheck>("duckdb", tags: ["ready"])
    .AddApplicationLifecycleHealthCheck(tags: ["live"]);
builder.Services.AddTelemetryHealthCheckPublisher();

// ... rest of services

var app = builder.Build();

// Trace-aware log flushing
app.UseQylTraceAwareLogFlush();
app.UseHttpLogging();

// ... rest of middleware

app.Run();
```

### appsettings.json

```json
{
  "Redaction": {
    "Hmac": {
      "Key": "base64-encoded-32-byte-key-here"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## 20. Important Gotchas & Fixes

### ⚠️ AddServiceLogEnricher is OBSOLETE

```csharp
// ❌ OLD (will be removed)
builder.Services.AddServiceLogEnricher();

// ✅ NEW (.NET 10+)
builder.Services.AddApplicationLogEnricher();
```

### ⚠️ Http.Diagnostics DI Prerequisites

`AddExtendedHttpClientLogging` und `AddHttpClientLatencyTelemetry` benötigen:

```csharp
// MUSS vorher registriert sein, sonst Runtime-Exception!
builder.Services.AddRedaction(...);
builder.Services.AddLatencyContext();

// Dann erst:
builder.Services.AddHttpClientLatencyTelemetry();
builder.Services.AddExtendedHttpClientLogging(...);
```

### ⚠️ [TagName] ist Experimental

```csharp
// Erzeugt Warning EXTEXP0003
[TagName("qyl.session.id")] string sessionId
```

**Lösung:** In `.csproj` oder `Directory.Build.props`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);EXTEXP0003</NoWarn>
</PropertyGroup>
```

### ⚠️ Content.Headers werden nicht geloggt

`AddExtendedHttpClientLogging` loggt **keine** `Content.Headers` (Content-Type, Content-Encoding etc.).

**Workaround:** Custom `IHttpClientLogEnricher`:

```csharp
public class ContentHeadersEnricher : IHttpClientLogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector, 
        HttpRequestMessage request, 
        HttpResponseMessage? response, 
        Exception? exception)
    {
        if (request.Content?.Headers.ContentType is { } ct)
        {
            collector.Add("http.request.content_type", ct.MediaType);
        }
        if (response?.Content.Headers.ContentType is { } rct)
        {
            collector.Add("http.response.content_type", rct.MediaType);
        }
    }
}

builder.Services.AddHttpClientLogEnricher<ContentHeadersEnricher>();
```

### ⚠️ Transitive für nested Objects

```csharp
// Ohne Transitive: nur top-level Properties
[LogProperties(OmitReferenceName = true)] in SpanRecord span

// Mit Transitive: auch nested Objects (GenAiData etc.)
[LogProperties(OmitReferenceName = true, SkipNullProperties = true, Transitive = true)] in SpanRecord span
```

---

## 21. .NET 10 / C# 13 Performance Patterns Reference

### Quick Decision Matrix

| Scenario                   | Old Pattern                | New Pattern                          | Why                        |
|----------------------------|----------------------------|--------------------------------------|----------------------------|
| Check if list has items    | `list.Any()`               | `list.Count > 0` or `list.Exists(p)` | No enumerator allocation   |
| Count with predicate       | `list.Count(p)`            | `list.FindAll(p).Count`              | Direct array access        |
| First match                | `list.FirstOrDefault(p)`   | `list.Find(p)`                       | No LINQ overhead           |
| Process tasks as completed | `while` + `WhenAny`        | `Task.WhenEach()`                    | .NET 9+, cleaner           |
| Stream results             | `IEnumerable<T>` + yield   | `IAsyncEnumerable<T>`                | Non-blocking               |
| Cache sync fast path       | `Task<T>`                  | `ValueTask<T>`                       | No allocation on cache hit |
| Time access                | `DateTime.UtcNow`          | `TimeProvider.GetUtcNow()`           | Testable                   |
| Parallel streams           | Manual async               | `Channel<T>`                         | Backpressure, bounded      |
| SSE endpoints              | Manual headers             | `TypedResults.ServerSentEvents()`    | Type-safe                  |
| Group + count              | `GroupBy().ToDictionary()` | `CountBy()`                          | Single pass                |
| Group + aggregate          | `GroupBy().ToDictionary()` | `AggregateBy()`                      | Single pass                |
| Locking                    | `object _lock`             | `Lock _lock`                         | Faster, cleaner            |
| Collections init           | `new List<T>()`            | `List<T> x = []`                     | C# 12+                     |

### List<T> Direct Methods (Always Prefer Over LINQ)

```csharp
List<SpanData> spans = GetSpans();

// ❌ LINQ Any() - allocates enumerator
bool hasItems = spans.Any();
bool hasErrors = spans.Any(s => s.Status == StatusCode.Error);

// ✅ Direct property / Exists() - no allocation
bool hasItems = spans.Count > 0;
bool hasErrors = spans.Exists(s => s.Status == StatusCode.Error);

// ❌ LINQ First/Where
var first = spans.FirstOrDefault(s => s.IsError);
var all = spans.Where(s => s.IsError).ToList();

// ✅ List<T> methods
var first = spans.Find(s => s.IsError);           // null if not found
var all = spans.FindAll(s => s.IsError);          // returns List<T>

// ✅ Index operations
int idx = spans.FindIndex(s => s.IsError);        // -1 if not found

// ✅ Bool checks
bool allOk = spans.TrueForAll(s => s.Status == StatusCode.Ok);

// ✅ In-place mutations
spans.RemoveAll(s => s.IsError);  // returns count removed

// ✅ Conversions
List<string> ids = spans.ConvertAll(s => s.SpanId);
```

### CountBy / AggregateBy / Index (.NET 9+)

```csharp
// ❌ OLD: Two passes, intermediate IGrouping allocations
var providerCounts = spans
    .GroupBy(s => s.ProviderName)
    .ToDictionary(g => g.Key, g => g.Count());

// ✅ NEW: Single pass, streaming
IEnumerable<KeyValuePair<string?, int>> providerCounts = spans.CountBy(s => s.ProviderName);

// ❌ OLD: GroupBy for aggregation
var tokensByProvider = spans
    .GroupBy(s => s.ProviderName)
    .ToDictionary(g => g.Key, g => g.Sum(s => s.InputTokens + s.OutputTokens));

// ✅ NEW: Single pass with seed and accumulator
IEnumerable<KeyValuePair<string?, long>> tokensByProvider = spans.AggregateBy(
    keySelector: s => s.ProviderName,
    seed: 0L,
    func: (total, span) => total + span.InputTokens + span.OutputTokens);

// With seed factory (different initial value per key)
var stats = spans.AggregateBy(
    keySelector: s => s.ProviderName,
    seedSelector: key => new ProviderStats(key),
    func: (stats, span) => stats.Add(span));

// ✅ Index() for position tracking
foreach (var (index, span) in spans.Index())
{
    Console.WriteLine($"{index}: {span.SpanId}");
}
```

### Task.WhenEach (.NET 9+)

```csharp
// ❌ OLD: WhenAny loop - O(n²) removals, error-prone
var tasks = new List<Task<SpanData>>(pendingExports);
while (tasks.Count > 0)
{
    var completed = await Task.WhenAny(tasks);
    tasks.Remove(completed);  // O(n) removal each iteration!
    var result = await completed;
    ProcessResult(result);
}

// ✅ NEW: Task.WhenEach - clean, efficient
var tasks = spans.Select(s => ExportSpanAsync(s));

await foreach (var completedTask in Task.WhenEach(tasks))
{
    var result = await completedTask;
    ProcessResult(result);
}
```

### ValueTask<T> - Avoid Allocation on Sync Paths

```csharp
// ❌ Task<T> ALWAYS allocates, even for cached results
public async Task<SessionStats?> GetSessionAsync(string sessionId)
{
    if (_cache.TryGetValue(sessionId, out var cached))
        return cached;  // STILL allocates Task wrapper!
    return await LoadFromDatabaseAsync(sessionId);
}

// ✅ ValueTask<T> - no allocation on cache hit
public ValueTask<SessionStats?> GetSessionAsync(string sessionId)
{
    if (_cache.TryGetValue(sessionId, out var cached))
        return ValueTask.FromResult<SessionStats?>(cached);  // No allocation
    return new ValueTask<SessionStats?>(LoadFromDatabaseAsync(sessionId));
}

// ⚠️ CRITICAL: Never await ValueTask multiple times!
```

### Lock Class (.NET 9+ / C# 13)

```csharp
// ❌ OLD - object lock
private readonly object _lock = new object();
lock (_lock) { /* ... */ }

// ✅ NEW - Lock class (MANDATORY)
private readonly Lock _lock = new();

lock (_lock)
{
    _sessions[sessionId] = stats;
}

// Or explicit for try-finally patterns
using (_lock.EnterScope())
{
    _sessions[sessionId] = stats;
}
```

### Collection Expressions (C# 12+)

```csharp
// ❌ OLD - NEVER use these patterns
var list = new List<int>();
var list = new List<int> { 1, 2, 3 };
List<int> list = new();
var arr = new int[] { 1, 2, 3 };

// ✅ NEW - Collection Expressions (MANDATORY)
List<int> list = [];
List<int> list = [1, 2, 3];
int[] arr = [1, 2, 3];
Dictionary<string, int> dict = [];
HashSet<string> set = ["a", "b"];

// Spread operator for combining
List<SpanData> allSpans = [..baseSpans, ..newSpans];
```

### params Collections (C# 13)

```csharp
// ❌ OLD - params only worked with arrays
public void AddTags(params KeyValuePair<string, object?>[] tags) { }

// ✅ NEW - params works with any collection type
public void AddTags(params ReadOnlySpan<KeyValuePair<string, object?>> tags)
{
    foreach (var tag in tags)
        _activity.SetTag(tag.Key, tag.Value);
}
```

### TypedResults.ServerSentEvents (.NET 9+)

```csharp
// ❌ OLD: Manual SSE - error-prone
app.MapGet("/api/v1/stream", async (HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    await foreach (var evt in GetEventsAsync(ct))
    {
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(evt)}\n\n");
        await ctx.Response.Body.FlushAsync(ct);
    }
});

// ✅ NEW: TypedResults.ServerSentEvents
app.MapGet("/api/v1/stream", (CancellationToken ct) =>
    TypedResults.ServerSentEvents(GetTelemetryEventsAsync(ct), eventType: "telemetry"));

async IAsyncEnumerable<TelemetryEvent> GetTelemetryEventsAsync(
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var evt in _broadcaster.Subscribe(ct))
        yield return evt;
}
```

### FrozenDictionary / FrozenSet (.NET 8+)

```csharp
// Read-optimized immutable collections
private static readonly FrozenDictionary<string, string> DeprecatedMappings =
    new Dictionary<string, string>
    {
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
    }.ToFrozenDictionary(StringComparer.Ordinal);

private static readonly FrozenSet<string> PromotedFields =
    new[] { "gen_ai.provider.name", "gen_ai.request.model" }
    .ToFrozenSet(StringComparer.Ordinal);

// AlternateLookup for zero-allocation span lookups (.NET 9+)
var lookup = DeprecatedMappings.GetAlternateLookup<ReadOnlySpan<char>>();
if (lookup.TryGetValue(keySpan, out var replacement))
{
    // Found without allocating a string from the span
}
```

### JsonSerializerOptions.Strict (.NET 10)

```csharp
// NEW - .NET 10 strict serialization preset
var options = JsonSerializerOptions.Strict;

// Equivalent to:
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true
};
```

### TimeProvider (.NET 8+)

```csharp
// ❌ OLD - DateTime.UtcNow (untestable)
var startTime = DateTime.UtcNow;

// ✅ NEW - TimeProvider (MANDATORY)
public class SessionAggregator(TimeProvider timeProvider)
{
    public void RecordSpan(SpanData span)
    {
        var now = timeProvider.GetUtcNow();
        var elapsed = now - span.StartTime;
    }
}

// In tests with FakeTimeProvider
var fakeTime = new FakeTimeProvider();
fakeTime.Advance(TimeSpan.FromMinutes(5));
```

### Async LINQ (.NET 10)

```csharp
// Full LINQ support for IAsyncEnumerable<T>
IAsyncEnumerable<SpanData> errors = spans.Where(s => s.IsError);
IAsyncEnumerable<string> ids = spans.Select(s => s.SpanId);

// Async aggregation
int count = await spans.CountAsync(ct);
bool hasErrors = await spans.AnyAsync(s => s.IsError, ct);
List<SpanData> list = await spans.ToListAsync(ct);

// CountBy / AggregateBy for async
IAsyncEnumerable<KeyValuePair<string, int>> providerCounts = 
    spans.CountBy(s => s.ProviderName);

// LEFT/RIGHT JOINs - .NET 10 NEW!
IAsyncEnumerable<(Span, Session?)> leftJoined = spans.LeftJoin(
    sessions,
    span => span.SessionId,
    session => session.Id,
    (span, session) => (span, session));

// Shuffle - .NET 10 NEW!
IAsyncEnumerable<SpanData> shuffled = spans.Shuffle();
```

### Quick Reference Card

```
┌─────────────────────────────────────────────────────────────────┐
│ .NET 10 / C# 13 MANDATORY PATTERNS                              │
├─────────────────────────────────────────────────────────────────┤
│ LOCKING:      Lock _lock = new();                               │
│ COUNTING:     items.CountBy(x => x.Key)                         │
│ AGGREGATING:  items.AggregateBy(key, seed, func)                │
│ INDEXING:     foreach (var (i, x) in items.Index())             │
│ PARALLEL:     await foreach (var t in Task.WhenEach(tasks))     │
│ ORDERED MAP:  OrderedDictionary<K,V>                            │
│ FROZEN MAP:   FrozenDictionary<K,V> (read-only)                 │
│ SSE:          TypedResults.ServerSentEvents(stream)             │
│ JSON:         JsonNamingPolicy.SnakeCaseLower                   │
│ JSON STRICT:  JsonSerializerOptions.Strict (.NET 10)            │
│ TIME:         TimeProvider.GetUtcNow()                          │
│ CACHE:        HybridCache.GetOrCreateAsync()                    │
│ PARAMS:       params ReadOnlySpan<T>                            │
│ COLLECTIONS:  List<T> list = []; NOT new()                      │
│ SPREAD:       [..existing, ..more]                              │
└─────────────────────────────────────────────────────────────────┘
```

### Patterns to REJECT

| REJECT                                           | USE INSTEAD                            |
|--------------------------------------------------|----------------------------------------|
| `private readonly object _lock = new();`         | `private readonly Lock _lock = new();` |
| `.GroupBy().ToDictionary(g => g.Count())`        | `.CountBy()`                           |
| `Task.WhenAny` in loops                          | `Task.WhenEach()`                      |
| `ctx.Response.ContentType = "text/event-stream"` | `TypedResults.ServerSentEvents()`      |
| `DateTime.UtcNow`                                | `TimeProvider.GetUtcNow()`             |
| `new List<T>()` or `List<T> x = new()`           | `List<T> x = []`                       |
| `Task<T>` for cache hits                         | `ValueTask<T>`                         |
| `list.Any()`                                     | `list.Count > 0` or `list.Exists()`    |
| `list.FirstOrDefault(p)`                         | `list.Find(p)`                         |

---

## 22. (Platzhalter für nächstes Feature)

<!-- Weitere Features hier einfügen -->

---

## TODO

- [ ] Packages zu Directory.Packages.props hinzufügen
- [ ] QylDataClasses implementieren
- [ ] Redaction in collector Program.cs einbauen
- [ ] Testen mit sensiblen Daten

