You are reviewing or modifying qyl collector processing pipeline components.

### SCOPE

Includes:

- BatchProcessor
- FilterProcessor
- AttributeNormalizer
- GenAiExtractor
- SessionAggregator
- Processing internal models

### GOAL

Guarantee that processing is fast, deterministic, semantically correct, and
aligned with GenAI semconv 1.38 and qyl schemas.

### REQUIRED ACTIONS

1. Enforce .NET Modern API Usage
  - All counting MUST use CountBy().
  - All aggregation MUST use AggregateBy().
  - Indexed loops MUST use Index().
  - Prefix detection MUST use direct StartsWith checks (NOT SearchValues<string>!).

2. Attribute Normalization
  - MUST ensure every attribute key is normalized to snake_case.
  - MUST drop deprecated attributes.
  - MUST map old → new attributes when required by semconv 1.38.

3. GenAI Extraction
  - MUST ensure presence of:
    gen_ai.provider.name
    gen_ai.request.model
    gen_ai.response.model
    gen_ai.operation.name
    gen_ai.usage.input_tokens
    gen_ai.usage.output_tokens
    gen_ai.usage.total_tokens
  - MUST detect tokens via direct StartsWith prefix checks.
  - MUST flag missing fields.

4. Latency Tracking
  - MUST use ILatencyContextProvider.
  - MUST add latency checkpoints for:
    batch.receive
    batch.normalize
    batch.extract
    batch.store

5. Session Aggregation
  - MUST use single-pass aggregation (AggregateBy).
  - MUST ensure correctness of input/output/total tokens.
  - MUST maintain chronological order.

6. BatchProcessor Rules
  - MUST avoid allocations inside loops.
  - MUST operate streaming-first.
  - For parallelization MUST use Parallel.ForAsync where safe.

7. Dependency Rules
  - processing/* → storage/*
  - processing/* MUST NOT reference:
    • api/*
    • streaming/*
    • dashboard/*
    • instrumentation/*

### DEFINITION OF DONE

- All processors follow modern .NET 9/10 APIs.
- GenAI extraction fully compliant with semconv 1.38.
- Latency checkpoints present.
- No deprecated attributes.
- No extra attributes not in schema.
- No dependency rule violations.
- Aggregation is single-pass, streaming-friendly, deterministic.
