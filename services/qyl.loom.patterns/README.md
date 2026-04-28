# qyl.loom.patterns

MAF-1.1 composition-primitives cookbook. Six self-contained patterns, autofix-shaped
synthetic domain, no LLM / API keys required — `FakeChatClient` supplies canned
responses so every sample exercises the real MAF wiring without network calls.

## Layout

| Path         | Role                                                                                                             |
|--------------|------------------------------------------------------------------------------------------------------------------|
| `Program.cs` | CLI — dispatches to one pattern by arg                                                                           |
| `Clients/`   | `IQylLoomPatternsChatClientBuilder` — provider-agnostic `IChatClient` factory (qyl seam)                         |
| `Agents/`    | `IQylLoomPatternsAgentsBuilder` — one factory method per bounded agent, wrapped with `UseQylAgentTelemetry`      |
| `Contracts/` | `[LoomContract]`-decorated records: `RootCauseHypothesis`, `SolutionPlan`, `ConfidenceVerdict`, `IncidentSignal` |
| `Patterns/`  | Six pattern files, each exposing `public static Task RunAsync(agents, ct)`                                       |

## Patterns

| File                               | Primitives                                                                                                     |
|------------------------------------|----------------------------------------------------------------------------------------------------------------|
| `Pattern01_SwitchRouting.cs`       | `AddSwitch` + `AddCase<T>(predicate)` + `WithDefault`                                                          |
| `Pattern02_SubWorkflow.cs`         | `Workflow.BindAsExecutor(id)` — inner `rca → solution` as one node                                             |
| `Pattern03_CheckpointResume.cs`    | `CheckpointManager` + `SuperStepCompletedEvent.CompletionInfo.Checkpoint` + `RestoreCheckpointAsync`           |
| `Pattern04_HitlViaExternalCall.cs` | `RequestPort.Create<TReq,TResp>` + `AddExternalCall<TReq,TResp>` + `ForwardMessage<TResp>`                     |
| `Pattern05_StatefulExecutor.cs`    | `StatefulExecutor<TState,TIn,TOut>` + `InvokeWithStateAsync` + `ctx.AddEventAsync` + caller-minted `sessionId` |
| `Pattern06_AllCombined.cs`         | All of the above in one autofix-shaped graph, plus `StreamingRun.GetStatusAsync`                               |

## qyl conventions

- Every `AIAgent` construction chains `.AsBuilder().UseQylAgentTelemetry().Build()` —
  `QYL0135` enforces this at build time.
- Every `IChatClient` is built via `new ChatClientBuilder(fake).UseQylTelemetry("qyl.genai").Build()`.
- qyl three-builder shape: `IQylLoomPatternsChatClientBuilder` →
  `IQylLoomPatternsAgentsBuilder` → pattern code consumes the agents.
- `[LoomContract]`, `[LoomStep]`, `[LoomWorkflow]` attributes decorate contracts,
  executors, and the combined workflow in `Pattern06` so `nuke Generate` has real
  inputs to validate.

## Run

```bash
# default — the combined autofix demo
dotnet run --project services/qyl.loom.patterns

# or a single pattern
dotnet run --project services/qyl.loom.patterns -- switch-routing
dotnet run --project services/qyl.loom.patterns -- sub-workflow
dotnet run --project services/qyl.loom.patterns -- checkpoint-resume
dotnet run --project services/qyl.loom.patterns -- hitl
dotnet run --project services/qyl.loom.patterns -- stateful-executor
dotnet run --project services/qyl.loom.patterns -- all-combined
```

Every pattern is console-only — no HTTP, no SSE, no DevUI. Pair with the Aspire
Dashboard (default OTel backend) if you want to see the `qyl.genai` + `qyl.agent`
spans the telemetry wrap emits.
