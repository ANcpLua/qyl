# ChatKit Python → C# (MAF) Port

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 1:1 port of OpenAI ChatKit Python SDK to C# within the qyl/MAF ecosystem.

**Architecture:** Contracts (pure records, enums, interfaces) go in `qyl.contracts/ChatKit/`. Runtime (server, agent bridge) goes in a new `qyl.chatkit` class library. All polymorphic types use `[JsonPolymorphic]` with STJ source generation. No reflection, no dynamic. C# 14, file-scoped namespaces, primary constructors.

**Tech Stack:** .NET 10, System.Text.Json, Microsoft.Extensions.AI, qyl.contracts

---

## Source → Target File Map

| Python source | C# target | Responsibility |
|---|---|---|
| `icons.py` | `qyl.contracts/ChatKit/IconName.cs` | Icon name constants |
| `errors.py` | `qyl.contracts/ChatKit/Errors.cs` | ErrorCode, StreamError, CustomStreamError |
| `actions.py` | `qyl.contracts/ChatKit/Actions.cs` | Action<TType,TPayload>, ActionConfig |
| `types.py` (Page) | `qyl.contracts/ChatKit/Page.cs` | Generic paginated collection |
| `types.py` (sources) | `qyl.contracts/ChatKit/Sources.cs` | URLSource, FileSource, EntitySource |
| `types.py` (messages) | `qyl.contracts/ChatKit/Messages.cs` | UserMessageContent, AssistantMessageContent, Annotation, InferenceOptions |
| `types.py` (attachments) | `qyl.contracts/ChatKit/Attachments.cs` | Attachment hierarchy, AudioInput, TranscriptionResult |
| `types.py` (threads) | `qyl.contracts/ChatKit/ThreadTypes.cs` | Thread, ThreadMetadata, ThreadStatus |
| `types.py` (thread items) | `qyl.contracts/ChatKit/ThreadItems.cs` | ThreadItem hierarchy (10 variants) |
| `types.py` (events) | `qyl.contracts/ChatKit/ThreadEvents.cs` | ThreadStreamEvent hierarchy (12 variants) |
| `types.py` (item updates) | `qyl.contracts/ChatKit/ThreadItemUpdates.cs` | ThreadItemUpdate hierarchy (10 variants) |
| `types.py` (requests) | `qyl.contracts/ChatKit/Requests.cs` | ChatKitRequest hierarchy (15 variants) |
| `types.py` (workflows) | `qyl.contracts/ChatKit/Workflows.cs` | Workflow, Task hierarchy, WorkflowSummary |
| `types.py` (misc) | `qyl.contracts/ChatKit/FeedbackKind.cs` | FeedbackKind enum |
| `widgets.py` (enums) | `qyl.contracts/ChatKit/Widgets/WidgetEnums.cs` | All Literal→enum (RadiusValue, TextAlign, etc.) |
| `widgets.py` (base) | `qyl.contracts/ChatKit/Widgets/WidgetComponentBase.cs` | Base widget record |
| `widgets.py` (components) | `qyl.contracts/ChatKit/Widgets/WidgetComponents.cs` | 25+ widget types |
| `widgets.py` (roots) | `qyl.contracts/ChatKit/Widgets/WidgetRoots.cs` | Card, ListView, BasicRoot as roots |
| `widgets.py` (charts) | `qyl.contracts/ChatKit/Widgets/ChartTypes.cs` | Chart, Series, XAxisConfig |
| `store.py` | `qyl.contracts/ChatKit/IStore.cs` | IStore<TContext>, IAttachmentStore<TContext>, StoreItemType |
| — | `qyl.contracts/ChatKit/ChatKitJsonContext.cs` | STJ source-gen context |
| `server.py` | `qyl.chatkit/ChatKitServer.cs` | Abstract server: process, route, stream |
| `server.py` (diff) | `qyl.chatkit/WidgetDiff.cs` | diff_widget, stream_widget |
| `server.py` (results) | `qyl.chatkit/StreamingResult.cs` | StreamingResult, NonStreamingResult |
| `agents.py` (context) | `qyl.chatkit/AgentContext.cs` | AgentContext<TContext> |
| `agents.py` (stream) | `qyl.chatkit/StreamAgentResponse.cs` | stream_agent_response → MAF bridge |
| `agents.py` (converter) | `qyl.chatkit/ThreadItemConverter.cs` | ThreadItemConverter |
| `agents.py` (response) | `qyl.chatkit/ResponseStreamConverter.cs` | ResponseStreamConverter |

## Type Mapping Rules

Every agent MUST follow these rules exactly:

### Python → C# type mapping

| Python | C# |
|---|---|
| `class Foo(BaseModel)` | `public sealed record Foo` |
| `x: str` | `public required string X { get; init; }` |
| `x: str \| None = None` | `public string? X { get; init; }` |
| `x: int = 0` | `public int X { get; init; }` |
| `x: list[T]` | `public IReadOnlyList<T> X { get; init; } = []` |
| `x: list[T] = Field(default_factory=list)` | `public IReadOnlyList<T> X { get; init; } = []` |
| `x: dict[str, Any]` | `public Dictionary<string, object?>? X { get; init; }` |
| `x: dict[str, Any] = Field(default_factory=dict)` | `public Dictionary<string, object?> X { get; init; } = []` |
| `x: Literal["a", "b"]` | enum or string const (context-dependent) |
| `x: datetime` | `public DateTime X { get; init; }` (or DateTimeOffset) |
| `x: AnyUrl` | `public Uri X { get; init; }` |
| `x: bytes` | `public byte[] X { get; init; }` |
| `x: Any` | `public object? X { get; init; }` |

### Discriminated unions

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(VariantA), "variant_a")]
[JsonDerivedType(typeof(VariantB), "variant_b")]
public abstract record Base;

public sealed record VariantA : Base;
public sealed record VariantB(string? Reason = null) : Base;
```

### JSON property naming

Use `[JsonPropertyName("snake_case")]` on every property. Python uses snake_case wire format; C# uses PascalCase properties but must serialize to the same wire format.

Exception: Widget properties use camelCase on wire (e.g., `onClickAction`, `textAlign`). Use `[JsonPropertyName("camelCase")]` for those.

### Async patterns

| Python | C# |
|---|---|
| `async def foo() -> T` | `ValueTask<T> FooAsync(CancellationToken ct = default)` |
| `AsyncIterator[T]` / `AsyncGenerator[T, None]` | `IAsyncEnumerable<T>` |
| `asyncio.Queue` | `Channel<T>` |
| `ABC` + `@abstractmethod` | `abstract class` or `interface` |

### Namespace convention

- Contracts: `Qyl.Contracts.ChatKit`
- Contracts/Widgets: `Qyl.Contracts.ChatKit.Widgets`
- Runtime: `Qyl.ChatKit`

---

## Dependency Order (batches that can run in parallel)

```
Batch 1 (parallel, no deps):
  ├── IconName.cs
  ├── Errors.cs
  ├── FeedbackKind.cs
  ├── Page.cs
  ├── Actions.cs
  └── Widgets/WidgetEnums.cs

Batch 2 (parallel, depends on Batch 1):
  ├── Sources.cs          (depends on IconName)
  ├── Attachments.cs      (depends on nothing new)
  ├── Messages.cs         (depends on Sources, IconName)
  ├── Workflows.cs        (depends on IconName, Sources)
  └── Widgets/Base+Components+Roots+Charts (depends on WidgetEnums, Actions, IconName)

Batch 3 (parallel, depends on Batch 2):
  ├── ThreadTypes.cs      (depends on Page)
  ├── ThreadItems.cs      (depends on Messages, Widgets, Workflows, Attachments)
  └── ThreadItemUpdates.cs (depends on Messages, Widgets, Workflows)

Batch 4 (parallel, depends on Batch 3):
  ├── ThreadEvents.cs     (depends on ThreadItems, ThreadItemUpdates)
  └── Requests.cs         (depends on ThreadItems, Messages, Actions, Attachments)

Batch 5 (depends on Batch 4):
  ├── IStore.cs           (depends on ThreadItems, Attachments, Page, ThreadTypes)
  └── ChatKitJsonContext.cs (depends on everything)

Batch 6 (depends on Batch 5):
  └── qyl.chatkit/ (all runtime files)
```

---

## Task 1: Project scaffolding + Enums/Primitives (Batch 1)

**Files:**
- Create: `qyl.contracts/ChatKit/IconName.cs`
- Create: `qyl.contracts/ChatKit/Errors.cs`
- Create: `qyl.contracts/ChatKit/FeedbackKind.cs`
- Create: `qyl.contracts/ChatKit/Page.cs`
- Create: `qyl.contracts/ChatKit/Actions.cs`
- Create: `qyl.contracts/ChatKit/Widgets/WidgetEnums.cs`
- Create: `qyl.chatkit/qyl.chatkit.csproj`

- [ ] **Step 1: Create `qyl.chatkit.csproj`**

```xml
<Project Sdk="ANcpLua.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Qyl.ChatKit</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\qyl.contracts\qyl.contracts.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `IconName.cs`**

Port `icons.py`. IconName is a string-typed concept (supports `vendor:*` and `lucide:*` prefixes, so it cannot be a closed enum). Use a static class with string constants for the known values.

- [ ] **Step 3: Create `Errors.cs`**

Port `errors.py`. `ErrorCode` as a string enum. `StreamError` and `CustomStreamError` as exception types.

- [ ] **Step 4: Create `FeedbackKind.cs`**

```csharp
namespace Qyl.Contracts.ChatKit;

[JsonConverter(typeof(JsonStringEnumConverter<FeedbackKind>))]
public enum FeedbackKind { Positive, Negative }
```

- [ ] **Step 5: Create `Page.cs`**

Port the generic `Page[T]` model.

- [ ] **Step 6: Create `Actions.cs`**

Port `actions.py`: `Action<TType, TPayload>`, `ActionConfig`, `Handler` enum, `LoadingBehavior` enum.

- [ ] **Step 7: Create `Widgets/WidgetEnums.cs`**

Port all `Literal` types from `widgets.py` as enums: `RadiusValue`, `TextAlign`, `TextSize`, `TitleSize`, `CaptionSize`, `IconSize`, `Alignment`, `Justification`, `ControlVariant`, `ControlSize`, `CurveType`.

- [ ] **Step 8: Commit**

---

## Task 2: Source types + Messages + Attachments + Workflows (Batch 2)

**Files:**
- Create: `qyl.contracts/ChatKit/Sources.cs`
- Create: `qyl.contracts/ChatKit/Messages.cs`
- Create: `qyl.contracts/ChatKit/Attachments.cs`
- Create: `qyl.contracts/ChatKit/Workflows.cs`

- [ ] **Step 1: Create `Sources.cs`**

Port `SourceBase`, `FileSource`, `URLSource`, `EntitySource` from `types.py` lines 939-991. Use `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` with derived types.

- [ ] **Step 2: Create `Messages.cs`**

Port `UserMessageInput`, `UserMessageTextContent`, `UserMessageTagContent`, `AssistantMessageContent`, `Annotation`, `InferenceOptions`, `ToolChoice` from `types.py`.

- [ ] **Step 3: Create `Attachments.cs`**

Port `AttachmentBase`, `FileAttachment`, `ImageAttachment`, `AttachmentUploadDescriptor`, `AttachmentCreateParams`, `AudioInput`, `TranscriptionResult` from `types.py`.

- [ ] **Step 4: Create `Workflows.cs`**

Port `Workflow`, `BaseTask`→`CustomTask`/`SearchTask`/`ThoughtTask`/`FileTask`/`ImageTask`, `WorkflowSummary`→`CustomSummary`/`DurationSummary` from `types.py` lines 848-934.

- [ ] **Step 5: Commit**

---

## Task 3: Widget components (Batch 2, parallel with Task 2)

**Files:**
- Create: `qyl.contracts/ChatKit/Widgets/WidgetComponentBase.cs`
- Create: `qyl.contracts/ChatKit/Widgets/WidgetComponents.cs`
- Create: `qyl.contracts/ChatKit/Widgets/WidgetRoots.cs`
- Create: `qyl.contracts/ChatKit/Widgets/ChartTypes.cs`

- [ ] **Step 1: Create `WidgetComponentBase.cs`**

Port `WidgetComponentBase` from `widgets.py` lines 173-195. Abstract record with `Key`, `Id`, `Type` properties.

- [ ] **Step 2: Create `WidgetComponents.cs`**

Port all 25 widget component types: `Text`, `Title`, `Caption`, `Markdown`, `Badge`, `Box`, `Row`, `Col`, `Form`, `Divider`, `Icon`, `Image`, `Button`, `Spacer`, `Select`, `DatePicker`, `Checkbox`, `Input`, `Label`, `RadioGroup`, `Textarea`, `Transition`, `ListView`, `ListViewItem`. Each is a sealed record extending `WidgetComponentBase`.

Also port the TypedDict types as records: `ThemeColor`, `Spacing`, `Border`, `Borders`, `MinMax`, `EditableProps`, `SelectOption`, `RadioOption`, `CardAction`, `WidgetStatus`.

- [ ] **Step 3: Create `WidgetRoots.cs`**

Port `Card` (as root), `ListView` (as root), `BasicRoot`, `DynamicWidgetRoot`, `DynamicWidgetComponent`. Define `WidgetComponent` and `WidgetRoot` as the polymorphic base types.

- [ ] **Step 4: Create `ChartTypes.cs`**

Port `Chart`, `BarSeries`, `AreaSeries`, `LineSeries`, `XAxisConfig` from `widgets.py` lines 908-1058.

- [ ] **Step 5: Commit**

---

## Task 4: Thread types + Thread items + Thread item updates (Batch 3)

**Files:**
- Create: `qyl.contracts/ChatKit/ThreadTypes.cs`
- Create: `qyl.contracts/ChatKit/ThreadItems.cs`
- Create: `qyl.contracts/ChatKit/ThreadItemUpdates.cs`

- [ ] **Step 1: Create `ThreadTypes.cs`**

Port `ThreadMetadata`, `Thread`, `ActiveStatus`/`LockedStatus`/`ClosedStatus` (as `ThreadStatus` polymorphic hierarchy) from `types.py` lines 555-600.

- [ ] **Step 2: Create `ThreadItems.cs`**

Port `ThreadItem` polymorphic hierarchy with 10 variants: `UserMessageItem`, `AssistantMessageItem`, `ClientToolCallItem`, `WidgetItem`, `GeneratedImageItem`, `WorkflowItem`, `TaskItem`, `HiddenContextItem`, `SDKHiddenContextItem`, `EndOfTurnItem`. Also `GeneratedImage`, `SyncCustomActionResponse` from `types.py` lines 602-716.

- [ ] **Step 3: Create `ThreadItemUpdates.cs`**

Port `ThreadItemUpdate` polymorphic hierarchy with 10 variants: `AssistantMessageContentPartAdded`, `AssistantMessageContentPartTextDelta`, `AssistantMessageContentPartAnnotationAdded`, `AssistantMessageContentPartDone`, `WidgetStreamingTextValueDelta`, `WidgetRootUpdated`, `WidgetComponentUpdated`, `WorkflowTaskAdded`, `WorkflowTaskUpdated`, `GeneratedImageUpdated` from `types.py` lines 441-546.

- [ ] **Step 4: Commit**

---

## Task 5: Thread events + Requests (Batch 4)

**Files:**
- Create: `qyl.contracts/ChatKit/ThreadEvents.cs`
- Create: `qyl.contracts/ChatKit/Requests.cs`

- [ ] **Step 1: Create `ThreadEvents.cs`**

Port `ThreadStreamEvent` polymorphic hierarchy with 12 variants: `ThreadCreatedEvent`, `ThreadUpdatedEvent`, `ThreadItemAddedEvent`, `ThreadItemUpdatedEvent`, `ThreadItemDoneEvent`, `ThreadItemRemovedEvent`, `ThreadItemReplacedEvent`, `StreamOptionsEvent`, `ProgressUpdateEvent`, `ClientEffectEvent`, `ErrorEvent`, `NoticeEvent`. Also `StreamOptions` from `types.py` lines 316-438.

- [ ] **Step 2: Create `Requests.cs`**

Port `ChatKitRequest` polymorphic hierarchy with all request types. Include param records inline. Define `StreamingRequest` and `NonStreamingRequest` marker interfaces. Port `IsStreamingReq` as a static method from `types.py` lines 35-313.

- [ ] **Step 3: Commit**

---

## Task 6: Store interfaces + JSON context (Batch 5)

**Files:**
- Create: `qyl.contracts/ChatKit/IStore.cs`
- Create: `qyl.contracts/ChatKit/ChatKitJsonContext.cs`

- [ ] **Step 1: Create `IStore.cs`**

Port `Store` → `IStore<TContext>` and `AttachmentStore` → `IAttachmentStore<TContext>` as interfaces. Port `StoreItemType` as enum. Port `default_generate_id` as static helper. Port `NotFoundError` as exception from `store.py`.

- [ ] **Step 2: Create `ChatKitJsonContext.cs`**

Create a `[JsonSerializable]` source-generated context covering all ChatKit contract types. This enables AOT-friendly serialization.

- [ ] **Step 3: Commit**

---

## Task 7: Runtime — ChatKitServer + WidgetDiff + StreamingResult (Batch 6)

**Files:**
- Create: `qyl.chatkit/ChatKitServer.cs`
- Create: `qyl.chatkit/WidgetDiff.cs`
- Create: `qyl.chatkit/StreamingResult.cs`

- [ ] **Step 1: Create `StreamingResult.cs`**

Port `StreamingResult` (wraps `IAsyncEnumerable<byte[]>`) and `NonStreamingResult` (wraps `byte[]`) from `server.py` lines 264-275.

- [ ] **Step 2: Create `WidgetDiff.cs`**

Port `diff_widget` and `stream_widget` from `server.py` lines 96-249. The diffing algorithm compares two `WidgetRoot` instances and returns deltas.

- [ ] **Step 3: Create `ChatKitServer.cs`**

Port the full `ChatKitServer<TContext>` abstract class from `server.py` lines 281-982. This is the core: `Process`, `_ProcessNonStreaming`, `_ProcessStreaming`, `_ProcessEvents`, `HandleStreamCancelled`, `Respond` (abstract), `Action`, `SyncAction`, `AddFeedback`, `Transcribe`, `GetStreamOptions`.

Key C# adaptations:
- `match request:` → `switch (request)`
- `async for event in stream():` → `await foreach (var event in stream(ct))`
- `asyncio.CancelledError` → `OperationCanceledException`
- `model_dump_json()` → `JsonSerializer.SerializeToUtf8Bytes(obj, ChatKitJsonContext.Default.X)`

- [ ] **Step 4: Commit**

---

## Task 8: Runtime — Agent bridge (Batch 6, after Task 7)

**Files:**
- Create: `qyl.chatkit/AgentContext.cs`
- Create: `qyl.chatkit/ResponseStreamConverter.cs`
- Create: `qyl.chatkit/ThreadItemConverter.cs`
- Create: `qyl.chatkit/StreamAgentResponse.cs`

- [ ] **Step 1: Create `AgentContext.cs`**

Port `AgentContext` from `agents.py` lines 108-226. Uses `Channel<ThreadStreamEvent>` instead of `asyncio.Queue`. Provides `StreamWidget`, `StartWorkflow`, `AddWorkflowTask`, `UpdateWorkflowTask`, `EndWorkflow`, `Stream` methods.

- [ ] **Step 2: Create `ResponseStreamConverter.cs`**

Port `ResponseStreamConverter` from `agents.py` lines 313-411. Adapts MAF/IChatClient streamed responses (image gen, citations) into ChatKit types.

- [ ] **Step 3: Create `ThreadItemConverter.cs`**

Port `ThreadItemConverter` from `agents.py` lines 834-1213. Converts ChatKit thread items to MAF `ChatMessage` / input items. Each thread item type has a virtual conversion method.

- [ ] **Step 4: Create `StreamAgentResponse.cs`**

Port `stream_agent_response` from `agents.py` lines 457-797. The big one: merges MAF agent stream events with the AgentContext event queue, converts raw response events to ChatKit thread stream events. Handles workflow tracking, image gen, reasoning summaries, guardrail tripwires.

Note: The Python version uses the OpenAI Agents SDK (`RunResultStreaming`, `StreamEvent`). The C# version should use MAF's `IChatClient` streaming or `AIAgent` abstractions. This requires adapting the event types but the control flow is 1:1.

- [ ] **Step 5: Commit**

---

## Execution tracks (4 parallel agents)

To avoid "6 Codex Sparks doing the same thing", split into 4 disjoint ownership tracks:

| Track | Tasks | File ownership |
|---|---|---|
| **A: Widgets** | Task 1 (WidgetEnums only) + Task 3 | `qyl.contracts/ChatKit/Widgets/*` |
| **B: Core DTOs** | Task 1 (Icons, Errors, FeedbackKind, Page, Actions) + Task 2 | `qyl.contracts/ChatKit/{IconName,Errors,FeedbackKind,Page,Actions,Sources,Messages,Attachments,Workflows}.cs` |
| **C: Protocol** | Task 4 + Task 5 + Task 6 | `qyl.contracts/ChatKit/{ThreadTypes,ThreadItems,ThreadItemUpdates,ThreadEvents,Requests,IStore,ChatKitJsonContext}.cs` |
| **D: Runtime** | Task 1 (csproj only) + Task 7 + Task 8 | `qyl.chatkit/*` |

Tracks A, B run fully in parallel. Track C starts after A+B (needs their types). Track D starts after C.
