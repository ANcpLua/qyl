# .NET 10 / C# 14 / C# 13 Mandatory Reference

> **THIS IS NOT OPTIONAL.** If you use older patterns, your code will be **REJECTED**.

---

## STOP. Before Writing ANY Code

You are on **.NET 10** (November 2025 LTS, supported through November 2028) with **C# 14**.

**Do NOT use:**

- C# 11/12 patterns
- .NET 6/7/8/9 workarounds
- Pre-LINQ aggregation (GroupBy → ToDictionary)
- Manual lock objects (`object _lock`)
- Legacy SSE formatting
- DateTime.UtcNow (use TimeProvider)
- BCL ArgumentXxxException.ThrowIf* (use qyl Throw.*)

---

## qyl Framework Integration (MASTER.md Alignment)

This reference is specifically aligned with the **qyl AI Observability Platform**:

### OTel SemConv 1.38 Required Attributes

```
gen_ai.provider.name       (REQUIRED)
gen_ai.request.model       (REQUIRED)
gen_ai.response.model      (RECOMMENDED)
gen_ai.operation.name      (REQUIRED)
gen_ai.usage.input_tokens  (REQUIRED)
gen_ai.usage.output_tokens (REQUIRED)
gen_ai.usage.total_tokens  (RECOMMENDED)
```

### Deprecated Fields - REJECT IMMEDIATELY

```
gen_ai.system              → gen_ai.provider.name
gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens
gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens
```

### Module-Specific API Requirements

| Module               | Required APIs                                                                                                      |
|----------------------|--------------------------------------------------------------------------------------------------------------------|
| Receivers/Processing | `Task.WhenEach()`, `IUtf8SpanParsable<T>`, `Lock`, `CountBy()`, `AggregateBy()`, `Index()`, `SearchValues<string>` |
| Storage              | `OrderedDictionary<K,V>`, `FrozenDictionary<K,V>`                                                                  |
| API                  | `JsonNamingPolicy.SnakeCaseLower`, `JsonSerializerOptions.Strict`                                                  |
| Streaming            | `TypedResults.ServerSentEvents()`, `SseItem<T>`, `IAsyncEnumerable<T>`                                             |
| Instrumentation      | `[LogProperties]`, `[TagName]`, `ILogEnricher`                                                                     |

---

## C# 14 Features (USE THESE)

### 1. Extension Members (NEW - Use for Activity/Span enrichment)

> **SYNTAX:** C# 14 uses `extension(Type paramName)` blocks. The parameter name is used inside the block to access the
> instance.

```csharp
// OLD - Static extension methods scattered everywhere
public static class ActivityExtensions
{
    public static bool IsError(this Activity activity) =>
        activity.Status == ActivityStatusCode.Error;
    public static double GetDurationMs(this Activity activity) =>
        activity.Duration.TotalMilliseconds;
}

// NEW - C# 14 Extension Blocks (MANDATORY)
public static class ActivityExtensions
{
    // Instance extension block - parameter name used inside
    extension(Activity activity)
    {
        // Properties feel native
        public bool IsError => activity.Status == ActivityStatusCode.Error;
        public double DurationMs => activity.Duration.TotalMilliseconds;
        public bool IsRootSpan => activity.Parent == null;
        public string? ProviderName => activity.GetTagItem("gen_ai.provider.name")?.ToString();
        public string? RequestModel => activity.GetTagItem("gen_ai.request.model")?.ToString();
        public long InputTokens => activity.GetTagItem("gen_ai.usage.input_tokens") as long? ?? 0;
        public long OutputTokens => activity.GetTagItem("gen_ai.usage.output_tokens") as long? ?? 0;
        public long TotalTokens => InputTokens + OutputTokens;

        // Fluent methods
        public Activity RecordException(Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.stacktrace", ex.StackTrace);
            return activity;
        }

        public Activity SetGenAiTags(string provider, string model, long inputTokens, long outputTokens)
        {
            activity.SetTag("gen_ai.provider.name", provider);
            activity.SetTag("gen_ai.request.model", model);
            activity.SetTag("gen_ai.usage.input_tokens", inputTokens);
            activity.SetTag("gen_ai.usage.output_tokens", outputTokens);
            return activity;
        }
    }

    // Static extension block - receiver type only (no parameter name)
    extension(Activity)
    {
        public static Activity? StartGenAiSpan(ActivitySource source, string operation) =>
            source.StartActivity(operation, ActivityKind.Client);
    }
}

// Generic extension blocks
public static class EnumerableExtensions
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public bool IsEmpty => !source.Any();
        
        public IEnumerable<TSource> WhereNotNull() where TSource : class =>
            source.Where(x => x is not null)!;
    }
}

// Usage - feels like native properties/methods
if (activity.IsError && activity.DurationMs > 1000)
{
    logger.LogWarning("Slow error span: {Provider} used {Tokens} tokens", 
        activity.ProviderName, activity.TotalTokens);
}
activity.SetGenAiTags("anthropic", "claude-sonnet-4-20250514", 1500, 2000);
```

### 2. Field Keyword (NEW - Use for DTOs with validation)

```csharp
// OLD - Explicit backing fields everywhere
public class SpanData
{
    private string _spanId = null!;
    private long _startTime;

    public string SpanId
    {
        get => _spanId;
        set => _spanId = value ?? throw new ArgumentNullException(nameof(value));
    }

    public long StartTimeUnixNano
    {
        get => _startTime;
        set => _startTime = value >= 0 ? value : throw new ArgumentOutOfRangeException();
    }
}

// NEW - C# 14 field keyword (MANDATORY)
public class SpanData
{
    public string SpanId
    {
        get;
        set => field = Throw.IfNullOrWhitespace(value);  // Use qyl Throw.*
    }

    public long StartTimeUnixNano
    {
        get;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException();
    }

    // Lazy initialization pattern
    public string CachedHash
    {
        get => field ??= ComputeHash();
        set => field = value;
    }

    // With qyl validation
    public int TokenCount
    {
        get;
        set => field = Throw.IfLessThan(value, 0);
    }
}

// CAUTION: If your class has a member named 'field', use @field to escape
// or rename the existing member to avoid shadowing.
```

### 3. Implicit Span Conversions (NEW - Zero allocation parsing)

```csharp
// OLD - Allocates substring
void ParseTraceHeader(string header)
{
    var traceId = header.Substring(3, 32);  // ALLOCATION!
    var spanId = header.Substring(36, 16);  // ALLOCATION!
}

// NEW - C# 14 implicit span conversion (MANDATORY for parsing)
void ParseTraceHeader(ReadOnlySpan<char> header)
{
    ReadOnlySpan<char> traceId = header[3..35];   // Zero allocation
    ReadOnlySpan<char> spanId = header[36..52];   // Zero allocation
}

// Implicit conversion from string - no ceremony needed
string rawHeader = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
ParseTraceHeader(rawHeader);  // Just works, no cast needed

// Works with arrays too
string[] words = ["Hello", "World"];
ReadOnlySpan<string> span = words;  // Implicit conversion
```

### 4. Null-Conditional Assignment (NEW)

```csharp
// OLD
if (activity != null)
{
    activity.DisplayName = "GenAI Operation";
}

// NEW - C# 14
activity?.DisplayName = "GenAI Operation";

// Works with indexers too
spans?[0].Status = ActivityStatusCode.Ok;

// Compound assignment
customer?.LoyaltyPoints += 10;

// NOTE: Increment (++) and decrement (--) are NOT supported
// customer?.Points++;  // COMPILE ERROR
```

### 5. User-Defined Compound Assignment Operators (NEW - Performance)

```csharp
// OLD - += always creates new instance via operator+
public readonly struct TokenUsage
{
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new() { InputTokens = a.InputTokens + b.InputTokens, 
                OutputTokens = a.OutputTokens + b.OutputTokens };
}
// usage += other; creates new instance each time

// NEW - C# 14 compound operator mutates in place (for mutable types)
public class TokenCounter
{
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }

    // Traditional operator (still needed for a + b)
    public static TokenCounter operator +(TokenCounter a, TokenCounter b) =>
        new() { InputTokens = a.InputTokens + b.InputTokens, 
                OutputTokens = a.OutputTokens + b.OutputTokens };

    // NEW: Compound assignment mutates in place - no allocation!
    public void operator +=(TokenCounter other)
    {
        InputTokens += other.InputTokens;
        OutputTokens += other.OutputTokens;
    }

    public void operator +=(long tokens)
    {
        OutputTokens += tokens;
    }

    public void operator ++()
    {
        InputTokens++;
        OutputTokens++;
    }
}

// Now counter += other modifies counter in place
```

### 6. Partial Constructors and Events (NEW)

```csharp
// Useful with source generators
public partial class TelemetryService
{
    // Defining declaration
    public partial TelemetryService(ILogger<TelemetryService> logger);
    public partial event EventHandler<SpanReceivedEventArgs>? SpanReceived;
}

public partial class TelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    // Implementing declaration (only here can have constructor initializer)
    public partial TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = Throw.IfNull(logger);
    }

    public partial event EventHandler<SpanReceivedEventArgs>? SpanReceived
    {
        add => _handlers.Add(value);
        remove => _handlers.Remove(value);
    }
}
```

---

## C# 13 Features (USE THESE)

### 7. params Collections (Not just arrays)

```csharp
// OLD - params only worked with arrays
public void AddTags(params KeyValuePair<string, object?>[] tags) { }

// NEW - C# 13 params works with any collection type (MANDATORY)
public void AddTags(params ReadOnlySpan<KeyValuePair<string, object?>> tags)
{
    foreach (var tag in tags)
        _activity.SetTag(tag.Key, tag.Value);
}

// Compiler picks best overload (Span = no allocation)
AddTags(
    new("gen_ai.provider.name", "anthropic"),
    new("gen_ai.request.model", "claude-sonnet-4-20250514")
);
```

### 8. Lock Class (MANDATORY - replaces object locks)

```csharp
// OLD - REJECT THIS CODE
private readonly object _lock = new object();
lock (_lock) { /* ... */ }

// NEW - C# 13 / .NET 9+ Lock class (MANDATORY)
private readonly Lock _lock = new();

// Standard lock syntax works
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

### 9. Collection Expressions (MANDATORY for all collections)

```csharp
// OLD - NEVER use these patterns
var list = new List<int>();                    // NO
var list = new List<int> { 1, 2, 3 };         // NO
List<int> list = new();                        // NO (target-typed new)
var arr = new int[] { 1, 2, 3 };              // NO

// NEW - C# 12+ Collection Expressions (MANDATORY)
List<int> list = [];                           // Empty collection
List<int> list = [1, 2, 3];                   // With elements
int[] arr = [1, 2, 3];                        // Arrays
Dictionary<string, int> dict = [];             // Empty dictionary
HashSet<string> set = ["a", "b"];             // Sets

// Spread operator for combining
List<SpanData> allSpans = [..baseSpans, ..newSpans];
string[] combined = ["a", "b", ..moreItems];

// Target-typed new is ONLY for non-collection classes
Lock _lock = new();                            // OK - not a collection
CancellationTokenSource cts = new();           // OK - not a collection
var processor = new SpanProcessor();           // OK - not a collection
```

### 10. ref/unsafe in Iterators and Async (NEW)

```csharp
// OLD - Couldn't use ref in async methods
async Task ProcessSpansAsync(SpanData[] spans)
{
    foreach (var span in spans)  // Copies for structs
    {
        await ProcessAsync(span);
    }
}

// NEW - C# 13 allows ref in async/iterators
async Task ProcessSpansAsync(SpanData[] spans)
{
    ref SpanData first = ref spans[0];  // Now allowed!
    Span<SpanData> slice = spans.AsSpan()[..10];  // Span in async!

    foreach (ref readonly var span in spans.AsSpan())
    {
        // Zero-copy iteration for structs
        await ProcessAsync(in span);
    }
}
```

---

## .NET 10 APIs (MANDATORY)

### 11. CountBy / AggregateBy (MANDATORY for aggregation)

```csharp
// OLD - REJECT THIS PATTERN
var providerCounts = spans
    .GroupBy(s => s.ProviderName)
    .ToDictionary(g => g.Key, g => g.Count());

var tokensByProvider = spans
    .GroupBy(s => s.ProviderName)
    .ToDictionary(g => g.Key, g => g.Sum(s => s.InputTokens + s.OutputTokens));

// NEW - .NET 9+ CountBy/AggregateBy (MANDATORY)
var providerCounts = spans.CountBy(s => s.ProviderName);
// Returns: IEnumerable<KeyValuePair<string?, int>>

var tokensByProvider = spans.AggregateBy(
    keySelector: s => s.ProviderName,
    seed: 0L,
    func: (total, span) => total + span.InputTokens + span.OutputTokens
);
// Returns: IEnumerable<KeyValuePair<string?, long>>

// With Index() for position tracking
foreach (var (index, span) in spans.Index())
{
    Log.SpanProcessed(_logger, index, span.SpanId);
}
```

### 12. Task.WhenEach (MANDATORY for parallel processing)

```csharp
// OLD - Complex WhenAny loops
var tasks = new List<Task<SpanData>>(pendingExports);
while (tasks.Count > 0)
{
    var completed = await Task.WhenAny(tasks);
    tasks.Remove(completed);
    ProcessResult(await completed);
}

// NEW - .NET 9+ Task.WhenEach (MANDATORY)
var tasks = spans.Select(s => ExportSpanAsync(s));

await foreach (var completedTask in Task.WhenEach(tasks))
{
    var result = await completedTask;
    ProcessResult(result);
}
```

### 13. SearchValues (Character Detection & Substring Search)

> **CRITICAL:** `SearchValues<string>` does **SUBSTRING matching**, NOT prefix matching!
> Using `SearchValues<string>` for prefix detection like "gen_ai." will **incorrectly match**
> strings like `"my_custom_ai.field"` because "ai." appears as a substring.

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// SearchValues<char> - for single character detection (.NET 8+)
// ═══════════════════════════════════════════════════════════════════════════

// Delimiter detection
private static readonly SearchValues<char> Delimiters = SearchValues.Create(".,;:!?|_-");
int FindDelimiter(ReadOnlySpan<char> text) => text.IndexOfAny(Delimiters);

// Hex validation
private static readonly SearchValues<char> HexChars = SearchValues.Create("0123456789abcdefABCDEF");
bool IsValidHex(ReadOnlySpan<char> value) => !value.IsEmpty && !value.ContainsAnyExcept(HexChars);

// ═══════════════════════════════════════════════════════════════════════════
// SearchValues<string> - for SUBSTRING search only (.NET 9+)
// Use when keywords can appear ANYWHERE in the string
// ═══════════════════════════════════════════════════════════════════════════

// ✅ CORRECT: Keywords that can appear anywhere
private static readonly SearchValues<string> ErrorKeywords = SearchValues.Create(
    ["error", "failed", "timeout", "exception"],
    StringComparison.OrdinalIgnoreCase);

bool ContainsErrorKeyword(string message) => message.AsSpan().ContainsAny(ErrorKeywords);

// ✅ CORRECT: Model names in free-form text
private static readonly SearchValues<string> ClaudeModels = SearchValues.Create(
    ["claude-opus", "claude-sonnet", "claude-haiku"],
    StringComparison.OrdinalIgnoreCase);

bool MentionsClaude(string text) => text.AsSpan().ContainsAny(ClaudeModels);

// ═══════════════════════════════════════════════════════════════════════════
// PREFIX DETECTION - Direct StartsWith is correct (NOT SearchValues!)
// ═══════════════════════════════════════════════════════════════════════════

// ✅ CORRECT: Direct span checks (fastest for 2-4 prefixes)
[MethodImpl(MethodImplOptions.AggressiveInlining)]
static bool IsGenAiAttribute(ReadOnlySpan<char> key) =>
    key.StartsWith("gen_ai.") ||
    key.StartsWith("agents.") ||
    key.StartsWith("llm.");

// ✅ CORRECT: FrozenSet iteration (readable for many prefixes)
private static readonly FrozenSet<string> PromotedPrefixes = new[]
{
    "gen_ai.", "agents.", "session.", "service.", "deployment.", "exception."
}.ToFrozenSet(StringComparer.Ordinal);

static bool IsPromotedAttribute(string key)
{
    foreach (var prefix in PromotedPrefixes)
    {
        if (key.StartsWith(prefix, StringComparison.Ordinal))
            return true;
    }
    return false;
}

// ❌ WRONG: SearchValues<string> for prefixes - matches ANYWHERE!
// private static readonly SearchValues<string> GenAiPrefixes =
//     SearchValues.Create(["gen_ai.", "llm."], StringComparison.Ordinal);
// bool IsGenAi = key.ContainsAny(GenAiPrefixes);  // Matches "custom_ai.x" = BUG!
```

### 14. OrderedDictionary<K,V> (MANDATORY when order matters)

```csharp
// OLD - Dictionary doesn't preserve order
var attributes = new Dictionary<string, object?>();

// NEW - .NET 9+ generic OrderedDictionary (MANDATORY for deterministic output)
var attributes = new OrderedDictionary<string, object?>
{
    ["gen_ai.provider.name"] = "anthropic",
    ["gen_ai.request.model"] = "claude-sonnet-4-20250514",
    ["gen_ai.operation.name"] = "chat"
};

// Maintains insertion order in serialization
// Index-based access
var (key, value) = attributes.GetAt(0);
attributes.SetAt(0, "updated_value");

// TryAdd with index
if (attributes.TryAdd("new_key", "value", out int index))
{
    Console.WriteLine($"Added at index {index}");
}
```

### 15. FrozenDictionary / FrozenSet (MANDATORY for read-only lookups)

```csharp
// For immutable lookup tables - optimized read performance
private static readonly FrozenDictionary<string, string> DeprecatedMappings =
    new Dictionary<string, string>
    {
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",
    }.ToFrozenDictionary();

private static readonly FrozenSet<string> PromotedFields = new[]
{
    "gen_ai.provider.name",
    "gen_ai.request.model",
    "gen_ai.response.model",
    "gen_ai.operation.name",
    "gen_ai.usage.input_tokens",
    "gen_ai.usage.output_tokens",
    "session.id",
}.ToFrozenSet();

// AlternateLookup for zero-allocation span lookups (.NET 9+)
var lookup = DeprecatedMappings.GetAlternateLookup<ReadOnlySpan<char>>();
if (lookup.TryGetValue(keySpan, out var replacement))
{
    // Found without allocating a string from the span
}
```

### 16. TypedResults.ServerSentEvents (MANDATORY for SSE)

```csharp
// OLD - Manual SSE formatting - REJECT THIS
app.MapGet("/api/v1/live", async (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";  // NO!
    ctx.Response.Headers.CacheControl = "no-cache";
    await foreach (var evt in GetEvents())
    {
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(evt)}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
});

// NEW - .NET 9+ TypedResults.ServerSentEvents (MANDATORY)
app.MapGet("/api/v1/live", (CancellationToken ct) =>
    TypedResults.ServerSentEvents(GetTelemetryEventsAsync(ct), eventType: "telemetry"));

async IAsyncEnumerable<SseItem<TelemetryEvent>> GetTelemetryEventsAsync(
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var evt in _broadcaster.GetEventsAsync(ct))
    {
        yield return new SseItem<TelemetryEvent>(evt, "telemetry");
    }
}
```

### 17. JsonNamingPolicy.SnakeCaseLower (MANDATORY)

```csharp
// OLD - Custom snake_case converter - REJECT
public class SnakeCaseNamingPolicy : JsonNamingPolicy { /* 50 lines */ }

// NEW - .NET 8+ built-in (MANDATORY)
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// For Native AOT - source generated context
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SpanData))]
[JsonSerializable(typeof(SessionStats))]
[JsonSerializable(typeof(TelemetryEvent))]
internal partial class QylJsonContext : JsonSerializerContext { }
```

### 18. JsonSerializerOptions.Strict (NEW in .NET 10)

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

// Disallow duplicate properties explicitly
string json = """{ "value": 1, "value": -1 }""";
var opts = new JsonSerializerOptions { AllowDuplicateProperties = false };
JsonSerializer.Deserialize<MyRecord>(json, opts);  // throws JsonException
```

### 19. JsonSerializer PipeReader Support (NEW in .NET 10)

```csharp
// NEW - Direct PipeReader deserialization (zero-copy streaming)
var pipe = new Pipe();

// Deserialize directly from PipeReader - no Stream conversion
var result = await JsonSerializer.DeserializeAsync<SpanData>(
    pipe.Reader, 
    QylJsonContext.Default.SpanData);

// Streaming deserialization
await foreach (var span in JsonSerializer.DeserializeAsyncEnumerable<SpanData>(
    pipe.Reader,
    QylJsonContext.Default.SpanData))
{
    await ProcessSpanAsync(span);
}
```

### 20. HybridCache (MANDATORY for caching)

```csharp
// OLD - Separate memory/distributed cache logic
public async Task<SessionStats?> GetSessionAsync(string id)
{
    if (_memoryCache.TryGetValue(id, out SessionStats? cached))
        return cached;
    var bytes = await _distributedCache.GetAsync(id);
    // ... manual deserialization and cache population
}

// NEW - .NET 9+ HybridCache (MANDATORY)
public class SessionService(HybridCache cache)
{
    public async Task<SessionStats> GetSessionAsync(string sessionId, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            $"session:{sessionId}",
            async cancel => await LoadSessionFromDbAsync(sessionId, cancel),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: ct
        );
    }

    // With tags for bulk invalidation (.NET 10)
    public async Task<List<SpanData>> GetSpansByProviderAsync(string provider, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            $"spans:provider:{provider}",
            async cancel => await LoadSpansAsync(provider, cancel),
            tags: [$"provider:{provider}", "spans"],
            cancellationToken: ct
        );
    }

    public async Task InvalidateProviderAsync(string provider, CancellationToken ct) =>
        await cache.RemoveByTagAsync($"provider:{provider}", ct);
}

// Registration
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;  // 1MB
    options.MaximumKeyLength = 512;
    options.DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});
```

### 21. TimeProvider (MANDATORY for testable time)

```csharp
// OLD - DateTime.UtcNow scattered everywhere (untestable)
var startTime = DateTime.UtcNow;
var elapsed = DateTime.UtcNow - startTime;

// NEW - .NET 8+ TimeProvider (MANDATORY)
public class SessionAggregator(TimeProvider timeProvider)
{
    public void RecordSpan(SpanData span)
    {
        var now = timeProvider.GetUtcNow();
        var elapsed = now - span.StartTime;

        // Testable timers
        using var timer = timeProvider.CreateTimer(
            callback: _ => FlushStats(),
            state: null,
            dueTime: TimeSpan.FromSeconds(30),
            period: TimeSpan.FromSeconds(30)
        );
    }
}

// In tests
var fakeTime = new FakeTimeProvider(startDateTime);
var aggregator = new SessionAggregator(fakeTime);
fakeTime.Advance(TimeSpan.FromMinutes(5));  // Time travel!
```

---

## qyl-Specific Patterns (MANDATORY)

### 22. Throw.* Validation Helpers (MANDATORY - use instead of BCL)

> **Source:** `src/Shared/Throw/Throw.cs`  
> **ALWAYS use Throw.* instead of ArgumentNullException.ThrowIfNull, etc.**

```csharp
// Location: qyl.Shared namespace

// OLD - BCL ThrowIf methods (DON'T USE in qyl codebase)
public void Process(string input, int count)
{
    ArgumentNullException.ThrowIfNull(input);
    ArgumentException.ThrowIfNullOrEmpty(input);
    ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);
}

// NEW - qyl Throw.* helpers (MANDATORY)
using qyl.Shared;

public void Process(string input, int count)
{
    Throw.IfNull(input);                    // Returns input if valid
    Throw.IfNullOrEmpty(input);             // Returns string, throws if null/empty
    Throw.IfNullOrWhitespace(input);        // Returns string, throws if null/empty/whitespace
    Throw.IfLessThan(count, 0);             // Returns int if >= 0
    Throw.IfGreaterThan(count, 100);        // Returns int if <= 100
    Throw.IfOutOfRange(count, 0, 100);      // Returns int if in [0..100]
}

// KEY ADVANTAGE: Throw.* methods RETURN the validated value
var validInput = Throw.IfNullOrWhitespace(input);
var validCount = Throw.IfOutOfRange(count, 1, 1000);

// Use in constructors with primary constructor pattern
public class SpanProcessor(string name, int batchSize)
{
    private readonly string _name = Throw.IfNullOrWhitespace(name);
    private readonly int _batchSize = Throw.IfOutOfRange(batchSize, 1, 10000);
}

// Combine with C# 14 field keyword
public class SpanData
{
    public string SpanId
    {
        get;
        set => field = Throw.IfNullOrWhitespace(value);
    }

    public int TokenCount
    {
        get;
        set => field = Throw.IfLessThan(value, 0);
    }
}

// Available methods (from Throw.cs):
// Throw.IfNull<T>(T argument)                      → returns T, throws ArgumentNullException
// Throw.IfNullOrEmpty(string? argument)            → returns string, throws ArgumentNullException/ArgumentException
// Throw.IfNullOrWhitespace(string? argument)       → returns string, throws ArgumentNullException/ArgumentException
// Throw.IfLessThan(int argument, int min)          → returns int, throws ArgumentOutOfRangeException
// Throw.IfGreaterThan(int argument, int max)       → returns int, throws ArgumentOutOfRangeException
// Throw.IfOutOfRange(int argument, int min, int max) → returns int, throws ArgumentOutOfRangeException

// All methods use [CallerArgumentExpression] for automatic parameter names
// All methods use [MethodImpl(AggressiveInlining)] for performance
// All methods use [DoesNotReturn] on throw helpers for flow analysis
```

---

## High-Performance Patterns for Telemetry

### 23. Strongly-Typed IDs (Zero overhead, compile-time safety)

```csharp
// OLD - Everything is string, easy to mix up
public void ProcessSpan(string traceId, string spanId, string parentSpanId, string sessionId)
{
    // Which is which? Easy to pass wrong one
}

// NEW - readonly record struct IDs (MANDATORY for IDs)
public readonly record struct TraceId(string Value) : 
    ISpanParsable<TraceId>,
    IUtf8SpanParsable<TraceId>
{
    public static TraceId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(s.ToString());
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TraceId result)
    {
        result = new(s.ToString());
        return true;
    }
    
    // UTF-8 for zero-allocation JSON parsing
    public static TraceId Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => 
        new(Encoding.UTF8.GetString(utf8Text));
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out TraceId result)
    {
        result = new(Encoding.UTF8.GetString(utf8Text));
        return true;
    }
    
    public static TraceId Parse(string s, IFormatProvider? provider) => new(s);
    public static bool TryParse(string? s, IFormatProvider? provider, out TraceId result)
    {
        result = s is null ? default : new(s);
        return s is not null;
    }
    
    public override string ToString() => Value;
}

public readonly record struct SpanId(string Value);
public readonly record struct SessionId(string Value);

// Now compiler catches mistakes
public void ProcessSpan(TraceId traceId, SpanId spanId, SpanId? parentSpanId, SessionId sessionId)
{
    // Can't accidentally pass SpanId where TraceId expected!
}

// Zero overhead - same size as underlying type
```

### 24. ISpanParsable / IUtf8SpanParsable (Zero-allocation parsing)

```csharp
// OLD - String.Split allocates arrays
var parts = line.Split('|');  // ALLOCATES!

// NEW - Zero-allocation field extraction
static ReadOnlySpan<char> ReadField(ref ReadOnlySpan<char> line, char delimiter = '|')
{
    var i = line.IndexOf(delimiter);
    if (i < 0)
    {
        var field = line;
        line = [];
        return field;
    }
    var result = line[..i];
    line = line[(i + 1)..];
    return result;
}

public static SpanRecord ParseLine(ReadOnlySpan<char> line)
{
    var traceId = TraceId.Parse(ReadField(ref line), null);
    var spanId = SpanId.Parse(ReadField(ref line), null);
    var startNano = long.Parse(ReadField(ref line), CultureInfo.InvariantCulture);
    var name = ReadField(ref line).ToString();  // Only allocate what we must store
    return new SpanRecord(traceId, spanId, startNano, name);
}
```

### 25. ValueTask (Avoid allocation on sync paths)

```csharp
// OLD - Task<T> always allocates
public async Task<SessionStats?> GetSessionAsync(string sessionId)
{
    if (_cache.TryGetValue(sessionId, out var cached))
        return cached;  // STILL allocates Task!
    return await LoadFromDatabaseAsync(sessionId);
}

// NEW - ValueTask avoids allocation on cache hit (MANDATORY for hot paths)
public ValueTask<SessionStats?> GetSessionAsync(string sessionId)
{
    if (_cache.TryGetValue(sessionId, out var cached))
        return ValueTask.FromResult<SessionStats?>(cached);  // No allocation
    return new ValueTask<SessionStats?>(LoadFromDatabaseAsync(sessionId));
}
```

### 26. ConfigureAwait(false) in Library Code

```csharp
// In SDK/Library code (qyl.agents.telemetry, etc.)
// DON'T use in ASP.NET Core controllers (no SyncContext anyway)

// MANDATORY for SDKs and library code
public async Task<ExportResult> ExportAsync(SpanData[] spans)
{
    var batch = await PrepareBatchAsync(spans).ConfigureAwait(false);
    return await SendAsync(batch).ConfigureAwait(false);
}
```

### 27. readonly record struct for DTOs

```csharp
// OLD - class allocates on heap
public class TokenUsage { public int Input; public int Output; }

// NEW - readonly record struct (MANDATORY for small DTOs)
public readonly record struct TokenUsage(long InputTokens, long OutputTokens)
{
    public long TotalTokens => InputTokens + OutputTokens;
}

// Benefits: stack allocation, value equality, immutable, with-expressions
var updated = usage with { OutputTokens = 250 };
```

---

## Native AOT Requirements

When targeting Native AOT (qyl.collector), these are **MANDATORY**:

```csharp
// 1. JSON Source Generation (MANDATORY)
[JsonSerializable(typeof(SpanData))]
[JsonSerializable(typeof(LogRecord))]
[JsonSerializable(typeof(SessionStats))]
[JsonSerializable(typeof(TelemetryEvent))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
internal partial class QylJsonContext : JsonSerializerContext { }

// 2. Logging Source Generation (MANDATORY)
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Span received: {TraceId}/{SpanId} from {Provider}")]
    public static partial void SpanReceived(
        ILogger logger, string traceId, string spanId, string provider);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Deprecated attribute detected: {OldName} -> {NewName}")]
    public static partial void DeprecatedAttribute(
        ILogger logger, string oldName, string newName);
}

// 3. Trim-safe reflection (use typeof, not GetType())
var props = typeof(SpanData).GetProperties();  // GOOD
// var props = obj.GetType().GetProperties();  // BAD - may be trimmed

// 4. No dynamic, no COM, no binary serialization
```

---

## Quick Reference Card

```
┌─────────────────────────────────────────────────────────────────┐
│ .NET 10 / C# 14 MANDATORY PATTERNS                              │
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
│ FIELD:        set => field = validated;                         │
│ PARAMS:       params ReadOnlySpan<T>                            │
│ COLLECTIONS:  List<T> list = []; NOT new()                      │
│ SPREAD:       [..existing, ..more]                              │
│ COMPOUND OP:  public void operator +=(T other)                  │
├─────────────────────────────────────────────────────────────────┤
│ qyl-SPECIFIC (MANDATORY)                                        │
├─────────────────────────────────────────────────────────────────┤
│ VALIDATION:   Throw.IfNull(), Throw.IfNullOrEmpty()             │
│               Throw.IfNullOrWhitespace()                        │
│               Throw.IfLessThan(), Throw.IfGreaterThan()         │
│               Throw.IfOutOfRange()                              │
│               (NOT ArgumentNullException.ThrowIfNull!)          │
├─────────────────────────────────────────────────────────────────┤
│ OTel 1.38 REQUIRED ATTRIBUTES                                   │
├─────────────────────────────────────────────────────────────────┤
│ gen_ai.provider.name      (NOT gen_ai.system!)                  │
│ gen_ai.request.model                                            │
│ gen_ai.operation.name                                           │
│ gen_ai.usage.input_tokens (NOT prompt_tokens!)                  │
│ gen_ai.usage.output_tokens (NOT completion_tokens!)             │
├─────────────────────────────────────────────────────────────────┤
│ HIGH-PERFORMANCE TELEMETRY                                      │
├─────────────────────────────────────────────────────────────────┤
│ TYPED IDs:    readonly record struct TraceId(string Value)      │
│ PARSING:      ISpanParsable<T> + IUtf8SpanParsable<T>           │
│ ASYNC CACHE:  ValueTask<T> for sync fast paths                  │
│ LIB ASYNC:    .ConfigureAwait(false) in SDK code                │
│ SMALL DTOs:   readonly record struct                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Patterns to REJECT (With Corrections)

| REJECT                                           | USE INSTEAD                            | Why                      |
|--------------------------------------------------|----------------------------------------|--------------------------|
| `private readonly object _lock = new();`         | `private readonly Lock _lock = new();` | Lock class is faster     |
| `.GroupBy().ToDictionary(g => g.Count())`        | `.CountBy()`                           | Single pass              |
| `Task.WhenAny` in loops                          | `Task.WhenEach()`                      | Cleaner                  |
| `ctx.Response.ContentType = "text/event-stream"` | `TypedResults.ServerSentEvents()`      | Type-safe                |
| `new Dictionary<K,V>()` when order matters       | `new OrderedDictionary<K,V>()`         | Deterministic            |
| `DateTime.UtcNow`                                | `TimeProvider.GetUtcNow()`             | Testable                 |
| Custom snake_case policy                         | `JsonNamingPolicy.SnakeCaseLower`      | Built-in                 |
| Explicit backing fields                          | `field` keyword                        | Less boilerplate         |
| `new List<T>()` or `List<T> x = new()`           | `List<T> x = []`                       | Collection expressions   |
| `new[] { 1, 2, 3 }`                              | `[1, 2, 3]`                            | Collection expressions   |
| `string traceId, string spanId`                  | `TraceId traceId, SpanId spanId`       | Type safety              |
| `string.Split()` in hot paths                    | `ISpanParsable` + span                 | Zero allocation          |
| `ArgumentNullException.ThrowIfNull`              | `Throw.IfNull()`                       | qyl helper returns value |
| `ArgumentException.ThrowIfNullOrEmpty`           | `Throw.IfNullOrEmpty()`                | qyl helper returns value |
| `Task<T>` for cache hits                         | `ValueTask<T>`                         | No allocation on sync    |
| `class` for small DTOs                           | `readonly record struct`               | Stack allocation         |
| `gen_ai.system`                                  | `gen_ai.provider.name`                 | OTel 1.38                |
| `gen_ai.usage.prompt_tokens`                     | `gen_ai.usage.input_tokens`            | OTel 1.38                |
| `gen_ai.usage.completion_tokens`                 | `gen_ai.usage.output_tokens`           | OTel 1.38                |

---

## If You See These, FIX IMMEDIATELY

```csharp
// 1. Object lock
private readonly object _lock = new();  // FIX: Lock _lock = new();

// 2. GroupBy for counting
.GroupBy(x => x.Key).ToDictionary(g => g.Count())  // FIX: .CountBy(x => x.Key)

// 3. Manual SSE
ctx.Response.ContentType = "text/event-stream"  // FIX: TypedResults.ServerSentEvents()

// 4. Deprecated OTel attributes
"gen_ai.system"                    // FIX: "gen_ai.provider.name"
"gen_ai.usage.prompt_tokens"       // FIX: "gen_ai.usage.input_tokens"
"gen_ai.usage.completion_tokens"   // FIX: "gen_ai.usage.output_tokens"

// 5. DateTime.UtcNow
var now = DateTime.UtcNow;  // FIX: TimeProvider.GetUtcNow()

// 6. Target-typed new for collections
List<int> list = new();            // FIX: List<int> list = [];
var arr = new int[] { 1, 2, 3 };   // FIX: int[] arr = [1, 2, 3];
var dict = new Dictionary<K,V>();  // FIX: Dictionary<K,V> dict = [];

// 7. Raw string IDs
void Process(string traceId, string spanId)  // FIX: (TraceId traceId, SpanId spanId)

// 8. Split in hot paths
var parts = line.Split('|');  // FIX: ISpanParsable + ReadOnlySpan<char>

// 9. Task<T> for cache hits
public async Task<T?> Get(string key)  // FIX: ValueTask<T?> Get(string key)

// 10. Missing ConfigureAwait in library code
await DoWorkAsync();  // FIX: await DoWorkAsync().ConfigureAwait(false);

// 11. Class for small DTOs
public class TokenUsage { ... }  // FIX: public readonly record struct TokenUsage(...)

// 12. BCL validation instead of qyl Throw.*
ArgumentNullException.ThrowIfNull(arg);     // FIX: Throw.IfNull(arg)
ArgumentException.ThrowIfNullOrEmpty(str);  // FIX: Throw.IfNullOrEmpty(str)
ArgumentOutOfRangeException.ThrowIfLessThan(n, 0);  // FIX: Throw.IfLessThan(n, 0)

// 13. Wrong extension syntax (C# 14)
extension<Activity>(Activity activity)  // FIX: extension(Activity activity)
```

---

**Remember: You are on .NET 10 with C# 14. Act like it.**
