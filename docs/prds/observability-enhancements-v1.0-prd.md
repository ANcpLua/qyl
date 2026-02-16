⁄®# qyl Observability Enhancements - Implementation Plan

**Version**: 1.0
**Created**: 2026-02-14
**Owner**: qyl Platform Team
**Status**: Planning

## Executive Summary

Two complementary features to help AI agents (Claude) diagnose issues faster:

1. **MSBuild Binlog Auto-Capture**: Auto-capture binlogs with property tracking on build failures
2. **PDB-Enhanced Logging**: Enrich all logs with source file:line from Portable PDBs

**Combined Impact**: Reduce diagnosis time from 10+ minutes to <1 minute by providing exact source locations and build
context.

**Total Effort**: 12-17 hours (can be done in parallel by 2 devs)

---

## Architecture Decision Records (ADRs)

### ADR-001: Use DuckDB for Binlog Metadata Storage

**Context**: Need to store binlog metadata (timestamps, errors, property issues) for querying via MCP tools.

**Options Considered**:

1. Store binlogs as files only, parse on-demand
2. Store parsed metadata in JSON files
3. Store metadata in DuckDB with binlog files on disk

**Decision**: DuckDB metadata + file storage (Option 3)

**Why**:

- **Query Performance**: DuckDB enables indexed queries (<100ms) vs file scanning (seconds)
- **Retention Management**: SQL-based cleanup (DELETE oldest) vs manual file iteration
- **Consistency**: Existing qyl pattern (spans, logs use DuckDB)
- **Trade-off**: Extra storage overhead (~1KB metadata per 5MB binlog) is negligible

**Alternatives Rejected**:

- Option 1: Too slow for MCP queries (need to parse 5MB binlog every time)
- Option 2: No indexing, retention logic complex

### ADR-002: Zero-Copy BinaryData for Binlog Reading

**Context**: Binlogs are 5MB+ files. Using `BinaryData.FromStream()` copies the entire buffer.

**Options Considered**:

1. `BinaryData.FromStream(stream)` - simple but copies buffer
2. `new BinaryData(stream.GetBuffer().AsMemory(0, position))` - zero-copy
3. Memory-mapped files - most efficient but complex

**Decision**: Zero-copy MemoryStream (Option 2)

**Why**:

- **Performance**: Avoids 5MB+ buffer copy per binlog (saves memory allocation)
- **Simplicity**: One-line change from Option 1
- **Safety**: BinaryData constructor validates buffer bounds
- **Trade-off**: Buffer mutability risk (mitigated by reading file once)

**Alternatives Rejected**:

- Option 1: Wastes memory on large binlogs (unacceptable for 10 × 5MB = 50MB retention)
- Option 3: Overkill for sequential reads, adds complexity

**Code Pattern**:

```csharp
// ❌ BAD: Copies the entire buffer
var data = BinaryData.FromStream(stream);

// ✅ GOOD: Zero-copy
var data = new BinaryData(stream.GetBuffer().AsMemory(0, (int)stream.Position));
```

### ADR-003: Correlate Build Failures with Runtime Call Stacks

**Context**: Build failures show "target failed" but not WHERE in Build.cs the failure originated.

**Options Considered**:

1. Binlog only (property tracking)
2. Binlog + PDB call stack
3. Binlog + PDB + async stack traces

**Decision**: Binlog + PDB call stack (Option 2)

**Why**:

- **Root Cause Speed**: Shows "Compile target → called from Build.cs:156" immediately
- **Existing Infrastructure**: System.Reflection.Metadata already used for Option 2 (PDB logging)
- **Negligible Overhead**: PDB read is <5ms, happens only on failure
- **Trade-off**: Requires PDBs available (acceptable for dev/CI scenarios)

**Alternatives Rejected**:

- Option 1: Incomplete picture - shows property issues but not code location
- Option 3: Async stack traces need runtime profiler (too invasive)

**Example Output**:

```
Build failed: error CS0246: The type or namespace name 'Foo' could not be found
Property tracking: Property 'Foo' was never set
Call stack:
  → Build.Compile() at Build.cs:156
  → Build.Execute() at Build.cs:89
```

### ADR-004: Enrich All Logs with PDB Source Locations

**Context**: MCP tools show error messages but Claude must manually search code to find source location.

**Options Considered**:

1. Store PDB info only for errors (severity >= 17)
2. Store PDB info for all logs
3. On-demand PDB lookup at MCP query time

**Decision**: Store for all logs (Option 2)

**Why**:

- **Query Speed**: MCP queries are <100ms (pre-computed), Option 3 would be seconds (parse PDB every query)
- **Cache Hit Rate**: >95% of logs come from same methods (cache amortizes cost)
- **Completeness**: Info and Debug logs often contain context for errors
- **Trade-off**: Extra 3 columns per log (~30 bytes) - acceptable with 500K log retention limit

**Alternatives Rejected**:

- Option 1: Misses context logs that explain errors
- Option 3: Too slow for MCP queries, cache invalidation complex

**Cost Analysis**:

- Storage: 500K logs × 30 bytes = 15MB (negligible)
- Compute: <1ms per log (cached), amortized to <0.1ms at 95% hit rate

### ADR-005: LRU Cache for PDB Method Locations

**Context**: Reading PDB for every log is slow (~5ms). Need caching strategy.

**Options Considered**:

1. No cache (parse PDB every time)
2. LRU cache with fixed size (10K entries)
3. Per-assembly cache (unlimited size)
4. Time-based cache (TTL only)

**Decision**: LRU cache with idle timeout (Option 2 + 4 hybrid)

**Why**:

- **Memory Bounded**: 10K entries × ~200 bytes = 2MB max (predictable)
- **High Hit Rate**: Typical app has <1000 unique log call sites
- **Idle Cleanup**: Assemblies unload after 1 hour idle (prevents leak)
- **Trade-off**: May evict entries prematurely (acceptable - cache miss is 5ms)

**Alternatives Rejected**:

- Option 1: Too slow (5ms × 10K logs/day = 50 seconds wasted)
- Option 3: Memory leak risk if assemblies never unload
- Option 4: No memory bound (could grow indefinitely)

**Cache Parameters** (tunable):

```csharp
const int MaxCacheEntries = 10_000;
const int IdleTimeoutMinutes = 60;
```

### ADR-006: Graceful Degradation for Missing PDBs

**Context**: Production builds may strip PDBs. Feature must not break without them.

**Options Considered**:

1. Fail log ingestion if PDB missing (strict)
2. Log warning and continue (degraded)
3. Silently skip PDB enrichment (silent)

**Decision**: Log warning and continue (Option 2)

**Why**:

- **Reliability**: Log ingestion must never fail (observability is critical)
- **Debuggability**: Warning in qyl logs helps diagnose deployment issues
- **User Experience**: MCP tools show `null` for source fields (clear signal)
- **Trade-off**: Warning noise in production (acceptable - rare occurrence)

**Alternatives Rejected**:

- Option 1: Too fragile - breaks observability if PDB missing
- Option 3: Silent failures are invisible, hard to debug

**Behavior**:

```csharp
if (!pdbReader.TryGetMethodLocation(method, out var location))
{
    _logger.LogWarning("PDB not found for {Assembly}, source location unavailable",
        assembly.GetName().Name);
    return null; // Graceful degradation
}
```

---

## Unified Todo List

### Phase 1: Foundation (2-3 hours)

**Dependencies & Schema**

- [ ] **Add NuGet packages to qyl.mcp**
    - `Microsoft.Build.Logging.StructuredLogger` (binlog parsing)
    - `System.Reflection.Metadata` (PDB reading)
    - `System.Reflection.PortableExecutable` (PDB reading)
    - **Why**: Official Microsoft libraries, stable APIs, cross-platform

- [ ] **Add NuGet package to qyl.collector**
    - `System.Reflection.Metadata` (for log enrichment)

- [ ] **Create DuckDB migration: `001_build_failures.sql`**
  ```sql
  CREATE TABLE IF NOT EXISTS build_failures (
    id VARCHAR PRIMARY KEY,
    timestamp TIMESTAMP NOT NULL,
    target VARCHAR NOT NULL,
    exit_code INTEGER NOT NULL,
    binlog_path VARCHAR NOT NULL,
    error_summary TEXT,
    property_issues JSON,       -- MSBuild property tracking
    env_reads JSON,             -- Environment variables read
    call_stack JSON,            -- PDB call stack
    duration_ms INTEGER,
    created_at TIMESTAMP DEFAULT now()
  );
  CREATE INDEX idx_bf_timestamp ON build_failures(timestamp DESC);
  CREATE INDEX idx_bf_target ON build_failures(target);
  ```
    - **Why JSON columns**: Flexible schema for property issues (variable structure), DuckDB has native JSON support

- [ ] **Create DuckDB migration: `002_source_locations.sql`**
  ```sql
  ALTER TABLE logs ADD COLUMN IF NOT EXISTS source_file VARCHAR;
  ALTER TABLE logs ADD COLUMN IF NOT EXISTS source_line INTEGER;
  ALTER TABLE logs ADD COLUMN IF NOT EXISTS source_column INTEGER;
  ALTER TABLE logs ADD COLUMN IF NOT EXISTS source_method VARCHAR;
  CREATE INDEX IF NOT EXISTS idx_logs_source ON logs(source_file);

  ALTER TABLE console_logs ADD COLUMN IF NOT EXISTS source_file VARCHAR;
  ALTER TABLE console_logs ADD COLUMN IF NOT EXISTS source_line INTEGER;
  ALTER TABLE console_logs ADD COLUMN IF NOT EXISTS source_column INTEGER;
  CREATE INDEX IF NOT EXISTS idx_console_source ON console_logs(source_file);
  ```
    - **Why nullable columns**: Backward-compatible (existing logs have no source info), graceful degradation if PDB
      missing

- [ ] **Add `.qyl/binlogs/` to `.gitignore`**
    - **Why**: Binlogs may contain secrets (env vars, connection strings), should not be committed

- [ ] **Design test fixtures**
    - Sample .binlog with property tracking enabled
    - Sample assembly with embedded PDB
    - Sample assembly with separate .pdb file
    - Sample assembly with no PDB (test degradation)

### Phase 2: Core Components (6-8 hours)

**Shared Infrastructure**

- [ ] **Create `PdbReader.cs` (shared utility)**
  ```csharp
  public sealed class PdbReader : IDisposable
  {
      // Reads Portable PDB (embedded or separate)
      // Returns method source location (file, line, column)
      // Handles missing PDB gracefully (returns null)
  }
  ```
    - **Why separate class**: Reused by both binlog capture and log enrichment, single source of truth for PDB logic
    - **Why IDisposable**: PDB readers hold file handles, must be disposed

- [ ] **Implement zero-copy pattern in PdbReader**
  ```csharp
  using var fs = File.OpenRead(assemblyPath);
  using var peReader = new PEReader(fs);

  // Zero-copy: Use GetBuffer() instead of FromStream()
  var pdbData = new BinaryData(
      memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Position)
  );
  ```
    - **Why**: Avoids copying 5MB+ PDB buffers (saves memory allocation)

- [ ] **Create `SourceLocationCache.cs`**
  ```csharp
  public sealed class SourceLocationCache
  {
      private readonly ConcurrentDictionary<string, CacheEntry> _cache;
      private const int MaxEntries = 10_000;
      private const int IdleTimeoutMinutes = 60;

      public SourceLocation? GetOrAdd(MethodInfo method, Func<MethodInfo, SourceLocation?> factory);
      private void EvictLRU(); // Called when MaxEntries reached
      private void EvictIdle(); // Background task, runs every 5 minutes
  }
  ```
    - **Why LRU**: Predictable memory usage (max 2MB), high hit rate (>95%)
    - **Why idle timeout**: Prevents memory leak if assemblies never unload
    - **Why ConcurrentDictionary**: Thread-safe, lock-free reads (log ingestion is multi-threaded)

**Binlog Capture**

- [ ] **Create `BinlogParser.cs`**
  ```csharp
  public sealed class BinlogParser(PdbReader pdbReader)
  {
      public BuildFailureRecord Parse(string binlogPath);
      // Extracts:
      // - Property tracking events (MSBuildLogPropertyTracking=15)
      // - Environment variable reads
      // - First error message
      // - Build duration
  }
  ```
    - **Why dependency injection**: PdbReader is shared, testable
    - **Why sealed**: No inheritance needed, enables compiler optimizations

- [ ] **Create `PostToolUse` hook: `hooks/dotnet-build-capture.sh`**
  ```bash
  #!/usr/bin/env bash
  # Triggered on: dotnet build, dotnet test
  # If exit code != 0:
  #   1. Set MSBuildLogPropertyTracking=15
  #   2. Re-run with -bl:.qyl/binlogs/{timestamp}.binlog
  #   3. Parse binlog + PDB
  #   4. Store in DuckDB
  #   5. Cleanup if >10 failures
  ```
    - **Why PostToolUse**: Triggered after command fails, can inspect exit code
    - **Why re-run with -bl**: Original run didn't have binlog enabled, need second pass (fast, no rebuild)
    - **Why timestamp in filename**: Prevents collisions (concurrent builds)

- [ ] **Create `BuildFailureStore.cs`**
  ```csharp
  public interface IBuildFailureStore
  {
      Task<string> InsertAsync(BuildFailureRecord record);
      Task<BuildFailureRecord?> GetAsync(string id);
      Task<BuildFailureRecord[]> ListAsync(int limit);
      Task CleanupOldestAsync(); // Keeps only 10 most recent
  }

  public sealed class DuckDbBuildFailureStore : IBuildFailureStore
  {
      // Implements retention: DELETE FROM build_failures WHERE ... ORDER BY timestamp DESC OFFSET 10
  }
  ```
    - **Why interface**: Testable, could swap DuckDB for PostgreSQL later
    - **Why retention in store**: Encapsulates business rule (10 failures), automatic on insert

- [ ] **Unit tests for BinlogParser**
    - Parse binlog with property reassignment → extracts correctly
    - Parse binlog with missing env var → extracts env reads
    - Parse malformed binlog → returns null, logs warning
    - Parse binlog with no errors → handles gracefully

**Log Enrichment**

- [ ] **Create `PdbEnricher.cs`**
  ```csharp
  public sealed class PdbEnricher(SourceLocationCache cache)
  {
      public SourceLocation? EnrichLogRecord(LogRecord log);
      // Extracts MethodInfo from log attributes (if available)
      // Looks up in cache → if miss, reads PDB
      // Returns source file:line:column
  }
  ```
    - **Why separate from cache**: Single responsibility (enrichment logic vs caching)
    - **Why nullable return**: Graceful degradation if PDB missing

- [ ] **Integrate into OTLP log handler** (`qyl.collector/Handlers/OtlpLogHandler.cs`)
  ```csharp
  var sourceLocation = _pdbEnricher.EnrichLogRecord(logRecord);
  await _logStore.InsertAsync(new LogRecordDto
  {
      // ... existing fields ...
      SourceFile = sourceLocation?.FilePath,
      SourceLine = sourceLocation?.Line,
      SourceColumn = sourceLocation?.Column,
      SourceMethod = sourceLocation?.MethodName
  });
  ```
    - **Why at ingestion time**: Pre-computed (fast MCP queries), cache amortizes cost

- [ ] **Unit tests for PdbEnricher**
    - Enrich log with embedded PDB → extracts source location
    - Enrich log with separate .pdb → extracts source location
    - Enrich log with no PDB → returns null, logs warning (once per assembly)
    - Enrich 1000 logs from same method → cache hit rate >95%

### Phase 3: MCP Tools (4-5 hours)

**New Tools: Build Failures**

- [ ] **Create `BuildTools.cs` in qyl.mcp**
  ```csharp
  [McpServerToolType]
  public sealed class BuildTools(IBuildFailureStore store)
  {
      [McpServerTool(Name = "qyl.list_build_failures")]
      public async Task<string> ListBuildFailuresAsync(int limit = 10);

      [McpServerTool(Name = "qyl.get_build_failure")]
      public async Task<string> GetBuildFailureAsync(string id);

      [McpServerTool(Name = "qyl.search_build_failures")]
      public async Task<string> SearchBuildFailuresAsync(string pattern);
  }
  ```
    - **Why pattern matches existing tools**: ConsoleTools, StructuredLogTools use same conventions

- [ ] **Implement `qyl.list_build_failures`**
  ```csharp
  // Returns markdown formatted:
  // # Build Failures (3 entries)
  //
  // **2026-02-14 10:23:15** [Compile] Exit code: 1
  // Error: CS0246: The type 'Foo' could not be found
  // Property tracking: 'Foo' was never set
  // Call stack:
  //   → Build.Compile() at Build.cs:156
  //   → Build.Execute() at Build.cs:89
  // Binlog: .qyl/binlogs/2026-02-14-10-23-15-Compile-1.binlog
  ```
    - **Why markdown**: Rendered nicely in Claude UI, structured but human-readable

- [ ] **Implement `qyl.get_build_failure`**
    - Returns full details for single failure (all property issues, env reads, full call stack)

- [ ] **Implement `qyl.search_build_failures`**
    - SQL: `WHERE error_summary LIKE '%pattern%' OR property_issues::TEXT LIKE '%pattern%'`
    - **Why JSON search**: DuckDB supports searching inside JSON columns

**Updated Tools: Log Source Locations**

- [ ] **Update `StructuredLogTools.ListStructuredLogsAsync`**
  ```csharp
  // Before:
  // **10:23:45** [ERROR] Connection failed
  //
  // After:
  // **10:23:45** [ERROR] Connection failed
  //   at DbContext.ConnectAsync() in DbContext.cs:125
  ```
    - **Why indented**: Distinguishes source location from message, clear visual hierarchy

- [ ] **Update `ConsoleTools.ListConsoleErrorsAsync`**
    - Same format as StructuredLogTools

- [ ] **Update MCP tool descriptions**
    - Mention source location feature in `[Description]` attribute
    - Example: "Returns logs with source file:line if PDB available"

**Integration Tests**

- [ ] **Test: Binlog capture end-to-end**
    1. Trigger `dotnet build` failure (compile error)
    2. Verify binlog captured in `.qyl/binlogs/`
    3. Query `qyl.list_build_failures`
    4. Verify property issues + call stack present

- [ ] **Test: Log enrichment end-to-end**
    1. Log error from method with PDB
    2. Query `qyl.list_structured_logs(level="error")`
    3. Verify source location present: `at File.cs:line`

- [ ] **Test: Retention enforcement**
    1. Trigger 11 build failures
    2. Verify only 10 most recent kept
    3. Verify oldest binlog file deleted

- [ ] **Test: Graceful degradation**
    1. Deploy qyl.collector without PDBs
    2. Log error
    3. Verify log stored successfully (source fields null)
    4. Verify warning logged once per assembly

### Phase 4: Documentation & Deployment (2-3 hours)

**Documentation**

- [ ] **Update `/Users/ancplua/qyl/CLAUDE.md`**
    - Add section: "Build Failure Diagnosis"
    - Mention `qyl.list_build_failures` MCP tool
    - Add section: "Log Source Locations"
    - Mention PDB requirement for source info

- [ ] **Update `/Users/ancplua/qyl/src/qyl.mcp/CLAUDE.md`**
    - Document all 3 new BuildTools MCP tools
    - Add examples of source location in log responses
    - Document graceful degradation (PDB optional)

- [ ] **Update `/Users/ancplua/qyl/src/qyl.collector/CLAUDE.md`**
    - Document PdbEnricher component
    - Document SourceLocationCache tuning parameters
    - Add troubleshooting: "Source locations not appearing"

- [ ] **Create ADR summary document** (this file!)
    - Store at `/Users/ancplua/qyl/docs/adrs/001-observability-enhancements.md`
    - **Why**: 6 months from now, team needs to understand trade-offs

- [ ] **Document tuning parameters**
  ```csharp
  // SourceLocationCache.cs
  public const int MaxCacheEntries = 10_000;        // Tune if >10K unique log call sites
  public const int IdleTimeoutMinutes = 60;         // Tune if assemblies unload frequently
  public const int EvictionCheckIntervalMinutes = 5; // Tune for memory pressure

  // BuildFailureStore.cs
  public const int MaxRetainedFailures = 10;        // Tune for disk space vs history
  ```
    - **Why constants**: Easy to find and adjust, single source of truth

**Deployment**

- [ ] **Test in qyl CI failure scenario**
    - Trigger real build failure
    - Verify binlog capture works in CI environment
    - Verify MCP tools accessible (auth configured)

- [ ] **Verify cache hit rate in production**
    - Add telemetry: `_logger.LogInformation("Cache hit rate: {Rate}%", hitRate)`
    - Monitor for 1 week, ensure >95%
    - **Why**: Validates ADR-005 assumption

- [ ] **Verify retention cleanup works**
    - Check `.qyl/binlogs/` directory after 20 failures
    - Verify only 10 binlog files present
    - Verify DuckDB table has 10 rows

- [ ] **Security review**
    - Confirm binlogs not committed to git
    - Confirm MCP tools require authentication
    - Confirm PDB file paths not exposed (only relative source paths)

---

## Success Metrics

**Performance** (measure after 1 week in production):

- [ ] Binlog capture overhead: <5 seconds per failure
- [ ] MCP query latency: <100ms (p95)
- [ ] Source location cache hit rate: >95%
- [ ] Source location lookup time: <1ms (cached), <5ms (uncached)

**Reliability**:

- [ ] Log ingestion never fails due to missing PDBs (100% graceful degradation)
- [ ] Retention enforcement never fails (10 binlogs max, always)

**User Experience** (Claude feedback):

- [ ] Time to diagnose build failure: <1 minute (down from 10+ minutes)
- [ ] Source location accuracy: 100% (when PDB available)

---

## Trade-offs Summary

| Decision             | Benefit                    | Cost                        | Why Worth It                               |
|----------------------|----------------------------|-----------------------------|--------------------------------------------|
| DuckDB storage       | Fast queries (<100ms)      | Extra storage (~1KB/binlog) | Query speed critical for MCP tools         |
| Zero-copy BinaryData | Saves 5MB+ per binlog      | Buffer mutability risk      | Memory efficiency > marginal safety risk   |
| PDB call stacks      | Instant root cause         | Requires PDBs in CI/dev     | PDBs available in 99% of scenarios         |
| Enrich all logs      | Complete context           | 30 bytes/log × 500K = 15MB  | Storage cost negligible vs diagnosis speed |
| LRU cache            | >95% hit rate              | May evict prematurely       | 5ms miss penalty acceptable                |
| Graceful degradation | Never breaks observability | Warning noise               | Reliability > silence                      |

---

## Maintenance & Evolution

**When to revisit these decisions**:

1. **ADR-001 (DuckDB)**: If binlog metadata exceeds 1GB (query performance degrades)
    - **Mitigation**: Add partitioning by date, archive old failures

2. **ADR-004 (Enrich all logs)**: If storage exceeds 100MB for source locations
    - **Mitigation**: Switch to error-only enrichment (ADR-004 Option 1)

3. **ADR-005 (LRU cache)**: If cache hit rate drops below 90%
    - **Mitigation**: Increase MaxCacheEntries to 50K (10MB max)

4. **ADR-006 (Graceful degradation)**: If warning noise becomes excessive
    - **Mitigation**: Rate-limit warnings to 1 per assembly per hour

**Continuous improvement**:

- Monitor cache hit rate weekly (target: >95%)
- Monitor MCP query latency monthly (target: p95 <100ms)
- Review ADRs every 6 months (are assumptions still valid?)

---

## Rollback Plan

If issues arise, features can be disabled independently:

**Disable binlog capture**:

```bash
# Remove PostToolUse hook
rm ~/.claude/hooks/PostToolUse/dotnet-build-capture.sh
```

**Disable log enrichment**:

```csharp
// qyl.collector/Startup.cs
services.AddSingleton<PdbEnricher>(sp => new NoOpPdbEnricher()); // Returns null always
```

**Rollback schema changes**:

```sql
-- Build failures (safe - independent table)
DROP TABLE build_failures;

-- Source locations (safe - nullable columns)
ALTER TABLE logs DROP COLUMN source_file;
ALTER TABLE logs DROP COLUMN source_line;
ALTER TABLE logs DROP COLUMN source_column;
ALTER TABLE logs DROP COLUMN source_method;
-- (Repeat for console_logs)
```

---

## Related Work

- [MSBuild Property Tracking](https://www.meziantou.net/msbuild-binlogs-property-tracking.htm) - Inspiration for ADR-001
- [Zero-Copy BinaryData](https://www.meziantou.net/zero-copy-binarydata-from-memorystream.htm) - Inspiration for ADR-002
- [Portable PDB Source Locations](https://www.meziantou.net/retrieve-method-source-location-at-runtime-using-portable-pdbs.htm) -
  Inspiration for ADR-003, ADR-004

**Document Version**: 1.0
**Last Updated**: 2026-02-14
**Next Review**: 2026-08-14 (6 months)
