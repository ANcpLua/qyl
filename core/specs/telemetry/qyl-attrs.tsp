// =============================================================================
// DEPRECATED — qyl attribute registry migrated to Weaver YAML
// =============================================================================
// The @qylAttr decorator and QylTelemetryRegistry model have been replaced by
// the canonical YAML registry under eng/semconv/model/qyl/.
//
// Attribute constants are now generated via:
//   nuke OtelConventions (or nuke Generate)
//
// Outputs:
//   packages/Qyl.Telemetry/Conventions/Qyl.g.cs   (C# static constants)
//   packages/qyl-client/src/conventions.ts          (TS string constants)
//   docs/attributes/qyl.*.md                        (per-namespace docs)
//
// This file is intentionally empty — the import in main.tsp is kept as a
// tombstone so git blame can trace the migration history.
// =============================================================================
