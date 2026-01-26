---
paths:
  - "src/**/*.cs"
  - "**/*.csproj"
---

# .NET Development Rules

## Runtime & Language

- .NET 10.0 is LTS (since 2025-11-11)
- C# 14.0
- Use `net10.0` in TargetFramework

## SDK

Use ANcpLua.NET.Sdk variants:
- `ANcpLua.NET.Sdk` - libraries, console apps
- `ANcpLua.NET.Sdk.Web` - ASP.NET Core apps
- `ANcpLua.NET.Sdk.Test` - xUnit v3 test projects

## Banned APIs

NEVER use these legacy APIs - use modern alternatives instead:
- For current time: Use `TimeProvider.System.GetUtcNow()` (testable)
- For locking: Use `Lock _lock = new()` (C# 14 feature)
- For JSON: Use `System.Text.Json` (standard library)

## Patterns

**Locking (sync):**
```csharp
private readonly Lock _lock = new();
using (_lock.EnterScope()) { /* sync only */ }
```

**Locking (async):**
```csharp
private readonly SemaphoreSlim _asyncLock = new(1, 1);
await _asyncLock.WaitAsync(ct);
try { await DoWork(ct); }
finally { _asyncLock.Release(); }
```

**JSON options (CA1869):**
```csharp
private static readonly JsonSerializerOptions s_options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

**Time provider:**
```csharp
private readonly TimeProvider _timeProvider = TimeProvider.System;
var now = _timeProvider.GetUtcNow();
```
