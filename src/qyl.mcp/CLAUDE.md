@import ../../CLAUDE.md

# qyl.mcp

**Type:** Application  
**Framework:** net10.0  
**Language:** C# latest  
**SDK:** ANcpLua.NET.Sdk

## Banned APIs

- Use `TimeProvider` instead of current-time APIs
- Use `Lock` instead of `object` for locking
- Use `System.Text.Json` instead of Newtonsoft

## Result Cap Coaching

Tools that return lists/search results use `ResponseFormatter.AppendResultCap` to coach the LLM when the result cap is
hit. Constraints:

- **No shared `RESULT_LIMIT` constant.** Tools have legitimately different defaults (25 for search, 50 for errors, 100
  for spans).
- **No migration of ad-hoc tools to `FormatPagedList`.** Different tools need different formats.
- **No filter-level enforcement.** Coaching is explicit and context-aware at the tool level.
- **Fixed-scope tools skip coaching.** `list_trace_logs`, `get_error_issue` events, `export_for_agent`.

## Generator

`src/qyl.mcp.generators/` emits `QylToolManifest` with `ToolTypes[]`, `ToolDescriptors[]`, and `CreateTools()`. It is
the **single** MCP tool-discovery generator in qyl — the legacy `Qyl.Agents.Generator` / `Qyl.Agents.Abstractions` /
`Qyl.Agents` triad was removed on 2026-04-20 after convergence. Both `qyl.mcp` and `qyl.loom` now stand on the official
`ModelContextProtocol` SDK for protocol + dispatch, with `qyl.mcp.generators` layered on top for skill-aware manifest
emission.

The emitter uses `IndentedStringBuilder.BeginBlock()` pattern (not `Indent()/Outdent()` which are internal).
