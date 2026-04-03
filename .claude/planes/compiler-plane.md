# Compiler plane

Mission:
- make platform structure explicit at compile time

Owns:
- source generators
- descriptors and registries
- tool catalogs
- workflow manifests
- capability and policy manifests
- telemetry metadata emission
- projection/wiring helpers

Depends on:
- Roslyn utility libraries
- contracts and attribute surfaces

Must not depend on:
- runtime reflection as primary discovery
- ad hoc startup registration as the source of truth

Current qyl areas:
- `src/qyl.instrumentation.generators`
- Loom generator slice
- service-defaults and instrumentation emitters

Success condition:
- runtime registration, telemetry semantics, tool exposure, and workflow metadata are compiler-emitted rather than hand-wired.
