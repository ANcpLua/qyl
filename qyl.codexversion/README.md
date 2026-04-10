# qyl.codexversion

`qyl.codexversion` is a TypeScript-first prototype of an orchestration engine 
for running structured automation work across agents and plugins.

The project is intentionally lightweight and schema-driven:

- Uses TypeScript for the runtime and orchestration primitives.
- Uses Zod for runtime-validated contracts between pipeline stages.
- Uses `delegate-task` as the primary primitive for task ownership, handoff, and routing.
- Uses `call-omo-agent` hooks to execute delegated work and normalize responses.
- Enforces **background limits** so long-running operations remain bounded and safely degradable.
- Controls **concurrency** at orchestration boundaries to avoid saturation and contention.
- Uses declarative **plugin configuration** to wire agents, policies, and adapters without hardcoding behavior.

This README describes the current shape of the prototype, not a finished production platform.
