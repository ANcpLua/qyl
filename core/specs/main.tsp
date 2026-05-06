// =============================================================================
// QYL v2.0 - Main TypeSpec Entry Point
// =============================================================================
// OpenTelemetry Semantic Conventions v1.40 compliant schema.
//
// Structure:
//   - Common: types, errors, pagination
//   - OTel Core: enums, span, logs, metrics, profiles, resource, storage
//   - Generated: semconv.g.tsp (OTel 1.40 attribute keys from NPM)
//   - Domains: qyl-specific models (stats, entities, etc.)
//   - API: REST routes, SSE streaming
//
// Storage Tables (x-duckdb-table):
//   - Span → spans (otel/span.tsp)
//   - LogRecord → logs (otel/logs.tsp)
//   - SessionEntity → session_entities (models/session.tsp)
//   - ErrorEntity → errors (models/error.tsp)
//   - TestRunEntity → test_runs (models/test.tsp)
//   - TestCaseEntity → test_cases (models/test.tsp)
//   - DeploymentEntity → deployments (models/deployment.tsp)
// =============================================================================

import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/openapi3";
import "@typespec/versioning";
import "@typespec/sse";
import "@typespec/events";

// =============================================================================
// Generated semantic-convention key constants (Weaver — DO NOT EDIT)
// =============================================================================
// Regenerated from .tools/semconv-upstream/model (pinned v1.41.0) by
// ./eng/semconv/run-weaver.sh. See core/specs/generated/README.md.
// =============================================================================

import "./generated/otel-keys.gen.tsp";

// =============================================================================
// Common Infrastructure
// =============================================================================

import "./common/types.tsp";
import "./common/errors.tsp";
import "./common/pagination.tsp";

// =============================================================================
// qyl-native emit routing (Commit 5 decoration + Commit 6 duckdb tuple mapping)
// =============================================================================

import "./qyl-emit-config.tsp";
import "./qyl-emit-duckdb.tsp";

// =============================================================================
// OTel Core Types
// =============================================================================

import "./otel/enums.tsp";
import "./otel/resource.tsp";
import "./otel/span.tsp";
import "./otel/logs.tsp";
import "./otel/metrics.tsp";
import "./otel/profiles.tsp";
import "./otel/storage.tsp";

// =============================================================================
// QYL Domain Models
// =============================================================================
// qyl-specific models (stats, entities, aggregations). OTel semconv attribute
// keys come from Qyl.OTel.Keys.<Domain>.<Name> (generated/otel-keys.gen.tsp);
// hand-typed dotted strings inside @encodedName(...) are not allowed for
// upstream-OTel attribute names.
// =============================================================================

// AI Domain - GenAI stats and cost models
import "./models/genai.tsp";

// Transport Domain - HTTP/RPC/Messaging stats
import "./models/http.tsp";
import "./models/rpc.tsp";
import "./models/messaging.tsp";

// Data Domain - Database stats
import "./models/db.tsp";

// Observe Domain - Sessions, errors, logs, tests, OTel config
import "./models/session.tsp";
import "./models/otel-config.tsp";
import "./models/log.tsp";
import "./models/error.tsp";
import "./models/test.tsp";

// Ops Domain - Deployments
import "./models/deployment.tsp";

// Runtime Domain - System stats
import "./models/system.tsp";

// Identity Domain - User stats
import "./models/identity.tsp";

// =============================================================================
// API Routes
// =============================================================================

import "./api/routes.tsp";
import "./api/streaming.tsp";

// =============================================================================
// Namespace Re-exports for Convenience
// =============================================================================

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.OpenAPI;
using TypeSpec.Versioning;
