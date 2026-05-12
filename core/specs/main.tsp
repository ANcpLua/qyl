import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/openapi3";
import "@typespec/versioning";
import "@typespec/sse";
import "@typespec/events";

// Upstream OTel attribute key constants (ANcpLua.OtelConventions.OTel.Keys.*).
// Generated upstream by ANcpLua/typespec-otel-semconv from semantic-conventions
// v1.41.0 and shipped via the npm package
// `@o-ancpplua/otel-conventions-api`.
import "@o-ancpplua/otel-conventions-api/generated/otel-keys";

// Common scalars / errors / pagination ship via the npm package.
// ANcpLua.OtelConventions.Common.* is the canonical namespace; qyl maps it
// to Qyl.Common.* in C# output via qyl-emit-config.tsp.
import "@o-ancpplua/otel-conventions-api/common/types";
import "@o-ancpplua/otel-conventions-api/common/errors";
import "@o-ancpplua/otel-conventions-api/common/pagination";

import "./qyl-emit-config.tsp";
import "./qyl-emit-duckdb.tsp";

// OTel signal models — import via indirection barrel. The barrel pulls in the
// npm package's `otel/otel-conventions.tsp`, which itself imports the
// `ANcpLua.OtelConventions.Storage` flattened DuckDB record models. See
// otel/otel-conventions.tsp and MIGRATION-otel-conventions-api.md.
import "./otel/otel-conventions.tsp";

// qyl-specific models; OTel attribute keys come from
// ANcpLua.OtelConventions.OTel.Keys.* (the npm package's `generated/otel-keys`
// subpath); hand-typed strings in @encodedName are not allowed for
// upstream-OTel attribute names.
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
