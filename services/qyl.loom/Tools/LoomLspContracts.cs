// Copyright (c) 2025-2026 ancplua

using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.Tools;

/// <summary>Loom DTO for goto-definition and find-references output. Markdown wraps the Phase-1 MCP result.</summary>
[LoomContract("loom_lsp_location_list")]
public sealed partial record LoomLspLocationList(string Markdown);

/// <summary>Loom DTO for document and workspace symbol queries.</summary>
[LoomContract("loom_lsp_symbol_list")]
public sealed partial record LoomLspSymbolList(string Markdown);

/// <summary>Loom DTO for pull-model diagnostics.</summary>
[LoomContract("loom_lsp_diagnostic_list")]
public sealed partial record LoomLspDiagnosticList(string Markdown);

/// <summary>Loom DTO for prepare-rename validity.</summary>
[LoomContract("loom_lsp_prepare_rename_result")]
public sealed partial record LoomLspPrepareRenameResult(bool Valid);

/// <summary>Loom DTO summarizing an applied rename WorkspaceEdit.</summary>
[LoomContract("loom_lsp_rename_result")]
public sealed partial record LoomLspRenameResult(string Markdown, int FilesChanged, int TotalEdits);
