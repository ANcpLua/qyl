# Feature: Browser Telemetry with CORS

> **Status:** Ready
> **Effort:** ~1h
> **Backend:** Yes
> **Priority:** P0

---

## Problem

Browser apps cannot send OTLP telemetry to qyl because CORS is not configured on OTLP endpoints. The OTLP HTTP endpoint (4318) rejects cross-origin requests from browser JavaScript.

## Solution

Add CORS configuration for OTLP endpoints with configurable allowed origins and optional API key authentication for telemetry ingestion.

---

## Context

### Collector Location
```
/Users/ancplua/qyl/src/qyl.collector/
```

### Current State
- OTLP endpoints exist: gRPC (4317), HTTP (4318)
- Auth middleware excludes `/v1/traces` from authentication
- No CORS headers configured
- No API key validation for OTLP

### Environment Variables (New)
```bash
QYL_OTLP_CORS_ALLOWED_ORIGINS=http://localhost:5173,https://myapp.com  # Comma-separated, or * for all
QYL_OTLP_CORS_ALLOWED_HEADERS=x-otlp-api-key,content-type              # Optional headers
QYL_OTLP_AUTH_MODE=Unsecured                                           # ApiKey | Unsecured
QYL_OTLP_PRIMARY_API_KEY=                                              # Required if AuthMode=ApiKey
QYL_OTLP_SECONDARY_API_KEY=                                            # Optional rotation key
```

---

## Files

| File | Action | What |
|------|--------|------|
| `src/qyl.collector/Ingestion/OtlpCorsMiddleware.cs` | Create | CORS middleware for OTLP paths |
| `src/qyl.collector/Ingestion/OtlpApiKeyMiddleware.cs` | Create | API key validation for OTLP |
| `src/qyl.collector/Program.cs` | Modify | Register middleware, read config |

---

## Implementation

### Step 1: Create OTLP CORS Options

**File:** `src/qyl.collector/Ingestion/OtlpCorsOptions.cs`

```csharp
namespace qyl.collector.Ingestion;

/// <summary>
/// Configuration for OTLP endpoint CORS.
/// </summary>
public sealed class OtlpCorsOptions
{
    /// <summary>
    /// Allowed origins. Comma-separated list or "*" for all.
    /// Default: empty (CORS disabled).
    /// </summary>
    public string? AllowedOrigins { get; set; }

    /// <summary>
    /// Additional allowed headers beyond defaults.
    /// Default: content-type, x-otlp-api-key.
    /// </summary>
    public string? AllowedHeaders { get; set; }

    /// <summary>
    /// Max age for preflight cache in seconds.
    /// </summary>
    public int MaxAge { get; set; } = 86400; // 24 hours

    public bool IsEnabled => !string.IsNullOrWhiteSpace(AllowedOrigins);

    public IEnumerable<string> GetOrigins() =>
        AllowedOrigins?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? [];

    public IEnumerable<string> GetHeaders()
    {
        var defaults = new[] { "content-type", "x-otlp-api-key" };
        var custom = AllowedHeaders?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? [];
        return defaults.Concat(custom).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
```

### Step 2: Create OTLP CORS Middleware

**File:** `src/qyl.collector/Ingestion/OtlpCorsMiddleware.cs`

```csharp
namespace qyl.collector.Ingestion;

/// <summary>
/// CORS middleware specifically for OTLP endpoints.
/// Only applies to /v1/* paths (OTLP HTTP endpoints).
/// </summary>
public sealed class OtlpCorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly OtlpCorsOptions _options;
    private readonly HashSet<string> _allowedOrigins;
    private readonly string _allowedHeadersHeader;
    private readonly bool _allowAll;

    private static readonly string[] OtlpPaths = ["/v1/traces", "/v1/logs", "/v1/metrics"];

    public OtlpCorsMiddleware(RequestDelegate next, OtlpCorsOptions options)
    {
        _next = next;
        _options = options;
        _allowedOrigins = options.GetOrigins().ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allowedHeadersHeader = string.Join(", ", options.GetHeaders());
        _allowAll = _allowedOrigins.Contains("*");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only handle OTLP paths
        if (!IsOtlpPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault();

        // Handle preflight
        if (context.Request.Method == "OPTIONS")
        {
            if (IsOriginAllowed(origin))
            {
                SetCorsHeaders(context.Response, origin);
                context.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                context.Response.Headers["Access-Control-Max-Age"] = _options.MaxAge.ToString();
                context.Response.StatusCode = 204;
                return;
            }

            context.Response.StatusCode = 403;
            return;
        }

        // Handle actual request
        if (IsOriginAllowed(origin))
        {
            SetCorsHeaders(context.Response, origin);
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsOtlpPath(string path) =>
        OtlpPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private bool IsOriginAllowed(string? origin) =>
        !string.IsNullOrEmpty(origin) && (_allowAll || _allowedOrigins.Contains(origin));

    private void SetCorsHeaders(HttpResponse response, string? origin)
    {
        response.Headers["Access-Control-Allow-Origin"] = _allowAll ? "*" : origin;
        response.Headers["Access-Control-Allow-Headers"] = _allowedHeadersHeader;
        response.Headers["Access-Control-Allow-Credentials"] = "true";
    }
}
```

### Step 3: Create OTLP API Key Options

**File:** `src/qyl.collector/Ingestion/OtlpApiKeyOptions.cs`

```csharp
namespace qyl.collector.Ingestion;

/// <summary>
/// Configuration for OTLP endpoint authentication.
/// </summary>
public sealed class OtlpApiKeyOptions
{
    /// <summary>
    /// Auth mode: "ApiKey" or "Unsecured".
    /// </summary>
    public string AuthMode { get; set; } = "Unsecured";

    /// <summary>
    /// Primary API key for validation.
    /// </summary>
    public string? PrimaryApiKey { get; set; }

    /// <summary>
    /// Secondary API key for rotation.
    /// </summary>
    public string? SecondaryApiKey { get; set; }

    /// <summary>
    /// Header name for API key.
    /// </summary>
    public string HeaderName { get; set; } = "x-otlp-api-key";

    public bool IsApiKeyMode =>
        AuthMode.Equals("ApiKey", StringComparison.OrdinalIgnoreCase);

    public bool IsValidKey(string? key) =>
        !string.IsNullOrEmpty(key) &&
        (key == PrimaryApiKey || key == SecondaryApiKey);
}
```

### Step 4: Create OTLP API Key Middleware

**File:** `src/qyl.collector/Ingestion/OtlpApiKeyMiddleware.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace qyl.collector.Ingestion;

/// <summary>
/// API key authentication for OTLP endpoints.
/// </summary>
public sealed class OtlpApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly OtlpApiKeyOptions _options;
    private static readonly string[] OtlpPaths = ["/v1/traces", "/v1/logs", "/v1/metrics"];

    public OtlpApiKeyMiddleware(RequestDelegate next, OtlpApiKeyOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only validate OTLP paths when API key mode is enabled
        if (!_options.IsApiKeyMode || !IsOtlpPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Skip OPTIONS (preflight)
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var apiKey = context.Request.Headers[_options.HeaderName].FirstOrDefault();

        if (!ValidateApiKey(apiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Valid x-otlp-api-key header required"}"""
            ).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsOtlpPath(string path) =>
        OtlpPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private bool ValidateApiKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        var keyBytes = Encoding.UTF8.GetBytes(key);

        // Check primary key
        if (!string.IsNullOrEmpty(_options.PrimaryApiKey))
        {
            var primaryBytes = Encoding.UTF8.GetBytes(_options.PrimaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, primaryBytes))
                return true;
        }

        // Check secondary key
        if (!string.IsNullOrEmpty(_options.SecondaryApiKey))
        {
            var secondaryBytes = Encoding.UTF8.GetBytes(_options.SecondaryApiKey);
            if (CryptographicOperations.FixedTimeEquals(keyBytes, secondaryBytes))
                return true;
        }

        return false;
    }
}
```

### Step 5: Update Program.cs

**File:** `src/qyl.collector/Program.cs`

Add after existing configuration section (around line 30):

```csharp
// OTLP CORS configuration
var otlpCorsOptions = new OtlpCorsOptions
{
    AllowedOrigins = builder.Configuration["QYL_OTLP_CORS_ALLOWED_ORIGINS"],
    AllowedHeaders = builder.Configuration["QYL_OTLP_CORS_ALLOWED_HEADERS"],
};

// OTLP API key configuration
var otlpApiKeyOptions = new OtlpApiKeyOptions
{
    AuthMode = builder.Configuration["QYL_OTLP_AUTH_MODE"] ?? "Unsecured",
    PrimaryApiKey = builder.Configuration["QYL_OTLP_PRIMARY_API_KEY"],
    SecondaryApiKey = builder.Configuration["QYL_OTLP_SECONDARY_API_KEY"],
};

builder.Services.AddSingleton(otlpCorsOptions);
builder.Services.AddSingleton(otlpApiKeyOptions);
```

Add middleware registration after `var app = builder.Build();` (before token auth):

```csharp
// OTLP middleware (before token auth - OTLP has its own auth)
if (otlpCorsOptions.IsEnabled)
{
    app.UseMiddleware<OtlpCorsMiddleware>(otlpCorsOptions);
}
app.UseMiddleware<OtlpApiKeyMiddleware>(otlpApiKeyOptions);
```

### Step 6: Update Startup Banner

**File:** `src/qyl.collector/StartupBanner.cs`

Add to the banner output:

```csharp
// Add after existing endpoint logging
if (!string.IsNullOrEmpty(otlpCorsOptions.AllowedOrigins))
{
    Log.Information("  OTLP CORS: {Origins}", otlpCorsOptions.AllowedOrigins);
}
if (otlpApiKeyOptions.IsApiKeyMode)
{
    Log.Information("  OTLP Auth: API Key required (x-otlp-api-key header)");
}
else
{
    Log.Warning("  OTLP Auth: UNSECURED - telemetry accepted without authentication");
}
```

---

## Gotchas

- CORS middleware MUST come before API key middleware
- `OPTIONS` requests (preflight) should bypass API key check
- Use `CryptographicOperations.FixedTimeEquals` to prevent timing attacks
- `Access-Control-Allow-Credentials: true` needed for browsers sending headers
- When `AllowedOrigins` is `*`, don't echo the origin header

---

## Test

```bash
# Start collector with CORS enabled
QYL_OTLP_CORS_ALLOWED_ORIGINS=http://localhost:5173 \
dotnet run --project src/qyl.collector

# Test CORS preflight
curl -X OPTIONS http://localhost:4318/v1/traces \
  -H "Origin: http://localhost:5173" \
  -H "Access-Control-Request-Method: POST" \
  -v

# Test with API key
QYL_OTLP_AUTH_MODE=ApiKey \
QYL_OTLP_PRIMARY_API_KEY=test-key-123 \
dotnet run --project src/qyl.collector

curl -X POST http://localhost:4318/v1/traces \
  -H "x-otlp-api-key: test-key-123" \
  -H "Content-Type: application/json" \
  -d '{}'
```

- [ ] CORS preflight returns 204 with correct headers
- [ ] Request without origin header works (non-browser)
- [ ] Request with allowed origin gets CORS headers
- [ ] Request with disallowed origin gets 403
- [ ] API key mode rejects requests without key
- [ ] API key mode accepts valid primary key
- [ ] API key mode accepts valid secondary key
- [ ] No TS errors
- [ ] No console errors

---

## Browser Client Example

```typescript
// In browser app using @opentelemetry/exporter-trace-otlp-proto
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';

const exporter = new OTLPTraceExporter({
  url: 'http://localhost:4318/v1/traces',
  headers: {
    'x-otlp-api-key': 'your-api-key-here', // if API key mode enabled
  },
});
```

---

*Template v3 - One prompt, one agent, done.*
