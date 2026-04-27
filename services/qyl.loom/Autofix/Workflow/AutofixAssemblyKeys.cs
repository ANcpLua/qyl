// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

/// Shared scope + key constants for cross-executor durable state. The scope
/// participates in MAF's checkpoint persistence, so writes survive process
/// restarts — `ReportExecutor` falls back to these keys when the in-process
/// `AutofixReportAssemblyState` snapshot is empty after a resume.
internal static class AutofixAssemblyKeys
{
    public const string Scope = "autofix.assembly";

    public const string Fixability = "fixability";
    public const string Context = "context";
    public const string Hypothesis = "hypothesis";
    public const string Solution = "solution";
    public const string Confidence = "confidence";
}
