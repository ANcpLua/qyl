// =============================================================================
// QYL God Schema - Enumerations
// =============================================================================
// OTel standard enums with DuckDB storage hints.
// x-enum-varnames provides member names for C# generation.
// =============================================================================

import "@typespec/openapi";

namespace Qyl.Enums;

// =============================================================================
// Span Enumerations
// =============================================================================

@doc("Span kind describing the relationship between spans")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "TINYINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SpanKind")
@TypeSpec.OpenAPI.extension("x-enum-varnames", #["Unspecified", "Internal", "Server", "Client", "Producer", "Consumer"])
enum SpanKind {
  @doc("Default. Unspecified span kind")
  unspecified: 0,

  @doc("Internal operation within the application")
  internal: 1,

  @doc("Server-side operation handling a request")
  server: 2,

  @doc("Client-side operation making a request")
  client: 3,

  @doc("Producer creating a message for async processing")
  producer: 4,

  @doc("Consumer processing a message")
  consumer: 5,
}

@doc("Span status code")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "TINYINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "StatusCode")
@TypeSpec.OpenAPI.extension("x-enum-varnames", #["Unset", "Ok", "Error"])
enum StatusCode {
  @doc("Status not set")
  unset: 0,

  @doc("Operation completed successfully")
  ok: 1,

  @doc("Operation failed with an error")
  error: 2,
}

// =============================================================================
// Log Enumerations
// =============================================================================

@doc("Log severity number following OTel specification (1-24)")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "TINYINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SeverityNumber")
@TypeSpec.OpenAPI.extension("x-enum-varnames", #["Unspecified", "Trace", "Debug", "Info", "Warn", "Error", "Fatal"])
enum SeverityNumber {
  @doc("Unspecified severity")
  unspecified: 0,

  @doc("TRACE level")
  trace: 1,

  @doc("DEBUG level")
  debug: 5,

  @doc("INFO level")
  info: 9,

  @doc("WARN level")
  warn: 13,

  @doc("ERROR level")
  error: 17,

  @doc("FATAL level")
  fatal: 21,
}

// =============================================================================
// GenAI Enumerations (String-backed - no x-enum-varnames needed)
// =============================================================================

@doc("GenAI operation name")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
enum GenAiOperationName {
  @doc("Chat completion")
  chat: "chat",

  @doc("Text completion")
  completion: "completion",

  @doc("Embedding generation")
  embedding: "embedding",

  @doc("Image generation")
  imageGeneration: "image_generation",

  @doc("Text-to-speech")
  textToSpeech: "text_to_speech",

  @doc("Speech-to-text")
  speechToText: "speech_to_text",

  @doc("Agent invocation")
  invokeAgent: "invoke_agent",

  @doc("Tool execution")
  executeTool: "execute_tool",
}

@doc("GenAI finish reason")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
enum GenAiFinishReason {
  @doc("Normal stop")
  stop: "stop",

  @doc("Max tokens reached")
  maxTokens: "max_tokens",

  @doc("Tool call required")
  toolCalls: "tool_calls",

  @doc("Content filter triggered")
  contentFilter: "content_filter",

  @doc("Error occurred")
  error: "error",
}
