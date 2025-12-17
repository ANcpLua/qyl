# .NET Telemetry Evolution: .NET 8 → .NET 9 → .NET 10

## Overview

This document covers the evolution of telemetry, logging, and observability features across .NET 8, 9, and 10, focusing on source generators, `Microsoft.Extensions.Telemetry.*` packages, and the underlying `System.Diagnostics` APIs.

## Package Versions (Directory.Packages.props)

```xml
<!-- .NET 8 (LTS) -->
<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
<PackageVersion Include="Microsoft.Extensions.Telemetry" Version="8.10.0" />
<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="8.10.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Abstractions" Version="8.10.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Redaction" Version="8.10.0" />
<PackageVersion Include="Microsoft.Extensions.Http.Diagnostics" Version="8.10.0" />
<PackageVersion Include="Microsoft.AspNetCore.Diagnostics.Middleware" Version="8.10.0" />

<!-- .NET 9 -->
<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.1" />
<PackageVersion Include="Microsoft.Extensions.Telemetry" Version="9.9.0" />
<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="9.9.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Abstractions" Version="9.9.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Redaction" Version="9.9.0" />
<PackageVersion Include="Microsoft.Extensions.Http.Diagnostics" Version="9.9.0" />
<PackageVersion Include="Microsoft.AspNetCore.Diagnostics.Middleware" Version="9.9.0" />
<PackageVersion Include="Microsoft.Extensions.Diagnostics.Buffering" Version="9.9.0" />

<!-- .NET 10 -->
<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.1" />
<PackageVersion Include="Microsoft.Extensions.Logging.EventSource" Version="10.0.1" />
<PackageVersion Include="Microsoft.Extensions.Telemetry" Version="10.1.0" />
<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="10.1.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Abstractions" Version="10.1.0" />
<PackageVersion Include="Microsoft.Extensions.Compliance.Redaction" Version="10.1.0" />
<PackageVersion Include="Microsoft.Extensions.Http.Diagnostics" Version="10.1.0" />
<PackageVersion Include="Microsoft.AspNetCore.Diagnostics.Middleware" Version="10.1.0" />
<PackageVersion Include="Microsoft.Extensions.Diagnostics.Buffering" Version="10.1.0" />
```

---

## 1. LoggerMessage Source Generator Evolution

### .NET 6/7 - Built-in Generator (Microsoft.Extensions.Logging.Generators)

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 6/7: Basic LoggerMessage with LoggerMessage.Define<T>()
// ═══════════════════════════════════════════════════════════════════════════

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Processing order {OrderId}")]
    public static partial void ProcessingOrder(ILogger logger, int orderId);
}

// Generated code (.NET 6/7):
[GeneratedCode("Microsoft.Extensions.Logging.Generators", "6.0.0.0")]
private static readonly Action<ILogger, int, Exception?> __ProcessingOrderCallback =
    LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(1, "ProcessingOrder"),
        "Processing order {OrderId}",
        new LogDefineOptions() { SkipEnabledCheck = true }
    );

public static partial void ProcessingOrder(ILogger logger, int orderId)
{
    if (logger.IsEnabled(LogLevel.Information))
    {
        __ProcessingOrderCallback(logger, orderId, null);
    }
}
```

### .NET 8 - Enhanced Generator with [LogProperties] (Microsoft.Gen.Logging)

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 8: NEW - Microsoft.Extensions.Telemetry.Abstractions Generator
// Replaces built-in generator when package is referenced
// ═══════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Telemetry.Abstractions; // NEW in .NET 8

// NEW: [LogProperties] for automatic object property logging
public record OrderContext(int OrderId, string CustomerId, decimal Amount);

public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing order {OrderId}")]
    public static partial void ProcessingOrder(
        ILogger logger,
        int orderId,
        [LogProperties] OrderContext context);  // Logs all properties

    // NEW: [TagName] for OTel-compatible attribute names
    [LoggerMessage(Level = LogLevel.Debug, Message = "Request: {http.request.method}")]
    public static partial void HttpRequest(
        ILogger logger,
        [TagName("http.request.method")] string method,  // Custom tag name
        [TagName("http.request.path")] string path);
}

// Generated code (.NET 8 with Microsoft.Gen.Logging 8.x):
[GeneratedCode("Microsoft.Gen.Logging", "8.10.0")]
public static partial void ProcessingOrder(ILogger logger, int orderId, OrderContext context)
{
    if (!logger.IsEnabled(LogLevel.Information))
    {
        return;
    }

    var state = LoggerMessageHelper.ThreadLocalState;  // Pooled state
    _ = state.ReserveTagSpace(5);

    state.TagArray[4] = new("OrderId", orderId);
    state.TagArray[3] = new("context.OrderId", context.OrderId);
    state.TagArray[2] = new("context.CustomerId", context.CustomerId);
    state.TagArray[1] = new("context.Amount", context.Amount);
    state.TagArray[0] = new("{OriginalFormat}", "Processing order {OrderId}");

    logger.Log(
        LogLevel.Information,
        new EventId(0, nameof(ProcessingOrder)),
        state,
        null,
        static (s, _) =>
        {
            var orderId = s.TagArray[4].Value;
            return FormattableString.Invariant($"Processing order {orderId}");
        });

    state.Clear();
}
```

### .NET 9 - Primary Constructor Support + [TagProvider]

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 9: Primary constructor support + [TagProvider] + improvements
// ═══════════════════════════════════════════════════════════════════════════

// .NET 9: Logger from primary constructor parameter
public partial class OrderService(ILogger<OrderService> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} processed")]
    public partial void OrderProcessed(int orderId);  // No logger parameter needed!
}

// .NET 9: [TagProvider] for custom tag extraction
public static class OrderTagProvider
{
    public static void RecordTags(ITagCollector collector, OrderContext context)
    {
        collector.Add("order.id", context.OrderId);
        collector.Add("order.amount_usd", context.Amount);
        // Skip CustomerId (PII)
    }
}

public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing order")]
    public static partial void ProcessingOrder(
        ILogger logger,
        [TagProvider(typeof(OrderTagProvider), nameof(OrderTagProvider.RecordTags))]
        OrderContext context);  // Custom tag extraction
}

// Generated code (.NET 9):
[GeneratedCode("Microsoft.Gen.Logging", "9.9.0")]
public partial void OrderProcessed(int orderId)
{
    // Uses primary constructor logger automatically
    if (!logger.IsEnabled(LogLevel.Information))
    {
        return;
    }

    var state = LoggerMessageHelper.ThreadLocalState;
    _ = state.ReserveTagSpace(2);
    state.TagArray[1] = new("OrderId", orderId);
    state.TagArray[0] = new("{OriginalFormat}", "Order {OrderId} processed");

    logger.Log(
        LogLevel.Information,
        new EventId(0, nameof(OrderProcessed)),
        state,
        null,
        static (s, _) =>
        {
            var orderId = s.TagArray[1].Value;
            return FormattableString.Invariant($"Order {orderId} processed");
        });

    state.Clear();
}
```

### .NET 10 - FormattableString.Invariant + Activity Integration

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 10: Improved FormattableString handling + Activity correlation
// ═══════════════════════════════════════════════════════════════════════════

// Generated code (.NET 10):
[GeneratedCode("Microsoft.Extensions.Logging.Generators", "10.0.13.2411")]
public static partial void ProcessingOrder(ILogger logger, int orderId)
{
    if (!logger.IsEnabled(LogLevel.Information))
    {
        return;
    }

    var state = LoggerMessageHelper.ThreadLocalState;
    _ = state.ReserveTagSpace(2);
    state.TagArray[1] = new("OrderId", orderId);
    state.TagArray[0] = new("{OriginalFormat}", "Processing order {OrderId}");

    logger.Log(
        LogLevel.Information,
        new EventId(1, nameof(ProcessingOrder)),
        state,
        null,
        static (s, _) =>
        {
            var orderId = s.TagArray[1].Value;
            // .NET 10: Uses System.FormattableString.Invariant (fixed namespace)
            return global::System.FormattableString.Invariant($"Processing order {orderId}");
        });

    state.Clear();
}
```

---

## 2. ActivitySource Evolution (.NET 5 → .NET 10)

### .NET 5/6/7/8 - Basic ActivitySource

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 5-8: Basic ActivitySource pattern
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;

public static class Telemetry
{
    // Static singleton pattern
    public static readonly ActivitySource Source = new("MyApp.Orders", "1.0.0");
}

public class OrderService
{
    public void ProcessOrder(int orderId)
    {
        using var activity = Telemetry.Source.StartActivity("ProcessOrder");
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("gen_ai.operation.name", "chat");

        // ... processing

        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
```

### .NET 10 - ActivitySourceOptions + Schema URL Support

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 10: NEW - ActivitySourceOptions with telemetry schema URL
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;

public static class Telemetry
{
    // .NET 10: ActivitySourceOptions for configuration
    public static readonly ActivitySource Source = new(
        new ActivitySourceOptions
        {
            Name = "MyApp.Orders",
            Version = "1.0.0",
            // OTel Schema URL support
            TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.38.0"
        });

    // .NET 10: Meter also supports schema URL
    public static readonly Meter OrderMeter = new(
        new MeterOptions
        {
            Name = "MyApp.Orders.Metrics",
            Version = "1.0.0",
            TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.38.0"
        });
}

public class OrderService
{
    private static readonly Counter<long> _ordersProcessed =
        Telemetry.OrderMeter.CreateCounter<long>(
            "orders.processed",
            unit: "{order}",
            description: "Number of orders processed");

    public void ProcessOrder(int orderId)
    {
        using var activity = Telemetry.Source.StartActivity(
            "ProcessOrder",
            ActivityKind.Internal);

        activity?.SetTag("order.id", orderId);

        // .NET 10: ActivityLink and ActivityEvent serialization
        activity?.AddEvent(new ActivityEvent(
            "order.validation.complete",
            tags: new ActivityTagsCollection
            {
                ["validation.result"] = "success",
                ["validation.duration_ms"] = 42
            }));

        // ... processing

        _ordersProcessed.Add(1, new KeyValuePair<string, object?>("order.status", "completed"));
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
```

### .NET 10 - Activity Links and Events Serialization

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 10: NEW - Activity Links/Events serialized out-of-process
// ═══════════════════════════════════════════════════════════════════════════

// .NET 10 adds support for serializing ActivityLink and ActivityEvent
// via Microsoft-Diagnostics-DiagnosticSource event source provider

// Example output format:
// Events->"[(TestEvent1,2025-03-27T23:34:10.6225721+00:00,[E11:EV1,E12:EV2])]"
// Links->"[(19b6e8ea216cb2ba36dd5d957e126d9f,98f7abcb3418f217,Recorded,null,false,[alk1:alv1])]"

public void ProcessWithLinks(int orderId, ActivityContext parentContext)
{
    var links = new[]
    {
        new ActivityLink(parentContext, new ActivityTagsCollection
        {
            ["link.relationship"] = "parent_order"
        })
    };

    using var activity = Telemetry.Source.StartActivity(
        "ProcessOrder",
        ActivityKind.Internal,
        parentContext: default,
        tags: new[] { new KeyValuePair<string, object?>("order.id", orderId) },
        links: links);  // Links now serialized in .NET 10

    // Events also serialized
    activity?.AddEvent(new ActivityEvent("order.started"));

    // ... processing

    activity?.AddEvent(new ActivityEvent("order.completed", tags: new ActivityTagsCollection
    {
        ["order.total_items"] = 5
    }));
}
```

---

## 3. Log Enrichment and Redaction

### .NET 8+ - Data Classification and Redaction

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 8+: Data Classification and Redaction
// ═══════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

// Define data classifications
public static class MyTaxonomy
{
    public static DataClassification Pii => new("PII");
    public static DataClassification Secret => new("Secret");
}

// Mark properties with classification
public class UserInfo
{
    [PrivateData]  // Built-in classification
    public string Email { get; set; }

    [DataClassification("PII")]
    public string PhoneNumber { get; set; }

    public string DisplayName { get; set; }  // Not classified
}

// Configure redaction in DI
services.AddRedaction(builder =>
{
    // Erase PII completely
    builder.SetRedactor<ErasingRedactor>(MyTaxonomy.Pii);

    // HMAC hash secrets (for correlation)
    builder.SetRedactor<HmacRedactor>(MyTaxonomy.Secret);
});

// Enable redaction in logging
services.AddLogging(builder =>
{
    builder.EnableRedaction();
});

// Usage with [LogProperties]
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "User logged in")]
    public static partial void UserLoggedIn(
        ILogger logger,
        [LogProperties] UserInfo user);  // Email/Phone auto-redacted
}
```

### .NET 9+ - Log Buffering

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 9+: Log Buffering (Microsoft.Extensions.Diagnostics.Buffering)
// ═══════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.Buffering;

// Configure global log buffering
services.AddLogging(builder =>
{
    // Buffer Warning and lower level logs
    builder.AddGlobalBuffer(LogLevel.Warning);

    // Or with detailed rules
    builder.AddGlobalBuffer(options =>
    {
        options.Rules.Add(new LogBufferingFilterRule(logLevel: LogLevel.Information));
        options.Rules.Add(new LogBufferingFilterRule(categoryName: "Microsoft.*"));
    });
});

// Flush on exception
public class OrderService
{
    private readonly GlobalLogBuffer _logBuffer;
    private readonly ILogger<OrderService> _logger;

    public OrderService(GlobalLogBuffer logBuffer, ILogger<OrderService> logger)
    {
        _logBuffer = logBuffer;
        _logger = logger;
    }

    public void ProcessOrder(int orderId)
    {
        try
        {
            _logger.LogDebug("Starting order {OrderId}", orderId);  // Buffered
            _logger.LogInformation("Processing...");  // Buffered

            // ... processing
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order failed");

            // Flush buffered logs when exception occurs
            _logBuffer.Flush();  // All buffered Debug/Info logs now emitted!
            throw;
        }
    }
}
```

### .NET 9+ - Log Sampling

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 9+: Log Sampling
// ═══════════════════════════════════════════════════════════════════════════

// Random Probabilistic Sampling
services.AddLogging(builder =>
{
    // Sample 10% of all logs (90% dropped)
    builder.AddRandomProbabilisticSampler(0.1);

    // Sample 10% of Warning and lower, but keep all Error logs
    builder.AddRandomProbabilisticSampler(options =>
    {
        options.Rules.Add(new RandomProbabilisticSamplerFilterRule(0.1, logLevel: LogLevel.Warning));
        options.Rules.Add(new RandomProbabilisticSamplerFilterRule(1.0, logLevel: LogLevel.Error));
    });
});

// Trace-Based Sampling (aligns with OTel Activity sampling)
services.AddLogging(builder =>
{
    builder.AddTraceBasedSampler();  // Log sampling follows Activity sampling
});
```

---

## 4. Application Log Enrichment

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 8+: Application Log Enrichment
// ═══════════════════════════════════════════════════════════════════════════

// Configure enrichment
services.AddLogging(builder =>
{
    builder.EnableEnrichment(options =>
    {
        options.CaptureStackTraces = true;
        options.IncludeExceptionMessage = true;
        options.MaxStackTraceLength = 500;
    });
});

// Add built-in enrichers
services.AddApplicationLogEnricher(options =>
{
    options.ApplicationName = true;
    options.BuildVersion = true;
    options.DeploymentRing = true;
    options.EnvironmentName = true;
});

// Create custom enricher
public class CorrelationEnricher : ILogEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(IEnrichmentTagCollector collector)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            collector.Add("correlation.id", context.TraceIdentifier);
            collector.Add("http.route", context.GetRouteData()?.Values["action"]?.ToString());
        }
    }
}

services.AddLogEnricher<CorrelationEnricher>();
```

---

## 5. HTTP Client Diagnostics

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 8+: Extended HttpClient Logging
// ═══════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Http.Diagnostics;

services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
})
.AddExtendedHttpClientLogging(options =>
{
    // Log request/response bodies
    options.LogBody = true;
    options.BodySizeLimit = 32 * 1024;

    // Include specific headers
    options.RequestHeadersDataClasses.Add("Authorization", MyTaxonomy.Secret);
    options.ResponseHeadersDataClasses.Add("X-Request-Id", DataClassification.None);

    // Request path parameters to redact
    options.RouteParameterDataClasses.Add("userId", MyTaxonomy.Pii);
});
```

---

## 6. Latency Monitoring

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// .NET 8+: Latency Monitoring
// ═══════════════════════════════════════════════════════════════════════════

// Register checkpoints, measures, and tags
services.RegisterCheckpointNames("db.query", "external.api", "cache.lookup");
services.RegisterMeasureNames("response.time", "processing.time", "queue.wait");
services.RegisterTagNames("order.id", "user.tier");

// Add latency context
services.AddLatencyContext();

// Add console exporter (or custom)
services.AddConsoleLatencyDataExporter(options =>
{
    options.OutputCheckpoints = true;
    options.OutputMeasures = true;
    options.OutputTags = true;
});

// In ASP.NET Core - automatic export on request completion
app.UseRequestLatencyTelemetry();

// Usage
public class OrderService
{
    private readonly ILatencyContextProvider _latencyProvider;

    public OrderService(ILatencyContextProvider latencyProvider)
    {
        _latencyProvider = latencyProvider;
    }

    public async Task ProcessOrderAsync(int orderId)
    {
        var context = _latencyProvider.CreateContext();

        var dbToken = context.GetCheckpointToken("db.query");
        context.AddCheckpoint(dbToken);  // Start

        await _database.QueryAsync(...);

        context.AddCheckpoint(dbToken);  // End

        context.RecordMeasure(
            context.GetMeasureToken("processing.time"),
            123.45);

        context.SetTag(
            context.GetTagToken("order.id"),
            orderId.ToString());
    }
}
```

---

## 7. Source Generator Comparison Table

| Feature | .NET 6/7 | .NET 8 | .NET 9 | .NET 10 |
|---------|----------|--------|--------|---------|
| **Basic [LoggerMessage]** | Yes | Yes | Yes | Yes |
| **Extension method style** | Yes | Yes | Yes | Yes |
| **Instance method style** | Yes | Yes | Yes | Yes |
| **Dynamic log level** | Yes | Yes | Yes | Yes |
| **[LogProperties]** | No | Yes | Yes | Yes |
| **[TagName] (OTel semconv)** | No | Yes | Yes | Yes |
| **[TagProvider]** | No | No | Yes | Yes |
| **Primary constructor logger** | No | No | Yes | Yes |
| **LoggerMessageState pooling** | No | Yes | Yes | Yes |
| **Data redaction** | No | Yes | Yes | Yes |
| **Log enrichment** | No | Yes | Yes | Yes |
| **Log buffering** | No | No | Yes | Yes |
| **Log sampling** | No | No | Yes | Yes |
| **ActivitySource schema URL** | No | No | No | Yes |
| **Activity Links/Events serialization** | No | No | No | Yes |

---

## 8. Complete Example: .NET 10 Telemetry Setup

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// Program.cs - Complete .NET 10 Telemetry Configuration
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Diagnostics.Buffering;
using Microsoft.Extensions.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────
// 1. Define Telemetry Sources
// ─────────────────────────────────────────────────────────────────────────

public static class AppTelemetry
{
    public const string ServiceName = "MyApp.Orders";
    public const string ServiceVersion = "1.0.0";
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";

    // .NET 10: ActivitySourceOptions with schema URL
    public static readonly ActivitySource Source = new(new ActivitySourceOptions
    {
        Name = ServiceName,
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaUrl
    });

    // .NET 10: MeterOptions with schema URL
    public static readonly Meter Meter = new(new MeterOptions
    {
        Name = ServiceName,
        Version = ServiceVersion,
        TelemetrySchemaUrl = SchemaUrl
    });
}

// ─────────────────────────────────────────────────────────────────────────
// 2. Configure OpenTelemetry
// ─────────────────────────────────────────────────────────────────────────

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: AppTelemetry.ServiceName,
            serviceVersion: AppTelemetry.ServiceVersion))
    .WithTracing(tracing => tracing
        .AddSource(AppTelemetry.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AppTelemetry.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter());

// ─────────────────────────────────────────────────────────────────────────
// 3. Configure Logging with Enrichment, Buffering, Sampling
// ─────────────────────────────────────────────────────────────────────────

builder.Logging
    .EnableEnrichment(options =>
    {
        options.CaptureStackTraces = true;
        options.IncludeExceptionMessage = true;
    })
    .EnableRedaction()
    .AddGlobalBuffer(options =>
    {
        options.Rules.Add(new LogBufferingFilterRule(logLevel: LogLevel.Debug));
    })
    .AddTraceBasedSampler();  // Align log sampling with trace sampling

// ─────────────────────────────────────────────────────────────────────────
// 4. Configure Redaction
// ─────────────────────────────────────────────────────────────────────────

builder.Services.AddRedaction(redaction =>
{
    redaction.SetRedactor<ErasingRedactor>(DataClassificationExtensions.PrivateData);
    redaction.SetRedactor<HmacRedactor>(new DataClassification("Secret"));
});

// ─────────────────────────────────────────────────────────────────────────
// 5. Add Enrichers
// ─────────────────────────────────────────────────────────────────────────

builder.Services.AddApplicationLogEnricher(options =>
{
    options.ApplicationName = true;
    options.BuildVersion = true;
    options.EnvironmentName = true;
});

builder.Services.AddLogEnricher<CorrelationEnricher>();

// ─────────────────────────────────────────────────────────────────────────
// 6. Configure HTTP Client Diagnostics
// ─────────────────────────────────────────────────────────────────────────

builder.Services.AddHttpClient("external-api")
    .AddExtendedHttpClientLogging();

// ─────────────────────────────────────────────────────────────────────────
// 7. Configure Latency Monitoring
// ─────────────────────────────────────────────────────────────────────────

builder.Services.RegisterCheckpointNames("db.query", "cache.lookup");
builder.Services.AddLatencyContext();

var app = builder.Build();

app.UseRequestLatencyTelemetry();

app.MapGet("/orders/{id}", async (int id, OrderService service) =>
{
    return await service.GetOrderAsync(id);
});

app.Run();

// ─────────────────────────────────────────────────────────────────────────
// Service with Source-Generated Logging
// ─────────────────────────────────────────────────────────────────────────

public partial class OrderService(
    ILogger<OrderService> logger,  // .NET 9+: Primary constructor
    GlobalLogBuffer logBuffer,
    ILatencyContextProvider latencyProvider)
{
    private static readonly Counter<long> OrdersProcessed =
        AppTelemetry.Meter.CreateCounter<long>("orders.processed", "{order}");

    public async Task<Order?> GetOrderAsync(int orderId)
    {
        using var activity = AppTelemetry.Source.StartActivity("GetOrder");
        activity?.SetTag("order.id", orderId);

        var latencyContext = latencyProvider.CreateContext();

        try
        {
            LogOrderRequested(orderId);

            var dbToken = latencyContext.GetCheckpointToken("db.query");
            latencyContext.AddCheckpoint(dbToken);

            var order = await _database.GetOrderAsync(orderId);

            latencyContext.AddCheckpoint(dbToken);

            if (order is null)
            {
                LogOrderNotFound(orderId);
                return null;
            }

            LogOrderFound(orderId, order);
            OrdersProcessed.Add(1);

            return order;
        }
        catch (Exception ex)
        {
            LogOrderError(orderId, ex);
            logBuffer.Flush();  // Emit all buffered logs on error
            throw;
        }
    }

    // .NET 9+: No logger parameter needed (uses primary constructor)
    [LoggerMessage(Level = LogLevel.Debug, Message = "Order {OrderId} requested")]
    private partial void LogOrderRequested(int orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderId} not found")]
    private partial void LogOrderNotFound(int orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} found")]
    private partial void LogOrderFound(
        int orderId,
        [LogProperties] Order order);  // .NET 8+: Auto-log properties

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing order {OrderId}")]
    private partial void LogOrderError(int orderId, Exception ex);
}

public record Order(
    int OrderId,
    string CustomerId,
    [property: PrivateData] string CustomerEmail,  // Auto-redacted
    decimal Amount);
```

---

## References

- [What's new in .NET 10 Libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
- [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [Microsoft.Extensions.Telemetry NuGet](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Behind [LogProperties] and the new telemetry logging source generator](https://andrewlock.net/behind-logproperties-and-the-new-telemetry-logging-source-generator/)
