# Issue Fingerprinting

> Owner: collector
> SSOT: YES (error grouping algorithm, normalization rules, issue lifecycle)
> Depends on: `telemetry-data-model.md` (error_issues schema)
> Used by: `telemetry-intelligence.md` (pattern input), `loom.md` (investigation trigger)

Deterministic error grouping algorithm. Groups multiple error occurrences into stable issues. Grouping is **service-scoped** — the same exception in different services produces different fingerprints.

Implementation: `src/qyl.collector/Errors/ErrorFingerprinter.cs` (Version 2)

Code context attributes for Loom integration: see `instrumentation.md` section 5.1 (SSOT).

---

## 1. Algorithm

### 1.1 Base Fingerprint

```text
fingerprint = SHA256(
    service_name + "\n"
    + exception_type + "\n"
    + normalize_message(message) + "\n"
    + normalize_stacktrace(stacktrace)
    + "\n" + span_name       // if present
)[0:16]   // 64-bit hex
```

`service.name` is the first input. This scopes fingerprints per service — cross-service merging does not happen. `span.name` is a secondary grouping dimension when present.

### 1.2 GenAI-Aware Grouping

GenAI errors get special treatment based on `category`. `service.name` is prepended to all GenAI fingerprints.

| Category | Grouping Key | Rationale |
|----------|-------------|-----------|
| `rate_limit` | `{service}\nrate_limit\n{provider}` | Same provider = same issue regardless of message |
| `content_filter` | `{service}\ncontent_filter\n{model}` | Same model = same content policy |
| `token_limit` | `{service}\ntoken_limit\n{model}` | Same model = same context window |
| (other GenAI) | base + provider + finish_reason | Additional dimensions for specificity |

### 1.3 Inputs

```csharp
ErrorFingerprinter.Compute(
    exceptionType,      // e.g. "NullReferenceException"
    message,            // e.g. "Object reference not set to an instance of an object"
    stackTrace,         // full stacktrace string
    genAiOperation?,    // e.g. "chat", "completion"
    genAiProvider?,     // e.g. "openai", "anthropic"
    genAiModel?,        // e.g. "gpt-4o", "claude-sonnet-4-6"
    finishReason?,      // e.g. "stop", "length", "content_filter"
    category?,          // e.g. "rate_limit", "content_filter", "token_limit"
    serviceName?,       // e.g. "checkout-api" — scopes fingerprint per service
    spanName?           // e.g. "POST /api/orders" — secondary grouping dimension
)
```

### 1.4 Version

`ErrorFingerprinter.Version = 2`. Stored with each issue for migration when the algorithm changes.

---

## 1b. Error Categorization

Before fingerprinting, errors are classified by `ErrorCategorizer.cs`. Multi-level strategy (priority order):

1. **GenAI error type** (`gen_ai.error.type` attribute):
   - `rate_limit_exceeded` → `rate_limit`
   - `context_length_exceeded` → `token_limit`
   - `content_filter` / `content_policy_violation` → `content_filter`
   - `tool_execution_error` / `tool_not_found` → `tool_execution_error`

2. **Finish reason** (if GenAI error type absent):
   - Contains "content_filter" → `content_filter`
   - Contains "length" → `token_limit`

3. **Error message pattern matching:**
   - "rate limit" / "429" → `rate_limit`
   - "content filter" / "content_policy" → `content_filter`
   - "maximum context length" → `token_limit`

4. **Exception type** (.NET):
   - `HttpRequestException`, `SocketException` → `network`
   - `TimeoutException`, `TaskCanceledException` → `timeout`
   - `DbException`, `DuckDBException` → `database`
   - `ArgumentException`, `FormatException` → `validation`
   - `NullReferenceException` → `internal`

The category feeds into the fingerprinter's GenAI-aware grouping (section 1.2).

### 1c. Error Extraction

`ErrorExtractor.cs` maps OTLP spans (status_code = ERROR) to `ErrorEvent` records:

```text
span attributes → ErrorEvent
─────────────────────────────
exception.type (or error.type, or span.Name)  → ErrorType
exception.message (or status_message)         → Message
exception.stacktrace                          → StackTrace (for fingerprinting)
service.name                                  → ServiceName (for fingerprint scoping)
span.name                                     → SpanName (secondary grouping)
gen_ai.operation.name                         → GenAiOperation
gen_ai.system                                 → GenAiProvider
gen_ai.request.model                          → GenAiModel
gen_ai.response.finish_reasons                → FinishReasons
gen_ai.tool.name                              → ToolName
gen_ai.agent.name / gen_ai.agent.id           → AgentName / AgentId
enduser.id / user.id                          → UserId
```

---

## 2. Normalization

### 2.1 Stack Trace Normalization

Three-stage pipeline. Order matters.

**Stage 1: Remove volatile location info**

| Pattern | Action | Example |
|---------|--------|---------|
| ` in /path/to/file.cs: line 42` | Remove line numbers | Prevents split on code reformatting |
| ` in /path/to/file.cs` | Remove file paths | Prevents split on deployment path changes |

**Stage 2: Collapse framework frames**

Consecutive .NET framework/pipeline frames are collapsed into a single `[framework]` marker. This prevents ASP.NET Core middleware pipeline depth from splitting otherwise-identical issues.

Collapsed namespaces:
- `Microsoft.AspNetCore.*`
- `Microsoft.Extensions.*`
- `System.Runtime.*`
- `System.Threading.*`
- `System.Private.CoreLib`

Example:
```text
at CheckoutService.ProcessOrder()        ← user code (kept)
at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.Execute()
at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next()
at Microsoft.AspNetCore.Routing.EndpointMiddleware.Invoke()
                                          ↑ collapsed to [framework]
at Program.<Main>$()                      ← user code (kept)
```

**Stage 3: Trim whitespace**

### 2.2 Message Normalization

Removes dynamic content that would create unique fingerprints per occurrence:

| Pattern | Replacement | Example |
|---------|-------------|---------|
| GUIDs | `<GUID>` | `a1b2c3d4-e5f6-...` → `<GUID>` |
| Numbers (5+ digits) | `<N>` | `Order 123456 failed` → `Order <N> failed` |
| URLs | `<URL>` | `https://api.example.com/v1/users` → `<URL>` |

---

## 3. Issue Lifecycle

### 3.1 Upsert Flow

```text
ErrorEvent arrives (with ServiceName, SpanName)
    ↓
ErrorFingerprinter.Compute(..., serviceName, spanName) → fingerprint
    ↓
IssueService.UpsertIssueAsync(project_id, fingerprint, ...)
    ↓
  ┌─ fingerprint exists? → increment occurrence_count, update last_seen_at
  └─ new fingerprint?    → INSERT new error_issue (status: unresolved, priority: medium)
    ↓
IssueService.LinkEventAsync(issue_id, trace_id, span_id, stack_trace, ...)
    ↓
error_issue_events row created (links occurrence to issue)
```

### 3.2 Stability Guarantees

- Same service + same exception type + same normalized stack → same fingerprint. Always.
- Different services → different fingerprints. Always. (service.name is in the hash input)
- Code reformatting (line number changes) → same fingerprint. (line numbers stripped)
- Deployment path changes → same fingerprint. (file paths stripped)
- Framework version upgrades (middleware frame count changes) → same fingerprint. (framework frames collapsed)

### 3.3 Status Transitions

```text
unresolved → acknowledged → investigating → in_progress → resolved
    ↑                                                          ↓
  ignored ← unresolved                                    regressed → acknowledged
```

Enforced by `IssueService.TransitionStatusAsync()`. Invalid transitions throw `InvalidOperationException`.

### 3.4 Regression Detection

When a resolved issue gets a new occurrence, the status transitions to `regressed` and `regression_count` increments. This signals Loom to re-investigate.

---

## 4. Agent Integration Points

### 4.1 What Loom gets from a fingerprint

Given an `issue_id`, Loom can retrieve:

1. **Issue summary** — `error_issues` row (type, category, occurrence count, first/last seen)
2. **All occurrences** — `error_issue_events` (each with trace_id, span_id, stack_trace, environment, release_version)
3. **Trace context** — join events → spans → full trace graph
4. **Code location** — spans have `code.filepath`, `code.function`, `code.lineno` (guaranteed if instrumented with `[Traced]`)
5. **Deployment context** — `deployments` table has `git_commit`, `service_version` per deploy
6. **Historical fixes** — `fix_runs` table linked to issue_id

### 4.2 RCA Data Flow

```text
issue (fingerprint, service-scoped)
    → error_issue_events (trace_ids)
        → spans (code.filepath, code.function, exception.stacktrace)
            → deployments (git_commit, service_version)
                → Loom: root cause analysis
                    → Loom: diff generation
                        → Loom: confidence scoring
```

### 4.3 Fix History Learning

Same fingerprint = same bug class. Loom can:

1. Query `fix_runs WHERE issue_id = ?` for past fixes
2. Check if past fixes resolved the issue (status history)
3. Use successful past diffs as context for new fix generation
4. Adjust confidence scoring based on fix success rate

---

## 5. Known Gaps

| Gap | Impact | Priority |
|-----|--------|----------|
| `code.filepath` not set on `[GenAi]`/`[Db]` spans | Code location only available for `[Traced]` methods | Medium — runtime API change needed |
| No `fingerprint_version` column in `error_issues` | Cannot re-fingerprint old issues when algorithm changes | Medium — requires schema migration |
| No configurable grouping rules | Users can't customize grouping behavior | Low — current algorithm covers common cases |
