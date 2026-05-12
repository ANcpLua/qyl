// Migration indirection point for ANcpLua.OtelConventions.Api.
//
// 2026-05-12: swapped the six locally-authored OTel signal files for the
// published npm subpath `@o-ancpplua/otel-conventions-api/otel`. This pulls
// in the OTel-shaped enums / resource / span / logs / metrics / profiles models
// from the external TypeSpec library. The deleted local files are listed in
// `core/specs/MIGRATION-otel-conventions-api.md`.
//
// Note: this PR stages the swap locally but stays on a feature branch until
// the first GitHub Packages release of `@o-ancpplua/otel-conventions-api`
// lands. Until then, `npm install` will 404 on this dep.
//
// `core/specs/otel/storage.tsp` (qyl-specific DuckDB models in the
// `Qyl.Storage` namespace) stays out of the swap and is unaffected.

import "@o-ancpplua/otel-conventions-api/otel";
