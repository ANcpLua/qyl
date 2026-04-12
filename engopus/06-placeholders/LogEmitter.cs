// ROOT CAUSE: No Roslyn generator emits LogRecord creation code.
// Log schema + DuckDB storage + REST API all exist. GenAI prompts/completions
// produce rich logs that only land as span events today.
//
// Fix: New emitter in qyl.instrumentation.generators that intercepts
// ILogger.Log* calls and enriches them with gen_ai.* attributes,
// similar to how GenAiCallSiteAnalyzer intercepts ChatClient calls.
//
// Why it doesn't exist yet: the instrumentation generator was built
// traces-first. Logs were deferred to Microsoft.Extensions.Logging
// at runtime with no compile-time wrapping.
//
// This file compiles to nothing. It exists to mark the gap.

namespace Qyl.Instrumentation.Generators.Emitters;

// When implemented, this analyzer discovers ILogger usage sites in GenAI code
// and the emitter wraps them with structured LogRecord attributes:
//   gen_ai.system, gen_ai.request.model, gen_ai.operation.name
//
// Blocked on: unified tool model (engopus/04-models/UnifiedToolModel.cs)
// because log emission needs the same parameter extraction as tool spans.
