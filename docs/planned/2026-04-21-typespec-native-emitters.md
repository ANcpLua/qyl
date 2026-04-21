# Execution Mandate — `refactor/typespec-native-emitters-and-sdks`

**Repo:** `/Users/ancplua/qyl` (this repo). All paths below are qyl-internal.
**Baseline:** `main` at or after `ad71ecdb` (PR #143 merged). No known blockers.
**Execution:** atomic commits 1→9, no checkpoints, no interactive approval, no partial merges. Per `~/.claude/CLAUDE.md` "Whole-System Thinking" and "Decision Discipline": finish in one session.

## Preconditions (verify before Commit 1)

1. **Submodule init** (if fresh worktree): `git submodule update --init .tools/semconv-upstream`
2. **npm scope `@qyl` owned by the release principal** on npmjs.com. Verify: `npm access list packages @qyl 2>&1`. If the scope is unclaimed, run `npm org create qyl` or publish the first `@qyl/client` under a personal scope claim before Commit 8. This is the only step that *might* require user action — if the scope is not ownable, rename the npm package to `qyl-client` (unscoped) across Commits 6/7/8 and proceed.
3. **GitHub Actions secrets** present on the repo: `NUGET_API_KEY`, `NPM_TOKEN`. Verify: `gh secret list | grep -E 'NUGET_API_KEY|NPM_TOKEN'`. If missing, the Commit 8 release workflow will still land but its first tag-triggered run fails until secrets are provisioned — not a blocker for the branch merging.

## Pinned tool versions

Add to `core/specs/package.json` devDependencies (Commit 6 rewrites this block — use these exact versions, do not resolve to `latest`):

```json
"@typespec/compiler":         "1.7.0",
"@typespec/http":             "1.7.0",
"@typespec/rest":             "1.7.0",
"@typespec/http-client-csharp": "0.9.0",
"@typespec/http-client-js":   "0.1.0"
```

If any version no longer resolves from npm during Commit 6, bump forward to the nearest stable and record the bump in the commit body. Do not pin `latest`.

---

## Invariants

- `dotnet build qyl.slnx --tl:off` exits 0 after every commit.
- `cd core/specs && npm run compile` exits 0 after every commit from Commit 2 onward.
- `git blame --follow` traces every moved file back through Commit 1.
- Every public type exposed via `packages/*` has a stable `Qyl.*` or `@qyl/*` namespace. `services/*` and `internal/*` never leak types outward.

---

## Target layout

```
qyl/
├── packages/                               ← PUBLISHED — NuGet.org + npmjs.com
│   ├── Qyl.Client/                         ← TypeSpec http-client-csharp output
│   ├── qyl-client/                         ← TypeSpec http-client-js output
│   ├── Qyl.Contracts/                      ← shared models (TypeSpec-emitted)
│   └── Qyl.OpenTelemetry.Extensions/       ← data-plane convenience (hand)
├── services/                               ← RUNNABLE — container image only
│   ├── qyl.collector/
│   ├── qyl.mcp/
│   ├── qyl.loom/
│   └── qyl.dashboard/
├── internal/                               ← INTERNAL — project-reference only
│   ├── qyl.instrumentation/
│   ├── qyl.instrumentation.generators/
│   └── qyl.collector.storage.generators/
├── core/specs/                             ← TypeSpec source of truth
│   ├── emitters/
│   │   ├── csharp/                         ← @qyl/typespec-emit-csharp
│   │   ├── duckdb/                         ← @qyl/typespec-emit-duckdb
│   │   └── ts-types/                       ← @qyl/typespec-emit-ts-types
│   ├── *.tsp
│   ├── package.json
│   └── tspconfig.yaml
├── eng/
│   ├── build/                              ← NUKE — build/test/pack only
│   └── semconv/                            ← Weaver — unchanged
├── tests/qyl.collector.tests/              ← only surviving test project
└── .github/workflows/release.yml           ← tag-triggered pack + publish
```

---

## Commit 1 — Repo-layout refactor

`git mv` only, zero logic changes.

Move:
- `src/qyl.{collector,mcp,loom,dashboard}` → `services/`
- `src/qyl.{instrumentation,instrumentation.generators,collector.storage.generators}` → `internal/`
- `src/qyl.contracts` → `packages/Qyl.Contracts`
- `tests/qyl.collector.tests` stays.

Fix every `<ProjectReference Include=…>` across all `.csproj`. Update `qyl.slnx` Folder structure to the three tiers. Update `eng/build/BuildPaths.cs` — every `src/qyl.X` constant becomes `services/qyl.X` or `internal/qyl.X` or `packages/Qyl.X`. Update `.github/workflows/*.yml` path filters.

Verify: `dotnet build qyl.slnx --tl:off` exits 0.

---

## Commit 2 — C# emitter package (`@qyl/typespec-emit-csharp`)

Scaffold under `core/specs/emitters/csharp/`:

```
package.json   name: @qyl/typespec-emit-csharp, type: module,
                dependencies: @typespec/compiler
lib/main.tsp   decorators: @csharpNamespace(ns: string), @csharpRecord, @csharpEnum
src/
  index.ts     $onEmit(context)
  emitter.ts   navigateProgram walker, type-map switch
  $lib.ts      decorator state registration
```

Type map (exact):
- `int32 → int` · `int64 → long` · `float32 → float` · `float64 → double`
- `boolean → bool` · `string → string`
- `string (format=uuid) → Guid` · `string (format=url) → Uri`
- `utcDateTime → DateTimeOffset` · `duration → TimeSpan` · `bytes → ReadOnlyMemory<byte>`
- `Array<T> → IReadOnlyList<T>` · `Record<T> → IReadOnlyDictionary<string, T>`
- Model reference → qualified type (resolve `@csharpNamespace`)
- Enum reference → qualified type
- Any unmapped type → compiler diagnostic `QYL-EMIT-001: unmapped type`, fails compile.

Headers: UTF-8 BOM, `// <auto-generated/>` + `// Copyright (c) 2025-2026 ancplua`.

Verify: `npm install` in `core/specs/` resolves the local dep. Smoke: one model with `@csharpNamespace("Qyl.Test")` compiles and emits.

---

## Commit 3 — DuckDB emitter package (`@qyl/typespec-emit-duckdb`)

Scaffold under `core/specs/emitters/duckdb/`.

Decorators:
- `@duckdbTable(name: string)` on models
- `@duckdbColumn(type?: string)` on properties (override)
- `@duckdbPrimaryKey` on properties
- `@duckdbIndex(name: string)` on properties

Type map: numeric mappings from Commit 2 plus `string→VARCHAR`, `utcDateTime→TIMESTAMP`, `boolean→BOOLEAN`, etc.

Output: single file `DuckDbSchema.g.cs`:
- `public const int Version = <sha256(ddl)[:8] as int>;`
- `public const string <TableName>Ddl = """CREATE TABLE ...""";` per table
- `public static string GetSchemaDdl() => ...` concatenates

Migration policy — Option (A): before emit, read existing output file from target path via Node `fs.readFileSync`. Compute column-level diff. If diff non-empty, emit `Migrations/<previousVersion>-to-<currentVersion>.sql`. If target path doesn't exist, skip migration emit (first-run).

Verify: `npm install` resolves. Smoke: one `@duckdbTable` model emits DDL.

---

## Commit 4 — TypeScript types emitter package (`@qyl/typespec-emit-ts-types`)

Scaffold under `core/specs/emitters/ts-types/`.

Decorators: `@tsBrand` on scalars that represent opaque string IDs. Exact list — apply to **exactly** these TypeSpec scalars and no others:

- `TraceId`, `SpanId`, `SessionId`, `ProjectId`, `UserId`, `ApiKey`, `TeamId`, `FixRunId`, `TriageId`, `IssueId`
- Any scalar in `core/specs/common/types.tsp` whose `extends string` is accompanied by an `x-csharp-struct` extension → mark `@tsBrand` on the corresponding TypeSpec source scalar during Commit 5.

If a new scalar appears after Commit 5 that clearly represents an opaque ID (matches the shape `<Something>Id` or has `@pattern` / `@minLength` constraints indicating an ID), extend the list in the same commit that introduces it. Do not mint brands for numeric or enum-backed types.

Type map:
- `int32/int64 → number` · `float32/float64 → number` · `boolean → boolean`
- `string (no brand) → string` · `string (with @tsBrand) → type Foo = string & { __brand: "Foo" }`
- `utcDateTime → string` (ISO-8601) · `Array<T> → T[]` · `Record<T> → Record<string, T>`
- Enum → `const FooValues = { ... } as const; type Foo = typeof FooValues[keyof typeof FooValues]`
- Model → `interface Foo { ... }`

Output: single file `api.ts`.

Verify: `npm install` resolves. Smoke: compile, inspect output.

---

## Commit 5 — Decorator migration across `core/specs/**/*.tsp`

For every model currently routed by `NamespaceRoutingTable.cs`: add `@csharpNamespace("…")` matching current routing exactly. Source of truth: `eng/build/NamespaceRoutingTable.cs:22-77`.

For every DuckDB-mapped type carrying `x-duckdb-table` / `x-duckdb-column` / `x-duckdb-primary-key` / `x-duckdb-index` extensions: replace with the new decorators from Commit 3.

For every type that should emit as a branded TypeScript scalar: add `@tsBrand`.

Zero logical changes. Pure decoration.

Verify: `npm run compile` still produces the existing `openapi.yaml` (old emitter still active in tspconfig at this point).

---

## Commit 6 — `tspconfig.yaml` cutover

Replace the entire `emit:` + `options:` block:

```yaml
emit:
  - "@qyl/typespec-emit-csharp"
  - "@qyl/typespec-emit-duckdb"
  - "@qyl/typespec-emit-ts-types"
  - "@typespec/http-client-csharp"
  - "@typespec/http-client-js"

options:
  "@qyl/typespec-emit-csharp":
    emitter-output-dir: "{project-root}/packages/Qyl.Contracts/Generated"
  "@qyl/typespec-emit-duckdb":
    emitter-output-dir: "{project-root}/services/qyl.collector/Storage"
  "@qyl/typespec-emit-ts-types":
    emitter-output-dir: "{project-root}/packages/qyl-client/src/generated"
  "@typespec/http-client-csharp":
    emitter-output-dir: "{project-root}/packages/Qyl.Client/Generated"
    package-name: "Qyl.Client"
    namespace: "Qyl.Client"
  "@typespec/http-client-js":
    emitter-output-dir: "{project-root}/packages/qyl-client/src/generated"
    package-name: "@qyl/client"
```

`core/specs/package.json`:
- Remove `@typespec/openapi3`, `openapi-typescript` from devDependencies.
- Add the three local emitters as `"file:./emitters/<name>"` deps.
- Add `@typespec/http-client-csharp` + `@typespec/http-client-js` as devDependencies.

`npm install && npm run compile` emits all five target sets into their final destinations.

Verify: all five output directories populated; `dotnet build qyl.slnx` exits 0; `npm run build` in `services/qyl.dashboard` exits 0.

---

## Commit 7 — Package scaffolds for public SDKs

`packages/Qyl.Client/Qyl.Client.csproj`:
- `TargetFrameworks=net10.0;netstandard2.0`
- `PackageId=Qyl.Client`
- `Description`, `Authors`, `RepositoryUrl` metadata
- `GeneratePackageOnBuild=false`
- `Compile` glob over `Generated/**/*.cs`

`packages/qyl-client/package.json`:
- `name: "@qyl/client"`, `type: "module"`
- `exports.".": { "types": "./dist/index.d.ts", "default": "./dist/index.js" }`
- `files: ["dist/**", "src/**"]`
- TSConfig builds both `src/generated/` (from `@qyl/typespec-emit-ts-types`) and `src/client/` (from `@typespec/http-client-js`) into `dist/`. Entrypoint `src/index.ts` re-exports from both: `export * from "./generated/api"; export * from "./client";`

`packages/Qyl.Contracts/Qyl.Contracts.csproj`: update metadata — `PackageId=Qyl.Contracts`, public license/repo metadata, `GeneratePackageOnBuild=false`.

`packages/Qyl.OpenTelemetry.Extensions/` — new hand-written convenience package. Exact API (no variations):

```csharp
namespace Qyl.OpenTelemetry.Extensions;

public sealed class QylOtelOptions
{
    public required Uri Endpoint { get; init; }
    public required string ServiceName { get; init; }
    public string? ApiKey { get; init; }
    public double SampleRate { get; init; } = 1.0;
}

public static class QylOpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddQylOpenTelemetry(
        this IServiceCollection services,
        Action<QylOtelOptions> configure);
}
```

Implementation: call `services.AddOpenTelemetry()`, set `OTEL_EXPORTER_OTLP_ENDPOINT` via options.Endpoint, add W3C trace-context + baggage propagators, register a `TraceIdRatioBasedSampler(SampleRate)`, configure resource builder with `service.name = ServiceName`. If `ApiKey` is non-null, set `OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <key>`. No other behavior.

Add all four `packages/*` projects to `qyl.slnx`.

Verify: `dotnet build qyl.slnx` exits 0; `dotnet pack packages/Qyl.Client --no-build -o /tmp/nupkg` produces a valid `.nupkg`; `npm run build --workspace packages/qyl-client` emits `dist/`.

---

## Commit 8 — Release automation

`.github/workflows/release.yml`:

```yaml
name: Release
on:
  push:
    tags: ['v*']

jobs:
  pack-nuget:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { submodules: true }
      - uses: actions/setup-dotnet@v4
      - run: dotnet pack packages/Qyl.Client                    -c Release -o nupkg
      - run: dotnet pack packages/Qyl.Contracts                 -c Release -o nupkg
      - run: dotnet pack packages/Qyl.OpenTelemetry.Extensions  -c Release -o nupkg
      - run: dotnet nuget push "nupkg/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
  pack-npm:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { submodules: true }
      - uses: actions/setup-node@v4
      - run: npm ci --workspace packages/qyl-client
      - run: npm run build --workspace packages/qyl-client
      - run: npm publish --workspace packages/qyl-client --access public
        env:
          NODE_AUTH_TOKEN: ${{ secrets.NPM_TOKEN }}
  release-notes:
    needs: [pack-nuget, pack-npm]
    runs-on: ubuntu-latest
    steps:
      - run: gh release create ${{ github.ref_name }} --generate-notes
```

Repository secrets required (manual, one-time): `NUGET_API_KEY`, `NPM_TOKEN`.

Add `docs/releasing.md`: tag format `vMAJOR.MINOR.PATCH[-prerelease]`, semver contract (MAJOR bump iff `.tsp` breaking change), rollback (`nuget delete` / `npm deprecate`).

Verify: workflow dry-run via `act` or `gh workflow run` on a throwaway branch.

---

## Commit 9 — Deletes + NUKE consolidation

Delete without replacement:
- `eng/build/SchemaGenerator.cs`
- `eng/build/SchemaMigrationGenerator.cs`
- `eng/build/NamespaceRoutingTable.cs`
- `eng/build/TypeMappingTable.cs`
- `eng/build/ContractGenerator.cs`
- `eng/build/BuildApiDiff.cs` (OpenAPI intermediate is gone). Before deletion, file the follow-up tracking issue — do not hand this back to the user:
  ```
  gh issue create \
    --title "Port BuildApiDiff to TypeSpec program diff" \
    --body "BuildApiDiff.cs was deleted in the TypeSpec-native-emitters cutover because its input (openapi.yaml) no longer exists. Re-implement breaking-change detection against \`weaver registry diff\` or \`tsp resolve\` output between git revisions. Priority: low; the TypeSpec compiler emits breaking-change diagnostics on @added/@removed API surface already." \
    --label "chore,ci"
  ```
- `core/openapi/` (entire directory)

`eng/build/BuildPipeline.cs`:
- Merge `GenerateContracts` and `GenerateSemconv` into a single `Generate` target.
- Body:
  ```csharp
  ProcessTasks.StartProcess("npm", "run compile",
      workingDirectory: CoreSpecsDirectory).AssertZeroExitCode();
  ProcessTasks.StartProcess("bash", "eng/semconv/run-weaver.sh").AssertZeroExitCode();
  ```
- Rewire every `DependsOn(GenerateSemconv)` / `DependsOn(GenerateContracts)` to `DependsOn(Generate)`.

`eng/build/BuildPaths.cs`: delete OpenAPI / NamespaceRoutingTable / TypeMappingTable constants.

`eng/build/build.csproj`: remove `<Compile>` references to deleted files. Drop `YamlDotNet` if no other consumer.

Verify:
```
grep -rE 'SchemaGenerator|NamespaceRoutingTable|TypeMappingTable|ContractGenerator|SchemaMigrationGenerator|BuildApiDiff|openapi\.yaml' eng/ services/ internal/ packages/ core/
# expected: 0 matches
find eng/build -maxdepth 1 -name '*Generator.cs' -o -name '*Table.cs'
# expected: 0 matches
```

---

## Merge gate

- `dotnet build qyl.slnx --tl:off` → 0 errors
- `dotnet test tests/qyl.collector.tests` → all green
- `cd core/specs && npm run compile` → 0 diagnostics
- `cd services/qyl.dashboard && npm run build` → clean
- `dotnet pack packages/Qyl.Client -c Release` → valid `.nupkg`
- `dotnet pack packages/Qyl.Contracts -c Release` → valid `.nupkg`
- `dotnet pack packages/Qyl.OpenTelemetry.Extensions -c Release` → valid `.nupkg`
- `npm run build --workspace packages/qyl-client` → `dist/` populated
- `git diff main -- packages/Qyl.Contracts/Generated/ services/qyl.collector/Storage/DuckDbSchema.g.cs packages/qyl-client/src/generated/` → only header, formatting, or order-within-file differences. Any semantic shape drift = emitter bug; fix the emitter.
- Every file in `packages/*` has a stable public namespace. No `internal/*` types leak upward.
- All CI checks on the PR green.

---

## Anti-goals

- No hand-rolled C# codegen in `eng/build/`. All codegen lives in TypeSpec emitters or Weaver templates.
- No OpenAPI as intermediate. TypeSpec → target-language directly via native emitter.
- No re-introduction of `NamespaceRoutingTable.cs`, `TypeMappingTable.cs`, or `ContractGenerator.cs`.
- No synchronized parallel registries — `@csharpNamespace` on the model is single source.
- No manual `qyl-extensions.json`-style config. Decorators on models.
- No hand-written client SDKs. All language clients via `@typespec/http-client-*`.
- No publishing without a version tag. CI pack-and-push triggers exclusively on `tags/v*`.
- No per-finding rollback PRs after merge. Post-merge regressions → forward-fix commit, not revert.

---

**PR title:** `refactor: TypeSpec-native emitters + public SDK packages + repo tier layout`

Execute.
