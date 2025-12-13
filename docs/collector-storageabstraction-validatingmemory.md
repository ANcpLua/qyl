You are reviewing or modifying ANY part of qyl’s storage system.

This includes BOTH:

- storage/abstractions/ (IStore, ISessionStore)
- storage/memory/ (MemoryStore.cs + helpers)

Your job is to ensure the storage layer is schema-correct, deterministic, semconv-aligned,
and consistent across all storage implementations (memory + DuckDB).

======================================================================
SCOPE
======================================================================
Includes:

- storage/abstractions/IStore.cs
- storage/abstractions/ISessionStore.cs
- storage/memory/MemoryStore.cs
- Any helper logic for token aggregation, ordering, and session persistence

======================================================================
GOAL
======================================================================
Provide a consistent, canonical, deterministic storage contract AND a correct,
fast, schema-perfect in-memory implementation used for dev/test.

All storage backends MUST behave identically.

======================================================================
REQUIRED ACTIONS
======================================================================

1. Canonical Contract Enforcement (applies to abstractions/*)
  - Interfaces MUST reflect the canonical data model defined in:
    core/schema/span.json
    core/schema/session.json
    core/schema/genai.json
  - MUST NOT expose fields not present in schema.
  - MUST expose token fields:
    input_tokens
    output_tokens
    total_tokens
  - All methods MUST accept semconv-normalized data.

2. Read/Write Consistency Across Backends
  - All implementations MUST return objects suitable for:
    • API DTO mapping
    • SSE/WebSocket streaming
    • Session aggregation
  - The logical result of any query MUST be identical for:
    MemoryStore ↔ DuckDbStore

3. Ordering & Determinism (applies to ALL storage)
  - MUST preserve chronological order of spans and events.
  - MUST guarantee deterministic iteration order.
  - MemoryStore MUST use OrderedDictionary<TKey,TValue>.
  - MUST NOT use Dictionary<> with separate order-tracking lists.

4. Token Semantics (shared requirement)
  - MUST compute aggregate tokens exactly the same way as SessionAggregator.
  - input_tokens, output_tokens, total_tokens MUST always be numeric.
  - MUST NOT store partially aggregated or raw inconsistent values.

5. Schema Fidelity (shared requirement)
  - Stored objects MUST match schema exactly.
  - MUST NOT add extra fields.
  - MUST NOT drop schema-required fields.

6. Memory Safety (MemoryStore-specific)
  - MUST avoid unbounded growth (LRU/TTL allowed but not required).
  - MUST avoid ToList/ToArray in hot loops.
  - MUST avoid unnecessary heap allocations.

7. Dependency Rules (strict)
   abstractions/*:
   MAY depend only on core/dotnet models.
   MUST NOT depend on receivers/api/streaming/dashboard/instrumentation.

   memory/*:
   MAY depend only on:
   • abstractions/*
   • core/dotnet
   MUST NOT import:
   • receivers/*
   • processing/*
   • api/*
   • streaming/*
   • dashboard/*
   • instrumentation/*

======================================================================
DEFINITION OF DONE
======================================================================

- Storage contract is canonical, stable, schema-aligned, semconv-aligned.
- MemoryStore behavior matches DuckDB behavior for:
  ordering, token aggregation, filtering, retrieval.
- All stored objects are schema-perfect with no extra fields.
- All iteration is deterministic.
- No dependency rules are violated.
- No pre-.NET9 patterns are used (e.g., Dictionary+tracking list, manual ordering).
