# Feature: MCP Session Replay

> **Status:** Ready
> **Effort:** ~2h
> **Backend:** Yes
> **Priority:** P1

---

## Problem

Users cannot replay past AI sessions through MCP tools. When debugging issues, they must manually re-run prompts or dig
through raw span data.

## Solution

Add `replay_session` MCP tool that streams stored session spans back as if they were happening live, enabling debugging
and demo purposes.

---

## Context

### Dashboard Location

```
/Users/ancplua/qyl/src/qyl.mcp/
```

### Stack (DO NOT CHANGE)

| Tech                 | Version | Notes                  |
|----------------------|---------|------------------------|
| .NET                 | 10.0    | Native AOT             |
| C#                   | 14      | Extension blocks, Lock |
| ModelContextProtocol | 0.3.0   | MCP SDK                |
| System.Net.Http      | BCL     | HTTP client            |

### Patterns

```csharp
// MCP Tool pattern
[McpServerToolType]
public sealed class MyTool
{
    [McpServerTool("tool_name"), Description("What it does")]
    public async Task<string> Execute(string param)
    {
        // Implementation
    }
}

// HTTP calls to collector
await _client.GetFromJsonAsync<T>("/api/v1/path");
```

---

## Files

| File                                             | Action | What                |
|--------------------------------------------------|--------|---------------------|
| `src/qyl.mcp/Tools/ReplayTools.cs`               | Create | Replay MCP tool     |
| `src/qyl.collector/Query/SessionQueryService.cs` | Modify | Add replay query    |
| `src/qyl.collector/Program.cs`                   | Modify | Add replay endpoint |

---

## Implementation

### Step 1: Add Replay Endpoint

**File:** `src/qyl.collector/Program.cs`

```csharp
app.MapGet("/api/v1/sessions/{sessionId}/replay", async (
    string sessionId,
    [FromQuery] double speed,
    SessionQueryService sessions,
    CancellationToken ct) =>
{
    var spans = sessions.GetSessionSpansForReplayAsync(sessionId, ct);

    return TypedResults.ServerSentEvents(
        ReplaySpans(spans, speed, ct),
        "span");
});

static async IAsyncEnumerable<SseItem<SpanRecord>> ReplaySpans(
    IAsyncEnumerable<SpanRecord> spans,
    double speed,
    [EnumeratorCancellation] CancellationToken ct)
{
    SpanRecord? previous = null;

    await foreach (var span in spans.WithCancellation(ct))
    {
        if (previous is not null)
        {
            var delay = (span.StartTime - previous.StartTime) / speed;
            if (delay > TimeSpan.Zero && delay < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(delay, ct);
            }
        }

        yield return SseItem.Create(span, "span");
        previous = span;
    }
}
```

### Step 2: Add Session Query

**File:** `src/qyl.collector/Query/SessionQueryService.cs`

```csharp
public async IAsyncEnumerable<SpanRecord> GetSessionSpansForReplayAsync(
    string sessionId,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    const string sql = """
        SELECT *
        FROM spans
        WHERE attributes->>'session.id' = ?
        ORDER BY start_time ASC
        """;

    await foreach (var span in _store.QueryAsync<SpanRecord>(sql, sessionId, ct))
    {
        yield return span;
    }
}
```

### Step 3: Create MCP Replay Tool

**File:** `src/qyl.mcp/Tools/ReplayTools.cs`

```csharp
using System.ComponentModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
public sealed class ReplayTools(HttpClient client)
{
    [McpServerTool("replay_session"), Description("""
        Replay a recorded AI session. Streams spans back in chronological order
        with timing delays to simulate the original execution.

        Parameters:
        - session_id: The session ID to replay
        - speed: Playback speed multiplier (default: 1.0, use 0 for instant)

        Returns: Stream of span events as they occurred
        """)]
    public async Task<string> ReplaySession(
        string session_id,
        double speed = 1.0)
    {
        var response = await client.GetAsync(
            $"/api/v1/sessions/{session_id}/replay?speed={speed}",
            HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            return $"Error: Session {session_id} not found";
        }

        var content = await response.Content.ReadAsStringAsync();
        return content;
    }

    [McpServerTool("list_replayable_sessions"), Description("""
        List sessions available for replay.

        Parameters:
        - limit: Max sessions to return (default: 20)
        - gen_ai_only: Only show sessions with GenAI spans (default: true)

        Returns: List of session IDs with summary info
        """)]
    public async Task<string> ListReplayableSessions(
        int limit = 20,
        bool gen_ai_only = true)
    {
        var url = $"/api/v1/sessions?limit={limit}&genAiOnly={gen_ai_only}";
        var sessions = await client.GetFromJsonAsync<SessionSummary[]>(url);

        if (sessions is null || sessions.Length == 0)
        {
            return "No sessions found";
        }

        var lines = sessions.Select(s =>
            $"- {s.SessionId}: {s.SpanCount} spans, {s.DurationMs:F0}ms, {s.StartTime:u}");

        return string.Join("\n", lines);
    }

    [McpServerTool("get_session_transcript"), Description("""
        Get a human-readable transcript of an AI session.
        Shows prompts, responses, and timing information.

        Parameters:
        - session_id: The session ID

        Returns: Formatted transcript of the session
        """)]
    public async Task<string> GetSessionTranscript(string session_id)
    {
        var spans = await client.GetFromJsonAsync<SpanRecord[]>(
            $"/api/v1/sessions/{session_id}");

        if (spans is null || spans.Length == 0)
        {
            return $"Session {session_id} not found or empty";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Session: {session_id}");
        sb.AppendLine($"Spans: {spans.Length}");
        sb.AppendLine();

        foreach (var span in spans.OrderBy(s => s.StartTime))
        {
            var model = span.Attributes.GetValueOrDefault("gen_ai.request.model", "unknown");
            var input = span.Attributes.GetValueOrDefault("gen_ai.usage.input_tokens", "?");
            var output = span.Attributes.GetValueOrDefault("gen_ai.usage.output_tokens", "?");

            sb.AppendLine($"## {span.Name}");
            sb.AppendLine($"- Model: {model}");
            sb.AppendLine($"- Tokens: {input} in / {output} out");
            sb.AppendLine($"- Duration: {span.DurationMs:F1}ms");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

internal record SessionSummary(
    string SessionId,
    int SpanCount,
    double DurationMs,
    DateTimeOffset StartTime);

internal record SpanRecord(
    string SpanId,
    string Name,
    DateTimeOffset StartTime,
    double DurationMs,
    Dictionary<string, object> Attributes);
```

---

## Gotchas

- SSE streaming requires `HttpCompletionOption.ResponseHeadersRead`
- Speed 0 = instant (no delays between spans)
- Cap delay at 10 seconds to prevent hanging on gaps
- Session ID from `session.id` attribute, not trace ID

---

## Test

```bash
# Start collector
dotnet run --project src/qyl.collector

# Test endpoint directly
curl -N "http://localhost:5100/api/v1/sessions/abc123/replay?speed=2"

# Test via MCP (in Claude)
# Use replay_session tool with a known session ID
```

- [ ] list_replayable_sessions returns sessions
- [ ] replay_session streams spans with timing
- [ ] get_session_transcript formats readable output
- [ ] Speed multiplier works (2x = half the wait time)
- [ ] No errors on missing session

---

## Backend (if needed)

### Endpoint

```
GET /api/v1/sessions/{sessionId}/replay?speed=1.0
```

### Request/Response

```json
// SSE Response (text/event-stream)
event: span
data: {"span_id":"abc","name":"chat","duration_ms":150,...}

event: span
data: {"span_id":"def","name":"completion","duration_ms":200,...}
```

---

*Template v3 - One prompt, one agent, done.*
