// =============================================================================
// QYL v2.0 - Main TypeSpec Entry Point
// =============================================================================
// OpenTelemetry Semantic Conventions v1.39 compliant schema.
// 48 TypeSpec files across 9 domains.
//
// Domain Files:
//   - OTel Core: 5 (enums, span, logs, metrics, resource)
//   - AI: 3 (genai, code, cli)
//   - Security: 4 (network, dns, tls, security-rule)
//   - Transport: 7 (http, rpc, messaging, url, signalr, kestrel, user-agent)
//   - Infrastructure: 7 (host, container, k8s, cloud, faas, os, webengine)
//   - Runtime: 5 (process, system, thread, dotnet, aspnetcore)
//   - Data: 5 (db, file, elasticsearch, vcs, artifact)
//   - Observe: 8 (session, browser, feature-flags, exceptions, otel, log, error, test)
//   - Ops: 2 (cicd, deployment)
//   - Identity: 2 (user, geo)
//
// Storage Tables (x-duckdb-table):
//   - Span → spans (otel/span.tsp)
//   - LogRecord → logs (otel/logs.tsp)
//   - SessionEntity → session_entities (domains/observe/session.tsp)
//   - ErrorEntity → errors (domains/observe/error.tsp)
//   - TestRunEntity → test_runs (domains/observe/test.tsp)
//   - TestCaseEntity → test_cases (domains/observe/test.tsp)
//   - DeploymentEntity → deployments (domains/ops/deployment.tsp)
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

// =============================================================================
// AI Domain
// =============================================================================

import "./domains/ai/genai.tsp";
import "./domains/ai/code.tsp";
import "./domains/ai/cli.tsp";

// =============================================================================
// Security Domain
// =============================================================================

import "./domains/security/network.tsp";
import "./domains/security/dns.tsp";
import "./domains/security/tls.tsp";
import "./domains/security/security-rule.tsp";

// =============================================================================
// Transport Domain
// =============================================================================

import "./domains/transport/http.tsp";
import "./domains/transport/rpc.tsp";
import "./domains/transport/messaging.tsp";
import "./domains/transport/url.tsp";
import "./domains/transport/signalr.tsp";
import "./domains/transport/kestrel.tsp";
import "./domains/transport/user-agent.tsp";

// =============================================================================
// Infrastructure Domain
// =============================================================================

import "./domains/infra/host.tsp";
import "./domains/infra/container.tsp";
import "./domains/infra/k8s.tsp";
import "./domains/infra/cloud.tsp";
import "./domains/infra/faas.tsp";
import "./domains/infra/os.tsp";
import "./domains/infra/webengine.tsp";

// =============================================================================
// Runtime Domain
// =============================================================================

import "./domains/runtime/process.tsp";
import "./domains/runtime/system.tsp";
import "./domains/runtime/thread.tsp";
import "./domains/runtime/dotnet.tsp";
import "./domains/runtime/aspnetcore.tsp";

// =============================================================================
// Data Domain
// =============================================================================

import "./domains/data/db.tsp";
import "./domains/data/file.tsp";
import "./domains/data/elasticsearch.tsp";
import "./domains/data/vcs.tsp";
import "./domains/data/artifact.tsp";

// =============================================================================
// Observe Domain
// =============================================================================

import "./domains/observe/session.tsp";
import "./domains/observe/browser.tsp";
import "./domains/observe/feature-flags.tsp";
import "./domains/observe/exceptions.tsp";
import "./domains/observe/otel.tsp";
import "./domains/observe/log.tsp";
import "./domains/observe/error.tsp";
import "./domains/observe/test.tsp";

// =============================================================================
// Ops Domain
// =============================================================================

import "./domains/ops/cicd.tsp";
import "./domains/ops/deployment.tsp";

// =============================================================================
// Identity Domain
// =============================================================================

import "./domains/identity/user.tsp";
import "./domains/identity/geo.tsp";

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
