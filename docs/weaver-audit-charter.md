# Weaver Compliance Audit Charter (Seed)

**Owner.** weaver-spec-auditor (Phase 3 / Agent 2 resume — Task #7). This document is the **charter seed** dropped by Phase 0; the resuming agent extends it but does not rewrite §A's spec-doc invariants without coordinating with the team lead.

**Pinned references.**

- `open-telemetry/weaver` @ `3b490b72f2e901d267132a7295ef4800225e26b6` (`main`, 2026-05-19).
- `open-telemetry/opentelemetry-dotnet-contrib` @ `55978aae5ae5641a0b405028db0d94de8d6f2a90` (`main`, 2026-05-19).
- `ANcpLua/opentelemetry-dotnet-contrib` @ `dbd5af1b820aa5079fc091935d90b90eb98ccd55` (PR #4362 head, `semconv-generator-improvements`).
- `open-telemetry/semantic-conventions` `v1.41.0` (commit `e018fe6f91862f5ed63c082f87697cddac596784`) — the spec version both contrib and qyl pin.
- This worktree (`qyl` `agent/weaver-spec-auditor`) @ base `f76ed5e8b46deb766ad2e50c62a72d2c59fd0a25`.

The charter has three sections: **§A** spec compliance rules, **§B** verbatim template path cross-check, **§C** Roslyn pin resolution.

---

## §A — Spec Compliance Rules

Every emission from the qyl source generator (Tracks A–E in Phases 1–2) must be auditable against four upstream documents. URLs are anchored on `main` of `open-telemetry/opentelemetry-specification`.

### A.1 — Specification Principles

URL: <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/specification-principles.md>

Enforce, on every PR that touches generator output:

1. **Be User Driven.** The generated surface must serve OpenTelemetry users' instrumentation needs first; convenience helpers added without a user-facing need are slop. Reject "obvious improvements" that the upstream Jinja template would not produce.
2. **Be General.** No qyl-specific shortcuts in shipped attributes/metrics. Anything qyl-specific lives behind a qyl-only namespace or in non-shipped helpers.
3. **Be Stable ("Don't. Break. Users.").** No rename, no drop, no semantic shift of an already-emitted attribute outside the schema-migration mechanism (see A.2). Additive-only.
4. **Be Consistent.** Casing, namespacing, and naming must match the generator output upstream produces from the same spec version. Byte-identity audit (§B) is the strongest enforcement.
5. **Be Simple.** No new abstraction layer over Weaver output unless it carries its weight against a stated user need.

### A.2 — Telemetry Stability

URL: <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md>

Enforce:

1. **Stable instrumentations cannot rename or drop attributes** outside the schema-file migration mechanism (currently under moratorium). Any diff that deletes a string or renames a const member needs a paper trail showing the spec dropped it.
2. **Additive changes are always allowed** — new attributes, new metrics, new events. Generator must add, never silently remove.
3. **Stability tier respected.** `stability: stable` and `stability: experimental` (a.k.a. *incubating*) must land in distinct artefacts/namespaces — this is the entire reason contrib PR #4362 splits into `OpenTelemetry.SemanticConventions` (stable) and `OpenTelemetry.SemanticConventions.Incubating`. qyl mirrors this split.
4. **No tier downgrade.** Attributes promoted experimental → stable upstream must propagate; nothing in qyl pins an attribute to experimental once upstream marks it stable.
5. **Deprecated attributes retain emission until upstream removes them** — they are stable until the spec breaks them, and qyl follows the spec's lead, never anticipates.

### A.3 — Spec Compliance Matrix

URL: <https://github.com/open-telemetry/opentelemetry-specification/blob/main/spec-compliance-matrix.md>

Enforce:

1. **C# parity claims must be grounded.** Before claiming "qyl supports feature X for semantic conventions", confirm the matrix row marks C#/.NET as supported. If the matrix does not list it, the qyl PR description must explain the discrepancy.
2. **Feature-level granularity.** "Supports semantic conventions" is too coarse. Audit specifically: tracing attributes, metric instruments, logs/events, resource attributes — each has its own row.
3. **Track upstream movement.** When a row flips from gap to supported in upstream, that creates work for qyl. The audit checks the matrix on each emission run.

### A.4 — Glossary

URL: <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md>

Controlled vocabulary. Use these terms exactly in generator output, doc comments, commit messages, and PR text:

1. **"attribute"**, never "tag", "label", or "field" when talking about telemetry key-value pairs.
2. **"instrumentation scope"**, never "service version" or "library version" when naming the originating instrumentation library.
3. **"resource"** for entity-level attributes; not "service metadata".
4. **"span"** for the unit of trace work, "log record" for log lines, "metric instrument" for the measurement type — terms are not interchangeable.
5. **Synonyms are slop.** Mixed vocabulary in generated XML doc comments is a low-effort drift signal that wastes upstream maintainer review bandwidth. Audit reject.

---

## §B — Verbatim Jinja Template Paths

**Goal.** During Phase 3 byte-identity audit, the qyl-generated C# must match contrib's generated C# under identical Weaver version + semconv version + templates. Templates are the variable to lock down.

### B.1 — Templates in contrib `main` (`55978aae`)

Both paths from the orchestrator's table are **verified present** via `gh api repos/open-telemetry/opentelemetry-dotnet-contrib/contents/<path>?ref=main`:

| Path | Blob SHA | Size |
| --- | --- | --- |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/common.j2` | `1a477cd761fc21d0d160e4098605cb982ccebb69` | 152 bytes |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/SemanticConventionsAttributes.cs.j2` | `76aebe5f1aa03e39598fa1781dd6a149dc47f437` | 2433 bytes |

Companion files at the same prefix on `main` (worth tracking too, since the generator reads them):
- `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/weaver.yaml` — Weaver target config (excluded attributes, csharp comment format, csharp type-map, the `SemanticConventionsAttributes.cs.j2` template entry).

### B.2 — Templates in PR #4362 (`dbd5af1b`, fork `ANcpLua/opentelemetry-dotnet-contrib`)

The PR introduces two additional Jinja templates (Stable/Incubating split scaffolding):

| Path | Status |
| --- | --- |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/common.j2` | present |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/SemanticConventionsAttributes.cs.j2` | present |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/SchemaUrl.cs.j2` | **new in PR** |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/SchemaVersion.cs.j2` | **new in PR** |
| `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/weaver.yaml` | present |

### B.3 — Audit gate

On every qyl emission run, the auditor must:

1. Fetch the contrib templates at the SHA pinned in qyl's generator (initially `main` @ `55978aae`, or PR #4362 @ `dbd5af1b` if Track scaffolding pins to the PR head).
2. Diff against qyl's local copy. Any deviation requires an explicit justification in the PR description that maps to a qyl-specific need (e.g., qyl namespace prefix). No silent template forks.
3. Re-run `weaver registry generate` with the locked template set and compare emitted C# byte-for-byte against contrib's `Attributes/` output for the same semconv version. Diff = audit fail until reconciled.

---

## §C — Roslyn Pin Resolution

The orchestrator's table claims `Microsoft.CodeAnalysis.CSharp 5.0.0`; testbed#1 claims `4.14.0`.

### C.1 — Contrib search

Read both contrib trees:

- `open-telemetry/opentelemetry-dotnet-contrib` `main` @ `55978aae`, file `src/OpenTelemetry.SemanticConventions/OpenTelemetry.SemanticConventions.csproj`: **no `PackageReference` to `Microsoft.CodeAnalysis.CSharp` or any Roslyn analyzer package**. The project is a plain `Microsoft.NET.Sdk` netstandard library; the C# is generated by Weaver/Jinja, not by a Roslyn source generator.
- `ANcpLua/opentelemetry-dotnet-contrib` PR #4362 @ `dbd5af1b`, file `src/OpenTelemetry.SemanticConventions/OpenTelemetry.SemanticConventions.csproj` **and** the new `src/OpenTelemetry.SemanticConventions.Incubating/OpenTelemetry.SemanticConventions.Incubating.csproj`: **also no Roslyn analyzer reference**. Both are plain `Microsoft.NET.Sdk` netstandard libraries.

**Conclusion: contrib has no Roslyn pin.** Both orchestrator (`5.0.0`) and testbed (`4.14.0`) cited a Roslyn version for contrib that does not exist there. The Weaver-Jinja pipeline does not require any Roslyn package in the consuming project.

### C.2 — qyl `main` (`f76ed5e8b46deb766ad2e50c62a72d2c59fd0a25`)

The actual Roslyn pin lives in **qyl**, not contrib:

- `Version.props:16` — `<MicrosoftCodeAnalysisCSharpVersion>5.3.0</MicrosoftCodeAnalysisCSharpVersion>`.
- `Version.props:15` — `<MicrosoftCodeAnalysisAnalyzersVersion>5.3.0</MicrosoftCodeAnalysisAnalyzersVersion>`.
- `Directory.Packages.props:83` — `<PackageVersion Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="$(MicrosoftCodeAnalysisCSharpVersion)"/>` (i.e. resolves to 5.3.0).

This is the pin that the qyl semconv source generator will inherit when it is added to any `internal/qyl.*.generators/` project (the existing analyzer/generator csprojs already centralise on these variables — see `internal/qyl.instrumentation.generators/qyl.instrumentation.generators.csproj`, `internal/qyl.collector.storage.generators/qyl.collector.storage.generators.csproj`, `internal/qyl.mcp.generators/qyl.mcp.generators.csproj`).

### C.3 — Verdict for Agent 3

- **Use `MicrosoftCodeAnalysisCSharpVersion = 5.3.0`** (the centralised pin in `Version.props`) for the new generator project. Do not introduce a divergent version; reuse the variable, do not hard-code.
- Neither `5.0.0` (orchestrator) nor `4.14.0` (testbed#1) is correct for this repo. If a downstream agent re-cites those, treat as drift and correct against `Version.props:16`.
- If the semconv source generator ends up shaped like contrib's project (i.e. C# is generated *outside* the consumer csproj by Weaver/Jinja, and the csproj merely compiles the generated `.cs` files), **no Roslyn package is required at all** in the consumer. The Roslyn pin only matters if qyl wraps the Weaver invocation in a Roslyn source generator that runs in-process during compile. Agent 3's scaffold should document which path it picks.

---

## Audit Cadence (Phase 3)

When Task #7 starts:

1. Refresh the pinned SHAs in this header. Diff `open-telemetry/weaver`, contrib `main`, and PR #4362 against the SHAs above; record movement before auditing emissions.
2. Re-run all §A checks against every Track A–E emission PR.
3. Re-run §B byte-identity diff for every emission that touches generated C# under the semconv root namespaces.
4. Re-verify §C pin remains coherent — if a generator csproj appears under `internal/qyl.semconv.*` it must reference `$(MicrosoftCodeAnalysisCSharpVersion)`, never a literal.
5. Open follow-up tasks for any drift; do not silently fix without a paper trail. Maintainer review bandwidth is the most expensive resource in the chain.
