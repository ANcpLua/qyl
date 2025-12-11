# qyl. AI Development Guide

## STOP. READ THIS FIRST.

**Before writing ANY code, you MUST read these files. No exceptions. No shortcuts.**

| File                                     | Purpose                                | Violation =       |
|------------------------------------------|----------------------------------------|-------------------|
| `.claude/dotnet10-csharp14-reference.md` | .NET 10 / C# 14 APIs with examples     | **REJECTED CODE** |
| `spec-compliance-matrix/UML.md`          | OTel 1.38 attributes, dependency rules | **REJECTED CODE** |
| `spec-compliance-matrix/schema.yaml`     | TypeSpec schema, anti-patterns         | **REJECTED CODE** |
| `CHANGELOG.md`                           | Update for EVERY change                | **REJECTED PR**   |
| `README.md`                              | Commands, project structure            | Wasted time       |

**If you skip these files and produce code that violates the rules, your work will be rejected and you will redo it.**

---

## MANDATORY Reading Order

1. **FIRST**: Read `.claude/dotnet10-csharp14-reference.md` - **.NET 10 / C# 14 APIs you MUST use**
2. **SECOND**: Read `spec-compliance-matrix/UML.md` - OTel 1.38 required/deprecated attributes
3. **THIRD**: Read `spec-compliance-matrix/schema.yaml` - TypeSpec schema structure
4. **THEN**: Write code
5. **FINALLY**: Update `CHANGELOG.md` with ALL changes

**Do NOT:**

- Guess attribute names (read UML.md)
- Use old .NET patterns (read schema.yaml)
- Skip CHANGELOG (your PR will be rejected)
- Hallucinate version numbers or release dates (verify in actual files)

---

## Documentation Map

```
WHERE to find WHAT:

.NET 10 / C# 14 APIs      → .claude/dotnet10-csharp14-reference.md  ← START HERE
RULES & ATTRIBUTES        → spec-compliance-matrix/UML.md
SCHEMA & .NET APIs        → spec-compliance-matrix/schema.yaml
COMPONENT ARCHITECTURE    → C4-Documentation/*.md
CODE GENERATION           → core/CLAUDE.md
COMMANDS & QUICK START    → README.md
```

| Location                                 | Documents                            |
|------------------------------------------|--------------------------------------|
| `.claude/dotnet10-csharp14-reference.md` | **MANDATORY** .NET 10/C# 14 patterns |
| `C4-Documentation/`                      | WHERE code lives, WHAT classes do    |
| `spec-compliance-matrix/`                | RULES to follow, ATTRIBUTES to use   |
| `core/CLAUDE.md`                         | TypeSpec → Kiota pipeline            |
| `README.md`                              | Commands, ports, quick start         |

---

## Critical Rules

### OTel 1.38 GenAI (MANDATORY)

**Required attributes:**

```
gen_ai.provider.name      # openai, anthropic, gcp.gemini, aws.bedrock, etc.
gen_ai.request.model      # Model ID for request
gen_ai.operation.name     # chat, text_completion, embeddings, invoke_agent
gen_ai.usage.input_tokens # Token count (prompt)
gen_ai.usage.output_tokens # Token count (completion)
```

**Deprecated (REJECT):**

```
gen_ai.system             → gen_ai.provider.name
gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens
gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens
```

### .NET 10 / C# 14 (MANDATORY - DO NOT USE OLD PATTERNS)

**This project targets `net10.0` with `LangVersion=14`. You MUST use modern APIs.**

#### Threading & Synchronization

```csharp
// ✅ .NET 10: Use Lock class
private readonly Lock _lock = new();
using (_lock.EnterScope()) { /* critical section */ }

// ❌ OLD: Never use object lock
private readonly object _lock = new();
lock (_lock) { /* NO */ }
```

#### LINQ Extensions (.NET 9+)

```csharp
// ✅ CountBy - count occurrences by key
var counts = spans.CountBy(s => s.OperationName);
// Result: IEnumerable<KeyValuePair<string, int>>

// ❌ OLD
var counts = spans.GroupBy(s => s.OperationName).ToDictionary(g => g.Key, g => g.Count());

// ✅ AggregateBy - aggregate values by key
var totals = tokens.AggregateBy(
    t => t.Model,
    seed: 0L,
    (acc, t) => acc + t.Count);

// ❌ OLD
var totals = tokens.GroupBy(t => t.Model).ToDictionary(g => g.Key, g => g.Sum(t => t.Count));

// ✅ Index - get index with element
foreach (var (index, span) in spans.Index())
    Console.WriteLine($"{index}: {span.Name}");

// ❌ OLD
for (var i = 0; i < spans.Count; i++) { var span = spans[i]; }
```

#### Async Patterns (.NET 9+)

```csharp
// ✅ Task.WhenEach - process tasks as they complete
await foreach (var task in Task.WhenEach(tasks))
{
    var result = await task;
    Process(result);
}

// ❌ OLD: WhenAny loop pattern
while (tasks.Count > 0)
{
    var completed = await Task.WhenAny(tasks);
    tasks.Remove(completed);
    // NO - inefficient O(n²)
}
```

#### ASP.NET Core Minimal APIs (.NET 9+)

```csharp
// ✅ TypedResults.ServerSentEvents for SSE
app.MapGet("/events", () => TypedResults.ServerSentEvents(GetEventsAsync()));

// ❌ OLD: Manual SSE formatting
app.MapGet("/events", async (HttpContext ctx) => {
    ctx.Response.ContentType = "text/event-stream";
    // NO - use TypedResults
});
```

#### Collections (.NET 9+)

```csharp
// ✅ OrderedDictionary<K,V> - insertion order preserved
var ordered = new OrderedDictionary<string, int>();

// ❌ OLD: Dictionary doesn't preserve order
var dict = new Dictionary<string, int>(); // order not guaranteed

// ✅ FrozenDictionary for read-heavy immutable lookups
var frozen = attributes.ToFrozenDictionary();

// ✅ SearchValues<char> for delimiter/special character detection
private static readonly SearchValues<char> s_delimiters = SearchValues.Create("._-:");
int delimIndex = attributeName.AsSpan().IndexOfAny(s_delimiters);

// ✅ SearchValues<string> for SUBSTRING search (NOT prefix matching!)
// Use for keywords that can appear ANYWHERE in a string
private static readonly SearchValues<string> s_errorKeywords =
    SearchValues.Create(["error", "failed", "timeout"], StringComparison.OrdinalIgnoreCase);
bool hasErrorKeyword = logMessage.AsSpan().ContainsAny(s_errorKeywords);

// ✅ Direct StartsWith for PREFIX detection (SearchValues<string> is WRONG for this!)
[MethodImpl(MethodImplOptions.AggressiveInlining)]
static bool IsGenAiAttribute(ReadOnlySpan<char> key) =>
    key.StartsWith("gen_ai.") || key.StartsWith("agents.") || key.StartsWith("llm.");
```

#### JSON Serialization (.NET 9+)

```csharp
// ✅ Built-in snake_case policy
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};

// ❌ OLD: Custom snake_case converter - NEVER

// ✅ JsonSerializerContext for AOT (required for Native AOT)
[JsonSerializable(typeof(SpanModel))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class QylJsonContext : JsonSerializerContext;

// Usage
JsonSerializer.Serialize(span, QylJsonContext.Default.SpanModel);
```

#### C# 13/14 Language Features

```csharp
// ✅ Collection expressions
int[] numbers = [1, 2, 3];
List<string> names = ["alice", "bob"];
Span<byte> bytes = [0x00, 0xFF];

// ❌ OLD
var numbers = new int[] { 1, 2, 3 };
var names = new List<string> { "alice", "bob" };

// ✅ Primary constructors (classes)
public class SpanProcessor(ILogger<SpanProcessor> logger, IStore store)
{
    public void Process() => logger.LogInformation("Processing with {Store}", store);
}

// ❌ OLD
public class SpanProcessor
{
    private readonly ILogger<SpanProcessor> _logger;
    public SpanProcessor(ILogger<SpanProcessor> logger) => _logger = logger;
}

// ✅ field keyword (C# 14 preview)
public string Name
{
    get => field;
    set => field = value?.Trim() ?? throw new ArgumentNullException();
}

// ✅ params collections (C# 13)
void Log(params ReadOnlySpan<string> messages) { }
Log("a", "b", "c"); // no array allocation

// ✅ Lock parameter in async (C# 13)
public async Task ProcessAsync()
{
    using (_lock.EnterScope())
    {
        await Task.Yield(); // OK in C# 13+
    }
}

// ✅ Escape sequence \e (C# 13)
var ansi = "\e[31mRed\e[0m";

// ❌ OLD
var ansi = "\x1b[31mRed\x1b[0m";

// ✅ ref struct interfaces (C# 13)
ref struct SpanWrapper : IDisposable { }

// ✅ allows ref struct generic constraint (C# 13)
void Process<T>(T value) where T : allows ref struct { }
```

#### Quick Reference Table

| Section | Key Patterns |
|---------|--------------|
| `List<T>` Direct | `Find`, `FindAll`, `Exists`, `FindIndex`, `TrueForAll`, `ConvertAll`, `RemoveAll` - skip LINQ overhead |
| `CountBy`/`AggregateBy` | .NET 9 single-pass aggregation - no `GroupBy().ToDictionary()` allocations |
| `Task.WhenEach` | .NET 9 clean replacement for `while(WhenAny)` loops |
| `ValueTask<T>` | When to use (cache hits, hot paths), rules (never double-await) |
| `IAsyncEnumerable` | Full .NET 10 `System.Linq.AsyncEnumerable` - `LeftJoin`, `RightJoin`, `Shuffle`, `InfiniteSequence` |
| `Channel<T>` | Bounded/unbounded, backpressure modes, fan-out patterns |
| `TimeProvider` | `FakeTimeProvider` for testing, timer patterns |
| `ServerSentEvents` | `TypedResults.ServerSentEvents()`, `SseItem<T>`, heartbeats |
| `IAsyncDisposable` | `await using`, dual disposal pattern |
| `Span`/`Memory`/Pool | Zero-alloc parsing, `ArrayPool`, `stackalloc`, `MemoryPool` |
| `FrozenDictionary` | `GetAlternateLookup<ReadOnlySpan<char>>()` for span lookups |

| Use This (.NET 10/C# 14)          | Not This (Old)                     |
|-----------------------------------|------------------------------------|
| `Lock` + `EnterScope()`           | `object` + `lock`                  |
| `CountBy()`                       | `GroupBy().ToDictionary()`         |
| `AggregateBy()`                   | `GroupBy().ToDictionary(Sum)`      |
| `Task.WhenEach()`                 | `Task.WhenAny` loops |
| `TypedResults.ServerSentEvents()` | Manual SSE |
| `OrderedDictionary<K,V>`          | `Dictionary` when order matters |
| `FrozenDictionary`                | `Dictionary` for immutable lookups |
| `SearchValues<string>`            | Multiple `Contains` checks (SUBSTRING search only, NOT for prefixes!) |
| `JsonNamingPolicy.SnakeCaseLower` | Custom snake_case |
| `[1, 2, 3]`                       | `new[] { 1, 2, 3 }`                |
| Primary constructors | Field + constructor boilerplate |
| `\e`                              | `\x1b`                             |
| `params Span<T>`                  | `params T[]`                       |

### Dependencies (NEVER VIOLATE)

```
✅ instrumentation/* → core/*
✅ receivers/* → processing/*
✅ processing/* → storage/abstractions/*
✅ api/* → storage/abstractions/*
✅ streaming/* → processing/*
✅ dashboard → collector/api, collector/streaming
✅ cli → collector/api

❌ core/* → instrumentation/*, collector/*
❌ collector/* → dashboard, cli
❌ dashboard ↔ cli
❌ receivers/* → storage/*, api/*
❌ streaming/* → storage/*
```

---

## Key Locations

| Purpose             | Path                                            |
|---------------------|-------------------------------------------------|
| Collector Entry     | `src/qyl.collector/Program.cs`                  |
| GenAI Extraction    | `src/qyl.collector/Ingestion/GenAiExtractor.cs` |
| Session Aggregation | `src/qyl.collector/Query/SessionAggregator.cs`  |
| SSE Streaming       | `src/qyl.collector/Realtime/SseHub.cs`          |
| Storage             | `src/qyl.collector/Storage/DuckDbStore.cs`      |
| TypeSpec Schema     | `core/specs/main.tsp`                           |
| Build System        | `eng/build/Build.cs`                            |

---

## Commands

```bash
# Build & Test
nuke Compile              # Build all
nuke Test                 # Run tests
nuke Full                 # Backend + Frontend

# Run
dotnet run --project src/qyl.collector
npm run dev --prefix src/qyl.dashboard

# Frontend (aligned with package.json)
nuke FrontendInstall      # npm install
nuke FrontendBuild        # tsc -b && vite build
nuke FrontendTest         # vitest --run
nuke FrontendCoverage     # vitest run --coverage
nuke FrontendLint         # eslint src
nuke FrontendLintFix      # eslint src --fix
nuke FrontendDev          # vite (port 5173, API proxy → 5100)
nuke FrontendTypeCheck    # tsc --noEmit
nuke FrontendClean        # rm -rf dist

# Code Generation
nuke GenerateAll          # TypeSpec → C#/Python/TS (422 files)
nuke TypeSpecCompile      # TypeSpec → OpenAPI 3.1 (YAML only)
nuke GenerateCSharp       # Kiota → C# client
nuke GeneratePython       # Kiota → Python client
nuke GenerateTypeScript   # Kiota → TypeScript client
nuke TypeSpecInfo         # Show status
nuke TypeSpecClean        # Delete generated
```

---

## Code Generation Pipeline

```
core/specs/*.tsp (54 files) → OpenAPI 3.1 → Kiota → C#/Python/TypeScript
```

| Output     | Path                         | Files                |
|------------|------------------------------|----------------------|
| OpenAPI    | `core/openapi/openapi.yaml`  | 1 (188KB, YAML only) |
| C#         | `core/generated/dotnet/`     | 183                  |
| Python     | `core/generated/python/`     | 169                  |
| TypeScript | `core/generated/typescript/` | 70                   |

**Detailed docs:** `core/CLAUDE.md`

---

## Architecture (One Diagram)

```
PRODUCERS                    COLLECTOR                      CONSUMERS
─────────                    ─────────                      ─────────
instrumentation/dotnet   →   Receivers (HTTP/gRPC)      →   Dashboard
instrumentation/python   →   Processing (GenAiExtractor) →  CLI
instrumentation/typescript → Storage (DuckDB)            →  MCP
                             API + SSE Streaming
                                    ↓
                             TypeSpec Schema (54 .tsp files)
                             core/specs/ → OpenAPI → Kiota
```

---

## When Working On Code

1. **Check `spec-compliance-matrix/UML.md`** for required attributes and anti-patterns
2. **Check `spec-compliance-matrix/schema.yaml`** for TypeSpec structure and .NET API rules
3. **Use modern .NET 10 APIs** - never use pre-.NET 9 patterns
4. **Respect dependency rules** - layer boundaries are strict
5. **All JSON is snake_case** - use `JsonNamingPolicy.SnakeCaseLower`
6. **All attributes are snake_case** - no exceptions
7. **Update CHANGELOG.md** - MANDATORY for every change (see format below)

---

## CHANGELOG.md (MANDATORY)

**Every code change MUST update `CHANGELOG.md`.** Use this exact format:

```markdown
## [version] - YYYY-MM-DD

### Dependencies Changed

- Added: `package@version` - reason
- Removed: `package@version` - reason
- Updated: `package@old` → `package@new` - reason

### Files Changed

- `path/to/file.cs` - Description of what changed
- `path/to/file.ts` - Description of what changed
- `path/to/unchanged.cs` - No changes (if relevant to mention)

### Unsupported Features (Workarounds Needed)

- Feature X not supported in Y - manual workaround required
- API Z deprecated without replacement - blocked

### Runtime Verification Required

- [ ] Behavior X needs manual testing
- [ ] Integration with Y needs verification
- [ ] Performance of Z should be benchmarked

### Provider-Specific Changes

- OpenAI: Configuration change X affects behavior Y
- Anthropic: New attribute Z required for feature W

### Follow-Up Steps

1. Run `nuke Test` to verify changes
2. Manual verification of X in staging
3. Update documentation for Y
4. Notify team about breaking change Z
```

**Rules:**

- Never skip CHANGELOG update
- Include ALL files touched, even if unchanged but related
- Be explicit about what cannot be automated
- List runtime verifications as checkboxes
- Provider-specific changes are critical for GenAI work

---

## Module-Specific Guides

For detailed rules on specific modules, see `spec-compliance-matrix/`:

- `collector-*.md` - Collector subsystem rules
- `producer-*.md` - SDK instrumentation rules
- `consumer-*.md` - Dashboard/CLI rules
- `MASTER.md` - Global architecture rules

---

## CI/CD (GitHub Actions)

### Workflows

| Workflow      | Trigger            | Purpose                       |
|---------------|--------------------|-------------------------------|
| `ci.yml`      | push/PR to main    | Build, test, lint, coverage   |
| `release.yml` | tag `v*` or manual | Create release with artifacts |

### CI Jobs

- **backend**: .NET restore → build → test
- **frontend**: npm ci → typecheck → lint → test → build
- **frontend-coverage**: vitest coverage
- **dependency-audit**: npm audit + dotnet vulnerability scan

### Release Artifacts

```bash
# Create release
git tag v1.0.0 && git push origin v1.0.0

# Artifacts produced:
# - qyl-collector-linux-x64-v1.0.0.tar.gz (Native AOT)
# - qyl-dashboard-v1.0.0.tar.gz (Vite build)
# - ghcr.io/{repo}/collector:v1.0.0 (Docker)
```

### Ports

| Port | Service                   |
|------|---------------------------|
| 5100 | Collector API (REST, SSE) |
| 4317 | OTLP gRPC                 |
| 4318 | OTLP HTTP                 |
| 5173 | Dashboard dev (Vite)      |

### Environment

| Variable        | Default                 | Purpose          |
|-----------------|-------------------------|------------------|
| `QYL_PORT`      | 5100                    | Collector port   |
| `QYL_DATA_PATH` | `./data/qyl.duckdb`     | Database path    |
| `VITE_API_URL`  | `http://localhost:5100` | API proxy target |

---

## Documentation Index

### .NET 10 / C# 14 Reference (READ FIRST - MANDATORY)

| File                                     | Content                                    |
|------------------------------------------|--------------------------------------------|
| `.claude/dotnet10-csharp14-reference.md` | Lock, CountBy, WhenEach, Extension Members |

### Rules & Schema

| File                                 | Content                                               |
|--------------------------------------|-------------------------------------------------------|
| `spec-compliance-matrix/UML.md`      | OTel 1.38 attributes, anti-patterns, dependency rules |
| `spec-compliance-matrix/schema.yaml` | TypeSpec schema, anti-patterns                        |

### Component Architecture (C4)

| File                               | Content                             |
|------------------------------------|-------------------------------------|
| `C4-Documentation/INDEX.md`        | Navigation to all C4 docs           |
| `C4-Documentation/c4-collector.md` | HTTP API, Storage, Query, Realtime  |
| `C4-Documentation/c4-grpc.md`      | gRPC receivers, Protocol conversion |
| `C4-Documentation/c4-dashboard.md` | React 19 frontend                   |
| `C4-Documentation/c4-sdk.md`       | Instrumentation SDKs, Providers     |
| `C4-Documentation/c4-build.md`     | NUKE, CI/CD, Docker                 |

### Other

| File             | Content                                     |
|------------------|---------------------------------------------|
| `core/CLAUDE.md` | Code generation pipeline (TypeSpec → Kiota) |
| `CHANGELOG.md`   | Version history (MANDATORY updates)         |
