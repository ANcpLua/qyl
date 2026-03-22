# Decision: Loom as Standalone Product

## Status

Accepted.

## Context

Should qyl.loom be merged into the collector, or remain a standalone product?

## Decision

Standalone. `src/qyl.loom/` is its own product with its own project at `~/RiderProjects/qyl.loom/`.

Mechanically true means:

- `src/qyl.loom` is an executable web host, not a library-only code dump.
- Loom HTTP endpoints and hosted services are composed by `qyl.loom`, not by `qyl.collector`.
- `qyl.collector` owns ingestion/storage/query primitives only; it does not own Loom runtime behavior.
- The current `loom -> collector` dependency is temporary and only tolerated while storage/query seams are extracted.

## Rationale

Loom is a C# transpile of Sentry Seer — a complete AI-powered issue investigation system. It has its own domain model (5-stage pipeline, PolicyGate, autofix, code review, regression detection, triage) that is larger than any single collector feature.

Merging it into collector would:

- Bloat the collector with AI/LLM dependencies
- Make the collector harder to test and deploy without LLM infrastructure
- Couple Loom's release cycle to collector's release cycle
- Obscure Loom's domain model inside collector's service layer

Loom references collector, agents, workflows, contracts, and instrumentation via ProjectReference. The dependency flows one way: loom → collector. Collector must never depend on loom.

## Current Mechanical Drift

The ADR is accepted but not enforced:

- `src/qyl.loom/qyl.loom.csproj` is `OutputType=Library`, so Loom is not a runnable host.
- `src/qyl.collector/Program.cs` is the only real runtime host.
- `src/qyl.collector/Hosting/CollectorFeatureExtensions.cs` registers Loom services.
- `src/qyl.collector/Hosting/CollectorEndpointExtensions.cs` maps Loom endpoints.
- `src/qyl.collector/Autofix/*` and `src/qyl.collector/AgentRuns/*` still contain collector-owned Loom runtime code.
- `src/qyl.loom/*` contains duplicate Loom runtime code, but no host wires it up.

That state is worse than either option. It is neither a clean standalone product nor a clean merge.

## Implementation Plan

### 1. Make `qyl.loom` the real host first

Implement:

- `src/qyl.loom/Program.cs`
- `src/qyl.loom/Hosting/LoomFeatureExtensions.cs`
- `src/qyl.loom/Hosting/LoomEndpointExtensions.cs`

Patch sketch:

- Change `src/qyl.loom/qyl.loom.csproj` from library to web executable.
- In the first cut, let `qyl.loom/Program.cs` call the existing collector kernel setup:
  - `AddQylCollectorCore`
  - `AddQylCollectorStorage`
  - `AddQylCollectorAuth`
  - `AddQylCollectorTelemetry`
  - `InitializeQylCollectorAsync`
  - `UseQylCollectorMiddleware`
- Then add Loom-only DI and Loom-only endpoint mapping from `src/qyl.loom`.

This is the right first move because it makes the runtime boundary true immediately, before the storage/query extraction exists.

### 2. Remove collector ownership of Loom composition

Delete Loom composition from:

- `src/qyl.collector/Hosting/CollectorFeatureExtensions.cs`
- `src/qyl.collector/Hosting/CollectorEndpointExtensions.cs`

Delete these registrations from collector:

- `TriagePipelineService`
- `AutofixAgentService`
- `AutofixOrchestrator`
- `PrCreationService`
- `AgentHandoffService`
- `CodeReviewService`
- `LoomInsightService`
- `LoomExplorerService`

Delete these endpoint mappings from collector:

- `MapAutofixEndpoints`
- `MapRegressionEndpoints`
- `MapAgentHandoffEndpoints`
- `MapCodeReviewEndpoints`
- `MapGitHubWebhookEndpoints`
- `MapLoomEndpoints`
- `MapTriageEndpoints`
- `MapAgentRunEndpoints`
- `MapAgentInsightsEndpoints`

### 3. Delete collector-owned Loom runtime files instead of preserving duplicates

Delete from `src/qyl.collector` once `qyl.loom` host is live:

- `Autofix/AutofixAgentService.cs`
- `Autofix/AutofixEndpoints.cs`
- `Autofix/AutofixOrchestrator.cs`
- `Autofix/CodeReviewEndpoints.cs`
- `Autofix/CodeReviewService.cs`
- `Autofix/AgentHandoffEndpoints.cs`
- `Autofix/AgentHandoffService.cs`
- `Autofix/GitHubWebhookEndpoints.cs`
- `Autofix/LoomEndpoints.cs`
- `Autofix/LoomExplorerService.cs`
- `Autofix/LoomInsightService.cs`
- `Autofix/LoomPrompts.cs`
- `Autofix/PrCreationService.cs`
- `Autofix/RegressionDetectionService.cs`
- `Autofix/RegressionEndpoints.cs`
- `Autofix/TriageEndpoints.cs`
- `Autofix/TriagePipelineService.cs`
- `Autofix/IssueContextBuilder.cs`
- `AgentRuns/AgentRunEndpoints.cs`
- `AgentRuns/AgentInsightsEndpoints.cs`
- `AgentRuns/AgentInsightsService.cs`

Do not move those files back into collector-shaped namespaces. Keep the surviving runtime code in `src/qyl.loom`.

## Temporary vs Final Boundary

### Temporary boundary: acceptable first cut

Temporary rule:

- `qyl.loom -> qyl.collector` is allowed.
- `qyl.collector -> qyl.loom` remains forbidden.

Temporary dependency is specifically for:

- `Qyl.Collector.Storage.DuckDbStore`
- collector storage schema/migration code
- collector query and issue services already used by Loom
- collector auth/middleware/bootstrap helpers

This is intentionally ugly but mechanically honest: Loom becomes the runtime host now, and the remaining dependency is explicitly infrastructural.

### Final boundary: what the code should converge to

Final rule:

- no project reference between `qyl.loom` and `qyl.collector`

Extract the shared non-AI kernel into a third project instead of letting either product own it. Minimal target:

- new project for DuckDB storage, migrations, issue/query reads, and shared auth/bootstrap primitives

Move out of collector ownership before removing the final project reference:

- `src/qyl.collector/Storage/*`
- collector-side issue/query services consumed by Loom
- any host-neutral auth/bootstrap helpers required by both products

Do not try to perfect this in the first cut. First make Loom the host; then shrink the temporary seam.

## Migration Sequence

1. Convert `src/qyl.loom/qyl.loom.csproj` to a runnable web project and add `src/qyl.loom/Program.cs`.
2. Add Loom-local hosting extensions that map only Loom-owned services and endpoints.
3. Reuse collector core/storage/auth/telemetry bootstrap temporarily from `qyl.loom`.
4. Remove Loom service registrations and Loom endpoint mappings from collector hosting.
5. Delete collector Loom runtime files after route ownership flips to `qyl.loom`.
6. Add a Loom test project and route-ownership tests before extracting deeper seams.
7. Extract shared storage/query/bootstrap code into a third project.
8. Remove `src/qyl.loom/qyl.loom.csproj -> src/qyl.collector/qyl.collector.csproj`.

Runtime seams come before storage/query seams. Reversing that order creates a long-lived half-refactor with no product win.

## Validation

Required checks:

- `dotnet build src/qyl.loom/qyl.loom.csproj`
- `dotnet build src/qyl.collector/qyl.collector.csproj`
- route ownership test:
  - Loom host returns `200`/expected SSE for `/api/v1/loom/*`, `/api/v1/issues/*/fix-runs`, `/api/v1/regressions*`
  - collector host returns `404` for those Loom-owned routes
- architecture test:
  - collector assembly has no Loom runtime types
  - collector no longer references `Microsoft.Extensions.AI`
  - loom host is allowed to reference AI packages
- startup test:
  - `qyl.loom` can initialize schema and serve requests against the shared DuckDB database

## Major Risks

- Shared DuckDB bootstrapping is still collector-owned today; moving runtime first means Loom temporarily depends on collector initialization.
- `src/qyl.loom/Identity/GlobalUsings.cs` imports broad collector namespaces; extraction will expose hidden coupling quickly.
- Route ownership can silently regress if collector keeps stale endpoint mappings while Loom adds its own host.
- If both hosts point at the same database, migration/init ownership must be single-sourced to avoid startup races.
- Tests are collector-only today; without a Loom host test project, the ADR will drift again.

## Historical Note

A brainstorming session on 2026-03-12 incorrectly identified qyl.loom as "dead code" and deleted 54 files. This decision exists to prevent that from happening again.
