# Weaver CLI Decision — Source-Generator Consumption

**Audience.** semconv-srcgen team, Phase 1 generator scaffold (Agent 3) and Phase 3 audit (Agent 2 resume).

**Sources of truth.**

- `open-telemetry/weaver` @ `3b490b72f2e901d267132a7295ef4800225e26b6` (`main`, 2026-05-19; shallow clone at `/tmp/weaver-ro` for this audit; the same SHA is reachable on the public remote).
- `open-telemetry/opentelemetry-dotnet-contrib` @ `55978aae5ae5641a0b405028db0d94de8d6f2a90` (`main`, 2026-05-19; read via `gh api`).
- `ANcpLua/opentelemetry-dotnet-contrib` @ `dbd5af1b820aa5079fc091935d90b90eb98ccd55` (PR #4362 head, branch `semconv-generator-improvements`; read via `gh api`).

## Verdict

The canonical Weaver CLI for source-generator consumption is **`weaver registry generate`** with Jinja templates that emit C#.

The orchestrator's framing — "produces typed JSON conforming to the `resolved-registry-v2` schema" — is **not how Weaver is consumed by .NET semconv codegen today**. Weaver does not expose a "typed JSON, then C# in the consumer" pipeline that matches the contrib precedent. The shipping pipeline is **Weaver-Jinja → C# directly**: the .NET-shaped data structures live in the Jinja template, not in a JSON contract on the .NET side.

`weaver registry package` is a different operation: it serialises the resolved registry as **YAML** (`resolved.yaml` + `manifest.yaml`) for *publication* of the registry itself. It is not the C#-codegen entry point.

`weaver registry resolve` is **deprecated** and rejected under `--future`. Any agent reaching for it should redirect to `generate` or `package`.

## Evidence

| Question | Answer | Evidence (file:line in `open-telemetry/weaver` @ `3b490b72`) |
| --- | --- | --- |
| Canonical CLI for C# emission | `weaver registry generate` | `src/registry/generate.rs:26` — module doc: *"Generate artifacts from a semantic convention registry using Jinja templates"*; `src/registry/generate.rs:142,145` — `output.generate(v.template_schema())?` runs Jinja over `ForgeResolvedRegistry`. Output goes to the `--output` directory (`generate.rs:36`). |
| What `resolve` is now | Deprecated; redirect to `generate` or `package` | `src/registry/resolve.rs:23-24` — `Deprecated` error variant: *"The 'weaver registry resolve' command is deprecated and will be removed in a future version. Please use 'weaver registry generate' or 'weaver registry package' instead."* Hard-fails under `--future` (`resolve.rs:68-70`); logs deprecation otherwise (`resolve.rs:72-73`). |
| What `package` does | Writes `resolved.yaml` + `manifest.yaml` (YAML, not JSON), for registry **publication** — requires v2 | `src/registry/package.rs:24` — module doc: *"Package a resolved registry for publication (produces `resolved.yaml` and `manifest.yaml`)"*. `package.rs:55-65` — `write_yaml` uses `serde_yaml::to_writer`. `package.rs:142-143` — writes `output/resolved.yaml` and `output/manifest.yaml`. `package.rs:90-92` — `Err(Error::PackagingRequiresV2)` when `!v2`. `package.rs:24` docstring explicitly names this as publication; the test at `package.rs:217-220` confirms `file_format: resolved/2.0`. |
| Output filename convention for `generate` | Whatever the Jinja template writes; orchestrated by the template's `weaver.yaml` | `generate.rs:36-37` — `--output` is a *directory*; the template's `weaver.yaml` declares each file via `templates: - pattern: <name>.j2` (contrib's config at `src/OpenTelemetry.SemanticConventions/scripts/templates/registry/weaver.yaml` declares `SemanticConventionsAttributes.cs.j2`). |
| Output filename convention for `package` | Always `resolved.yaml` + `manifest.yaml` under `--output` | `package.rs:142-143`. |
| Schema name "resolved-registry-v2" | Refers to the `weaver_forge::v2::registry::ForgeResolvedRegistry` Rust type, surfaced to templates as `template_schema()`. The YAML serialisation written by `package` carries `file_format: resolved/2.0` (`package.rs:217-220`). It is **not** a `.json` artefact in either CLI path. | `src/weaver.rs:332` (`template_schema: weaver_forge::v2::registry::ForgeResolvedRegistry`) and `src/weaver.rs:349-350` (`pub fn template_schema(&self) -> &ForgeResolvedRegistry`). |

## Contrib precedent — direct corroboration

The shipping .NET semconv project drives Weaver via `registry generate`, not `package`, in both `main` and the in-flight refactor PR.

- **`main` @ `55978aae`** — `src/OpenTelemetry.SemanticConventions/scripts/generate.sh` invokes:
  ```
  otel/weaver:$GENERATOR_VERSION
    registry generate
    --registry=/source --templates=/templates "./" "/output/./"
  ```
  with `GENERATOR_VERSION="v0.23.0"` and `SEMCONV_VERSION="1.41.0"`.

- **PR #4362 @ `dbd5af1b`** (`ANcpLua/opentelemetry-dotnet-contrib:semconv-generator-improvements`) — same `registry generate` invocation, pinned to the same `v0.23.0`/`1.41.0` plus the semconv commit `e018fe6f91862f5ed63c082f87697cddac596784` for byte-reproducibility.

Both invocations confirm: **C# emission = `weaver registry generate` + Jinja templates**. There is no precedent for routing through `package` and parsing YAML in .NET.

## What this means for Agent 3 (Phase 1 scaffold)

- **Driver.** The qyl source-generator pipeline should pin and shell out to `weaver registry generate` (preferably via the same `otel/weaver:v0.23.0` Docker image used by contrib, for byte-identity with upstream output during Phase 3 audits).
- **Templates.** Reuse contrib's `common.j2` + `SemanticConventionsAttributes.cs.j2` (verified to exist at `main`; see `weaver-audit-charter.md` §B) verbatim, plus any qyl-specific templates layered on top.
- **Do not.** Don't write a "load `resolved.yaml`, parse YAML in C#, emit attributes" pipeline. That contradicts the contrib precedent and adds a parser surface area we don't need.
- **Do not.** Don't reach for `weaver registry resolve`. It is deprecated at the source and hard-fails under `--future`.

## What this means for Agent 2 resume (Phase 3 audit)

- Byte-identity check between qyl's generated C# and contrib's generated C# (same Weaver version, same semconv version, same templates) is the **strongest signal** that the pipeline is wired correctly. This is the Phase 3 audit's primary gate.
- Any divergence in Weaver CLI flags, generator version, or template content must be flagged immediately. See `weaver-audit-charter.md` §B for the exact template paths to diff against.
