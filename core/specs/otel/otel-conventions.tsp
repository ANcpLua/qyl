// Migration indirection point for ANcpLua.OtelConventions.Api.
//
// 2026-05-12: swapped the locally-authored OTel signal + storage files for
// the published npm subpath `@o-ancpplua/otel-conventions-api/otel`. This
// pulls in the OTel-shaped enums / resource / span / logs / metrics /
// profiles models plus the flattened DuckDB-shaped storage models
// (ANcpLua.OtelConventions.Storage) from the external TypeSpec library.
// The deleted local files are listed in
// `core/specs/MIGRATION-otel-conventions-api.md`.

import "@o-ancpplua/otel-conventions-api/otel";
