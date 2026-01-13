// =============================================================================
// QYL God Schema - Single Source of Truth
// =============================================================================
// All types are defined here with x-extensions for multi-target generation:
//   - x-duckdb-type: DuckDB column type
//   - x-duckdb-table: DuckDB table name
//   - x-duckdb-primary-key: Primary key column
//   - x-duckdb-index: Index hint
//   - x-csharp-struct: Generate as struct
//   - x-csharp-type: C# type override
//   - x-promoted: Fast-access column in DuckDB
//   - x-internal: Storage-only, not exposed in API
// =============================================================================

import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/openapi3";
import "@typespec/versioning";
import "@typespec/json-schema";

import "./primitives.tsp";
import "./enums.tsp";
import "./models.tsp";
import "./api.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.OpenAPI;
using TypeSpec.Versioning;
using TypeSpec.JsonSchema;

@service(#{
  title: "QYL Telemetry API",
})
@server("http://localhost:5100", "QYL Collector")
namespace Qyl;
