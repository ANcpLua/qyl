# qyl.mcp: Generated Metadata, Capability Guides, and Host Split

## Problem

The standalone `qyl.mcp` repo already has the core pieces needed for a serious MCP server:

- dual transport bootstrap in `src/qyl.mcp/Program.cs`
- host option parsing in `src/qyl.mcp/McpHostOptions.cs`
- skill-gated tool registration in `src/qyl.mcp/Skills/SkillRegistrationExtensions.cs`
- compile-time tool discovery in `src/qyl.mcp.generators/*`
- AOT-safe `AIFunction` creation in `src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs`
- no-runtime-reflection tool registry in `src/qyl.mcp/Agents/McpToolRegistry.cs`
- app resources in `src/qyl.mcp/Apps/*`
- structured response helpers in `src/qyl.mcp/Formatting/ResponseFormatter.cs`

The current weakness is not transport support or tool volume. The weakness is that public server identity, capability presentation, and host projection are still too handwritten and too scattered.

Today the repo has no first-class generated metadata layer that answers:

- what tools exist
- which skill owns them
- which tool family they belong to
- which are read-only vs mutating
- which capabilities they collectively implement
- how a client should plan a trace investigation versus an error triage versus a GenAI cost drill-down

As a result, `qyl.mcp` still relies on a mixture of:

- registration code
- descriptions on tool methods
- `README.md`
- runtime-composed `/mcp.json`
- runtime-composed `/llms.txt`
- implicit knowledge inside `qyl.use_qyl`

That is enough to run the server, but not enough to make the server self-describing, auditable, or cleanly packageable as a standalone product.

This plan defines one coherent architecture track for the standalone repo:

1. implement a generated metadata pipeline in `src/qyl.mcp.generators`
2. add `qyl.list_capabilities` and `qyl.get_capability_guide`
3. split `Program.cs` into a core server builder plus transport-specific host setup

## Current State

### What is already strong

#### Compile-time tool discovery

`src/qyl.mcp.generators/ToolManifestGenerator.cs` discovers `[McpServerToolType]` classes and their `[McpServerTool]` methods at compile time.

`src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs` emits:

- `QylToolManifest.ToolTypes`
- `QylToolManifest.CreateTools(IServiceProvider, filter?)`

This is already the right direction:

- AOT-safe
- compile-time discovered
- no runtime reflection in the main registry path

#### Runtime registry

`src/qyl.mcp/Agents/McpToolRegistry.cs` is already clean:

- it delegates entirely to `QylToolManifest.CreateTools(...)`
- it excludes `UseQylTools`
- it caches the result

That means the repo already has a canonical "tool truth" seam. It just does not expose enough metadata through it yet.

#### Skill-based public surface

`src/qyl.mcp/Skills/SkillRegistrationExtensions.cs` is effectively the current public-surface map.

It already groups tools by:

- `Inspect`
- `Health`
- `Analytics`
- `Agent`
- `Build`
- `Anomaly`
- `Loom`
- `Apps`
- `Debug`

This file is the strongest existing source for capability ownership, even though it is currently imperative and registration-oriented rather than descriptor-oriented.

#### Transport hosting

`src/qyl.mcp/Program.cs` already supports:

- stdio mode
- Streamable HTTP mode
- HTTP auth
- health endpoint
- `/mcp.json`
- `/llms.txt`
- landing page

The problem is not missing behavior. The problem is that the composition root is doing too many jobs at once.

### What is still weak

#### No generated server identity or capability descriptors

The generator emits only tool type names and tool factory delegates. It does not emit:

- tool descriptions
- title
- mutability flags
- open-world hint
- result type hints
- skill ownership
- capability membership

#### `Program.cs` is overloaded

It currently owns all of this:

- transport selection
- DI composition
- auth configuration
- JSON context registration
- MCP server builder configuration
- request/message filters
- landing page behavior
- manifest composition
- `llms.txt` composition

This is too much policy and too much server presentation logic in one file.

#### No deterministic capability discovery tools

The server has many domain tools, but no top-level deterministic guide to them.

`qyl.use_qyl` is helpful for natural-language investigations, but it is not a substitute for:

- machine-readable capability discovery
- deterministic planning hints
- explicit OpenTelemetry-oriented investigation workflows

#### Drift risk is growing

Now that this repo is standalone, drift becomes a product problem:

- docs will drift from runtime
- packaged metadata will drift from registration
- capability families will drift from actual skill ownership

The standalone repo should solve that at the generator boundary, not with more prose.

## Design Goals

1. Make the generator the canonical source of public MCP metadata.
2. Expose capability discovery as deterministic tools, not only embedded-agent behavior.
3. Keep OpenTelemetry and observability semantics explicit in the capability layer.
4. Reduce `Program.cs` to a thin bootstrapper.
5. Preserve AOT safety and the current "no runtime reflection" tool discovery posture.
6. Avoid inventing a second domain model for the server.

## Non-goals

- No migration to MAF runtime.
- No replacement of `qyl.use_qyl`.
- No bulk rewrite of existing tools.
- No forced conversion of all string outputs into one formatter.
- No attempt to auto-infer everything from prose descriptions.

## Core Proposal

Introduce three linked layers:

1. generated tool metadata
2. typed capability definitions
3. host projections derived from the same metadata

## 1. Generated Tool Metadata

### Today

The generator models only:

- `ToolTypeEntry`
- `ToolMethodEntry`

Current `ToolMethodEntry` in `src/qyl.mcp.generators/Models/ToolManifestModels.cs` stores:

- `MethodName`
- `ToolName`

That is enough for `CreateTools(...)`, but not enough for self-description.

### Proposed expansion

Expand generator models to capture the metadata already present in `[McpServerTool(...)]`:

```csharp
internal sealed record ToolMethodEntry(
    string MethodName,
    string ToolName,
    string? Title,
    string? Description,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool OpenWorld,
    string? ReturnTypeDisplayName);
```

Expand generated output to emit a descriptor table, for example:

```csharp
namespace Qyl.Generated;

internal static class QylToolManifest
{
    public static readonly QylToolDescriptor[] ToolDescriptors = ...;
}
```

### Descriptor shape

In runtime code, add a stable DTO:

```csharp
public sealed record QylToolDescriptor
{
    public required string Name { get; init; }
    public required string MethodName { get; init; }
    public required string DeclaringType { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required bool ReadOnly { get; init; }
    public required bool Destructive { get; init; }
    public required bool Idempotent { get; init; }
    public required bool OpenWorld { get; init; }
    public string? Skill { get; init; }
    public string? Category { get; init; }
    public string? ReturnType { get; init; }
}
```

### Why this matters

This turns the generator from "tool factory emitter" into "tool truth emitter."

That is the right center of gravity for the standalone repo because:

- it keeps metadata compiler-owned
- it preserves AOT safety
- it avoids runtime reflection
- it gives the rest of the repo something deterministic to consume

## 2. Typed Capability Definitions

Tool descriptors are still too low-level for clients.

The standalone repo needs one more layer: capabilities.

Capabilities are the operator-facing or agent-facing investigation domains that group tools into meaningful workflows.

Examples in this repo:

- trace investigation
- span drilldown
- error triage
- error enrichment
- log search
- session investigation
- GenAI usage and cost analysis
- anomaly and regression analysis
- release and service discovery
- management operations
- Loom workflows
- debug proxy operations
- MCP apps

### Why capabilities should be typed, not inferred

Do not derive capabilities from class names or folder names alone.

The `Tools/` tree is useful, but not precise enough:

- some tools live in family files like `ErrorTools.cs`
- others are single-purpose files like `GetProfileTool.cs`
- some tools belong to multiple investigative flows

Capabilities are domain contracts, not folder heuristics.

### Proposed file

Add:

- `src/qyl.mcp/Capabilities/QylCapabilityDefinitions.cs`

This file should be the canonical typed declaration for capability composition.

Example shape:

```csharp
public sealed record QylCapabilityDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Skill { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> ToolNames { get; init; } = [];
    public IReadOnlyList<string> UseCases { get; init; } = [];
    public IReadOnlyList<string> RelatedCapabilities { get; init; } = [];
}
```

### Proposed starter capability set

- `trace_investigation`
- `span_analysis`
- `error_investigation`
- `error_enrichment`
- `log_investigation`
- `session_investigation`
- `service_discovery`
- `release_health`
- `metrics_query`
- `genai_observability`
- `anomaly_detection`
- `regression_analysis`
- `artifact_exchange`
- `loom_triage_and_fix`
- `management_admin`
- `debugger_control`
- `mcp_apps`
- `meta_agent_investigation`

## 3. New Capability Tools

### `qyl.list_capabilities`

Purpose:

- provide a compact, deterministic capability inventory
- let external clients discover the surface area without reading `README.md`
- give `qyl.use_qyl` a complement rather than a competitor

Suggested parameters:

- `skill`
- `tag`
- `include_tools`

Suggested output:

- structured JSON
- source-generated serializer context
- small enough to be used as a first tool call

### `qyl.get_capability_guide`

Purpose:

- explain how to work a capability correctly
- keep that guidance deterministic and qyl-specific
- reduce pressure to stuff operational guidance into every tool description

Suggested parameters:

- `capability_id`
- `goal`

Suggested output structure:

```json
{
  "id": "trace_investigation",
  "summary": "...",
  "when_to_use": ["..."],
  "primary_identifiers": ["trace_id", "span_id", "service_name"],
  "starting_tools": ["search_traces", "get_trace_details"],
  "follow_up_tools": ["get_span", "qyl.list_trace_logs", "qyl.root_cause_analysis"],
  "scoping_hints": ["QYL_SERVICE narrows collector queries", "sessionId helps when replay/session context exists"],
  "evidence_hints": ["compare root-span duration to child bottlenecks", "use status plus duration, not just one"],
  "related_capabilities": ["log_investigation", "error_investigation"]
}
```

## OpenTelemetry-Centered Capability Guidance

This repo is not a generic SaaS MCP. It is an observability MCP.

That means capability guides should teach investigation semantics in telemetry terms.

### `trace_investigation`

Should make these concepts explicit:

- `trace_id` is the main investigation handle
- `span_id` is for sub-tree drilldown
- root-span latency and child-span latency tell different stories
- `status=error` and high duration are different signals
- `service_name` and release health are often the next join keys
- a good workflow is:
  - `search_traces`
  - `get_trace_details`
  - `get_span`
  - `qyl.list_trace_logs` or `search_logs`
  - `qyl.root_cause_analysis` after evidence capture

### `error_investigation`

Should teach:

- issue-level versus event-level reasoning
- when to use `qyl.list_error_issues` versus `qyl.get_error_issue`
- when breadcrumbs, attachments, and tag distribution matter
- when triage mutation tools become appropriate

### `genai_observability`

Should teach:

- provider/model/token/cost as primary dimensions
- model-specific latency versus platform latency
- when `qyl.get_genai_stats` is enough
- when to pivot into trace or anomaly tools

### `mcp_apps`

Should teach:

- that the interactive resources are UI entrypoints
- that app tools are for structured app launching, not generic data fetch
- when to prefer an app over low-level tool chaining

## Generator Changes

The best part of this repo is that it already has a dedicated generator project:

- `src/qyl.mcp.generators/Analyzers/*`
- `src/qyl.mcp.generators/Emitters/*`
- `src/qyl.mcp.generators/Models/*`

The new plan should lean on that, not bypass it.

### Proposed generator outputs

Add generated files for:

1. `QylToolManifest.g.cs`
   - existing
   - keep `ToolTypes`
   - keep `CreateTools(...)`

2. `QylToolDescriptors.g.cs`
   - new
   - compile-time array of `QylToolDescriptor`

3. `QylToolSkillMap.g.cs`
   - optional
   - generated or checked-in mapping from tool type to skill

### Skill ownership source

There are two approaches:

#### Option A: derive from `SkillRegistrationExtensions.cs`

Not recommended.

`SkillRegistrationExtensions.cs` is imperative and registration-oriented. Parsing it in the generator would be brittle and over-coupled to syntax.

#### Option B: add explicit tool-family metadata

Recommended.

Introduce a small shared declaration that both runtime registration and generator can consume.

Example:

- `src/qyl.mcp/Skills/QylSkillCatalog.cs`

This file could declare which tool types belong to which skills in a typed structure. Then:

- `SkillRegistrationExtensions.cs` reads from it
- the generator reads from it
- capability definitions can validate against it

That removes drift between:

- skill ownership
- runtime registration
- generated descriptors

## Host Split

`Program.cs` needs to become thinner.

Not because the current file is wrong, but because the next stage of metadata work needs clean boundaries.

### Current `Program.cs` responsibilities

- transport selection
- HTTP host bootstrap
- stdio host bootstrap
- common service registration
- auth wiring
- JSON context registration
- MCP server registration
- message filters
- request filters
- landing page
- `/mcp.json`
- `/llms.txt`
- health endpoint

This is too much.

### Proposed structure

Add:

- `src/qyl.mcp/Hosting/QylMcpServiceCollectionExtensions.cs`
- `src/qyl.mcp/Hosting/QylMcpServerRegistration.cs`
- `src/qyl.mcp/Hosting/QylMcpHttpHost.cs`
- `src/qyl.mcp/Hosting/QylMcpStdioHost.cs`
- `src/qyl.mcp/Hosting/QylMcpManifestBuilder.cs`
- `src/qyl.mcp/Hosting/QylMcpLlmsTextBuilder.cs`

### Responsibilities

#### `QylMcpServiceCollectionExtensions`

Own:

- collector client wiring
- auth services
- time provider
- telemetry store
- JSON source-generation chain
- debug services

#### `QylMcpServerRegistration`

Own:

- `AddMcpServer(...)`
- `WithHttpTransport(...)` / `WithStdioServerTransport()`
- filters
- `WithSkillTools(...)`
- capability tool registration
- app/resource registration

#### `QylMcpManifestBuilder`

Own:

- the shape currently produced by `CreateManifest(...)`
- future richer projections from generated descriptors

#### `QylMcpLlmsTextBuilder`

Own:

- text summary generated from canonical metadata
- no literal duplication in `Program.cs`

#### `QylMcpHttpHost`

Own:

- `/`
- `/mcp.json`
- `/llms.txt`
- `/healthz`
- `MapMcp(...)`
- HTTP auth middleware application

#### `QylMcpStdioHost`

Own:

- stdio-specific boot path only

### Final `Program.cs` target shape

`Program.cs` should do only this:

1. resolve `skills`
2. resolve `scope`
3. resolve `transport`
4. delegate to HTTP or stdio host runner

That is the right composition root size for the standalone repo.

## Generated Host Projections

Once tool descriptors and capability descriptors exist, use them to derive:

- `/mcp.json`
- `/llms.txt`
- landing-page capability summary
- future standalone package metadata artifacts

This prevents `README.md` and runtime endpoints from being the only documentation path.

## Packaging Direction

This standalone repo no longer needs to think like a subfolder inside `qyl`.

That means metadata generation should assume this repo is the source of truth for `qyl.mcp` itself.

The metadata pipeline should own:

- server display name
- summary
- canonical transport support
- public endpoint shape
- skill/capability inventory
- tool identity

Do not leave these as manually duplicated strings across runtime code and docs unless there is a strong reason.

## Tests

The standalone repo already has generator tests:

- `tests/qyl.mcp.generators.tests/ToolManifestGeneratorTests.cs`

Extend the test surface to cover:

1. descriptor generation
2. skill ownership presence
3. capability-to-tool reference validity
4. parity between generated descriptor names and registry-generated `AIFunction` names

Suggested additions:

- `ToolDescriptorGeneratorTests.cs`
- `CapabilityDefinitionTests.cs`

## Implementation Plan

### Phase 1: generator expansion

- extend `ToolManifestModels.cs`
- extend analyzer extraction logic
- emit tool descriptors
- keep backward compatibility for `QylToolManifest.CreateTools(...)`

Checkpoint:

- compile-time descriptors exist for every `[McpServerTool]`

### Phase 2: skill and capability catalog

- add typed skill catalog shared by runtime and generator
- add typed capability definitions
- validate capability references against generated tool names

Checkpoint:

- every tool belongs to a skill
- every capability references real tools

### Phase 3: capability tools

- add `qyl.list_capabilities`
- add `qyl.get_capability_guide`
- add source-generated JSON context for outputs

Checkpoint:

- clients can discover and plan without invoking `qyl.use_qyl`

### Phase 4: host split

- extract service registration
- extract server registration
- extract manifest and llms builders
- reduce `Program.cs`

Checkpoint:

- `Program.cs` becomes transport bootstrap only

### Phase 5: generated host metadata

- have `/mcp.json` and `/llms.txt` use canonical descriptors
- optionally generate additional static metadata artifacts in docs or packaging paths

Checkpoint:

- host presentation is derived, not improvised

## Proposed File Map

### New runtime files

- `src/qyl.mcp/Metadata/QylToolDescriptor.cs`
- `src/qyl.mcp/Metadata/QylCapabilityDescriptor.cs`
- `src/qyl.mcp/Capabilities/QylCapabilityDefinitions.cs`
- `src/qyl.mcp/Capabilities/CapabilityTools.cs`
- `src/qyl.mcp/Skills/QylSkillCatalog.cs`
- `src/qyl.mcp/Hosting/QylMcpServiceCollectionExtensions.cs`
- `src/qyl.mcp/Hosting/QylMcpServerRegistration.cs`
- `src/qyl.mcp/Hosting/QylMcpManifestBuilder.cs`
- `src/qyl.mcp/Hosting/QylMcpLlmsTextBuilder.cs`
- `src/qyl.mcp/Hosting/QylMcpHttpHost.cs`
- `src/qyl.mcp/Hosting/QylMcpStdioHost.cs`

### New generator work

- update `src/qyl.mcp.generators/Models/ToolManifestModels.cs`
- update `src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs`
- update `src/qyl.mcp.generators/ToolManifestGenerator.cs`
- possibly add `DescriptorEmitter.cs`

### Changed existing files

- `src/qyl.mcp/Program.cs`
- `src/qyl.mcp/Skills/SkillRegistrationExtensions.cs`
- `src/qyl.mcp/Agents/McpToolRegistry.cs`
- `src/qyl.mcp/README.md`

## Risks

### Risk 1: metadata gets duplicated anyway

If capability text and host text are still manually assembled in multiple files, the generator work will only move the duplication around.

Mitigation:

- require `mcp.json` and `llms.txt` builders to read canonical descriptors

### Risk 2: skill ownership remains imperative

If skill ownership lives only in `SkillRegistrationExtensions.cs`, generator and runtime may diverge.

Mitigation:

- introduce a shared typed skill catalog

### Risk 3: capability guides become too prose-heavy

If `qyl.get_capability_guide` just returns large markdown blobs, it will become another drift surface.

Mitigation:

- keep guide data structured first
- render human text from typed fields where helpful

### Risk 4: over-design before value

The repo could spend too much time on perfect metadata before shipping a useful first capability tool.

Mitigation:

- ship in this order:
  - generated tool descriptors
  - `qyl.list_capabilities`
  - host split
  - `qyl.get_capability_guide`

## Success Criteria

- the standalone repo has compiler-owned tool descriptors
- capabilities are declared in typed source, not implied by prose
- `qyl.list_capabilities` exists and is useful as a first call
- `qyl.get_capability_guide` teaches qyl-specific OpenTelemetry investigation workflows
- `Program.cs` becomes a thin bootstrapper
- `/mcp.json` and `/llms.txt` come from canonical metadata rather than handwritten string assembly

## Checkpoints

- [ ] CP1: generator emits metadata-rich tool descriptors in addition to `CreateTools(...)`
- [ ] CP2: typed skill catalog exists and runtime registration consumes it
- [ ] CP3: typed capability definitions exist and are validated against generated tool descriptors
- [ ] CP4: `qyl.list_capabilities` is implemented with source-generated structured output
- [ ] CP5: `qyl.get_capability_guide` is implemented with OpenTelemetry-centered guidance
- [ ] CP6: `Program.cs` is reduced to transport bootstrap and host delegation
- [ ] CP7: host metadata projections are driven by canonical descriptors

## Recommended Build Order

1. expand generator models and emit tool descriptors
2. introduce shared skill catalog
3. add capability definitions
4. ship `qyl.list_capabilities`
5. split host composition out of `Program.cs`
6. ship `qyl.get_capability_guide`
7. wire `/mcp.json` and `/llms.txt` to canonical metadata

## Final Position

The standalone repo does not need more ad hoc host prose or more embedded orchestration.

It needs a stronger compiler-owned contract.

The right path for `qyl.mcp` in this tree is:

- extend `src/qyl.mcp.generators`
- formalize skills and capabilities
- expose deterministic discovery tools
- split hosting from core server registration

That gives the standalone repo a cleaner serving-plane shape, a better client planning surface, and a stronger foundation for future packaging and auditability.
