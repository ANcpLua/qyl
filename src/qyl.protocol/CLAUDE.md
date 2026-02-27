# qyl.protocol - Shared Types

Kernel API contract. BCL-only shared types — zero external packages, leaf dependency. Every component that talks to the
kernel speaks this language.

## Identity

| Property   | Value                              |
|------------|------------------------------------|
| SDK        | ANcpLua.NET.Sdk                    |
| Framework  | net10.0                            |
| Constraint | **BCL-only** (no PackageReference) |

## Contents

| File                        | Content                                                            |
|-----------------------------|--------------------------------------------------------------------|
| `Primitives/Scalars.g.cs`   | TraceId, SpanId, SessionId (generated, IParsable)                  |
| `Enums/OTelEnums.cs`        | SpanKind, SpanStatusCode (hand-maintained, single source of truth) |
| `Models/SpanRecord.cs`      | Flattened span record for DuckDB storage                           |
| `Models/Common.g.cs`        | Attribute, InstrumentationScope (generated)                        |
| `Attributes/GenAiAttributes.g.cs` | GenAI semconv facades (generated from `eng/semconv/qyl-extensions.json`) |
| `Attributes/DbAttributes.g.cs`    | DB semconv facades (generated from `eng/semconv/qyl-extensions.json`)    |
| `Copilot/CopilotTypes.cs`  | Copilot integration DTOs                                           |

## Time Convention

Protocol uses `long` (signed 64-bit) for cross-platform. Collector converts to `ulong` for DuckDB.

## Rules

- Never add PackageReference — must stay BCL-only
- Never edit `*.g.cs` — they are generated from TypeSpec
