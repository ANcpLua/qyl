# Complete CP2 and CP3: Eliminate Manual Rosters via Generator

**Date:** 2026-04-10
**Depends on:** Monorepo merge (qyl.mcp + netagents Qyl.Agents projects into qyl)
**References:**
- `docs/plans/2026-04-07-generated-metadata-capability-guides-and-host-split.md` (the original plan)
- `docs/specs/2026-04-08-result-cap-coaching-design.md` (approved, fully implemented, no open work)

---

## Context

The plan at `docs/plans/2026-04-07-generated-metadata-capability-guides-and-host-split.md` proposed 7 checkpoints to make `qyl.mcp` self-describing with compiler-owned metadata. 5 of 7 are complete. 2 are not.

### Checkpoint status

| CP | Intent | Status |
|----|--------|--------|
| CP1 | Generator emits metadata-rich tool descriptors | **Done.** `QylToolManifest.ToolDescriptors[]` with Name, Title, Description, ReadOnly, Destructive, Idempotent, OpenWorld, ReturnType. |
| CP2 | Typed skill catalog exists **and runtime registration consumes it** | **Half-done.** `QylSkillCatalog.cs` exists as a hand-coded dictionary. But `SkillRegistrationExtensions.cs` does NOT read from it — it is a second parallel list of the same ~60 tool types. They must be manually synchronized. |
| CP3 | Typed capability definitions **validated against generated tool descriptors** | **Unvalidated.** `QylCapabilityCatalog.cs` exists with 16 capabilities, but tool name references are hardcoded strings with no compile-time or test-time check that they match the generated manifest. |
| CP4 | `qyl.list_capabilities` | **Done.** `CapabilityTools.cs`. |
| CP5 | `qyl.get_capability_guide` | **Done.** `CapabilityTools.cs`. |
| CP6 | `Program.cs` reduced to thin bootstrapper | **Done.** `Hosting/` folder has all 6 files the plan proposed. |
| CP7 | Host metadata driven by canonical descriptors | **Done.** `QylMcpManifestBuilder` + `QylMcpLlmsTextBuilder` consume `QylMcpMetadataCatalog`. |

### The structural problem CP2 was supposed to prevent

The same ~60 tool classes are manually enumerated in 4 parallel rosters that must stay synchronized by hand:

1. **Generator output** (`QylToolManifest.ToolTypes[]` / `ToolDescriptors[]`) — compile-time auto-discovered from `[McpServerToolType]` attributes. This is the only roster that is automatic.
2. **`SkillRegistrationExtensions.WithSkillTools()`** — hand-coded if/else branches calling `mcpBuilder.WithTools<T>()` per skill.
3. **`QylSkillCatalog.SkillMap`** — hand-coded `Dictionary<string, QylSkillKind>` mapping type names to skill enum values.
4. **`QylMcpServiceCollectionExtensions.AddQylMcpCommonServices()`** — hand-coded `AddCollectorToolClient<T>()` calls for DI registration.

Add a tool class with `[McpServerToolType]` and the generator picks it up automatically. But you still have to hand-add it in 3 other files or it either won't register as an MCP tool, won't have a skill label, or won't have DI.

The plan's Risk 2 warned exactly about this:
> "If skill ownership lives only in `SkillRegistrationExtensions.cs`, generator and runtime may diverge."
> Mitigation: `SkillRegistrationExtensions.cs` reads from it.

That mitigation was never implemented.

---

## What to implement

### CP2: Make the generator the single source of truth for skill ownership

**Step 1 — Define a skill attribute**

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class QylSkillAttribute(QylSkillKind skill) : Attribute
{
    public QylSkillKind Skill { get; } = skill;
}
```

**Step 2 — Decorate each `[McpServerToolType]` class with its skill**

```csharp
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class ErrorTools(CollectorClient client) { ... }
```

This is the single source of truth. One attribute, on the class, next to `[McpServerToolType]`.

**Step 3 — Extend the generator to scan `[QylSkill]`**

The generator (post-merge, this is the unified Qyl.Agents.Generator or the existing qyl.mcp.generators — whichever is active) already scans `[McpServerToolType]` via `ForAttributeWithMetadataName`. Extend the analyzer to also read `[QylSkill]` from the same class symbol and store the skill kind in the generator model.

**Step 4 — Generator emits 3 new outputs**

| Generated method/field | Purpose | Replaces |
|---|---|---|
| `QylToolManifest.SkillMap` — `IReadOnlyDictionary<Type, QylSkillKind>` | Compile-time type-to-skill mapping | `QylSkillCatalog.cs` manual dictionary (**delete**) |
| `QylToolManifest.RegisterTools(IMcpServerBuilder, SkillConfiguration, JsonSerializerOptions)` | Generated method that calls `mcpBuilder.WithTools<T>(jsonOptions)` per type, gated by `skills.IsEnabled(skill)` | `SkillRegistrationExtensions.cs` (**delete**) |
| `QylToolManifest.RegisterServices(IServiceCollection, SkillConfiguration)` | Generated method that calls `services.AddTransient<T>()` per type, gated by `skills.IsEnabled(skill)` | DI registration block in `QylMcpServiceCollectionExtensions` (**delete that block**) |

Each `ToolDescriptor` in `ToolDescriptors[]` also gains a `Skill` field, eliminating the need for `RuntimeToolDescriptor` to enrich it at runtime.

**Step 5 — Delete the 3 manual rosters**

- Delete `SkillRegistrationExtensions.cs` entirely
- Delete the `SkillMap` dictionary from `QylSkillCatalog.cs` (keep the `QylSkillKind` enum and `SkillConfiguration` — those are consumed by the generated code)
- Delete the per-skill `AddCollectorToolClient<T>()` block from `QylMcpServiceCollectionExtensions` (replace with a call to `QylToolManifest.RegisterServices(services, skills)`)

**Step 6 — Update call sites**

- `QylMcpServerRegistration.Configure()`: replace `mcpBuilder.WithSkillTools(skills, jsonOptions)` with `QylToolManifest.RegisterTools(mcpBuilder, skills, jsonOptions)`
- `QylMcpServiceCollectionExtensions.AddQylMcpCommonServices()`: replace the DI block with `QylToolManifest.RegisterServices(services, skills)`
- `QylMcpMetadataCatalog`: read `Skill` directly from `QylToolManifest.ToolDescriptors` instead of joining against `QylSkillCatalog.GetSkillLabel()`

**Result:** 4 rosters collapse to 1. The attribute on the class is the only place skill ownership is declared. The generator discovers it and emits everything else.

---

### CP3: Validate capability tool references against the generated manifest

**Step 1 — Add one test**

```csharp
[Fact]
public void AllCapabilityToolReferencesExistInManifest()
{
    var knownToolNames = QylToolManifest.ToolDescriptors
        .Select(d => d.Name)
        .ToHashSet(StringComparer.Ordinal);

    foreach (var capability in QylCapabilityCatalog.All)
    {
        foreach (var toolName in capability.StartingTools)
            knownToolNames.Should().Contain(toolName,
                $"capability '{capability.Id}' references starting tool '{toolName}' which does not exist in the generated manifest");

        foreach (var toolName in capability.FollowUpTools)
            knownToolNames.Should().Contain(toolName,
                $"capability '{capability.Id}' references follow-up tool '{toolName}' which does not exist in the generated manifest");
    }
}
```

**Step 2 — Fix any broken references**

Run the test. If any capability references a tool name that doesn't match the generated manifest (typo, renamed tool, deleted tool), fix the reference in `QylCapabilityCatalog.cs`.

**Result:** Tool name drift between capabilities and the generated manifest is caught at test time. No more silent string mismatches.

---

## Files changed summary

### New files
- `QylSkillAttribute.cs` (the attribute definition)

### Deleted files
- `SkillRegistrationExtensions.cs` (~170 lines of hand-coded if/else)

### Modified files
- Each `[McpServerToolType]` class: add `[QylSkill(QylSkillKind.X)]` attribute (~60 classes, 1 line each)
- Generator analyzer: scan `[QylSkill]` from class symbols
- Generator emitter: emit `SkillMap`, `RegisterTools`, `RegisterServices`, add `Skill` to `ToolDescriptors`
- Generator models: add `SkillKind` field to `ToolTypeEntry`
- `QylSkillCatalog.cs`: delete the manual `SkillMap` dictionary, keep `QylSkillKind` enum and helpers
- `QylMcpServerRegistration.cs`: call generated `RegisterTools` instead of deleted `WithSkillTools`
- `QylMcpServiceCollectionExtensions.cs`: call generated `RegisterServices` instead of manual DI block
- `QylMcpMetadataCatalog.cs`: read `Skill` from descriptor instead of joining catalog
- Test project: add capability validation test

### Apps skill: partial generation

The 3 Apps tool types (`TraceExplorerTools`, `ErrorExplorerTools`, `QueryStudioTools`) use custom registration extension methods (`WithTraceExplorer`, `WithErrorExplorer`, `WithQueryStudio`) that register both `WithTools<T>()` AND `WithResources<T>()`. The generated `RegisterTools` handles the `WithTools<T>(jsonOptions)` call. The 3 `WithResources<T>()` calls stay manual in `QylMcpServerRegistration.Configure()` — the generator does not handle MCP resource registration.

### DI registration stays manual

The DI block in `QylMcpServiceCollectionExtensions.AddQylMcpCommonServices()` uses different registration patterns per tool type: `AddCollectorToolClient<T>()` (typed HTTP client), `AddSingleton<T>()`, and custom wiring for Debug tools. This is not uniform enough to generate. The DI block stays as is — it is a construction concern, not a roster concern.

### Net effect
~250 lines of hand-maintained skill-to-type rosters deleted (SkillRegistrationExtensions + QylSkillCatalog dictionary). ~60 one-line attribute additions. Generator emits MCP tool registration and skill mapping. Single source of truth for "which type is which skill" enforced at compile time.

---

## Constraints

- No `[Obsolete]` attributes. Delete dead code, don't deprecate it.
- No `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>`.
- Roslyn generators must be `IIncrementalGenerator` with `ForAttributeWithMetadataName` and value-equatable models.
- The `result-cap-coaching-design.md` spec is fully implemented and its constraints (documented in `CLAUDE.md`) must not be violated: no shared `RESULT_LIMIT` constant, no migration of ad-hoc tools to `FormatPagedList`, no filter-level enforcement, fixed-scope tools skip coaching.
