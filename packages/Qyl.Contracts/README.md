# Qyl.Contracts

Shared BCL-only contract types for the [qyl](https://github.com/ancplua/qyl) observability platform.

All records, enums, and primitives on the qyl REST and OTLP surfaces are emitted from the TypeSpec
sources in `core/specs/` via `@qyl/typespec-emit-csharp`. Consumers pair this package with
`Qyl.Client` (transport) and `Qyl.OpenTelemetry.Extensions` (instrumentation).

No external dependencies beyond the base-class library. Safe to reference from any qyl service
or third-party consumer.

## License

MIT © 2025-2026 ancplua
