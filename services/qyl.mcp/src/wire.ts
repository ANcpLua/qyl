/**
 * Internal aliases over the generated Qyl contract types. This module contains
 * no handwritten wire shape; the TypeSpec in contracts/ generates both the
 * compile-time types and the JSON Schema documents the validators compile.
 */

export type {
  LogRecord as QylLogRecord,
  SessionEntity as QylSession,
  Span as QylSpan,
  Trace as QylTrace,
} from "./contracts/generated/api.js";
