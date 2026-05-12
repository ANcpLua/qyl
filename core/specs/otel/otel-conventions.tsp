// Migration indirection point for ANcpLua.OtelConventions.Api.
//
// Today: re-exports the locally-authored OTel signal-model files.
// When ANcpLua.OtelConventions.Api is published to npm/nuget, replace
// the six import lines below with:
//
//   import "@ancplua/otel-conventions-api";
//
// and delete otel/enums.tsp, otel/resource.tsp, otel/span.tsp,
// otel/logs.tsp, otel/metrics.tsp, otel/profiles.tsp.
// See core/specs/MIGRATION-otel-conventions-api.md for the full plan.

import "./enums.tsp";
import "./resource.tsp";
import "./span.tsp";
import "./logs.tsp";
import "./metrics.tsp";
import "./profiles.tsp";
