// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT

// Primary export surface — full HTTP client, models, and helpers emitted by
// @typespec/http-client-js.
export * from "./generated/src/index.js";

// The branded ID scalars (TraceId, SpanId, …) live only in the @qyl/typespec-emit-ts-types
// output; re-export them directly. The rest of ./generated/api.js duplicates names that the
// http-client-js emitter already owns, so consumers who want the flat interface surface
// import from the "@qyl/client/types" subpath (see package.json `exports`).
export type {
    TraceId,
    SpanId,
    SessionId,
    UserId,
} from "./generated/api.js";
