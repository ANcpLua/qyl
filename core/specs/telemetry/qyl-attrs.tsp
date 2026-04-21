// =============================================================================
// qyl telemetry attribute registry
// =============================================================================
// Every qyl-owned telemetry key that appears as a string literal in runtime
// source must be annotated here. The @qylAttr decorator feeds the
// @qyl/typespec-qyl-semconv-lint validator, which enforces:
//
//   QYL-LINT-001  no collisions with upstream OTel prefixes
//   QYL-LINT-002  every key starts with `qyl.`
//   QYL-LINT-003  OTel naming (lowercase, digits, underscores, dot-separated)
//   QYL-LINT-004  same key → same primitive type across all sites
//   QYL-LINT-005  stability one-way ratchet (stable → experimental = error)
//   QYL-LINT-006  cardinality drift across sites (warning)
//
// The marker model is compile-time-only: it carries no @csharpNamespace /
// @duckdbTable / @tsBrand decorators, so the three content emitters skip it.
// =============================================================================

import "@qyl/typespec-qyl-semconv-lint";

using Qyl.Semconv;

namespace Qyl.Telemetry.Attrs;

/**
 * Registry marker — not a runtime shape. Each property is one annotated
 * attribute; the property type mirrors the declared primitive so a future
 * code-gen step can project the registry into typed helpers if needed.
 */
@doc("qyl-owned telemetry attribute registry (names only).")
model QylTelemetryRegistry {
  @qylAttr("qyl.storage.size",               "long",   #{ cardinality: "low",  stability: "experimental" })
  storageSize: int64;

  @qylAttr("qyl.duckdb.dropped_jobs_total",  "long",   #{ cardinality: "low",  stability: "experimental" })
  droppedJobsTotal: int64;

  @qylAttr("qyl.duckdb.dropped_spans_total", "long",   #{ cardinality: "low",  stability: "experimental" })
  droppedSpansTotal: int64;

  @qylAttr("qyl.keycloak.claims",            "string", #{ cardinality: "high", stability: "experimental" })
  keycloakClaims: string;

  @qylAttr("qyl.instance_id",                "string", #{ cardinality: "high", stability: "experimental" })
  instanceId: string;
}
