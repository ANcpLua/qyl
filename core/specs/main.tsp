import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/openapi3";
import "@typespec/versioning";
import "@typespec/sse";
import "@typespec/events";

// Regenerated from .tools/semconv-upstream/model (pinned v1.41.0) by
// ./eng/semconv/run-weaver.sh. See core/specs/generated/README.md.
import "./generated/otel-keys.gen.tsp";

import "./common/types.tsp";
import "./common/errors.tsp";
import "./common/pagination.tsp";

import "./qyl-emit-config.tsp";
import "./qyl-emit-duckdb.tsp";

// OTel signal models — import via indirection barrel.
// See otel/otel-conventions.tsp and MIGRATION-otel-conventions-api.md.
import "./otel/otel-conventions.tsp";
import "./otel/storage.tsp";

// qyl-specific models; OTel attribute keys come from Qyl.OTel.Keys.*
// (generated/otel-keys.gen.tsp); hand-typed strings in @encodedName are
// not allowed for upstream-OTel attribute names.
import "./models/genai.tsp";
import "./models/http.tsp";
import "./models/rpc.tsp";
import "./models/messaging.tsp";
import "./models/db.tsp";
import "./models/session.tsp";
import "./models/otel-config.tsp";
import "./models/log.tsp";
import "./models/error.tsp";
import "./models/test.tsp";
import "./models/deployment.tsp";
import "./models/system.tsp";
import "./models/identity.tsp";

import "./api/routes.tsp";
import "./api/streaming.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.OpenAPI;
using TypeSpec.Versioning;
