# Copilot PR-review instructions for qyl

.NET 10 / C# 14 observability platform (collector, loom, mcp, dashboard).
Full developer guide lives in `AGENTS.md` + `CLAUDE.md`. This file is PR-review only.

## Flag

- Off-by-one in `Skip` / `Take`, slicing, paging math.
- `await` inside a `lock`, or in `Parallel.ForEachAsync` capturing a `using`-scoped outer resource.
- `.Result`, `.Wait()`, `Task.Run(() => asyncMethod().Result)`.
- SQL / command injection via interpolated strings in `DuckDbStore`, `ProcessTasks.StartProcess`, or shell calls.
- `catch (Exception) { }` swallowing silently.
- New `#pragma warning disable` / `[SuppressMessage]` / `<NoWarn>` / `null!` — fix at source.
- `dynamic` / `ExpandoObject` as control-plane.
- `DateTime.{UtcNow,Now}` / `DateTimeOffset.UtcNow` / `Stopwatch.GetTimestamp()` in business logic — use injected `TimeProvider`.

## qyl-specific

- `HttpClient` consumers must guard `ReadFromJsonAsync` on non-success responses with `ContentType == "application/json"` + `ContentLength > 0`. Pattern: `services/qyl.loom/CollectorClient.cs`.
- `DuckDbStore` writes go through `ExecuteWriteAsync(async (connection, ct) => …, ct)`. The read lease is `READ_ONLY`-enforced; any `INSERT` / `UPDATE` / `DELETE` outside that helper is a defect.
- `[McpServerTool]` methods are `partial`; never hand-write `[Description("…")]` — XML `<summary>` is the source, the upstream MCP analyzer emits `[Description]` from it.
- Agent composition needs BOTH wraps: `IChatClient` (`.WithQylTelemetry` / `.UseQylTelemetry`) AND `AIAgent` (`agent.AsBuilder().UseQylAgentTelemetry().Build()`). `[QYL0135]` catches only the agent half.

## Do not flag

- Allow-listed suppressions: `MEAI001`, `OPENAI002`, `CA1812` on `[JsonSerializable]` partials, `IL2026` / `IL3050` on verified-safe AOT paths, framework `#pragma` inside MAF interop code.
- Analyzer-enforced rules: `IDisposable` disposal (`CA2000`), `ConfigureAwait` (`CA2007`), formatting / naming (`.editorconfig` + `ANcpLua.NET.Sdk`).
- Missing XML docs on `internal` / `private` types — only `packages/Qyl.*` is public NuGet surface.
- Test-only patterns: `FakeTimeProvider`, `FakeChatClient`, `Path.Join(Path.GetTempPath(), …)`.
- Generated files: `*.g.cs`, `*.g.ts`, `*.g.sql`, `*.g.tsp` — edit the source spec and regenerate.
- Direct `@radix-ui/*` / `asChild` usage inside `services/qyl.dashboard/src/components/ui/` — that IS the wrapper layer the rule exempts.

## Project context

Solo-dev repo. Breaking changes are allowed; the owner fixes downstream consumers in the same session. Don't suggest backwards-compat shims, feature flags, or deprecated-marker dual paths within a single PR.
