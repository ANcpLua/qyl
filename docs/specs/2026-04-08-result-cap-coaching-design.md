# Result Cap Coaching

**Date:** 2026-04-08
**Status:** Approved

## What this does

When a tool returns its maximum number of results, the LLM doesn't know there might be more data. It stops investigating or tries to paginate — both waste tokens.

The fix: append a one-line coaching message that tells the LLM to narrow its query.

## Before and after

### Before (no coaching)

The LLM calls `list_error_issues(limit=50)` and gets 50 results back:

```
# Error Issues (50 of 312)

| Status | Priority | Title | ... |
|--------|----------|-------|-----|
| unresolved | high | NullRef in AuthService | ... |
...50 rows...

## Next steps
- Use `get_error_issue(issueId: '<id>')` for full details
```

The LLM sees 50 rows and has no signal that 262 more exist or what to do about it.

### After (with coaching)

Same call, same 50 results, but now:

```
# Error Issues (50 of 312)

| Status | Priority | Title | ... |
...50 rows...

## Next steps
- Use `get_error_issue(issueId: '<id>')` for full details

**Note:** Showing 50 results (maximum). Filter by status or priority to narrow results.
```

The LLM reads the note and calls `list_error_issues(status="unresolved", priority="high")` — gets 8 focused results instead of paginating through 312.

### Tool descriptions also change

Before:
```
List fingerprinted error groups (issues) with optional filtering.
```

After:
```
List fingerprinted error groups (issues) with optional filtering.
Returns up to 50 results. Use filters to narrow if needed.
```

The LLM knows the limit *before* it calls the tool.

## How it works

### One new method: `ResponseFormatter.AppendResultCap`

```csharp
public static void AppendResultCap(
    StringBuilder sb,
    int returnedCount,
    int requestedLimit,
    string narrowHint)
{
    if (returnedCount < requestedLimit)
        return;

    sb.AppendLine();
    sb.AppendLine(CultureInfo.InvariantCulture,
        $"**Note:** Showing {returnedCount} results (maximum). {narrowHint}");
}
```

If results came back less than the limit — do nothing. If results hit the limit — append the note.

### `FormatPagedList` calls it automatically

Add one new parameter (`requestedLimit`) and call `AppendResultCap` at the end:

```csharp
public static string FormatPagedList<T>(
    PagedResult<T> result,
    int requestedLimit,        // NEW
    string title,
    Func<T, string> rowFormatter,
    string searchToolName,
    string detailToolName,
    string detailIdParam)
```

The 5 tools already using `FormatPagedList` pass their `limit` variable — they already have it from `Math.Clamp`:
- `SearchTracesTool` (default 25)
- `SearchSessionsTool` (default 25)
- `SearchLogsTool` (default 25)
- `ListTeamsTool` (default 25)
- `ListProjectsTool` (default 25)

### Ad-hoc tools add one line each

Tools that build responses with `StringBuilder` add this before `return sb.ToString()`:

```csharp
ResponseFormatter.AppendResultCap(sb, items.Count, limit,
    "Filter by status or priority to narrow results.");
```

Each tool gets its own hint message:

| Tool | Hint |
|------|------|
| `list_structured_logs` | `"Filter by level, session, or trace ID to narrow results."` |
| `search_logs` (StructuredLogTools) | `"Add minSeverity or narrow the time window."` |
| `list_error_issues` | `"Filter by status or priority to narrow results."` |
| `find_similar_errors` | `"Provide a more specific span ID."` |
| `list_genai_spans` | `"Filter by service or model name to narrow results."` |
| `search_spans` | `"Add service, operation, or time range filters."` |
| `list_sessions` | `"Filter by service_name to narrow results."` |
| `list_services` | `"Filter by service name to narrow results."` |
| `list_triage` | `"Filter by status to narrow results."` |
| `list_regressions` | `"Filter by issue ID to narrow results."` |
| `list_github_events` | `"Filter by event type or repository to narrow results."` |
| `list_fix_runs` | `"Filter by issue ID to narrow results."` |

### Tools that skip coaching (already narrow)

These tools are already scoped to a specific resource — capping makes no sense:
- `list_trace_logs` — scoped to one trace, limit 500 is intentional
- `get_error_issue` events — scoped to one issue
- `export_for_agent` — fixed 3-event fetch
- `generate_test_from_error` — fixed 3-event fetch

## Constraints

- **No shared `RESULT_LIMIT` constant.** Tools have legitimately different defaults (25 for search, 50 for errors, 100 for spans). A single constant would be wrong.
- **No migration of ad-hoc tools to `FormatPagedList`.** Different tools need different formats (tables, grouped by span, etc.).
- **No filter-level enforcement.** Coaching is explicit and context-aware at the tool level — each tool provides its own `narrowHint`.
- **Fixed-scope tools skip coaching.** `list_trace_logs`, `get_error_issue` events, `export_for_agent` — when the query is already maximally narrow, capping makes no sense.

## All files that change

| File | What changes |
|------|-------------|
| `Formatting/ResponseFormatter.cs` | Add `AppendResultCap` method, add `requestedLimit` param to `FormatPagedList` |
| `Tools/Traces/SearchTracesTool.cs` | Pass `limit` to `FormatPagedList` |
| `Tools/Sessions/SearchSessionsTool.cs` | Pass `limit` to `FormatPagedList` |
| `Tools/Logs/SearchLogsTool.cs` | Pass `limit` to `FormatPagedList` |
| `Tools/Management/ListTeamsTool.cs` | Pass `limit` to `FormatPagedList` |
| `Tools/Discovery/ListProjectsTool.cs` | Pass `limit` to `FormatPagedList` |
| `Tools/StructuredLogTools.cs` | Add `AppendResultCap` + update descriptions |
| `Tools/ErrorTools.cs` | Add `AppendResultCap` + update descriptions |
| `Tools/GenAiTools.cs` | Add `AppendResultCap` + update description |
| `Tools/SpanQueryTools.cs` | Add `AppendResultCap` + update description |
| `Tools/ReplayTools.cs` | Add `AppendResultCap` + update description |
| `Tools/ServiceTools.cs` | Add `AppendResultCap` + update description |
| `Tools/TriageTools.cs` | Add `AppendResultCap` + update description |
| `Tools/RegressionTools.cs` | Add `AppendResultCap` + update description |
| `Tools/GitHubMcpTools.cs` | Add `AppendResultCap` + update description |
| `Tools/AutofixMcpTools.cs` | Add `AppendResultCap` + update description |
| `CLAUDE.md` | Add constraints section |

## Done when

- Every list/search tool that can hit a cap includes the coaching note when `returnedCount >= requestedLimit`
- Every list/search tool description mentions its default limit
- `FormatPagedList` callers get it for free via the new parameter
- Fixed-scope tools are explicitly excluded
- Build passes, no new warnings
