# Qyl Local AI Debugger Architecture (Ollama + MCP)

This document outlines the design for implementing an automated root cause analysis and autofix engine for `qyl`,
inspired by Sentry Seer but executing **entirely locally using Ollama**, requiring zero configuration beyond having
Ollama running.

## Objective

Provide an "it just works from the get-go" AI debugging experience. When an error occurs or a user asks their local
IDE (Cursor/Claude) to debug a trace, `qyl` handles the heavy lifting of gathering context, feeding it to a local Ollama
model, and returning an actionable Root Cause Analysis (RCA).

## 1. The MCP Bridge

Instead of forcing the LLM to query raw spans and logs (which blows up the context window), `qyl` provides a high-level
MCP tool. Sentry does this via `analyze_issue_with_seer`. We do this via:

```typescript
{
  name: "analyze_trace_with_qyl",
  description: "Triggers a local AI analysis of an error or trace to find the root cause and suggest a fix.",
  parameters: {
    traceId: "string"
  }
}
```

## 2. Context Gathering Engine (The "Secret Sauce")

When `analyze_trace_with_qyl(traceId)` is called, the `qyl.backend` executes the following pipeline, completely
bypassing the need for an external cloud:

1. **Trace Fetching:** Retrieves all spans and logs associated with the `traceId` from DuckDB.
2. **Symbolication & AST:** Identifies the exact files and lines of code referenced in the stack traces within the
   spans.
3. **Local Context Extraction:** Reads the surrounding code context directly from the local disk (since `qyl` runs
   locally).
4. **Prompt Assembly:** Constructs a highly compressed, structured Markdown prompt:
    * **Error Synopsis:** High-level description of the failure.
    * **The Code:** Relevant snippets extracted from disk.
    * **The Evidence:** Filtered log lines and slow spans.

## 3. Local Inference (Ollama)

`qyl` automatically detects the local Ollama instance (defaulting to `http://localhost:11434`) and streams the assembled
prompt.

* **No Selection Required:** `qyl` defaults to pulling and running a highly capable coding model (e.g., `llama3` or
  `qwen2.5-coder`) if one isn't explicitly defined. The user literally just starts the tool.
* **Zero API Keys:** Completely local, private, and free. No $40/month subscription like Seer.

## 4. The Response

Ollama streams back a structured RCA:

1. **Root Cause:** "The `user.id` field is null because the authentication middleware failed to parse the token..."
2. **Suggested Fix:** A unified diff or code block showing the exact fix.

The MCP server passes this markdown back to the user's IDE, allowing the developer to accept the fix directly in their
editor. This perfectly mimics the Seer + Cursor workflow, but with absolute data privacy.
