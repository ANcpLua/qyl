// Copyright (c) 2025-2026 ancplua

using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.Tools;

/// <summary>
///     Loom bridge over the Phase-1 LSP MCP surface (<c>services/qyl.mcp/Tools/Lsp/LspTools.cs</c>).
///     Attribute declarations in this file are the Scope A deliverable — they register the six
///     LSP-backed tools in <see cref="LoomGeneratedRegistry" /> with phase, capability, side-effect,
///     structured-output, and approval metadata. Runtime wiring (LspClientWrapper injection,
///     cross-project DI, approval enforcement) is deferred to a follow-up phase because
///     <c>LspClientWrapper</c> lives in <c>qyl.mcp</c> and qyl.loom does not depend on qyl.mcp.
///     See <c>MAF.Advanced.Patterns.QylLoomExtensions</c> for the eventual composition approach.
/// </summary>
public static partial class LoomLspTools
{
    [LoomTool("lsp_goto_definition",
        Description = "Go to the definition of the symbol at the given 1-based line/column in a source file.",
        Phase = LoomPhase.Explore,
        UseOnlyWhen = "A Loom step needs to ground a symbol reference in its declaration.",
        DoNotUseWhen = "A grep result already pinpoints the declaration unambiguously.")]
    [RequiresCapability("qyl.loom.lsp.navigate")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomLspLocationList))]
    public static LoomLspLocationList GotoDefinition(string filePath, int line, int column) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");

    [LoomTool("lsp_find_references",
        Description = "Find all references to the symbol at the given 1-based line/column across the workspace.",
        Phase = LoomPhase.Explore,
        UseOnlyWhen = "A Loom step needs the full fan-out of call sites before planning an edit.",
        DoNotUseWhen = "The Loom run only requires the declaration — use goto_definition.")]
    [RequiresCapability("qyl.loom.lsp.navigate")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomLspLocationList))]
    public static LoomLspLocationList FindReferences(string filePath, int line, int column,
        bool includeDeclaration = true) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");

    [LoomTool("lsp_symbols",
        Description = "List symbols in a document, or query workspace symbols when 'query' is provided.",
        Phase = LoomPhase.Explore,
        UseOnlyWhen = "A Loom step needs a structural map of a file or a workspace-wide symbol lookup.",
        DoNotUseWhen = "The target symbol and location are already known.")]
    [RequiresCapability("qyl.loom.lsp.navigate")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomLspSymbolList))]
    public static LoomLspSymbolList Symbols(string filePath, string? query = null) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");

    [LoomTool("lsp_diagnostics",
        Description = "Return compiler or analyzer diagnostics for a source file.",
        Phase = LoomPhase.Detect,
        UseOnlyWhen = "A Loom Detect phase needs deterministic diagnostics for a specific file.",
        DoNotUseWhen = "The failure already surfaced as a collector span — use that instead.")]
    [RequiresCapability("qyl.loom.lsp.diagnose")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomLspDiagnosticList))]
    public static LoomLspDiagnosticList Diagnostics(string filePath) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");

    [LoomTool("lsp_prepare_rename",
        Description = "Validate whether the symbol at the given 1-based line/column can be renamed.",
        Phase = LoomPhase.Plan,
        UseOnlyWhen = "A Loom Plan phase is evaluating a rename as a candidate fix strategy.",
        DoNotUseWhen = "The fix plan does not involve renaming a symbol.")]
    [RequiresCapability("qyl.loom.lsp.rename")]
    [ToolSideEffect(ToolSideEffect.ReadsExternalState)]
    [EmitsStructuredOutput(typeof(LoomLspPrepareRenameResult))]
    public static LoomLspPrepareRenameResult PrepareRename(string filePath, int line, int column) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");

    [LoomTool("lsp_rename",
        Description =
            "Rename the symbol at the given 1-based line/column across the workspace. Writes the WorkspaceEdit to disk.",
        Phase = LoomPhase.Fix,
        UseOnlyWhen = "A Loom Fix phase is applying an approved rename identified during Plan.",
        DoNotUseWhen = "PrepareRename has not validated the symbol or approval has not been granted.")]
    [RequiresCapability("qyl.loom.lsp.rename")]
    [RequiresApproval]
    [ToolSideEffect(ToolSideEffect.MutatesCode)]
    [EmitsStructuredOutput(typeof(LoomLspRenameResult))]
    public static LoomLspRenameResult Rename(string filePath, int line, int column, string newName) =>
        throw new NotImplementedException(
            "LoomLspTools runtime wiring is deferred. Runtime implementation lives in qyl.mcp; composition facade in MAF.Advanced.Patterns.QylLoomExtensions will wire the cross-project dependency.");
}
