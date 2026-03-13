# Conversational Loom Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the one-shot Loom pipeline and Copilot adapter with a unified conversational AIAgent that supports background auto-investigation, mid-stream user intervention, and session persistence in DuckDB.

**Architecture:** Unified AIAgent (via QylAgentBuilder) with tool-call stage signals (root_cause/solution/code_it_up), two-dimensional state machine (LoomStage x LoomStatus), AG-UI SSE streaming via LoomAguiEndpoints, DuckDB session persistence, and background-to-conversation handoff with replay protocol.

**Tech Stack:** .NET 10 / C# 14, Microsoft.Agents.AI (rc3), DuckDB, AG-UI SSE (CopilotKit), xUnit v3 / MTP

**Spec:** `docs/superpowers/specs/2026-03-12-conversational-loom-design.md`

---

## Chunk 1: Contract Types (Phase 1)

No runtime dependencies. Pure type definitions in `qyl.contracts`.

### Task 1: LoomStage enum and LoomStatus enum

**Files:**
- Create: `src/qyl.contracts/Autofix/LoomStage.cs`

- [ ] **Step 1: Create the stage and status enums**

```csharp
namespace Qyl.Contracts.Autofix;

/// <summary>Where in the investigation pipeline the session is.</summary>
public enum LoomStage
{
    Idle = 0,
    Insight = 1,
    Exploring = 2,
    Reasoning = 3,
    RootCause = 4,
    Solution = 5,
    CodeItUp = 6
}

/// <summary>What the session is doing at its current stage.</summary>
public enum LoomStatus
{
    /// <summary>Agent is actively running at this stage.</summary>
    Active,
    /// <summary>Agent paused — waiting for user input to continue.</summary>
    Paused,
    /// <summary>Session exists but no agent is running (pre-start or between runs).</summary>
    Idle,
    /// <summary>Terminal: investigation completed successfully.</summary>
    Completed,
    /// <summary>Terminal: unrecoverable error.</summary>
    Failed,
    /// <summary>Terminal: user or system cancelled.</summary>
    Cancelled
}

public static class LoomStageExtensions
{
    public static bool IsTerminal(this LoomStatus status) =>
        status is LoomStatus.Completed or LoomStatus.Failed or LoomStatus.Cancelled;
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/qyl.contracts/qyl.contracts.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 3: Commit**

```bash
git add src/qyl.contracts/Autofix/LoomStage.cs
git commit -m "feat(contracts): add LoomStage and LoomStatus enums for session state machine"
```

### Task 2: LoomSessionTypes — session, message, and tool result records

**Files:**
- Create: `src/qyl.contracts/Autofix/LoomSessionTypes.cs`

**Context:** This file contains ALL the contract types for the Loom session model. The `LoomCausalStep` and `LoomSolutionStep` records here supersede the identically-named records in `src/qyl.loom/LoomModels.cs` (which uses `Qyl.Loom` namespace). The loom copies will be deleted in Phase 6.

- [ ] **Step 1: Create the session types file**

```csharp
using System.Text.Json.Serialization;

namespace Qyl.Contracts.Autofix;

// ── Session model ────────────────────────────────────────────────────────────

public enum LoomSessionMode { Interactive, Background }
public enum LoomMessageRole { User, Assistant, System, Tool }
public enum LoomPauseReason { WaitingForUser, NeedMoreInfo }

public sealed record LoomSession
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public required LoomSessionMode Mode { get; set; }
    public required LoomStage Stage { get; set; }
    public required LoomStatus Status { get; set; }
    public LoomPauseReason? PauseReason { get; set; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; set; }
    public LoomRootCauseResult? RootCause { get; set; }
    public LoomSolutionResult? Solution { get; set; }
    public string? FixRunId { get; set; }
    public string? Error { get; set; }

    // In-memory only (not persisted to DuckDB)
    [JsonIgnore] public List<LoomMessage> Messages { get; } = [];
    [JsonIgnore] public CancellationTokenSource? CancellationTokenSource { get; set; }
}

public sealed record LoomSessionSummary(
    string SessionId,
    string IssueId,
    LoomStage Stage,
    LoomStatus Status,
    LoomSessionMode Mode,
    long CreatedAt,
    bool HasRootCause,
    bool HasSolution);

public sealed record LoomMessage(
    LoomMessageRole Role,
    string Content,
    string? ToolName = null,
    string? ToolArgs = null);

// ── Wire format types (SSE contract) ─────────────────────────────────────────
// JSON serialization uses snake_case: order, description, is_root_cause.
// Supersedes the id/text naming in docs/loom-design.md Section 8.

public sealed record LoomCausalStep(int Order, string Description, bool IsRootCause);
public sealed record LoomSolutionStep(string Title, string Description);

// ── Tool return types ────────────────────────────────────────────────────────

public sealed record LoomRootCauseResult(string Summary, LoomCausalStep[] Steps);
public sealed record LoomSolutionResult(string Summary, LoomSolutionStep[] Steps);
public sealed record LoomCodeItUpResult(
    bool Success, string? RunId, string? PrUrl, double Confidence, string? Error);

// ── Request types ────────────────────────────────────────────────────────────

public sealed record InterruptRequest(string Message);
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/qyl.contracts/qyl.contracts.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 3: Commit**

```bash
git add src/qyl.contracts/Autofix/LoomSessionTypes.cs
git commit -m "feat(contracts): add LoomSession, LoomMessage, and tool result types"
```

### Task 3: IIssueContextSource interface

**Files:**
- Create: `src/qyl.contracts/Copilot/IIssueContextSource.cs`

**Context:** This interface lives in `qyl.contracts` so both `qyl.agents` (ObservabilityContextProvider) and `qyl.collector` (IssueContextBuilder) can reference it. Must be BCL-only — no package dependencies.

- [ ] **Step 1: Create the interface**

```csharp
namespace Qyl.Contracts.Copilot;

/// <summary>
///     Provides formatted issue context for AIAgent sessions.
///     Implemented by <c>IssueContextBuilder</c> in <c>qyl.collector</c>.
/// </summary>
public interface IIssueContextSource
{
    Task<string> GetFormattedContextAsync(
        string issueId, string? userContext = null, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/qyl.contracts/qyl.contracts.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 3: Commit**

```bash
git add src/qyl.contracts/Copilot/IIssueContextSource.cs
git commit -m "feat(contracts): add IIssueContextSource interface for shared context building"
```

---

## Chunk 2: Storage + Context (Phase 2)

DuckDB schema migration, session store CRUD, and shared context builder.

### Task 4: DuckDB migration for loom_sessions and loom_messages

**Files:**
- Create: `src/qyl.collector/Storage/Migrations/V20260312__create_loom_sessions.sql`

**Context:** Migration naming follows the existing pattern: `V{YYYYMMDD}__description.sql`. The last existing migration is `V20260227__add_service_registry.sql`. The `MigrationRunner.cs` in this directory auto-applies SQL files in version order on startup.

- [ ] **Step 1: Create the migration SQL**

```sql
-- Loom session state (two-dimensional: stage x status)
CREATE TABLE IF NOT EXISTS loom_sessions (
    session_id       VARCHAR PRIMARY KEY,
    issue_id         VARCHAR NOT NULL,
    mode             VARCHAR NOT NULL DEFAULT 'interactive',
    stage            INTEGER NOT NULL DEFAULT 0,
    stage_name       VARCHAR NOT NULL DEFAULT 'idle',
    status           VARCHAR NOT NULL DEFAULT 'idle',
    created_at       BIGINT  NOT NULL,
    updated_at       BIGINT  NOT NULL,
    root_cause_json  VARCHAR,
    solution_json    VARCHAR,
    fix_run_id       VARCHAR,
    pause_reason     VARCHAR,
    error            VARCHAR,
    metadata_json    VARCHAR
);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_issue
    ON loom_sessions (issue_id);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_mode_status
    ON loom_sessions (mode, status);

CREATE INDEX IF NOT EXISTS idx_loom_sessions_status_stage
    ON loom_sessions (status, stage);

-- Loom message history (persisted for replay protocol)
CREATE TABLE IF NOT EXISTS loom_messages (
    message_id   VARCHAR PRIMARY KEY,
    session_id   VARCHAR NOT NULL,
    role         VARCHAR NOT NULL,
    content      VARCHAR NOT NULL,
    tool_name    VARCHAR,
    tool_args    VARCHAR,
    created_at   BIGINT  NOT NULL,
    sequence     INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_loom_messages_session
    ON loom_messages (session_id, sequence);
```

- [ ] **Step 2: Verify migration applies in-memory**

Write a quick smoke test to confirm the DDL runs without error:

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*DuckDbStore*" -v minimal`
Expected: Existing tests still pass (migration is auto-applied on startup)

- [ ] **Step 3: Commit**

```bash
git add src/qyl.collector/Storage/Migrations/V20260312__create_loom_sessions.sql
git commit -m "feat(storage): add DuckDB migration for loom_sessions and loom_messages"
```

### Task 5: LoomSessionStore — DuckDB CRUD

**Files:**
- Create: `src/qyl.collector/Autofix/LoomSessionStore.cs`
- Create: `tests/qyl.collector.tests/Autofix/LoomSessionStoreTests.cs`

**Context:** Follows the DuckDB test pattern: `await using var store = new DuckDbStore(":memory:");` — migrations auto-apply in constructor. The DuckDB API uses:
- Reads: `store.GetReadConnectionAsync(ct)` → `ReadLease` → `lease.Connection.CreateCommand()` → `ExecuteReaderAsync`
- Writes: `store.ExecuteWriteAsync(async (con, token) => { ... }, ct)` — delegate receiving `DuckDBConnection`
- `TimeProvider` for timestamps (not `DateTime.UtcNow`)

- [ ] **Step 1: Write the failing tests**

```csharp
using Qyl.Collector.Autofix;
using Qyl.Collector.Storage;
using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Tests.Autofix;

public sealed class LoomSessionStoreTests
{
    [Fact]
    public async Task CreateAsync_returns_session_with_defaults()
    {
        await using var db = new DuckDbStore(":memory:");
        var store = new LoomSessionStore(db, TimeProvider.System);

        LoomSession session = await store.CreateAsync("issue-1");

        Assert.NotNull(session.SessionId);
        Assert.Equal("issue-1", session.IssueId);
        Assert.Equal(LoomSessionMode.Interactive, session.Mode);
        Assert.Equal(LoomStage.Idle, session.Stage);
        Assert.Equal(LoomStatus.Idle, session.Status);
        Assert.True(session.CreatedAt > 0);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_session()
    {
        await using var db = new DuckDbStore(":memory:");
        var store = new LoomSessionStore(db, TimeProvider.System);

        LoomSession? result = await store.GetAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_persists_stage_and_status()
    {
        await using var db = new DuckDbStore(":memory:");
        var store = new LoomSessionStore(db, TimeProvider.System);

        LoomSession session = await store.CreateAsync("issue-1");
        session.Stage = LoomStage.RootCause;
        session.Status = LoomStatus.Active;
        await store.UpdateAsync(session);

        LoomSession? reloaded = await store.GetAsync(session.SessionId);
        Assert.NotNull(reloaded);
        Assert.Equal(LoomStage.RootCause, reloaded.Stage);
        Assert.Equal(LoomStatus.Active, reloaded.Status);
    }

    [Fact]
    public async Task GetPendingHandoffsAsync_returns_completed_background_sessions()
    {
        await using var db = new DuckDbStore(":memory:");
        var store = new LoomSessionStore(db, TimeProvider.System);

        // Create background session that completed at RootCause
        LoomSession bg = await store.CreateAsync("issue-bg", LoomSessionMode.Background);
        bg.Stage = LoomStage.RootCause;
        bg.Status = LoomStatus.Completed;
        bg.RootCause = new LoomRootCauseResult("test", []);
        await store.UpdateAsync(bg);

        // Create interactive session (should not appear)
        await store.CreateAsync("issue-interactive");

        IReadOnlyList<LoomSessionSummary> handoffs = await store.GetPendingHandoffsAsync();
        Assert.Single(handoffs);
        Assert.Equal(bg.SessionId, handoffs[0].SessionId);
        Assert.True(handoffs[0].HasRootCause);
    }

    [Fact]
    public async Task AppendMessageAsync_and_GetMessagesAsync_round_trip()
    {
        await using var db = new DuckDbStore(":memory:");
        var store = new LoomSessionStore(db, TimeProvider.System);

        LoomSession session = await store.CreateAsync("issue-1");
        await store.AppendMessageAsync(session.SessionId,
            new LoomMessage(LoomMessageRole.User, "What caused this?"));
        await store.AppendMessageAsync(session.SessionId,
            new LoomMessage(LoomMessageRole.Assistant, "Looking at the trace..."));

        IReadOnlyList<LoomMessage> messages = await store.GetMessagesAsync(session.SessionId);
        Assert.Equal(2, messages.Count);
        Assert.Equal(LoomMessageRole.User, messages[0].Role);
        Assert.Equal(LoomMessageRole.Assistant, messages[1].Role);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*LoomSessionStoreTests" -v minimal`
Expected: FAIL — `LoomSessionStore` does not exist

- [ ] **Step 3: Implement LoomSessionStore**

```csharp
using System.Text.Json;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;
using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Autofix;

/// <summary>
///     DuckDB persistence for Loom investigation sessions and message history.
///     Follows the DuckDbStore API pattern:
///     - Reads: <c>GetReadConnectionAsync</c> → <c>ReadLease</c> → ADO.NET
///     - Writes: <c>ExecuteWriteAsync(Func&lt;DuckDBConnection, CancellationToken, ValueTask&gt;)</c>
/// </summary>
public sealed class LoomSessionStore(DuckDbStore store, TimeProvider timeProvider)
{
    public async Task<LoomSession> CreateAsync(
        string issueId,
        LoomSessionMode mode = LoomSessionMode.Interactive,
        CancellationToken ct = default)
    {
        long now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000;
        string sessionId = $"loom-{Guid.NewGuid():N}";
        string modeName = mode == LoomSessionMode.Background ? "background" : "interactive";

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO loom_sessions (session_id, issue_id, mode, stage, stage_name, status, created_at, updated_at)
                VALUES ($1, $2, $3, 0, 'idle', 'idle', $4, $4)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = modeName });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);

        return new LoomSession
        {
            SessionId = sessionId,
            IssueId = issueId,
            Mode = mode,
            Stage = LoomStage.Idle,
            Status = LoomStatus.Idle,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task<LoomSession?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, mode, stage, status, pause_reason,
                   created_at, updated_at, root_cause_json, solution_json,
                   fix_run_id, error
            FROM loom_sessions WHERE session_id = $1
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapSession(reader);
    }

    public async Task UpdateAsync(LoomSession session, CancellationToken ct = default)
    {
        long now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000;
        session.UpdatedAt = now;

        string? rootCauseJson = session.RootCause is not null
            ? JsonSerializer.Serialize(session.RootCause) : null;
        string? solutionJson = session.Solution is not null
            ? JsonSerializer.Serialize(session.Solution) : null;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE loom_sessions SET
                    stage = $2, stage_name = $3, status = $4, updated_at = $5,
                    root_cause_json = $6, solution_json = $7, fix_run_id = $8,
                    pause_reason = $9, error = $10
                WHERE session_id = $1
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = session.SessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = (int)session.Stage });
            cmd.Parameters.Add(new DuckDBParameter { Value = session.Stage.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = session.Status.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)rootCauseJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)solutionJson ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)session.FixRunId ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)session.PauseReason?.ToString().ToLowerInvariant() ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)session.Error ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public async Task<IReadOnlyList<LoomSession>> GetByIssueAsync(
        string issueId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, mode, stage, status, pause_reason,
                   created_at, updated_at, root_cause_json, solution_json,
                   fix_run_id, error
            FROM loom_sessions WHERE issue_id = $1
            ORDER BY created_at DESC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        var results = new List<LoomSession>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapSession(reader));

        return results;
    }

    public async Task<IReadOnlyList<LoomSessionSummary>> GetPendingHandoffsAsync(
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, issue_id, stage, status, mode, created_at,
                   root_cause_json IS NOT NULL, solution_json IS NOT NULL
            FROM loom_sessions
            WHERE mode = 'background' AND status = 'completed' AND stage >= 4
            ORDER BY created_at DESC
            """;

        var results = new List<LoomSessionSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(new LoomSessionSummary(
                reader.GetString(0),
                reader.GetString(1),
                (LoomStage)reader.GetInt32(2),
                ParseStatus(reader.GetString(3)),
                reader.GetString(4) == "background" ? LoomSessionMode.Background : LoomSessionMode.Interactive,
                reader.GetInt64(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7)));

        return results;
    }

    public async Task AppendMessageAsync(
        string sessionId, LoomMessage message, CancellationToken ct = default)
    {
        long now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000;
        string messageId = $"msg-{Guid.NewGuid():N}";

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            // Get next sequence number
            await using var seqCmd = con.CreateCommand();
            seqCmd.CommandText = "SELECT COALESCE(MAX(sequence), -1) + 1 FROM loom_messages WHERE session_id = $1";
            seqCmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            int seq = Convert.ToInt32(await seqCmd.ExecuteScalarAsync(token).ConfigureAwait(false));

            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO loom_messages (message_id, session_id, role, content, tool_name, tool_args, created_at, sequence)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = messageId });
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            cmd.Parameters.Add(new DuckDBParameter { Value = message.Role.ToString().ToLowerInvariant() });
            cmd.Parameters.Add(new DuckDBParameter { Value = message.Content });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)message.ToolName ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)message.ToolArgs ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = seq });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public async Task<IReadOnlyList<LoomMessage>> GetMessagesAsync(
        string sessionId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT role, content, tool_name, tool_args
            FROM loom_messages WHERE session_id = $1
            ORDER BY sequence
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });

        var results = new List<LoomMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(new LoomMessage(
                Enum.Parse<LoomMessageRole>(reader.GetString(0), ignoreCase: true),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));

        return results;
    }

    private static LoomSession MapSession(System.Data.Common.DbDataReader reader)
    {
        string modeStr = reader.GetString(2);
        string statusStr = reader.GetString(4);
        string? pauseStr = reader.IsDBNull(5) ? null : reader.GetString(5);

        return new LoomSession
        {
            SessionId = reader.GetString(0),
            IssueId = reader.GetString(1),
            Mode = modeStr == "background" ? LoomSessionMode.Background : LoomSessionMode.Interactive,
            Stage = (LoomStage)reader.GetInt32(3),
            Status = ParseStatus(statusStr),
            PauseReason = pauseStr is null ? null : Enum.Parse<LoomPauseReason>(pauseStr, ignoreCase: true),
            CreatedAt = reader.GetInt64(6),
            UpdatedAt = reader.GetInt64(7),
            RootCause = reader.IsDBNull(8) ? null : JsonSerializer.Deserialize<LoomRootCauseResult>(reader.GetString(8)),
            Solution = reader.IsDBNull(9) ? null : JsonSerializer.Deserialize<LoomSolutionResult>(reader.GetString(9)),
            FixRunId = reader.IsDBNull(10) ? null : reader.GetString(10),
            Error = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    private static LoomStatus ParseStatus(string s) => s switch
    {
        "active" => LoomStatus.Active,
        "paused" => LoomStatus.Paused,
        "idle" => LoomStatus.Idle,
        "completed" => LoomStatus.Completed,
        "failed" => LoomStatus.Failed,
        "cancelled" => LoomStatus.Cancelled,
        _ => LoomStatus.Idle
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*LoomSessionStoreTests" -v minimal`
Expected: 5 passed, 0 failed

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Autofix/LoomSessionStore.cs tests/qyl.collector.tests/Autofix/LoomSessionStoreTests.cs
git commit -m "feat(storage): add LoomSessionStore with DuckDB CRUD and message history"
```

### Task 6: IssueContextBuilder — shared context builder

**Files:**
- Create: `src/qyl.collector/Autofix/IssueContextBuilder.cs`
- Create: `tests/qyl.collector.tests/Autofix/IssueContextBuilderTests.cs`

**Context:** Extracts duplicated `BuildContextBlock` from:
- `LoomExplorerService.cs:162-191` (800-char stack, has Env, has userContext, label `"Error type:"`)
- `LoomInsightService.cs:90-107` (500-char stack, no Env, no userContext, label `"Type:"`)

Adopts the Explorer format (richer). Stack truncation parameterized via `maxStackLength`.

- [ ] **Step 1: Write the failing tests for FormatBlock (static, no DuckDB needed)**

```csharp
using Qyl.Collector.Autofix;
using Qyl.Collector.Storage;
using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Tests.Autofix;

public sealed class IssueContextBuilderTests
{
    [Fact]
    public void FormatBlock_includes_error_type_and_message()
    {
        var issue = MakeIssue("NullReferenceException", "Object reference not set");
        string block = IssueContextBuilder.FormatBlock(issue, [], null);

        Assert.Contains("Error type: NullReferenceException", block);
        Assert.Contains("Message: Object reference not set", block);
    }

    [Fact]
    public void FormatBlock_truncates_stack_at_maxStackLength()
    {
        var issue = MakeIssue("Error", "msg");
        string longStack = new('X', 1000);
        var events = new[] { MakeEvent(longStack) };

        string block800 = IssueContextBuilder.FormatBlock(issue, events, null, maxStackLength: 800);
        string block200 = IssueContextBuilder.FormatBlock(issue, events, null, maxStackLength: 200);

        // Stack line should be truncated at the specified length
        Assert.DoesNotContain(longStack, block800);
        Assert.DoesNotContain(longStack, block200);
        // 200-char version should be shorter
        Assert.True(block200.Length < block800.Length);
    }

    [Fact]
    public void FormatBlock_includes_user_context_when_provided()
    {
        var issue = MakeIssue("Error", "msg");
        string block = IssueContextBuilder.FormatBlock(issue, [], "We saw this after deploy");

        Assert.Contains("We saw this after deploy", block);
    }

    [Fact]
    public void FormatBlock_omits_user_context_when_null()
    {
        var issue = MakeIssue("Error", "msg");
        string block = IssueContextBuilder.FormatBlock(issue, [], null);

        Assert.DoesNotContain("Additional context", block);
    }

    [Fact]
    public void FormatBlock_includes_environment_from_events()
    {
        var issue = MakeIssue("Error", "msg");
        var events = new[] { MakeEvent("stack", environment: "production") };
        string block = IssueContextBuilder.FormatBlock(issue, events, null);

        Assert.Contains("Env: production", block);
    }

    // ── helpers ──
    // IssueSummary: DuckDbStore.Issues.cs:375-386
    // ErrorIssueEventRow: Errors/IssueService.cs:552-575

    private static IssueSummary MakeIssue(string errorType, string message) => new()
    {
        IssueId = "test-issue",
        Fingerprint = "fp-test",
        ErrorType = errorType,
        ErrorMessage = message,
        Status = IssueStatus.Unresolved,
        EventCount = 42,
        FirstSeen = DateTime.UnixEpoch,
        LastSeen = DateTime.UnixEpoch
    };

    private static ErrorIssueEventRow MakeEvent(
        string? stackTrace = null,
        string? environment = null) => new()
    {
        Id = "evt-1",
        IssueId = "test-issue",
        Timestamp = DateTime.UnixEpoch,
        StackTrace = stackTrace,
        Environment = environment
    };
}

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*IssueContextBuilderTests" -v minimal`
Expected: FAIL — `IssueContextBuilder` does not exist

- [ ] **Step 3: Implement IssueContextBuilder**

```csharp
using System.Text;
using Qyl.Collector.Storage;
using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Autofix;

public sealed class IssueContextBuilder(DuckDbStore store, IssueService issueService)
    : IIssueContextSource
{
    /// <summary>Default stack trace truncation length (chars).</summary>
    public const int DefaultMaxStackLength = 800;

    public async Task<IssueContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = 5,
        int maxStackLength = DefaultMaxStackLength,
        CancellationToken ct = default)
    {
        IssueSummary? issue = await store.GetIssueByIdAsync(issueId, ct);
        if (issue is null) return IssueContext.Empty;

        IReadOnlyList<ErrorIssueEventRow> events =
            await issueService.GetEventsAsync(issueId, maxEvents, ct);

        string block = FormatBlock(issue, events, userContext, maxStackLength);
        return new IssueContext(issue, events, userContext, block);
    }

    async Task<string> IIssueContextSource.GetFormattedContextAsync(
        string issueId, string? userContext, CancellationToken ct)
    {
        IssueContext ctx = await BuildAsync(issueId, userContext, ct: ct);
        return ctx.FormattedBlock;
    }

    internal static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext,
        int maxStackLength = DefaultMaxStackLength)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Error type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");

        if (events.Count > 0)
        {
            sb.AppendLine("\nRecent events:");
            foreach (ErrorIssueEventRow e in events)
            {
                sb.AppendLine($"  [{e.Timestamp:O}] {e.Message ?? "no message"}");
                if (e.StackTrace is not null)
                    sb.AppendLine($"    Stack: {e.StackTrace[..Math.Min(maxStackLength, e.StackTrace.Length)]}");
                if (e.Environment is not null)
                    sb.AppendLine($"    Env: {e.Environment}");
            }
        }

        if (userContext is not null)
            sb.AppendLine($"\nAdditional context from user:\n{userContext}");

        return sb.ToString();
    }
}

public sealed record IssueContext(
    IssueSummary? Issue,
    IReadOnlyList<ErrorIssueEventRow> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static IssueContext Empty { get; } = new(null, [], null, string.Empty);
    public bool IsEmpty => Issue is null;
}
```

**Verified API surface:**
- `store.GetIssueByIdAsync(issueId, ct)` → `IssueSummary?` — exists on `DuckDbStore.Issues.cs:168`
- `issueService.GetEventsAsync(issueId, maxEvents, ct)` → `IReadOnlyList<ErrorIssueEventRow>` — exists on `IssueService.cs:364`
- `IssueSummary` properties: `IssueId`, `Fingerprint`, `ErrorType`, `ErrorMessage`, `Status` (IssueStatus), `Owner`, `EventCount`, `FirstSeen` (DateTime), `LastSeen` (DateTime) — from `DuckDbStore.Issues.cs:375-386`
- `ErrorIssueEventRow` properties: `Id`, `IssueId`, `Message`, `StackTrace`, `Environment`, `Timestamp` (DateTime), plus 15 more — from `IssueService.cs:552-575`

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*IssueContextBuilderTests" -v minimal`
Expected: 5 passed, 0 failed

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Autofix/IssueContextBuilder.cs tests/qyl.collector.tests/Autofix/IssueContextBuilderTests.cs
git commit -m "feat(autofix): add IssueContextBuilder extracting duplicated BuildContextBlock"
```

---

## Chunk 3: Agent Layer (Phase 3)

ObservabilityContextProvider, QylAgentBuilder modification, LoomAgent factory, LoomTools.

### Task 7: ObservabilityContextProvider

**Files:**
- Create: `src/qyl.agents/Context/ObservabilityContextProvider.cs`
- Create: `tests/qyl.collector.tests/Agents/ObservabilityContextProviderTests.cs`

**Context:** This is a `MessageAIContextProvider` subclass (from `Microsoft.Agents.AI.Abstractions`). It reads `StateBag["qyl.issueId"]` and calls `IIssueContextSource.GetFormattedContextAsync()` to inject issue context as a system message.

**Type alias required:** `qyl.contracts` has its own `ChatMessage`/`ChatRole`. Use aliases:
```csharp
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
```

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.Agents.AI.Abstractions;
using Microsoft.Extensions.AI;
using Qyl.Agents.Context;
using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Tests.Agents;

public sealed class ObservabilityContextProviderTests
{
    [Fact]
    public async Task ProvideMessagesAsync_returns_empty_when_no_issue_id()
    {
        var provider = new ObservabilityContextProvider(new FakeContextSource());
        var session = new AgentSession();

        var messages = await provider.GetMessagesAsync(session);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ProvideMessagesAsync_returns_system_message_when_issue_id_set()
    {
        var source = new FakeContextSource("Formatted error context here");
        var provider = new ObservabilityContextProvider(source);
        var session = new AgentSession();
        session.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, "issue-123");

        var messages = await provider.GetMessagesAsync(session);

        var msg = Assert.Single(messages);
        Assert.Equal(ChatRole.System, msg.Role);
        Assert.Contains("Formatted error context here", msg.Text);
    }

    [Fact]
    public async Task ProvideMessagesAsync_returns_empty_when_context_is_empty()
    {
        var source = new FakeContextSource("");
        var provider = new ObservabilityContextProvider(source);
        var session = new AgentSession();
        session.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, "issue-123");

        var messages = await provider.GetMessagesAsync(session);

        Assert.Empty(messages);
    }

    private sealed class FakeContextSource(string result = "") : IIssueContextSource
    {
        public Task<string> GetFormattedContextAsync(
            string issueId, string? userContext = null, CancellationToken ct = default)
            => Task.FromResult(result);
    }
}
```

**Warning:** The test above assumes `MessageAIContextProvider` has a public `GetMessagesAsync` or similar method. The actual API is `ProvideMessagesAsync` which is `protected override`. You may need to test via the `AIContextProvider.GetContextAsync` public entry point, or create an `InvokingContext`. Check the SDK API before implementing.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*ObservabilityContextProviderTests" -v minimal`
Expected: FAIL — `ObservabilityContextProvider` does not exist

- [ ] **Step 3: Implement ObservabilityContextProvider**

```csharp
using Microsoft.Agents.AI.Abstractions;
using Qyl.Contracts.Copilot;

using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Qyl.Agents.Context;

public sealed class ObservabilityContextProvider(IIssueContextSource contextSource)
    : MessageAIContextProvider
{
    /// <summary>Key in AgentSession.StateBag that holds the issue ID.</summary>
    public const string IssueIdKey = "qyl.issueId";

    protected override async ValueTask<IEnumerable<AiChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        string? issueId = context.Session.StateBag.GetValue<string>(IssueIdKey);
        if (issueId is null) return [];

        string formatted = await contextSource
            .GetFormattedContextAsync(issueId, ct: cancellationToken);

        if (string.IsNullOrEmpty(formatted)) return [];

        return [new AiChatMessage(AiChatRole.System,
            $"## Error Context\n{formatted}")];
    }
}
```

**Verify:** `MessageAIContextProvider.InvokingContext` provides `Session` property with `StateBag`. Check the Microsoft.Agents.AI.Abstractions rc3 API.

- [ ] **Step 4: Adjust tests to match actual SDK API, then verify they pass**

Run: `dotnet test tests/qyl.collector.tests/ --filter-class "*ObservabilityContextProviderTests" -v minimal`
Expected: 3 passed, 0 failed

- [ ] **Step 5: Commit**

```bash
git add src/qyl.agents/Context/ObservabilityContextProvider.cs tests/qyl.collector.tests/Agents/ObservabilityContextProviderTests.cs
git commit -m "feat(agents): add ObservabilityContextProvider for auto-injecting issue context"
```

### Task 8: Modify QylAgentBuilder — ChatClientAgentOptions with providers

**Files:**
- Modify: `src/qyl.agents/Agents/QylAgentBuilder.cs:50-77`

**Context:** Current `FromChatClient` uses `AsAIAgent()` extension which doesn't support `contextProviders` or `ChatHistoryProvider`. Switch to constructing `ChatClientAgent` directly with `ChatClientAgentOptions`.

- [ ] **Step 1: Modify FromChatClient to accept contextProviders**

Replace the `FromChatClient` method (lines 50-77) with:

```csharp
    public static AIAgent FromChatClient(
        IChatClient chatClient,
        string agentName = "qyl-assistant",
        string description = "qyl AI assistant",
        string? instructions = null,
        IReadOnlyList<AITool>? tools = null,
        IReadOnlyList<AIContextProvider>? contextProviders = null,
        TimeProvider? timeProvider = null)
    {
        Guard.NotNull(chatClient);

        InstrumentedChatClient instrumented = new(chatClient, agentName, timeProvider);

        var options = new ChatClientAgentOptions
        {
            Name = agentName,
            Description = description,
            ChatHistoryProvider = new InMemoryChatHistoryProvider(),
        };

        if (instructions is not null)
            options.ChatOptions = new() { Instructions = instructions };

        if (tools is { Count: > 0 })
        {
            options.ChatOptions ??= new();
            options.ChatOptions.Tools = [.. tools];
        }

        if (contextProviders is { Count: > 0 })
        {
            options.AIContextProviders ??= [];
            options.AIContextProviders.AddRange(contextProviders);
        }

        return new ChatClientAgent(instrumented, options);
    }
```

Add required usings at top of file:
```csharp
using Microsoft.Agents.AI.Abstractions;
```

**Important:** This changes the return type construction from `AsAIAgent()` to `new ChatClientAgent(...)`. Verify:
- `ChatClientAgent` constructor takes `(IChatClient, ChatClientAgentOptions)`
- `ChatClientAgentOptions` has `AIContextProviders` property (type `List<AIContextProvider>`)
- `InMemoryChatHistoryProvider` exists in `Microsoft.Agents.AI.Abstractions`
- The `try/finally` dispose pattern for `InstrumentedChatClient` may need to be preserved if `ChatClientAgent` doesn't take ownership

- [ ] **Step 2: Update the Program.cs call site**

The call at `Program.cs:398` currently is:
```csharp
app.MapQylAguiChat(QylAgentBuilder.FromChatClient(aguiChatClient, agentName: "qyl-llm"));
```
This still works — the new `contextProviders` parameter is optional (defaults to null). No change needed.

- [ ] **Step 3: Verify build**

Run: `dotnet build src/qyl.agents/qyl.agents.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 4: Commit**

```bash
git add src/qyl.agents/Agents/QylAgentBuilder.cs
git commit -m "feat(agents): add contextProviders + InMemoryChatHistoryProvider to QylAgentBuilder"
```

### Task 9: LoomAgent factory

**Files:**
- Create: `src/qyl.agents/Agents/LoomAgent.cs`

**Context:** Static factory that creates an AIAgent configured for Loom investigation. Combines `LoomTools.All` with observability tools, passes context providers through to `QylAgentBuilder.FromChatClient`.

- [ ] **Step 1: Create LoomAgent.cs**

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Abstractions;
using Microsoft.Extensions.AI;

namespace Qyl.Agents.Agents;

public static class LoomAgent
{
    public static AIAgent Create(
        IChatClient chatClient,
        IReadOnlyList<AITool> observabilityTools,
        IReadOnlyList<AIContextProvider> contextProviders,
        TimeProvider? timeProvider = null)
    {
        List<AITool> tools =
        [
            .. LoomTools.All,
            .. observabilityTools
        ];

        return QylAgentBuilder.FromChatClient(
            chatClient,
            agentName: "loom",
            description: "AI debugging assistant that investigates errors and proposes fixes",
            instructions: Instructions,
            tools: tools,
            contextProviders: contextProviders,
            timeProvider: timeProvider);
    }

    private const string Instructions = """
        You are Loom, an AI debugging assistant embedded in the qyl observability platform.
        You investigate production errors by analyzing telemetry data (traces, logs, metrics).

        ## Investigation Flow

        1. **Explore**: Read the error context injected into this conversation.
           Query telemetry using your observability tools to understand the full picture.
           Stream your analysis as you go — the user sees your reasoning in real-time.

        2. **Root Cause**: When you've identified the root cause, call the `root_cause` tool
           with a structured causal chain. Each step should be a link in the chain from
           the triggering event to the fundamental cause. Mark exactly one step as `is_root_cause`.

        3. **Solution**: After root cause, call the `solution` tool with implementation steps.
           Each step should be concrete and actionable. The user can approve, modify, or
           reject individual steps.

        4. **Code It Up**: If the user approves, call `code_it_up` to generate a fix.
           This creates a PR with the changes. Only do this when explicitly asked.

        ## Interaction Guidelines

        - Stream your thinking — don't silently process. The user watches your investigation live.
        - Ask clarifying questions if the error context is ambiguous.
        - Use observability tools to query spans, logs, and metrics. Don't guess — verify.
        - If you need more information from the user, explain what you need and why.
        - After delivering root cause + solution, remain available for follow-up questions.
        """;
}
```

- [ ] **Step 2: Verify build (will fail until LoomTools exists)**

This depends on Task 10 (LoomTools). If implementing sequentially, proceed to Task 10 first.

### Task 10: LoomTools — root_cause, solution, code_it_up

**Files:**
- Create: `src/qyl.agents/Agents/LoomTools.cs`

**Context:** Three tools exposed to the LoomAgent. `root_cause` and `solution` are pure data-return tools (the AG-UI event stream carries arguments to the dashboard). `code_it_up` has DI-injected parameters (`AgentSession`, `AutofixOrchestrator`, etc.) — verify `AIFunctionFactory.Create` supports this.

- [ ] **Step 1: Create LoomTools.cs**

```csharp
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Abstractions;
using Microsoft.Extensions.AI;
using Qyl.Contracts.Autofix;

namespace Qyl.Agents.Agents;

public static class LoomTools
{
    /// <summary>Key in AgentSession.StateBag for the current Loom session ID.</summary>
    public const string SessionIdKey = "qyl.loomSessionId";

    public static IReadOnlyList<AITool> All { get; } =
    [
        AIFunctionFactory.Create(RootCause),
        AIFunctionFactory.Create(Solution),
        AIFunctionFactory.Create(CodeItUp)
    ];

    [Description("Report the root cause analysis as a structured causal chain. " +
        "Call this once you've identified the root cause.")]
    private static LoomRootCauseResult RootCause(
        [Description("One-sentence summary of the root cause")]
        string summary,
        [Description("Ordered causal chain from trigger to root cause")]
        LoomCausalStep[] steps)
    {
        return new LoomRootCauseResult(summary, steps);
    }

    [Description("Report the proposed solution as implementation steps. " +
        "Call this after root cause analysis.")]
    private static LoomSolutionResult Solution(
        [Description("One-sentence summary of the fix")]
        string summary,
        [Description("Ordered implementation steps")]
        LoomSolutionStep[] steps)
    {
        return new LoomSolutionResult(summary, steps);
    }

    [Description("Generate a code fix and open a pull request. " +
        "Only call when the user explicitly asks to code it up.")]
    private static async Task<LoomCodeItUpResult> CodeItUp(
        [Description("Repository full name (owner/repo)")]
        string repo,
        [Description("Base branch for the PR (default: main)")]
        string? baseBranch,
        AgentSession agentSession,
        CancellationToken ct)
    {
        // Note: AutofixOrchestrator, PrCreationService, LoomSessionStore
        // are NOT injected here because AIFunctionFactory DI injection
        // for arbitrary services is not guaranteed in rc3.
        //
        // Instead, the code_it_up tool returns a result that signals intent.
        // LoomAguiEndpoints intercepts TOOL_CALL_END for "code_it_up" and
        // orchestrates the actual fix run.
        //
        // This is a design tradeoff: tool purity vs DI complexity.
        string? sessionId = agentSession.StateBag.GetValue<string>(SessionIdKey);
        if (sessionId is null)
            return new LoomCodeItUpResult(false, null, null, 0, "No session ID in StateBag");

        return new LoomCodeItUpResult(true, null, null, 0, $"code_it_up requested for {repo}:{baseBranch ?? "main"} on session {sessionId}");
    }
}
```

**Design note:** The spec shows `code_it_up` with full DI injection (`AutofixOrchestrator`, `PrCreationService`, `LoomSessionStore`). However, `AIFunctionFactory.Create` may not support arbitrary DI injection for non-standard types. Two options:
1. If `AIFunctionFactory` supports DI: implement as spec shows
2. If not: make `code_it_up` a signal tool (returns intent), and `LoomAguiEndpoints` handles the actual orchestration by intercepting the tool result

The implementation above uses option 2 (safer). If DI injection works, switch to option 1 from the spec.

- [ ] **Step 2: Verify build**

Run: `dotnet build src/qyl.agents/qyl.agents.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 3: Commit LoomAgent + LoomTools together**

```bash
git add src/qyl.agents/Agents/LoomAgent.cs src/qyl.agents/Agents/LoomTools.cs
git commit -m "feat(agents): add LoomAgent factory and LoomTools (root_cause, solution, code_it_up)"
```

---

## Chunk 4: Endpoints (Phase 4)

LoomAguiEndpoints (new AG-UI SSE), service migrations to IssueContextBuilder.

### Task 11: LoomAguiEndpoints — AG-UI SSE for conversational Loom

**Files:**
- Create: `src/qyl.collector/Copilot/LoomAguiEndpoints.cs`

**Context:** Replaces `CopilotAguiEndpoints` (which is a thin wrapper around `MapAGUI`). The new endpoints handle session lifecycle, interrupt, handoff. The actual AG-UI SSE protocol is provided by `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`.

This is the most complex file. Key endpoints:
- `POST /api/v1/loom/{issueId}/chat` — start/continue session
- `POST /api/v1/loom/{sessionId}/interrupt` — mid-stream intervention
- `GET /api/v1/loom/pending-handoffs` — list background sessions
- `POST /api/v1/loom/{sessionId}/attach` — convert background to interactive
- `GET /api/v1/loom/{sessionId}` — get session state
- `GET /api/v1/loom/{sessionId}/messages` — get message history

- [ ] **Step 1: Create LoomAguiEndpoints.cs**

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Abstractions;
using Qyl.Agents.Agents;
using Qyl.Agents.Context;
using Qyl.Contracts.Autofix;

namespace Qyl.Collector.Copilot;

public static class LoomAguiEndpoints
{
    public static IEndpointRouteBuilder MapLoomAguiEndpoints(
        this IEndpointRouteBuilder endpoints,
        AIAgent agent)
    {
        Guard.NotNull(endpoints);
        Guard.NotNull(agent);

        var group = endpoints.MapGroup("/api/v1/loom");

        // Start or continue conversational Loom session
        group.MapPost("/{issueId}/chat", async (
            string issueId,
            LoomSessionStore sessionStore,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // Find or create session for this issue
            IReadOnlyList<LoomSession> existing =
                await sessionStore.GetByIssueAsync(issueId, ct);
            LoomSession session = existing.FirstOrDefault(s => !s.Status.IsTerminal())
                ?? await sessionStore.CreateAsync(issueId, ct: ct);

            // Set up AG-UI SSE streaming
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            // Create agent session with StateBag keys
            var agentSession = await agent.CreateSessionAsync(ct);
            agentSession.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, issueId);
            agentSession.StateBag.SetValue(LoomTools.SessionIdKey, session.SessionId);

            // Replay prior messages if this is a resumption
            IReadOnlyList<LoomMessage> history =
                await sessionStore.GetMessagesAsync(session.SessionId, ct);
            if (history.Count > 0)
            {
                await ReplayHistoryAsAguiEvents(httpContext.Response, history, ct);
            }

            // Update session to active
            session.Stage = LoomStage.Exploring;
            session.Status = LoomStatus.Active;
            await sessionStore.UpdateAsync(session, ct);

            // Run agent with streaming
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            session.CancellationTokenSource = sessionCts;

            try
            {
                await foreach (var evt in agent.RunStreamingAsync(
                    "Investigate the error described in the context.",
                    session: agentSession,
                    cancellationToken: sessionCts.Token))
                {
                    // Write AG-UI event to SSE stream
                    await WriteAguiEvent(httpContext.Response, evt, ct);

                    // Track stage transitions from tool calls
                    await TrackStageFromEvent(evt, session, sessionStore, ct);
                }

                session.Status = LoomStatus.Completed;
            }
            catch (OperationCanceledException) when (sessionCts.IsCancellationRequested)
            {
                // Interrupt — session stays active, client reconnects
            }
            catch (Exception ex)
            {
                session.Status = LoomStatus.Failed;
                session.Error = ex.Message;
            }

            await sessionStore.UpdateAsync(session, ct);
        });

        // Interrupt running agent
        group.MapPost("/{sessionId}/interrupt", async (
            string sessionId,
            InterruptRequest request,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            if (session is null) return Results.NotFound();
            if (session.Status.IsTerminal())
                return Results.Conflict("Session is terminal");

            session.CancellationTokenSource?.Cancel();

            await sessionStore.AppendMessageAsync(sessionId,
                new LoomMessage(LoomMessageRole.User, request.Message), ct);
            session.Stage = LoomStage.Exploring;
            session.Status = LoomStatus.Active;
            session.PauseReason = null;
            await sessionStore.UpdateAsync(session, ct);

            return Results.Ok();
        });

        // List background sessions ready for handoff
        group.MapGet("/pending-handoffs", async (
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            IReadOnlyList<LoomSessionSummary> handoffs =
                await sessionStore.GetPendingHandoffsAsync(ct);
            return Results.Ok(handoffs);
        });

        // Convert background session to interactive
        group.MapPost("/{sessionId}/attach", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            if (session is null) return Results.NotFound();
            if (session.Mode != LoomSessionMode.Background)
                return Results.Conflict("Session is not a background session");
            if (session.Status != LoomStatus.Completed)
                return Results.Conflict("Session is not completed");

            session.Mode = LoomSessionMode.Interactive;
            session.Status = LoomStatus.Idle;
            await sessionStore.UpdateAsync(session, ct);

            return Results.Ok(session);
        });

        // Get session state
        group.MapGet("/{sessionId}", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            LoomSession? session = await sessionStore.GetAsync(sessionId, ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });

        // Get full message history
        group.MapGet("/{sessionId}/messages", async (
            string sessionId,
            LoomSessionStore sessionStore,
            CancellationToken ct) =>
        {
            IReadOnlyList<LoomMessage> messages =
                await sessionStore.GetMessagesAsync(sessionId, ct);
            return Results.Ok(messages);
        });

        return endpoints;
    }

    private static async Task ReplayHistoryAsAguiEvents(
        HttpResponse response,
        IReadOnlyList<LoomMessage> history,
        CancellationToken ct)
    {
        // Synthesize AG-UI events from stored messages, each with replay=true
        foreach (LoomMessage msg in history)
        {
            string eventType = msg.Role switch
            {
                LoomMessageRole.Assistant => "TEXT_MESSAGE_CONTENT",
                LoomMessageRole.Tool => "TOOL_CALL_ARGS",
                _ => "TEXT_MESSAGE_CONTENT"
            };

            await response.WriteAsync(
                $"event: {eventType}\ndata: {{\"delta\": {System.Text.Json.JsonSerializer.Serialize(msg.Content)}, \"replay\": true}}\n\n", ct);
            await response.Body.FlushAsync(ct);
        }
    }

    private static Task WriteAguiEvent(
        HttpResponse response, object evt, CancellationToken ct)
    {
        // Serialize AG-UI event to SSE format
        // The exact event types depend on Microsoft.Agents.AI.Hosting.AGUI.AspNetCore
        string json = System.Text.Json.JsonSerializer.Serialize(evt);
        return response.WriteAsync($"data: {json}\n\n", ct);
    }

    private static async Task TrackStageFromEvent(
        object evt, LoomSession session, LoomSessionStore store, CancellationToken ct)
    {
        // Track stage transitions based on tool call events
        // The exact event type hierarchy depends on the SDK
        // Example: if evt carries toolName "root_cause" → stage = RootCause
        // This is a placeholder — adapt to actual SDK event types
    }
}
```

**Important notes:**
- The AG-UI SSE protocol may be handled by `MapAGUI()` from the SDK. If so, `LoomAguiEndpoints` should wrap `MapAGUI()` with session lifecycle management rather than reimplementing SSE serialization.
- The `RunStreamingAsync` return type and event model needs verification against rc3 SDK.
- `WriteAguiEvent` is a simplified placeholder — the actual AG-UI protocol uses specific event types (RUN_STARTED, TEXT_MESSAGE_CONTENT, etc.) that the SDK handles.

- [ ] **Step 2: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 3: Commit**

```bash
git add src/qyl.collector/Copilot/LoomAguiEndpoints.cs
git commit -m "feat(copilot): add LoomAguiEndpoints for conversational AG-UI SSE"
```

### Task 12: Migrate LoomExplorerService to IssueContextBuilder

**Files:**
- Modify: `src/qyl.collector/Autofix/LoomExplorerService.cs:43,162-191`

**Context:** Replace inline `BuildContextBlock` (lines 162-191) with delegation to `IssueContextBuilder`. The call site is at line 43: `string contextBlock = BuildContextBlock(issue, events, userContext);`

- [ ] **Step 1: Add IssueContextBuilder to primary constructor**

Add `IssueContextBuilder contextBuilder` to the primary constructor parameters of `LoomExplorerService`.

- [ ] **Step 2: Replace the call site (line 43)**

Before:
```csharp
string contextBlock = BuildContextBlock(issue, events, userContext);
```

After:
```csharp
IssueContext ctx = await contextBuilder.BuildAsync(issueId, userContext, ct: ct);
string contextBlock = ctx.FormattedBlock;
```

- [ ] **Step 3: Delete the BuildContextBlock method (lines 160-191)**

Remove the entire `BuildContextBlock` method and its section comment.

- [ ] **Step 4: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Autofix/LoomExplorerService.cs
git commit -m "refactor(autofix): migrate LoomExplorerService to IssueContextBuilder"
```

### Task 13: Migrate LoomInsightService to IssueContextBuilder

**Files:**
- Modify: `src/qyl.collector/Autofix/LoomInsightService.cs:39,90-107`

**Context:** Same pattern as Task 12. The call site is at line 39: `string context = BuildContextBlock(issue, events);`. The `BuildContextBlock` method is at lines 90-107.

- [ ] **Step 1: Add IssueContextBuilder to primary constructor**

- [ ] **Step 2: Replace the call site (line 39)**

Before:
```csharp
string context = BuildContextBlock(issue, events);
```

After:
```csharp
IssueContext ctx = await contextBuilder.BuildAsync(issueId, ct: ct);
string context = ctx.FormattedBlock;
```

Note: No `userContext` parameter here — LoomInsightService doesn't have one.

- [ ] **Step 3: Delete the BuildContextBlock method (lines 90-107)**

- [ ] **Step 4: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Autofix/LoomInsightService.cs
git commit -m "refactor(autofix): migrate LoomInsightService to IssueContextBuilder"
```

### Task 14: Add deprecation markers to LoomEndpoints

**Files:**
- Modify: `src/qyl.collector/Autofix/LoomEndpoints.cs`

**Context:** The `/explore` and `/code-it-up` endpoints are deprecated but kept for backward compatibility. Add `[Obsolete]` markers. The `/insight` endpoint stays unchanged.

- [ ] **Step 1: Read current LoomEndpoints.cs**

Read the file to find the `MapLoomEndpoints` method and identify the explore and code-it-up endpoint registrations.

- [ ] **Step 2: Add deprecation comments**

Add comments to the `explore` and `code-it-up` endpoint registrations indicating they are deprecated and replaced by `/api/v1/loom/{issueId}/chat`.

- [ ] **Step 3: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 4: Commit**

```bash
git add src/qyl.collector/Autofix/LoomEndpoints.cs
git commit -m "chore(autofix): mark LoomEndpoints explore/code-it-up as deprecated"
```

---

## Chunk 5: Integration (Phase 5)

Program.cs DI wiring and TriagePipelineService background execution.

### Task 15: Program.cs DI wiring

**Files:**
- Modify: `src/qyl.collector/Program.cs:186-200,396-399,861`

**Context:** Add DI registrations for `LoomSessionStore`, `IssueContextBuilder`, `IIssueContextSource`, `ObservabilityContextProvider`, `LoomAgent`. Replace the current AG-UI endpoint with `LoomAguiEndpoints`. Keep `AddQylAgui()` for SSE infrastructure.

- [ ] **Step 1: Add Loom DI registrations (after line 209)**

After the existing `TriagePipelineService` registration (line 209), add:

```csharp
// Loom session persistence and context
builder.Services.AddSingleton<LoomSessionStore>();
builder.Services.AddSingleton<IssueContextBuilder>();
builder.Services.AddSingleton<IIssueContextSource>(sp =>
    sp.GetRequiredService<IssueContextBuilder>());
builder.Services.AddSingleton<ObservabilityContextProvider>();
```

Add required usings:
```csharp
using Qyl.Agents.Context;
using Qyl.Contracts.Copilot;
```

- [ ] **Step 2: Replace AG-UI endpoint mapping (lines 396-399)**

Before:
```csharp
if (app.Services.GetService<IChatClient>() is { } aguiChatClient)
{
    app.MapQylAguiChat(QylAgentBuilder.FromChatClient(aguiChatClient, agentName: "qyl-llm"));
}
```

After:
```csharp
if (app.Services.GetService<IChatClient>() is { } aguiChatClient)
{
    // Generic AG-UI chat (no issue context)
    app.MapQylAguiChat(QylAgentBuilder.FromChatClient(aguiChatClient, agentName: "qyl-llm"));

    // Loom conversational AG-UI (issue-aware)
    var observabilityTools = app.Services.GetRequiredService<IReadOnlyList<AITool>>();
    var contextProvider = app.Services.GetRequiredService<ObservabilityContextProvider>();
    var loomAgent = LoomAgent.Create(
        aguiChatClient, observabilityTools, [contextProvider]);
    app.MapLoomAguiEndpoints(loomAgent);
}
```

Add required usings:
```csharp
using Qyl.Agents.Agents;
using Qyl.Collector.Copilot;
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 4: Commit**

```bash
git add src/qyl.collector/Program.cs
git commit -m "feat(collector): wire Loom DI registrations and LoomAguiEndpoints"
```

### Task 16: TriagePipelineService background LoomAgent execution

**Files:**
- Modify: `src/qyl.collector/Autofix/TriagePipelineService.cs`

**Context:** Add `ExecuteBackgroundLoomAsync` method that creates headless sessions and runs `LoomAgent` autonomously. The triage pipeline already calls autofix — add Loom as an additional step for high-confidence issues.

- [ ] **Step 1: Add LoomSessionStore and AIAgent to constructor**

Add to the primary constructor (line 10-15):
```csharp
LoomSessionStore? loomSessionStore = null,
AIAgent? loomAgent = null
```

These are optional (null) so existing DI registration doesn't break.

- [ ] **Step 2: Add ExecuteBackgroundLoomAsync method**

```csharp
private async Task ExecuteBackgroundLoomAsync(string issueId, CancellationToken ct)
{
    if (loomAgent is null || loomSessionStore is null) return;

    LoomSession session = await loomSessionStore.CreateAsync(
        issueId, LoomSessionMode.Background, ct);

    var agentSession = await loomAgent.CreateSessionAsync(ct);
    agentSession.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, issueId);
    agentSession.StateBag.SetValue(LoomTools.SessionIdKey, session.SessionId);

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

    try
    {
        session.Status = LoomStatus.Active;
        await loomSessionStore.UpdateAsync(session, ct);

        await foreach (var _ in loomAgent.RunStreamingAsync(
            "Investigate this error. Identify the root cause and propose a solution.",
            session: agentSession,
            cancellationToken: timeoutCts.Token))
        {
            // Events consumed but not streamed (no SSE for background)
            // Stage tracking would be done via tool call interception
        }

        session.Status = LoomStatus.Completed;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        session.Status = LoomStatus.Failed;
        session.Error = ex.Message;
    }

    await loomSessionStore.UpdateAsync(session, ct);
}
```

Add required usings:
```csharp
using Qyl.Agents.Agents;
using Qyl.Agents.Context;
using Qyl.Contracts.Autofix;
```

- [ ] **Step 3: Call ExecuteBackgroundLoomAsync from the triage pipeline**

Find the location in the triage method where high-confidence issues trigger autofix. Add a call to `ExecuteBackgroundLoomAsync` alongside or after the existing autofix call.

- [ ] **Step 4: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Autofix/TriagePipelineService.cs
git commit -m "feat(autofix): add background LoomAgent execution to TriagePipelineService"
```

---

## Chunk 6: Salvage + Cleanup (Phase 6)

Move salvageable files from qyl.loom, delete the project, remove Copilot SDK.

### Task 17: Salvage StatisticalMath and DistributionComparer

**Files:**
- Copy+modify: `src/qyl.loom/StatisticalMath.cs` → `src/qyl.collector/Analytics/StatisticalMath.cs`
- Copy+modify: `src/qyl.loom/DistributionComparer.cs` → `src/qyl.collector/Analytics/DistributionComparer.cs`
- Copy+modify: `src/qyl.loom/AnomalyTypes.cs` → `src/qyl.collector/Analytics/AnomalyTypes.cs`

**Context:** Only change is namespace: `Qyl.Loom` → `Qyl.Collector.Analytics`. All code is pure static math — no dependencies.

- [ ] **Step 1: Create Analytics directory if needed**

```bash
mkdir -p src/qyl.collector/Analytics
```

Check if files already exist (e.g., `AnomalyService.cs`).

- [ ] **Step 2: Copy and update namespace for each file**

For each file:
1. Copy from `src/qyl.loom/` to `src/qyl.collector/Analytics/`
2. Replace `namespace Qyl.Loom;` with `namespace Qyl.Collector.Analytics;`

- [ ] **Step 3: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 4: Commit**

```bash
git add src/qyl.collector/Analytics/StatisticalMath.cs src/qyl.collector/Analytics/DistributionComparer.cs src/qyl.collector/Analytics/AnomalyTypes.cs
git commit -m "feat(analytics): salvage StatisticalMath, DistributionComparer, AnomalyTypes from qyl.loom"
```

**Note on LoomModels.cs:** The spec lists `qyl.loom/LoomModels.cs` as a salvage target, but its types (`LoomCausalStep`, `LoomSolutionStep`, `LoomInsight`, `LoomRootCause`, `LoomSolution`) are already defined in the new `qyl.contracts/Autofix/LoomSessionTypes.cs` (Task 2) and in the existing `qyl.collector/Autofix/LoomExplorerService.cs` models. The `LoomJsonContext` source generator moves to where the serializable types live. No separate file copy needed — Task 2 supersedes this.

### Task 18: Salvage AutofixConstants and AutofixArtifacts

**Files:**
- Copy+modify: `src/qyl.loom/AutofixConstants.cs` → `src/qyl.collector/Autofix/AutofixConstants.cs`
- Copy+modify: `src/qyl.loom/AutofixArtifacts.cs` → `src/qyl.collector/Autofix/AutofixArtifacts.cs`

**Context:** Namespace change: `Qyl.Loom` → `Qyl.Collector.Autofix`. Check for naming conflicts — there may already be types with these names in the collector's Autofix directory.

- [ ] **Step 1: Check for existing files with same names**

```bash
ls src/qyl.collector/Autofix/AutofixConstants.cs src/qyl.collector/Autofix/AutofixArtifacts.cs 2>/dev/null
```

If they exist, compare contents and merge rather than overwrite.

- [ ] **Step 2: Copy and update namespaces**

- [ ] **Step 3: Verify build**

Run: `dotnet build src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded, 0 warnings

- [ ] **Step 4: Commit**

```bash
git add src/qyl.collector/Autofix/AutofixConstants.cs src/qyl.collector/Autofix/AutofixArtifacts.cs
git commit -m "feat(autofix): salvage AutofixConstants and AutofixArtifacts from qyl.loom"
```

### Task 19: Remove qyl.loom ProjectReference and delete project

**Files:**
- Modify: `src/qyl.collector/qyl.collector.csproj` — remove ProjectReference to qyl.loom
- Modify: `qyl.slnx` — remove qyl.loom entry
- Delete: `src/qyl.loom/` (entire directory)

**Context:** After salvage, all valuable code has been moved. The remaining 28 files in qyl.loom are byte-for-byte duplicates of collector counterparts. The project is dead code.

- [ ] **Step 1: Remove ProjectReference from qyl.collector.csproj**

Search for `qyl.loom` in `src/qyl.collector/qyl.collector.csproj` and remove the `<ProjectReference>` line.

- [ ] **Step 2: Remove from qyl.slnx**

Search for `qyl.loom` in `qyl.slnx` and remove the project entry.

- [ ] **Step 3: Verify build WITHOUT qyl.loom**

Run: `dotnet build qyl.slnx`
Expected: Build succeeded. If there are compilation errors from missing types, those types were not properly salvaged — go back and salvage them.

- [ ] **Step 4: Delete the qyl.loom directory**

```bash
rm -rf src/qyl.loom
```

- [ ] **Step 5: Verify solution still builds**

Run: `dotnet build qyl.slnx`
Expected: Build succeeded, 0 warnings

- [ ] **Step 6: Commit**

```bash
git add -u
git commit -m "chore: delete qyl.loom project — all salvageable code moved to collector"
```

### Task 20: Remove Copilot SDK dependency and delete adapter

**Files:**
- Modify: `src/qyl.agents/qyl.agents.csproj` — remove `Microsoft.Agents.AI.GitHub.Copilot` package
- Delete: `src/qyl.agents/Adapters/QylCopilotAdapter.cs`
- Delete: `src/qyl.agents/Instrumentation/CopilotSessionStore.cs` (if exists)
- Delete: `src/qyl.agents/Auth/CopilotAuthProvider.cs` (if exists)
- Delete: `src/qyl.agents/Auth/GitHubPkceFlow.cs` (if exists)
- Modify: `src/qyl.agents/Agents/QylAgentBuilder.cs` — remove `FromCopilotAdapter` method (lines 33-37)
- Delete: `src/qyl.collector/Copilot/CopilotEndpoints.cs`
- Modify: `src/qyl.collector/Program.cs` — remove CopilotAuthOptions (lines 196-199), `MapCopilotEndpoints` (line 394)

**Context:** The Copilot SDK (`Microsoft.Agents.AI.GitHub.Copilot`) is no longer needed after LoomAgent replaces QylCopilotAdapter. All Copilot-specific auth, endpoints, and session handling are removed.

- [ ] **Step 1: Remove Copilot package from .csproj**

In `src/qyl.agents/qyl.agents.csproj`, remove:
```xml
<PackageReference Include="Microsoft.Agents.AI.GitHub.Copilot" />
```

- [ ] **Step 2: Delete Copilot adapter files**

```bash
rm -f src/qyl.agents/Adapters/QylCopilotAdapter.cs
rm -f src/qyl.agents/Instrumentation/CopilotSessionStore.cs
rm -f src/qyl.agents/Auth/CopilotAuthProvider.cs
rm -f src/qyl.agents/Auth/GitHubPkceFlow.cs
```

Check which of these actually exist first.

- [ ] **Step 3: Remove FromCopilotAdapter from QylAgentBuilder**

Delete lines 25-37 (the `FromCopilotAdapter` method and its doc comment). Also remove the `using Qyl.Agents.Adapters;` import if no longer needed.

- [ ] **Step 4: Delete CopilotEndpoints.cs**

```bash
rm -f src/qyl.collector/Copilot/CopilotEndpoints.cs
```

- [ ] **Step 5: Clean up Program.cs**

Remove:
- `CopilotAuthOptions` registration (lines 196-199)
- `app.MapCopilotEndpoints();` (line 394)
- Any unused `using` statements related to Copilot

- [ ] **Step 6: Verify solution builds**

Run: `dotnet build qyl.slnx`
Expected: Build succeeded, 0 warnings

- [ ] **Step 7: Commit**

```bash
git add -u
git commit -m "chore: remove Copilot SDK dependency and delete QylCopilotAdapter"
```

---

## Chunk 7: Final Verification

### Task 21: Full build and test verification

- [ ] **Step 1: Full solution build**

Run: `dotnet build qyl.slnx`
Expected: Build succeeded, 0 warnings

- [ ] **Step 2: Run all tests**

Run: `dotnet test qyl.slnx -v minimal`
Expected: All tests pass, including new LoomSessionStore and IssueContextBuilder tests

- [ ] **Step 3: Verify migration applies**

The `loom_sessions` and `loom_messages` tables should be created automatically when `DuckDbStore` is constructed (migration runner picks up the new SQL file).

- [ ] **Step 4: Verify verification gates from spec**

Cross-check against spec section "Verification gates" (lines 1286-1299):

| # | Gate | How to verify |
|---|------|---------------|
| 1 | Build with zero warnings | `dotnet build qyl.slnx` |
| 2 | Schema tables created on startup | Check `DuckDbStore(":memory:")` applies migration |
| 3 | Interactive flow | Manual: `POST /api/v1/loom/{issueId}/chat` streams events |
| 4 | Tool signals | Manual: Check SSE for `TOOL_CALL_START` with `root_cause` |
| 5 | Interrupt | Manual: `POST /interrupt` cancels running agent |
| 6 | Background | Unit test: TriagePipelineService creates headless sessions |
| 7 | Handoff | Unit test: `GetPendingHandoffsAsync` + attach flow |
| 8 | Salvage | Build: StatisticalMath compiles in Analytics/ |
| 9 | Cleanup | Verify: qyl.loom gone, CopilotAdapter gone, Copilot SDK gone |
| 10 | Tests | `dotnet test` — LoomSessionStore, IssueContextBuilder, ObservabilityContextProvider |

Gates 3-6 require a running instance with an LLM configured. Gates 1-2, 7-10 can be verified in CI.

- [ ] **Step 5: Final commit with plan completion marker**

Move spec status from "Draft" to "Implemented":

```bash
# In docs/superpowers/specs/2026-03-12-conversational-loom-design.md
# Change: > **Status:** Draft
# To:     > **Status:** Implemented
```
