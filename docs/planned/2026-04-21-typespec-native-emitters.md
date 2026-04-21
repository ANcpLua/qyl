# Execution Mandate — `refactor/typespec-native-emitters-and-sdks`

**Repo:** `/Users/ancplua/qyl` (this repo). All paths below are qyl-internal.
**Baseline:** `main` at or after `ad71ecdb` (PR #143 merged). No known blockers.
**Execution:** atomic commits 1→9, no checkpoints, no interactive approval, no partial merges. Per `~/.claude/CLAUDE.md` "Whole-System Thinking" and "Decision Discipline": finish in one session.

## Preconditions (verify before Commit 1)

1. **Submodule init** (if fresh worktree): `git submodule update --init .tools/semconv-upstream`
2. **npm scope `@qyl` owned by the release principal** on npmjs.com. Verify: `npm access list packages @qyl 2>&1`. If the scope is unclaimed, run `npm org create qyl` or publish the first `@qyl/client` under a personal scope claim before Commit 9. This is the only step that *might* require user action — if the scope is not ownable, rename the npm package to `qyl-client` (unscoped) across Commits 6/8/9 and proceed.
3. **GitHub Actions secrets** present on the repo: `NUGET_API_KEY`, `NPM_TOKEN`. Verify: `gh secret list | grep -E 'NUGET_API_KEY|NPM_TOKEN'`. If missing, the Commit 9 release workflow will still land but its first tag-triggered run fails until secrets are provisioned — not a blocker for the branch merging.
4. **npm peer-dep policy**: Commit 6 writes `core/specs/.npmrc` with `legacy-peer-deps=true`. This is mandatory because `@typespec/http-client-js@0.14.1` peer-demands `@typespec/rest ^0.80.0` while `@typespec/http-client-csharp@1.0.0-alpha.*` peer-demands `@typespec/rest >=0.81.0 <0.82.0`. The two emitters therefore cannot co-exist under strict peer resolution until Microsoft realigns one of the ranges. Do not pin rest down to 0.80.x to avoid the flag — that breaks the C# emitter entirely.

## Pinned tool versions

Add to `core/specs/package.json` devDependencies (Commit 6 rewrites this block — use these exact versions, do not resolve to `latest`). These are the resolved latest-stable as of 2026-04-21. Breaking-change audit against the previous 1.7.0 pin set (`@typespec/compiler` 1.8.0 → 1.11.0 release notes): `$onEmit(context: EmitContext<TOptions>)`, `navigateProgram`, `createTypeSpecLibrary` surface unchanged; `createStateSymbol` is no longer a top-level compiler export but `$lib.createStateSymbol(name)` and the `state:` field on `createTypeSpecLibrary` both remain (see Commit 2 notes). 1.11.0 is explicitly a no-breaking-changes release.

```json
"@typespec/compiler":           "1.11.0",
"@typespec/http":               "1.11.0",
"@typespec/rest":               "0.81.0",
"@typespec/openapi3":           "1.11.0",
"@typespec/json-schema":        "1.11.0",
"@typespec/http-client-csharp": "1.0.0-alpha.20260420.8",
"@typespec/http-client-js":     "0.14.1",
"@typespec/http-server-csharp": "0.58.0-alpha.27"
```

Transitive constraint (do not pin, but expect in the lockfile): `@azure-tools/typespec-client-generator-core ~0.67.1` is a peer of `http-client-csharp`. It pulls in the Azure SDK's client-generator tooling; that's unavoidable and the reason the C# client emitter exists at all.

If any version no longer resolves from npm during Commit 6, bump forward to the nearest stable and record the bump in the commit body. Do not pin `latest`. If a future rest release (0.82.x) removes the peer conflict, drop the `.npmrc` `legacy-peer-deps=true` in a follow-up — not in this branch.

---

## Invariants

- `dotnet build qyl.slnx --tl:off` exits 0 after every commit.
- `cd core/specs && npm run compile` exits 0 after every commit from Commit 2 onward.
- `git blame --follow` traces every moved file back through Commit 1.
- Every public type exposed via `packages/*` has a stable `Qyl.*` or `@qyl/*` namespace. `services/*` and `internal/*` never leak types outward.
- From Commit 14 onward, every `qyl.*` telemetry key literal in `src/`/`services/` has a matching `@qylAttr` site in `core/specs/**/*.tsp`. The semconv-lint library (Commit 12) catches drift; the annotation step (Commit 14) establishes the baseline.
- Commit 15 is the ONLY commit allowed to reduce the `docs/planned/` tree — every other commit that touches planning docs must only add or update, never delete.

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
                peerDependencies: @typespec/compiler ^1.11.0
lib/main.tsp   decorators: @csharpNamespace(ns: string), @csharpRecord, @csharpEnum
src/
  index.ts     $onEmit(context: EmitContext<CsharpEmitterOptions>)
  emitter.ts   navigateProgram walker, type-map switch
  $lib.ts      createTypeSpecLibrary({ state: { csharpNamespace, csharpRecord, csharpEnum } })
```

`$lib.ts` — use the modern 1.10+ library definition (state declared in the library def, accessed via `$lib.stateKeys.<name>` — confirmed against `node_modules/@typespec/compiler/dist/src/core/library.d.ts` and `types.d.ts:1788-1930`):

```ts
import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";

export const $lib = createTypeSpecLibrary({
  name: "@qyl/typespec-emit-csharp",
  diagnostics: {
    "unmapped-type": {
      severity: "error",
      messages: { default: paramMessage`QYL-EMIT-001: unmapped type ${"name"}` },
    },
  },
  state: {
    csharpNamespace: { description: "C# namespace override on a model/namespace" },
    csharpRecord:    { description: "Emit the target model as a C# record" },
    csharpEnum:      { description: "Emit the target union/enum as a C# enum" },
  },
} as const);

export const { reportDiagnostic, createDiagnostic, stateKeys } = $lib;
```

Decorator implementations then use `program.stateMap($lib.stateKeys.csharpNamespace).set(target, ns)` — do **not** call a standalone `createStateSymbol` (no longer a top-level export from `@typespec/compiler` as of 1.10+).

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

Replace the entire `emit:` + `options:` block. Note: `@typespec/http-client-csharp`'s `CSharpEmitterOptions` (verified in `node_modules/@typespec/http-client-csharp/dist/emitter/src/options.d.ts`) has **no `namespace` option** — namespace derives from the TypeSpec `@service` namespace plus optional `@clientName` decorators from `@azure-tools/typespec-client-generator-core`. `@typespec/http-client-js`'s `JsClientEmitterOptions` only exposes `package-name`. Do not invent options; the `.d.ts` is the authority.

```yaml
emit:
  - "@qyl/typespec-emit-csharp"
  - "@qyl/typespec-emit-duckdb"
  - "@qyl/typespec-emit-ts-types"
  - "@typespec/http-client-csharp"
  - "@typespec/http-client-js"
  - "@typespec/json-schema"

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
    generate-protocol-methods: true
    generate-convenience-methods: true
    disable-xml-docs: false
    new-project: false
  "@typespec/http-client-js":
    emitter-output-dir: "{project-root}/packages/qyl-client/src/generated"
    package-name: "@qyl/client"
  "@typespec/json-schema":
    emitter-output-dir: "{project-root}/packages/qyl-client/schemas"
    file-type: json
    bundleId: qyl-api
```

(`@typespec/http-server-csharp` is intentionally absent here — Commit 7 adds it atomically with the controller swap to avoid route ambiguity between hand-written and emitted controllers.)

`core/specs/.npmrc` (new file, Commit 6 creates it):

```
legacy-peer-deps=true
```

Justification: `@typespec/http-client-js@0.14.1` peer range is `@typespec/rest ^0.80.0`; `@typespec/http-client-csharp@1.0.0-alpha.*` peer range is `@typespec/rest >=0.81.0 <0.82.0`. Strict peer resolution refuses the union. Upstream (microsoft/typespec) has not yet realigned — verified via `npm info` on 2026-04-21. Drop this `.npmrc` in a follow-up branch once both emitters converge on the same rest major.

`core/specs/package.json`:
- Remove `@typespec/openapi3`, `openapi-typescript`, `@typespec/events`, `@typespec/sse`, `@typespec/streams`, `@typespec/versioning`, `@typespec/openapi` from devDependencies (the retired OpenAPI intermediate is gone; any surviving `sse`/`streams` usage moves into model-level decorators on the relevant operations — no separate compile-time imports needed).
- Add the three local emitters as `"file:./emitters/<name>"` deps.
- Add `@typespec/http-client-csharp` + `@typespec/http-client-js` + `@typespec/json-schema` as devDependencies at the versions pinned in "Pinned tool versions" above. (`@typespec/http-server-csharp` is added by Commit 7, not here.)

`npm install && npm run compile` emits all six target sets into their final destinations.

Verify: all six output directories populated (`packages/Qyl.Contracts/Generated/`, `services/qyl.collector/Storage/`, `packages/qyl-client/src/generated/` (TS types + JS client share the directory), `packages/Qyl.Client/Generated/`, `packages/qyl-client/schemas/`); `dotnet build qyl.slnx` exits 0; `npm run build` in `services/qyl.dashboard` exits 0.

---

## Commit 7 — ASP.NET controller cutover via `@typespec/http-server-csharp`

Atomic replacement of hand-written controllers in `services/qyl.collector/` with the `@typespec/http-server-csharp` alpha emitter's output. This commit accepts the architectural breakage: existing `Controllers/*.cs` are deleted, emitted controllers take over routing, and new hand-written implementations satisfy the emitted operation interfaces.

**Scope that stays hand-written**:
- OTLP gRPC receiver (`Services/Grpc/` under qyl.collector) — NOT TypeSpec-modeled; gRPC surface is owned by OTel upstream, keep hand-written.
- DuckDB storage layer, auth middleware, ring buffers, MCP deep-link handlers — hand-written.
- Any controller whose TypeSpec model is not in `core/specs/**/*.tsp` stays hand-written (verify by enumerating `@route` / `@service` coverage in `core/specs/api/routes.tsp` before the swap).

**Scope that is emitted**:
- Every controller that today corresponds to a TypeSpec-modeled route in `core/specs/api/routes.tsp` — namely the REST API at `:5100` (traces, spans, services, agent runs, capability catalog).

### Steps (all in one commit)

1. `cd core/specs && npm install --save-dev @typespec/http-server-csharp@0.58.0-alpha.27`
2. Append to `tspconfig.yaml` `emit:` list: `- "@typespec/http-server-csharp"`.
3. Append to `tspconfig.yaml` `options:` block:
   ```yaml
     "@typespec/http-server-csharp":
       emitter-output-dir: "{project-root}/services/qyl.collector/Generated"
       output-type: all
       emit-mocks: none
       skip-format: false
       overwrite: true
       project-name: "Qyl.Collector.Generated"
       collection-type: array
       use-swaggerui: false
   ```
   Option meanings verified against `node_modules/@typespec/http-server-csharp/dist/src/lib/lib.d.ts`:
   - `output-type: all` — emit controllers + models + project scaffolding (vs `models` only)
   - `emit-mocks: none` — do NOT emit mock business-logic stubs; we write real ones
   - `skip-format: false` — run `dotnet format` on output (default)
   - `use-swaggerui: false` — we already expose Scalar/Swagger UI via the dashboard; avoid the emitter's opinionated Swagger-UI middleware
   - `collection-type: array` — emitted collection params as arrays (matches the existing controller shape)
4. Run `nuke Generate` (runs `npm run compile`). `services/qyl.collector/Generated/` is populated with controllers, models, and a project file. The emitted project file is a scaffold — do **not** add it to `qyl.slnx`; `.csproj` inclusion happens through step 5.
5. Update `services/qyl.collector/qyl.collector.csproj`:
   - Add `<Compile Include="Generated/**/*.cs" />` (glob-pick-up of emitted controllers).
   - Add `<Compile Remove="Generated/Program.cs" />` if the emitter drops a program entry — qyl.collector already owns its `Program.cs`.
   - Verify no `<PackageReference Include="Swashbuckle.*" />` remain; the emitter doesn't need them.
6. Delete the hand-written `Controllers/` directory contents that the emitter now covers. Leave any controller outside the TypeSpec surface (OTLP HTTP endpoints at `/v1/traces`, `/v1/metrics`, `/v1/logs` are OTel-protocol-owned — keep hand-written if they aren't in TypeSpec).
7. For every emitted `interface I<Name>Controller`, create a partial class implementation under `services/qyl.collector/Controllers/Impl/` that wires the real business logic (DuckDB reads via the existing storage layer, auth via existing middleware, etc.). Name pattern: `TracesController.Impl.cs`, etc.
8. Register the emitted controllers in DI — typically `builder.Services.AddControllers()` already discovers them via the compile-glob; confirm no hand-written `AddSingleton<IXxxController, XxxController>()` remains if the emitter produces `abstract` base classes.

### Verify

- `dotnet build qyl.slnx --tl:off` exits 0.
- `dotnet test tests/qyl.collector.tests` — all green. Integration tests that hit the REST API at `:5100` must still pass without modification (route shape is byte-identical; TypeSpec → controller is deterministic).
- `curl -s localhost:5100/api/traces?limit=1 | jq` returns the same schema as before the cutover. Any shape drift = emitter bug; fix the emitter or the TypeSpec source, not the hand-written impl.
- Compare `git diff HEAD~1 -- services/qyl.collector/Controllers/`: expected to be "most files deleted, a few `.Impl.cs` added under `Controllers/Impl/`".
- `grep -rn 'class .*Controller' services/qyl.collector/Controllers/ services/qyl.collector/Generated/` — emitted controllers are under `Generated/`, impls under `Controllers/Impl/`, no duplicates.

### Rollback

If the alpha emitter produces unusable output (ambiguous routes, wrong auth attributes, broken async signatures), the rollback is this commit alone — revert Commit 7 and the tree returns to Commit 6's green state with hand-written controllers intact. Do NOT try to patch the emitted output; fix the TypeSpec source or file an issue against `microsoft/typespec` and re-run Commit 7 once upstream ships the fix.

---

## Commit 8 — Package scaffolds for public SDKs

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

## Commit 9 — Release automation

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

## Commit 10 — Deletes + NUKE consolidation

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

## Commit 11 — Weaver registry snapshot for qyl-semconv-lint

A new TypeSpec library `@qyl/typespec-qyl-semconv-lint` (Commits 12-14) consumes a flat JSON of upstream OTel attribute names to catch qyl-owned telemetry names that collide with semconv, drift in type, or violate naming rules. Weaver is the only semconv parser — do not introduce a second one. This commit extends Weaver's template set to emit one additional artifact.

### Template

Add `eng/semconv/templates/registry/qyl/otel-attribute-registry.json.j2` (MiniJinja):

```jinja
[
{%- for attr in ctx | selectattr("type", "eq", "attribute_group") | list -%}
  {%- for m in attr.attributes -%}
  {
    "name": "{{ m.name }}",
    "type": "{{ m.type | string }}",
    "stability": "{{ m.stability | default('experimental') }}",
    "deprecated": {{ (m.deprecated is not none) | tojson }},
    "group": "{{ attr.id }}"
  }{%- if not loop.last -%},{%- endif %}
  {%- endfor -%}
  {%- if not loop.last -%},{%- endif %}
{%- endfor -%}
]
```

Field names MUST match Weaver's semconv model exactly — consult `.tools/semconv-upstream/model/**/*.yaml` and Weaver's JSON schema before finalizing. If upstream uses `stability_level` vs `stability` or `members` vs `attributes`, conform — do not invent.

### Runner wiring

Update `eng/semconv/run-weaver.sh`. After the existing `install -m 0644` lines:

```bash
REGISTRY_DEST="${REPO_ROOT}/core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json"
mkdir -p "$(dirname "${REGISTRY_DEST}")"
install -m 0644 "${STAGING_DIR}/otel-attribute-registry.json" "${REGISTRY_DEST}"
```

Extend the final `echo` summary to include the new file. Add it to `.gitattributes` as `linguist-generated=true` so it doesn't inflate review diffs.

### Verify

```bash
./eng/semconv/run-weaver.sh
jq 'length' core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json
# expect: > 500 entries (semconv 1.40 attribute registry surface)
jq '.[] | select(.name == "gen_ai.system")' core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json
# expect: single hit, type "string"
```

`nuke Generate` exits 0.

---

## Commit 12 — `@qyl/typespec-qyl-semconv-lint` library scaffold

Mirror the csharp emitter's shape from Commit 2.

`core/specs/emitters/qyl-semconv-lint/package.json`:

```json
{
  "name": "@qyl/typespec-qyl-semconv-lint",
  "version": "0.1.0",
  "type": "module",
  "main": "dist/index.js",
  "tspMain": "lib/main.tsp",
  "exports": {
    ".": {
      "typespec": "./lib/main.tsp",
      "default": "./dist/index.js"
    }
  },
  "scripts": { "build": "tsc -p .", "test": "vitest run" },
  "peerDependencies": { "@typespec/compiler": "^1.11.0" },
  "devDependencies": {
    "@typespec/compiler": "1.11.0",
    "typescript": "~5.6.0",
    "vitest": "~2.1.0"
  }
}
```

`core/specs/emitters/qyl-semconv-lint/lib/main.tsp`:

```tsp
import "../dist/index.js";

namespace Qyl.Semconv;

model QylAttrOptions {
  cardinality?: "low" | "medium" | "high";
  stability?: "experimental" | "stable" | "deprecated";
  required?: boolean;
}

extern dec qylAttr(
  target: ModelProperty | Operation,
  key: valueof string,
  type: valueof "string" | "int" | "long" | "double" | "boolean" | "string[]",
  options?: valueof QylAttrOptions
);
```

`core/specs/emitters/qyl-semconv-lint/src/index.ts` — library definition with 6 diagnostic codes (`QYL-LINT-001..006`) and a single state key `qylAttr`:

```ts
import { createTypeSpecLibrary, paramMessage, Program, Type } from "@typespec/compiler";
import { runAllRules } from "./rules.js";

export const $lib = createTypeSpecLibrary({
  name: "@qyl/typespec-qyl-semconv-lint",
  diagnostics: {
    "upstream-collision": { severity: "error",
      messages: { default: paramMessage`QYL-LINT-001: attribute '${"key"}' collides with upstream OTel namespace '${"prefix"}' — qyl attributes must live under 'qyl.'` } },
    "bad-namespace":      { severity: "error",
      messages: { default: paramMessage`QYL-LINT-002: attribute '${"key"}' must start with 'qyl.' — project-owned namespaces are forbidden outside that prefix` } },
    "bad-naming":         { severity: "error",
      messages: { default: paramMessage`QYL-LINT-003: attribute '${"key"}' violates OTel naming: lowercase letters, digits, underscores, dot-separated segments, no leading/trailing/doubled dots` } },
    "type-drift":         { severity: "error",
      messages: { default: paramMessage`QYL-LINT-004: attribute '${"key"}' declared as '${"typeA"}' here but as '${"typeB"}' at ${"otherSite"}` } },
    "stability-regression": { severity: "error",
      messages: { default: paramMessage`QYL-LINT-005: attribute '${"key"}' stability regressed from '${"prior"}' to '${"current"}' — 'stable' is a one-way ratchet` } },
    "cardinality-drift":  { severity: "warning",
      messages: { default: paramMessage`QYL-LINT-006: attribute '${"key"}' cardinality differs across sites: '${"a"}' vs '${"b"}'` } },
  },
  state: { qylAttr: { description: "Collected qyl attribute declarations" } },
} as const);

export const { reportDiagnostic, stateKeys } = $lib;

export interface QylAttrRecord {
  key: string;
  type: "string" | "int" | "long" | "double" | "boolean" | "string[]";
  cardinality?: "low" | "medium" | "high";
  stability?: "experimental" | "stable" | "deprecated";
  required?: boolean;
  target: Type;
}

export function $qylAttr(
  context: { program: Program },
  target: Type,
  key: string,
  type: QylAttrRecord["type"],
  options?: Partial<Omit<QylAttrRecord, "key" | "type" | "target">>,
): void {
  const map = context.program.stateMap(stateKeys.qylAttr);
  const bucket = (map.get(target) as QylAttrRecord[] | undefined) ?? [];
  bucket.push({ key, type, ...(options ?? {}), target });
  map.set(target, bucket);
}

export function $onValidate(program: Program): void {
  runAllRules(program);
}
```

`src/registry.ts` — loads `../data/otel-attribute-registry.json` with `with { type: "json" }` and builds a `Map<string, OtelAttr>`. Declares `RESERVED_PREFIXES` (semconv 1.40 top-level groups: `gen_ai.`, `http.`, `db.`, `rpc.`, `network.`, `server.`, `client.`, `url.`, `user_agent.`, `code.`, `exception.`, `event.`, `log.`, `messaging.`, `faas.`, `cloud.`, `aws.`, `azure.`, `gcp.`, `k8s.`, `container.`, `host.`, `os.`, `process.`, `thread.`, `service.`, `deployment.`, `telemetry.`, `otel.`, `session.`, `enduser.`, `feature_flag.`, `error.`, `file.`, `peer.`, `source.`, `destination.`, `device.`, `browser.`, `disk.`, `hw.`, `jvm.`, `nodejs.`, `dotnet.`, `aspnetcore.`, `signalr.`, `v8js.`, `webengine.`, `android.`, `ios.`). When Weaver advances past 1.40, add new top-level groups here — don't let the list drift silently.

Add `tsconfig.json` targeting `ES2022` / `Node16` / `rootDir: src` / `outDir: dist` / `strict: true` / `resolveJsonModule: true`.

### Verify

```bash
cd core/specs/emitters/qyl-semconv-lint && npm install && npm run build
# expect: clean tsc, dist/index.js + dist/rules.js + dist/registry.js emitted
```

### Design choice — `$onValidate` + decorator, NOT `extern fn`

`extern fn` is for pure transforms that return TypeSpec values/types; it re-runs on every alias site (documented footgun in the functions doc). Side-effecting validation belongs in `$onValidate` which runs exactly once per compile. Do not "refactor" this to `extern fn` in a future session — the rejection is deliberate and the same as the Pattern-4 rejection in root `CLAUDE.md`'s 2026-04 collapse note.

---

## Commit 13 — Rule implementations + vitest fixtures

`core/specs/emitters/qyl-semconv-lint/src/rules.ts` — one function per rule, all driven from the stateMap populated in Commit 12:

- **`checkNamespace`** — `QYL-LINT-001` (starts with any `RESERVED_PREFIXES` entry) and `QYL-LINT-002` (does not start with `qyl.`).
- **`checkNaming`** — `QYL-LINT-003` against the regex `^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$`.
- **`checkTypeConsistency`** — `QYL-LINT-004` by grouping records by `key`, reporting when the `Set<type>` has size > 1; also cross-check against upstream registry as defense-in-depth even though `checkNamespace` should have caught upstream shadowing.
- **`checkStabilityConsistency`** — `QYL-LINT-005` as a one-way ratchet: once any site declares `stable`, no later site may regress to `experimental` or `deprecated`. Rank map: `experimental: 0, stable: 1, deprecated: 2`; `deprecated` is allowed after `stable` (that's how deprecation works), `experimental` is not.
- **`checkCardinalityConsistency`** — `QYL-LINT-006` by grouping on `key`, reporting when the set of `cardinality` values across sites has size > 1.

`runAllRules(program)` calls them in order. `collectAll(program)` flattens the stateMap buckets into a single `QylAttrRecord[]`. Map upstream OTel types (`int`, `double`, `boolean`, `string[]`, `string`) to the library's canonical types via a small switch; `int` upstream → `long` canonical because OTel attribute ints are 64-bit.

### Tests

`test/rules.test.ts` via `@typespec/compiler/testing`'s `createTester`. One `describe` per rule, each with at least one positive (clean input) and one negative (triggers diagnostic) case. Baseline count: ≥ 10 tests.

```ts
import { describe, it, expect } from "vitest";
import { createTester } from "@typespec/compiler/testing";

const tester = createTester({ libraries: ["@qyl/typespec-qyl-semconv-lint"] });

describe("QYL-LINT-001 upstream-collision", () => {
  it("rejects gen_ai.* as qyl attr", async () => {
    const { diagnostics } = await tester.compile(`
      import "@qyl/typespec-qyl-semconv-lint";
      using Qyl.Semconv;
      model X {
        @qylAttr("gen_ai.foo", "string")
        foo: string;
      }
    `);
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0].code).toBe("@qyl/typespec-qyl-semconv-lint/upstream-collision");
  });
});
```

### Verify

```bash
cd core/specs/emitters/qyl-semconv-lint && npm test
# expect: all green
```

---

## Commit 14 — Wire-up + annotate every existing `qyl.*` telemetry name

### tspconfig + main.tsp

`core/specs/package.json` — add to `devDependencies`:

```json
"@qyl/typespec-qyl-semconv-lint": "file:./emitters/qyl-semconv-lint"
```

`core/specs/main.tsp` — add near the top:

```tsp
import "@qyl/typespec-qyl-semconv-lint";
using Qyl.Semconv;
```

No `emit:` entry needed — `$onValidate` runs on any compile that imports the library.

### Annotate every real qyl.* attribute

Ground-truth grep first — anything hard-coded in runtime code must appear as `@qylAttr` in TypeSpec:

```bash
grep -rE '"qyl\.[a-z]+\.[a-z_.]+"' src/qyl.collector/ src/qyl.instrumentation/ src/qyl.mcp/ src/qyl.loom/ \
  | grep -Ev 'McpServerTool|SourceName|RequiresCapability|Dockerfile|app\.' \
  | sort -u
```

Expected (verify against current sources; do not copy blind):

- `qyl.capability.id`, `qyl.capability.kind` (resource attrs) — source: `services/qyl.collector/Ingestion/OtlpConverter.cs` (post-Commit-1 path)
- `qyl.storage.size`, `qyl.duckdb.dropped_jobs_total`, `qyl.duckdb.dropped_spans_total` (meter names) — sources: `QylTelemetry.cs`, `DuckDbStore.cs`
- `qyl.keycloak.claims` (auth claim key) — source: `TokenAuth.cs`
- `qyl.instance_id` (log enrichment) — source: `QylLogEnricher.cs`

Attach each to the TypeSpec model where it belongs. For names with no existing TypeSpec home (meter names, log enrichment), create a marker registry:

```tsp
// core/specs/telemetry/qyl-attrs.tsp
import "@qyl/typespec-qyl-semconv-lint";
using Qyl.Semconv;

namespace Qyl.Telemetry.Attrs;

// Marker model. Not emitted. Exists only so @qylAttr annotations have a
// ModelProperty target for accurate diagnostic location. Do NOT add a
// @csharpNamespace decorator here — that would route it into Qyl.Contracts.
@doc("qyl-owned telemetry attribute registry — names only, not a runtime shape.")
model QylTelemetryRegistry {
  @qylAttr("qyl.storage.size",                "long",   #{ cardinality: "low",  stability: "experimental" }) storageSize: int64;
  @qylAttr("qyl.duckdb.dropped_jobs_total",   "long",   #{ cardinality: "low",  stability: "experimental" }) droppedJobsTotal: int64;
  @qylAttr("qyl.duckdb.dropped_spans_total",  "long",   #{ cardinality: "low",  stability: "experimental" }) droppedSpansTotal: int64;
  @qylAttr("qyl.keycloak.claims",             "string", #{ cardinality: "high", stability: "experimental" }) keycloakClaims: string;
  @qylAttr("qyl.instance_id",                 "string", #{ cardinality: "high", stability: "experimental" }) instanceId: string;
}
```

For capability attrs that do have a TypeSpec home (Qyl.Capabilities), attach inline:

```tsp
model CapabilityResourceAttributes {
  @qylAttr("qyl.capability.id",   "string", #{ cardinality: "low", stability: "experimental" }) id: string;
  @qylAttr("qyl.capability.kind", "string", #{ cardinality: "low", stability: "experimental" }) kind: "Starting" | "FollowUp";
}
```

### Verify

```bash
cd core/specs && npm install && npm run compile
# expect: 0 diagnostics — every real qyl.* attr is legal

# Inject a deliberate bug — change one annotation to "qyl.storage..size"
npm run compile   # expect: QYL-LINT-003 on the exact line

# Change another to "gen_ai.storage.size"
npm run compile   # expect: QYL-LINT-001 on the exact line

# Revert before finishing the commit.
```

### Diagnostics catalog

| Code           | Severity | Rule                                                                                       |
|----------------|----------|--------------------------------------------------------------------------------------------|
| `QYL-LINT-001` | error    | Attribute starts with an upstream OTel prefix.                                             |
| `QYL-LINT-002` | error    | Attribute does not start with `qyl.`.                                                      |
| `QYL-LINT-003` | error    | Attribute violates OTel naming (`^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$`).                  |
| `QYL-LINT-004` | error    | Same attribute key declared with different primitive types across sites.                   |
| `QYL-LINT-005` | error    | Attribute stability regressed from `stable` back to `experimental` (one-way ratchet).      |
| `QYL-LINT-006` | warning  | Same attribute key declared with different cardinality hints across sites.                 |

---

## Commit 15 — Plan cleanup (self-deletion)

Delete both planning artifacts now that the plan has executed to completion. Planning docs are not long-lived artifacts — the code and tests are the authoritative record of what shipped.

```bash
git rm docs/planned/2026-04-21-typespec-native-emitters.md
git rm docs/planned/2026-04-21-typespec-qyl-semconv-lint.md
git commit -m "chore(planned): delete executed TypeSpec-native-emitters + semconv-lint mandates"
```

Verify:

```bash
ls docs/planned/2026-04-21-typespec*.md 2>&1
# expect: "No such file or directory"
grep -rn '2026-04-21-typespec-native-emitters\|2026-04-21-typespec-qyl-semconv-lint' docs/ .github/ eng/ services/ internal/ packages/ core/ 2>&1 | grep -v '^Binary' || true
# expect: 0 lingering references
```

If any doc or CI file referenced the planning artifacts by path (usually none do — planning docs are one-shot execution inputs), drop those references in the same commit. Do NOT replace with "see commit history" pointers — the PR description and the commits themselves are the record.

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
- `git diff main -- packages/Qyl.Contracts/Generated/ services/qyl.collector/Storage/DuckDbSchema.g.cs packages/qyl-client/src/generated/ services/qyl.collector/Generated/` → only header, formatting, or order-within-file differences. Any semantic shape drift = emitter bug; fix the emitter. (`services/qyl.collector/Generated/` is the http-server-csharp emitter's output from Commit 7.)
- `packages/qyl-client/schemas/` — JSON Schema bundle from `@typespec/json-schema` exists and is non-empty.
- Every file in `packages/*` has a stable public namespace. No `internal/*` types leak upward.
- REST API at `:5100` returns byte-identical response shapes pre- vs post-Commit-7 for every TypeSpec-modeled route (spot-check `/api/traces`, `/api/spans`, `/api/services`, `/api/agent-runs`, `/api/capabilities`). Any shape drift is a Commit-7 regression, not an acceptable evolution.
- `cd core/specs/emitters/qyl-semconv-lint && npm test` — all vitest fixtures green (≥ 10 cases across rules QYL-LINT-001..006).
- `core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json` — present, > 500 entries, `gen_ai.system` resolvable.
- `grep -rE '"qyl\.[a-z]+\.[a-z_.]+"' src/ services/ internal/` vs `grep -rE '@qylAttr\("qyl\.' core/specs/` — the left set is a subset of the right set. Every runtime `qyl.*` key has a matching `@qylAttr` annotation.
- `ls docs/planned/2026-04-21-typespec*.md 2>&1 | grep -c "No such"` → 2 (Commit 15 executed — both planning docs deleted).
- All CI checks on the PR green.

---

## Anti-goals

- No hand-rolled C# codegen in `eng/build/`. All codegen lives in TypeSpec emitters or Weaver templates.
- No OpenAPI as intermediate. TypeSpec → target-language directly via native emitter.
- No re-introduction of `NamespaceRoutingTable.cs`, `TypeMappingTable.cs`, or `ContractGenerator.cs`.
- No synchronized parallel registries — `@csharpNamespace` on the model is single source.
- No manual `qyl-extensions.json`-style config. Decorators on models.
- No hand-written client SDKs. All language clients via `@typespec/http-client-*`.
- No hand-written REST controllers for TypeSpec-modeled routes (Commit 7 delta). OTel-protocol-owned surfaces (OTLP gRPC/HTTP) stay hand-written — that is an intentional carve-out, not a loophole.
- No publishing without a version tag. CI pack-and-push triggers exclusively on `tags/v*`.
- No per-finding rollback PRs after merge. Post-merge regressions → forward-fix commit, not revert.
- No re-pinning back down to `@typespec/compiler@1.7.x` to "avoid bleeding edge" — 1.11 is current stable, 1.7 is already obsolete on npm-dist-tags.

### Semconv-lint anti-goals (Commits 11-14)

- No TypeSpec `extern fn` as a validator. `$onValidate` is the correct tool for side-effecting validation; `extern fn` is for pure transforms. See Commit 12 design-choice note.
- No type synthesis from the semconv registry. The library emits diagnostics only — no `.g.cs`, no `.g.ts`, no synthesized TypeSpec types. The Pattern-4 "derive telemetry from API surface" variant is rejected — same reasoning as the 2026-04 `[AgentTraced]` collapse note in root `CLAUDE.md`.
- No second semconv parser. Weaver is the only tool that reads `.tools/semconv-upstream/model/**/*.yaml`. Commit 11 extends Weaver; it does not replace it.
- No runtime validation in `qyl.instrumentation` derived from this library. Compile-time only.
- No span-name / metric-name / event-name linting in these commits. Scope is attribute names and shapes. File a follow-up if that value materializes — do not scope-creep.
- No `@@suppress("@qyl/typespec-qyl-semconv-lint/...")` escape hatch. Per root `CLAUDE.md` Suppression Policy — fix the attribute or fix the rule.

### Documentation anti-goals (Commit 15)

- No planning-doc hoarding. Executed mandates are deleted — their record lives in commits, tests, and the code itself. Do not preserve as "archive" or move to `docs/history/`.

---

## Bleeding-edge decisions (2026-04-21 refresh)

Record of every new/upgraded TypeSpec feature evaluated for this mandate and the disposition. If a later session wants to challenge a deferral, the reason must still apply.

| Feature | Disposition | Reason |
|---|---|---|
| `@typespec/compiler` 1.8 → 1.11 bump | **INCLUDED** (mandatory — all pins) | 1.11 is no-breaking-changes. `$onEmit`, `navigateProgram`, `createTypeSpecLibrary`, `EmitContext` surface unchanged. `createStateSymbol` moved off top-level exports; use `$lib.stateKeys` via library def (Commit 2 reflects this). |
| `.npmrc` `legacy-peer-deps=true` | **INCLUDED** (Commit 6) | `http-client-js@0.14.1` peer `rest^0.80.0` vs `http-client-csharp@1.0.0-alpha.*` peer `rest>=0.81.0` is a real peer conflict. Waiting for upstream to realign would block the whole mandate indefinitely. |
| `@typespec/json-schema` emit | **INCLUDED** (Commit 6) | Three lines of `tspconfig.yaml`. Outputs JSON Schema bundle into `packages/qyl-client/schemas/` — useful for MCP tool-manifest validation and dashboard form generation. Zero runtime cost. |
| `@typespec/http-server-csharp` 0.58.0-alpha.27 | **INCLUDED** (new Commit 7) | User directive 2026-04-21 — "breaking doesn't matter". Atomic controller-swap in qyl.collector; non-TypeSpec surfaces (OTLP gRPC, DuckDB, auth, MCP deep links) stay hand-written. Alpha quality accepted because rollback is a single-commit revert. |
| `@typespec/protobuf` 0.81.0 preview | **DEFERRED** | qyl's only gRPC surface is OTLP ingress, which is owned by OTel semconv upstream — not ours to model in TypeSpec. No first-party gRPC surface exists to emit. Revisit when/if qyl exposes a custom gRPC API. |
| `@typespec/http-client-java` 0.8.1 | **DEFERRED** | Near-empty option surface (`license`, `dev-options` only). Maven publish infra = new workflow + new secret (`MAVEN_CENTRAL_TOKEN`) + Sonatype account. Cost > demand (no known Java consumer). Re-evaluate when a Java consumer appears. |
| `@typespec/http-client-python` 0.28.3 | **DEFERRED** | Mature option surface, but no known Python consumer. PyPI publish infra exists but is additional maintenance. Revisit when a Python consumer appears. |
| TypeSpec `extern fn` functions (1.10, experimental) | **NOT ADOPTED** | `extern fn` is for TypeSpec-language users declaring type-level transforms on the `.tsp` side. Our three custom emitters (`csharp`, `duckdb`, `ts-types`) are TypeScript-side walkers — functions do not simplify them. |
| TypeSpec `internal` modifiers (1.10, experimental) | **NOT ADOPTED** | Marginal payoff for the custom emitters; `core/specs/**/*.tsp` does not expose cross-package types that need access gates. |
| `FilterVisibility` template (1.11, replaces `@withVisibilityFilter`) | **NOT ADOPTED** | The TypeSpec models we emit do not currently use visibility filters. If a visibility-aware contract appears later, use `FilterVisibility` from day one — do not reach for the deprecated `@withVisibilityFilter`. |
| `EmitContext.perf` PerfReporter (1.9) | **OPTIONAL** — adopt only if Commits 2/3/4 emitters get slow | Wrap `navigateProgram` loops in `context.perf.time("walk", () => ...)` if `nuke Generate` regresses past 10 s. Not required on day one. |
| OpenAPI 3.2 output via `openapi-versions: [3.2.0]` | **NOT ADOPTED** (we removed the OpenAPI intermediate) | Commit 10 deletes `core/openapi/` entirely. External consumers who want OpenAPI can generate it from `@typespec/http-server-csharp`'s Swagger-UI feature (disabled by default in Commit 7 — re-enable per-env if needed). |
| `@qyl/typespec-qyl-semconv-lint` library (Commits 11-14) | **INCLUDED** | User directive 2026-04-21 — absorb into this mandate to avoid hoarding a second planning doc. Compile-time-only diagnostic library for qyl-owned telemetry attribute names. Consumes one new Weaver artifact (`otel-attribute-registry.json`). Zero runtime impact; zero new `.g.*` files. |
| Planning-doc self-deletion (Commit 15) | **INCLUDED** | User directive 2026-04-21 — "goal is not to hoard markdownfiles, instead cleanup the drift in code". Both mandates delete themselves at the end of execution. The code and commits are the record. |

If a future session wants to flip a DEFERRED item to INCLUDED, insert it as a new commit (renumber subsequent commits, update the merge gate) and append a row to this table with the flip reason. If Commit 15 has already executed and this file is gone, that means the plan shipped — open a new mandate rather than resurrecting this one.

---

**PR title:** `refactor: TypeSpec-native emitters + ASP.NET server emitter + public SDKs + qyl-semconv-lint + repo tier layout`

Execute.
