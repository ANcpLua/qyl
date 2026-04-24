// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Workflows.Prompts;

/// <summary>
///     MCP prompts that drive the Loom .NET SDK onboarding workflow. Each prompt takes the
///     JSON output of <c>loom_detect_dotnet</c> and returns a concrete directive the calling
///     agent can execute. Detection-first is enforced at the prompt level: every setup
///     recommendation names the detected framework, the chosen package, and the evidence
///     it was derived from.
/// </summary>
[McpServerPromptType]
internal sealed class OnboardingPrompts
{
    [McpServerPrompt(Name = "qyl.loom.setup_dotnet", Title = ".NET SDK setup (detect → recommend → guide)")]
    [Description("Full .NET SDK setup wizard preamble. Takes a detection JSON and optional feature list.")]
    public static string SetupWizard(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson,
        [Description("Comma-separated feature list: error,tracing,logging,profiling,metrics,crons. Default: error,tracing,logging.")]
        string? features = null) =>
        $$"""
          You are running the .NET SDK onboarding wizard for the user's project.

          ## Non-negotiable rules
          1. **Detect first, recommend second.** The detection JSON below is the only source of
             truth for framework / package choice. Do not pick a package that contradicts it.
          2. **Tracing is disabled by default.** Profiling requires tracing. Both
             `options.TracesSampleRate > 0` and `options.AddProfilingIntegration(...)` are needed.
          3. **`EnableLogs` and `EnableMetrics` are opt-in gates.** Without them, every
             `SentrySdk.Logger.*` / `SentrySdk.Metrics.*` call is a silent no-op. State this
             explicitly when adding those features.
          4. **Desktop apps require `IsGlobalModeEnabled = true`** (WPF, WinForms, Console).
             Missing this loses background-thread exceptions.
          5. **Serverless requires `FlushOnCompletedRequest = true`** (AWS Lambda, Azure Functions
             via Sentry.OpenTelemetry). Missing this loses events when the container freezes.
          6. **Never skip verification.** Finish with a test capture and confirm the event in the
             Issues dashboard.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          ## Features requested
          {{features ?? "error,tracing,logging"}}

          ## Your plan, in order
          1. Parse the detection JSON. If `framework == "Unknown"`, stop and ask the user which
             project to target — do not guess.
          2. If `existing_sentry_packages` is non-empty, skip install. Jump to feature configuration.
          3. Otherwise: run `dotnet add package <recommended_package> -v 6.1.0` in the project directory.
          4. Open `recommended_init_file`. Add the initialisation block for the detected framework.
          5. For each feature the user agreed to, load the matching feature prompt:
             - `qyl.loom.setup_dotnet_error` — error monitoring (scopes, enrichment, filtering)
             - `qyl.loom.setup_dotnet_tracing` — tracing (sampling, propagation, OTel)
             - `qyl.loom.setup_dotnet_profiling` — profiling (requires tracing, .NET 8+ only)
             - `qyl.loom.setup_dotnet_logging` — logging (ILogger / Serilog / NLog / log4net)
             - `qyl.loom.setup_dotnet_metrics` — metrics (EnableMetrics + SENTRY1001 analyzer)
             - `qyl.loom.setup_dotnet_crons` — crons (Hangfire auto, Quartz manual, heartbeat vs two-signal)
          6. If `sibling_frontend_dirs` is non-empty, point the user at the matching frontend SDK.
             Same project = distributed tracing across frontend + .NET server.
          7. Verify end-to-end: throw a test exception, confirm it appears, confirm stack traces
             show file names + line numbers (requires PDB upload via MSBuild
             `SentryOrg` / `SentryProject` / `SentryUploadSymbols` / `SENTRY_AUTH_TOKEN`).
          8. Remove the test throw and commit.

          Apply as a single coherent edit set, not a paragraph-by-paragraph tutorial.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_error", Title = "Error Monitoring")]
    [Description("Error monitoring feature prompt. Consumes detection JSON.")]
    public static string ErrorMonitoring(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Error Monitoring** for the detected project.

          ## Automatic vs manual capture (core rule)
          > "If you catch an exception and don't re-throw it, Sentry never sees it."

          Captured automatically:
          - Unhandled `AppDomain.CurrentDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`
          - ASP.NET Core request errors via Sentry middleware
          - WPF `DispatcherUnhandledException` (hook it in `App()` constructor, NOT `OnStartup`)
          - MAUI native + managed exceptions
          - WinForms with `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)`

          NOT captured:
          - `try/catch` with graceful return — call `SentrySdk.CaptureException(ex)` before the return.
          - Swallowed errors from background threads when `IsGlobalModeEnabled = false` on desktop.

          ## Scope discipline
          - `SentrySdk.ConfigureScope(...)` — session-level data (user, tenant, request).
          - Inline `configureScope` callback on `CaptureException` — one-off enrichment; scope is
            cloned, next event is NOT affected.
          - `SentrySdk.PushScope()` + `using` — temporary batch context.
          - Clear user on logout: `scope.User = new SentryUser();`.

          ## Filtering and fingerprinting
          - `options.SetBeforeSend((@event, hint) => ...)` — scrub / drop events.
          - `options.AddExceptionFilterForType<OperationCanceledException>()` — kill noise.
          - `@event.SetFingerprint(...)` with Sentry's default template variable (literal double-brace `default`) plus an extra dimension — extends grouping without replacing the stack hash.
          - Scrub `@event.ServerName`, remove sensitive headers, never log raw tokens.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          Apply the minimal change set for the detected framework. Quote concrete line numbers
          when you edit `recommended_init_file`.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_tracing", Title = "Tracing")]
    [Description("Tracing feature prompt — enforces that tracing must be explicitly enabled.")]
    public static string Tracing(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Tracing** for the detected project.

          ## Enablement gate
          Tracing is **disabled by default.** It turns on only when **one** of the following is set
          during `SentrySdk.Init`:
          - `options.TracesSampleRate = 0.2;` (uniform rate 0.0–1.0)
          - `options.TracesSampler = ctx => …;` (per-transaction dynamic; overrides TracesSampleRate)

          Neither set → no transactions, no spans, no AI monitoring either (AI inherits sampling
          from the parent transaction — see `qyl.loom.setup_ai_monitoring`).

          ## What ASP.NET Core gives for free
          - One transaction per request, named with the route template.
          - `SentryHttpMessageHandler` injects `sentry-trace` + `baggage` into outbound HTTP.
          - Incoming `sentry-trace` + `baggage` auto-continued via `ContinueTrace()`.
          - EF Core spans: `db.query_compiler`, `db.connection`, `db.query` (via DiagnosticSource).
          - Outgoing HTTP spans — only created when a transaction is on scope.

          ## Custom instrumentation
          - `var tx = SentrySdk.StartTransaction("checkout", "perform-checkout");`
          - `SentrySdk.ConfigureScope(s => s.Transaction = tx);` — **this** is the step that makes
            auto instrumentation attach to your transaction. Skip and you get a detached transaction.
          - `var span = tx.StartChild("db.query", "SELECT …"); span.SetData("db.system", "postgresql"); span.Finish(SpanStatus.Ok);`
          - `tx.Finish(exception)` auto-maps exception → SpanStatus.

          ## Dynamic sampling + DSC
          - Head service picks the sampling decision. Downstream services honour via `baggage`.
          - Use `TransactionNameSource.Route` — parameterised templates. Raw URLs break grouping.
          - For AI traffic: see `qyl.loom.setup_ai_monitoring` — gen_ai spans inherit the parent
            HTTP transaction's sampling decision. Biggest load-bearing gotcha in AI setup.

          ## OpenTelemetry interop (when using `Sentry.OpenTelemetry`)
          - Two parts: `builder.WebHost.UseSentry(o => o.UseOpenTelemetry(); ...)` AND
            `builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSentry().AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());`
          - Never call `activity.RecordException(ex)` — it strips exception data. Use
            `SentrySdk.CaptureException(ex)` or `_logger.LogError(ex, ...)` instead.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          Apply the minimal change for the detected framework; if `requires_flush_on_completed_request`,
          pair tracing with `FlushOnCompletedRequest = true` to avoid losing trailing events.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_profiling", Title = "Profiling")]
    [Description("Profiling feature prompt — enforces tracing-prerequisite + platform caveats.")]
    public static string Profiling(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Profiling** for the detected project.

          ## Prerequisites
          1. **Tracing must already be enabled.** `options.TracesSampleRate > 0` (or a sampler
             returning >0 for this path). Profiling attaches to transactions — no transaction → no profile.
          2. **.NET 8+ is required.** .NET Framework, Blazor WebAssembly, Android, and non-iOS Native AOT
             are NOT supported. Check `detection.supports_profiling` before proceeding — if false,
             stop and tell the user why.
          3. **Install `Sentry.Profiling`** — but NOT on iOS / Mac Catalyst (built-in Mono AOT profiler
             inside `Sentry.Maui` already handles those targets).

          ## Configuration — three steps, not one
          ```csharp
          options.TracesSampleRate   = 1.0;                                      // required
          options.ProfilesSampleRate = 1.0;                                      // fraction of sampled transactions to profile
          options.AddProfilingIntegration(TimeSpan.FromMilliseconds(500));       // sync startup, 500ms warmup budget
          ```

          Net profiling rate = TracesSampleRate × ProfilesSampleRate. Production typical:
          `TracesSampleRate = 0.2, ProfilesSampleRate = 0.5` → 10% profiling coverage.

          ## Platform caveats
          - **Linux containers:** `ReflectionTypeLoadException` at startup is a known issue (#4815).
            Wrap `AddProfilingIntegration()` in try/catch and log gracefully.
          - **Nested / concurrent transactions:** only one profile per process. Second concurrent
            transaction does not get profiled.
          - **30-second cap:** profiles longer than that are truncated. Split long operations.
          - **OTel mode quirk:** `UseOpenTelemetry()` + `AddProfilingIntegration()` has shown profiles
            showing only `Program.Main` (issue #4820).

          ## Detection
          ```json
          {{detectionJson}}
          ```

          If `supports_profiling == false`, return: "Profiling unavailable for this target
          framework / platform combination." Do not install the package. Do not half-configure.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_logging", Title = "Logging")]
    [Description("Logging feature prompt — ILogger / Serilog / NLog / log4net.")]
    public static string Logging(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Logging** for the detected project.

          ## Integration selection
          Pick by what the project already uses — do not introduce a new logger:
          - `Microsoft.Extensions.Logging` (ILogger) → `Sentry.Extensions.Logging`
          - Serilog → `Sentry.Serilog`
          - NLog → `Sentry.NLog`
          - log4net → `Sentry.Log4Net`

          Each integration carries three capabilities simultaneously:
          1. Lower-level entries become **breadcrumbs** attached to the next error event.
          2. At-or-above `MinimumEventLevel` entries become **error events**.
          3. SDK ≥ 6.1.0 forwards entries as **native structured logs** when `options.EnableLogs = true`.

          ## Enablement gate
          **`options.EnableLogs = true` is required** for native structured logs. Without it,
          `SentrySdk.Logger.LogInfo/LogWarning/LogError/LogFatal` are silent no-ops. Call it out —
          silent no-op is the single most common misconfiguration.

          ## Level thresholds (defaults)
          - `MinimumBreadcrumbLevel = Information`
          - `MinimumEventLevel = Error`

          ## NLog trap
          `SentryTarget` must receive **all** entries to classify them correctly. Set NLog's
          `<logger minlevel="Debug">` **lower** than `MinimumBreadcrumbLevel`. Otherwise breadcrumbs
          never arrive.

          ## Dual init guard
          If `SentrySdk.Init(...)` is called elsewhere, set `InitializeSdk = false` on the logging
          integration. Otherwise the SDK double-inits and events duplicate.

          ## Log ↔ trace correlation
          Automatic when `TracesSampleRate > 0` and logs are emitted inside an active span.
          Every log carries `TraceId` + `SpanId` → the UI links from an error to the exact log
          stream from that trace.

          ## Filtering
          `options.SetBeforeSendLog(log => log.Level >= SentryLogLevel.Warning ? log : null);`
          to drop Trace / Debug in production.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          If `logging_libraries` is empty, the project doesn't have a logger wired yet. Propose the
          MEL integration since that's the .NET default; do not add Serilog unless the user asked.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_metrics", Title = "Metrics")]
    [Description("Metrics feature prompt — EnableMetrics gate + SENTRY1001 analyzer.")]
    public static string Metrics(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Trace-connected Metrics** for the detected project.

          ## Enablement gate
          `options.EnableMetrics = true` is required (SDK ≥ 6.1.0). Without it, every
          `SentrySdk.Metrics.EmitCounter/EmitGauge/EmitDistribution` call is a silent no-op.
          State this up front — silent drops are the top misconfiguration.

          ## API surface
          - `EmitCounter("orders.completed", 1, attributes, scope)` — increments.
          - `EmitGauge("queue.depth", 42)` / with unit — current value snapshots.
          - `EmitDistribution("response.time", 120.5, MeasurementUnit.Duration.Millisecond)` —
            statistical (histogram).

          ## Supported numeric types — narrower than you think
          Allowed at runtime: `byte`, `short`, `int`, `long`, `float`, `double`.
          **Silently dropped** at runtime: `uint`, `ulong`, `decimal`, `Int128`.
          The Roslyn analyzer `SENTRY1001` (ships with `Sentry.Compiler.Extensions`) flags these at
          compile time. Never suppress — the metric is dropped regardless of the pragma.

          ## Attribute cardinality
          Prefer low-cardinality values (region, tenant, endpoint template). A raw URL or user id
          makes the metric unaggregatable and expensive.

          ## Filtering
          `options.SetBeforeSendMetric(metric => metric);` — return null to drop. Use
          `metric.TryGetValue<double>(out var v)` to inspect; same supported-type rules apply.

          ## Trace correlation
          Metrics carry `TraceId` + `SpanId` of the active trace. Emit from inside a span so the UI
          can link a metric spike to the traces that produced it.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          If the user did not explicitly ask for metrics, confirm before adding — metrics are opt-in,
          not part of the recommended baseline.
          """;

    [McpServerPrompt(Name = "qyl.loom.setup_dotnet_crons", Title = "Cron Monitoring")]
    [Description("Cron monitoring feature prompt — two-signal vs heartbeat + Hangfire/Quartz.")]
    public static string Crons(
        [Description("JSON payload produced by loom_detect_dotnet. Required.")]
        string detectionJson) =>
        $$"""
          Configure **Cron Monitoring** for the detected project.

          ## Two patterns — pick by need
          ### Pattern A — Two-signal (recommended when you care about timeouts)
          ```csharp
          var id = SentrySdk.CaptureCheckIn("my-job", CheckInStatus.InProgress);
          try { DoWork(); SentrySdk.CaptureCheckIn("my-job", CheckInStatus.Ok,    id); }
          catch  { SentrySdk.CaptureCheckIn("my-job", CheckInStatus.Error, id); throw; }
          ```
          Detects: missed runs **and** timeout violations.

          ### Pattern B — Heartbeat (simpler, no timeout detection)
          ```csharp
          try { DoWork(); SentrySdk.CaptureCheckIn("my-job", CheckInStatus.Ok); }
          catch { SentrySdk.CaptureCheckIn("my-job", CheckInStatus.Error); throw; }
          ```
          Detects: missed runs only.

          ## Monitor config (upsert on first check-in)
          Pass `configureMonitorOptions` to `CaptureCheckIn` — crontab or interval, `CheckInMargin`,
          `MaxRuntime`, `TimeZone`, `FailureIssueThreshold`, `RecoveryThreshold`. Idempotent, safe
          to call on every run.

          ```csharp
          o.Schedule = "0 2 * * *";           // crontab — 2 AM daily
          o.CheckInMargin = 15;                // grace period before "missed"
          o.MaxRuntime = 60;                   // minutes before "timeout"
          o.TimeZone = "UTC";                  // IANA zone
          o.FailureIssueThreshold = 1;         // open issue on first failure
          o.RecoveryThreshold = 1;             // close on first success
          ```

          ## Integrations detected in this project
          - **Hangfire** → `Sentry.Hangfire` + `config.UseSentry()` inside `AddHangfire(...)` →
            automatic check-ins per recurring job (slug = `RecurringJobId`).
          - **Quartz.NET** → no official integration. Wrap `CaptureCheckIn` inside `IJob.Execute`.
          - **`BackgroundService` / `IHostedService`** → manual `CaptureCheckIn` as in the patterns above.

          ## Rate limits
          6 check-ins / minute / monitor / environment. Exceeding silently drops events.

          ## Detection
          ```json
          {{detectionJson}}
          ```

          Emit the minimal change per detected scheduler. If `scheduler_libraries` is empty, do not
          add crons — ask whether the project runs scheduled work at all before proceeding.
          """;
}
