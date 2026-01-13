// =============================================================================
// QYL God Schema - Primitive Types
// =============================================================================
// Strongly-typed scalars with multi-target extensions.
// =============================================================================

import "@typespec/json-schema";
import "@typespec/openapi";

using TypeSpec.JsonSchema;

namespace Qyl.Primitives;

// =============================================================================
// Trace Identifiers
// =============================================================================

@jsonSchema
@doc("Unique trace identifier (32 lowercase hex characters)")
@pattern("^[a-f0-9]{32}$")
@minLength(32)
@maxLength(32)
@example("0af7651916cd43dd8448eb211c80319c")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(32)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "TraceId")
scalar TraceId extends string;

@jsonSchema
@doc("Unique span identifier (16 lowercase hex characters)")
@pattern("^[a-f0-9]{16}$")
@minLength(16)
@maxLength(16)
@example("b7ad6b7169203331")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(16)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "SpanId")
scalar SpanId extends string;

@jsonSchema
@doc("Session identifier (GUID without hyphens)")
@pattern("^[a-f0-9]{32}$")
@minLength(32)
@maxLength(32)
@example("550e8400e29b41d4a716446655440000")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(32)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionId")
scalar SessionId extends string;

// =============================================================================
// Temporal Types
// =============================================================================

@jsonSchema
@doc("Unix timestamp in nanoseconds since epoch")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "UnixNano")
@TypeSpec.OpenAPI.extension("x-ts-type", "number")
scalar UnixNano extends int64;

@jsonSchema
@doc("Duration in nanoseconds")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar DurationNs extends int64;

// =============================================================================
// Size & Count Types
// =============================================================================

@jsonSchema
@doc("Token count for LLM operations")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "BIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar TokenCount extends int64;

@jsonSchema
@doc("Generic non-negative counter")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "BIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar Count extends int64;

@jsonSchema
@doc("Cost in USD (floating point)")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar CostUsd extends float64;

@jsonSchema
@doc("Temperature setting for LLM requests (0.0-2.0)")
@minValue(0)
@maxValue(2)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar Temperature extends float64;
