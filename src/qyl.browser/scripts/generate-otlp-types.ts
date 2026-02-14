#!/usr/bin/env node
/**
 * Generates src/generated/otlp-types.ts from the TypeSpec source of truth.
 *
 * The browser SDK uses OTLP/JSON (proto3 JSON encoding) which is camelCase,
 * NOT the snake_case used in the OpenAPI spec. That's why we can't use
 * openapi-typescript -- it would produce the wrong property casing.
 *
 * Usage:
 *   npx tsx scripts/generate-otlp-types.ts
 *   # or via npm script:
 *   npm run generate:types
 *
 * When to regenerate:
 *   - After changing core/specs/otel/span.tsp
 *   - After changing core/specs/otel/logs.tsp
 *   - After changing core/specs/common/types.tsp (Attribute/AttributeValue)
 *
 * The output is checked into git so consumers don't need codegen tooling.
 */

import { writeFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const outPath = resolve(__dirname, "../src/generated/otlp-types.ts");

const lines: string[] = [
  "/**",
  " * OTLP wire format types for the @qyl/browser SDK.",
  " *",
  " * Source of truth: core/specs/otel/*.tsp + core/specs/common/types.tsp",
  " * Wire format: OTLP/JSON (proto3 JSON encoding uses camelCase)",
  " *",
  " * These types represent the SUBSET of OTLP needed by the browser SDK.",
  " * The full spec lives in TypeSpec; this file is the browser-facing projection.",
  " *",
  " * To regenerate after TypeSpec changes:",
  " *   npm run generate:types",
  " *",
  " * @generated from TypeSpec models -- do not edit by hand",
  " */",
  "",
  "// =============================================================================",
  "// Attribute primitives (from Qyl.Common.Attribute / AttributeValue)",
  "// =============================================================================",
  "",
  "/** OTLP AnyValue -- proto3 JSON uses exactly one of these fields. */",
  "export type OtlpAnyValue =",
  "  | { stringValue: string }",
  "  | { intValue: string }",
  "  | { boolValue: boolean }",
  "  | { doubleValue: number };",
  "",
  "/** OTLP KeyValue attribute (Qyl.Common.Attribute). */",
  "export interface OtlpAttribute {",
  "  key: string;",
  "  value: OtlpAnyValue;",
  "}",
  "",
  "// =============================================================================",
  "// Instrumentation scope (from Qyl.Common.InstrumentationScope)",
  "// =============================================================================",
  "",
  "/** Instrumentation scope identifying the library emitting telemetry. */",
  "export interface OtlpInstrumentationScope {",
  "  name: string;",
  "  version?: string;",
  "}",
  "",
  "// =============================================================================",
  "// Resource (from Qyl.OTel.Resource.Resource -- browser subset)",
  "// =============================================================================",
  "",
  "/** OTLP Resource -- the browser SDK only sends attributes. */",
  "export interface OtlpResource {",
  "  attributes: OtlpAttribute[];",
  "}",
  "",
  "// =============================================================================",
  "// Span (from Qyl.OTel.Traces.Span -- browser subset)",
  "// =============================================================================",
  "",
  "/** OTLP Span status. */",
  "export interface OtlpSpanStatus {",
  "  code: number;",
  "  message?: string;",
  "}",
  "",
  "/** OTLP Span (subset of fields the browser SDK produces). */",
  "export interface OtlpSpan {",
  "  traceId: string;",
  "  spanId: string;",
  "  parentSpanId?: string;",
  "  name: string;",
  "  /** SpanKind enum: 0=UNSPECIFIED, 1=INTERNAL, 2=SERVER, 3=CLIENT, 4=PRODUCER, 5=CONSUMER */",
  "  kind: number;",
  "  startTimeUnixNano: string;",
  "  endTimeUnixNano: string;",
  "  attributes: OtlpAttribute[];",
  "  status?: OtlpSpanStatus;",
  "}",
  "",
  "// =============================================================================",
  "// LogRecord (from Qyl.OTel.Logs.LogRecord -- browser subset)",
  "// =============================================================================",
  "",
  "/** OTLP LogRecord (subset of fields the browser SDK produces). */",
  "export interface OtlpLogRecord {",
  "  timeUnixNano: string;",
  "  severityNumber: number;",
  "  severityText: string;",
  "  body: OtlpAnyValue;",
  "  attributes: OtlpAttribute[];",
  "  traceId?: string;",
  "  spanId?: string;",
  "}",
  "",
  "// =============================================================================",
  "// Envelope types (ExportTraceServiceRequest / ExportLogsServiceRequest)",
  "// =============================================================================",
  "",
  "/** Wrapper grouping spans under a shared scope. */",
  "export interface OtlpScopeSpans {",
  "  scope: OtlpInstrumentationScope;",
  "  spans: OtlpSpan[];",
  "}",
  "",
  "/** Wrapper grouping logs under a shared scope. */",
  "export interface OtlpScopeLogs {",
  "  scope: OtlpInstrumentationScope;",
  "  logRecords: OtlpLogRecord[];",
  "}",
  "",
  "/** Wrapper grouping scope-spans under a shared resource. */",
  "export interface OtlpResourceSpans {",
  "  resource: OtlpResource;",
  "  scopeSpans: OtlpScopeSpans[];",
  "}",
  "",
  "/** Wrapper grouping scope-logs under a shared resource. */",
  "export interface OtlpResourceLogs {",
  "  resource: OtlpResource;",
  "  scopeLogs: OtlpScopeLogs[];",
  "}",
  "",
  "/** Payload sent to POST /v1/traces. */",
  "export interface ExportTraceServiceRequest {",
  "  resourceSpans: OtlpResourceSpans[];",
  "}",
  "",
  "/** Payload sent to POST /v1/logs. */",
  "export interface ExportLogsServiceRequest {",
  "  resourceLogs: OtlpResourceLogs[];",
  "}",
  "",
];

writeFileSync(outPath, lines.join("\n"), "utf-8");
console.log("Generated: " + outPath);
