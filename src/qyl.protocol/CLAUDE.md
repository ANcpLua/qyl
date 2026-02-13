# qyl.protocol - Shared Types

Kernel API contract. BCL-only shared types — zero external packages, leaf dependency. Every component that talks to the kernel speaks this language.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| Constraint | **BCL-only** (no PackageReference) |

## Contents

| Directory | Content |
|-----------|---------|
| `Primitives/Scalars.g.cs` | TraceId, SpanId, SessionId (generated, IParsable) |
| `Enums/Enums.g.cs` | SpanKind, StatusCode, SeverityNumber (generated) |
| `Models/*.g.cs` | Record types (generated) |
| `Attributes/` | GenAI provider constants (manual) |

## Time Convention

Protocol uses `long` (signed 64-bit) for cross-platform. Collector converts to `ulong` for DuckDB.

## Rules

- Never add PackageReference — must stay BCL-only
- Never edit `*.g.cs` — they are generated from TypeSpec
