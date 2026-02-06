// =============================================================================
// QYL v2.0 - Main TypeSpec Entry Point
// =============================================================================
// OpenTelemetry Semantic Conventions v1.39 compliant schema.
//
// Structure:
//   - Common: types, errors, pagination
//   - OTel Core: enums, span, logs, metrics, resource, storage
//   - Generated: semconv.g.tsp (OTel 1.39 attribute keys from NPM)
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
// Common Infrastructure
// =============================================================================

import "./common/types.tsp";
import "./common/errors.tsp";
import "./common/pagination.tsp";

// =============================================================================
// OTel Core Types
// =============================================================================

import "./otel/enums.tsp";
import "./otel/resource.tsp";
import "./otel/span.tsp";
import "./otel/logs.tsp";
import "./otel/metrics.tsp";
import "./otel/storage.tsp";

// =============================================================================
// Generated Semantic Conventions (OTel 1.39)
// =============================================================================
// Type-safe attribute keys from @opentelemetry/semantic-conventions NPM package.
// Contains Keys.GenAi, Keys.Db, Keys.Http, etc. for all OTel semconv attributes.
import "./generated/semconv.g.tsp";
// =============================================================================

// =============================================================================
// QYL Domain Models
// =============================================================================
// These files contain qyl-specific models (stats, entities, aggregations).
// OTel attribute definitions now come from generated/semconv.g.tsp.
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
