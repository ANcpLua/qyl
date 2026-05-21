# Path correction — `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration`

**Owner:** generator-foundation-eng (Phase 1, Task #3).
**Supersedes (qyl-side only):** strategist's `docs/dual-target-mapping.md` §2 row "Solution folder = `src/SemConv.Gen/`".
**Does NOT change:** contrib-side path map, the §2.1 invariant (emit logic does not branch on target), the namespace token mechanism, or the cherry-pick cadence in §1.3.

## Correction

| Concern | Strategist's mapping | Actual qyl path used |
|---|---|---|
| Solution folder | `src/SemConv.Gen/` | `packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/` |
| Root namespace | `Qyl.SemConv.Gen` | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` |
| Generator csproj | `Qyl.SemConv.Gen.Generator.csproj` | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator.csproj` |
| Published PackageId | (not specified) | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` |
| Marker attribute FQN | `Qyl.SemConv.Gen.SemanticConventionAttributesAttribute` | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesAttribute` |
| Embedded resource logical name | `Qyl.SemConv.Gen.resolved-registry.json` | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json` |

## Reasons (three; ordered by weight)

### 1. qyl has no `src/` top-level

Verified: `ls /Users/ancplua/RiderProjects/qyl` shows `packages/`, `services/`, `internal/`, `tests/`, `eng/`, `core/` — no `src/`. Strategist's path was inherited from contrib's tree; qyl's actual convention is `packages/Qyl.<Name>/` for publishable NuGets and `internal/qyl.<area>.generators/` for internal-only generators (verified `internal/qyl.mcp.generators/`, `internal/qyl.instrumentation.generators/`, `internal/qyl.collector.storage.generators/`).

### 2. This generator is publishable, not internal

The Generator csproj is the consumer-facing surface (the marker attribute is emitted via `RegisterPostInitializationOutput`, so no companion runtime library ships — single-package layout). Per the strategist's §1.3 cadence table, PR-A's output ships as `OpenTelemetry.SemanticConventions.SourceGeneration` on contrib; the qyl twin should be a publishable NuGet on the `github-O-ANcppLua` feed alongside its sister `Qyl.OpenTelemetry.SemanticConventions` and `Qyl.OpenTelemetry.SemanticConventions.Incubating` packages. `packages/` is the right folder.

### 2.1 — Two-project layout collapsed to one

The strategist's mapping (and the lead's PR-A spec) named two csprojs: a marker library and a sibling Generator. PR-A's marker attribute is emitted via `RegisterPostInitializationOutput`, not authored — so a separate marker library would have shipped no source. Combining both into one csproj that packs the analyzer DLL under `analyzers/dotnet/cs` and is itself the consumer-facing NuGet keeps the layout one project per concept. Phase 2/3 can re-split if they need a runtime shim (e.g., extension methods that live in `lib/`).

### 3. Matching contrib's name keeps cherry-picks mechanical

Strategist §2 maps the contrib root namespace to `OpenTelemetry.SemanticConventions.SourceGeneration`. Using `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` on the qyl side means the cherry-pick map is a single `s/Qyl\.//` substitution at csproj level (RootNamespace and folder path) — no inner-source edits, fully preserving §2.1 (emit logic does not branch on target). The strategist's name `Qyl.SemConv.Gen` would have required a second mapping rule (namespace remap inside any source that names the type directly).

## What this preserves

- **§2.1 invariant** — emit logic still does not branch on target. The new generator reads its own `Prefix` constructor arg from the marker attribute; namespace flows in only as the user-declared partial class's `ContainingNamespace`. No code references the literal `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` outside csproj metadata and the post-init attribute definition (which the cherry-pick rewrites uniformly to `OpenTelemetry.SemanticConventions.SourceGeneration`).
- **§1.3 cadence** — qyl source-of-truth, contrib downstream. Branch graph unchanged.
- **§4 version pins** — semconv `v1.41.0`, Weaver `v0.23.0`, semconv commit `e018fe6f91862f5ed63c082f87697cddac596784` — all pinned in `Resources/resolved-registry.json` header, `scripts/generate.sh`, and `scripts/templates/registry/weaver.yaml`.

## What this leaves to the strategist

When the strategist cherry-picks PR-A into `feat/semconv-srcgen` and then to contrib:

- Apply the path map: `packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/` → `src/OpenTelemetry.SemanticConventions.SourceGeneration/` (contrib's PR-A scaffolds a single project named `OpenTelemetry.SemanticConventions.SourceGeneration`; if maintainers ask for `.Generator` suffix, the rename is mechanical).
- Apply the namespace map: `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` → `OpenTelemetry.SemanticConventions.SourceGeneration` everywhere (csproj `<RootNamespace>`, post-init attribute source literal, `LogicalName` on the embedded resource, FQN in `SemConvAttributesGenerator.AttributeFullName`).
- Apply the csproj-shape map: `Sdk="ANcpLua.NET.Sdk"` → `Sdk="Microsoft.NET.Sdk"`; drop the qyl-only `<Using>` items; drop the `ANcpLua.Roslyn.Utilities.Sources` PackageReference (contrib will need its own polyfill or use direct Roslyn primitives).

This is mechanical and matches the strategist's stated cherry-pick filter pattern.
