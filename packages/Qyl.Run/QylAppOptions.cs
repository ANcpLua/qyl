// Copyright (c) 2025-2026 ancplua

using System.ComponentModel.DataAnnotations;

namespace Qyl.Run;

/// <summary>
///     Validated runtime options for <see cref="QylApp" />. Bound via
///     <c>
///         AddOptionsWithValidateOnStart&lt;QylAppOptions&gt;
///         ().BindConfiguration(QylAppOptions.SectionName).ValidateDataAnnotations()
///     </c>
///     .
///     Mirrors the <c>AbcOptions</c> idiom — every path that could misconfigure the runner fails
///     fast on startup rather than later at a process-spawn attempt.
/// </summary>
public sealed class QylAppOptions
{
    /// <summary>Configuration section name (<c>appsettings.json</c> key).</summary>
    public const string SectionName = "Qyl:Run";

    /// <summary>Port the runner's own HTTP surface listens on (the Spectre dashboard's <c>[B]</c> target).</summary>
    [Range(0, 65535, ErrorMessage = $"{SectionName}:{nameof(RunnerPort)} must be a valid TCP port (0 = auto-allocate)")]
    public int RunnerPort { get; set; } = QylConstants.Ports.Dashboard;

    /// <summary>Bind host for the runner's HTTP surface.</summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(RunnerHost)} is required")]
    public string RunnerHost { get; set; } = QylConstants.Network.Loopback;

    /// <summary>
    ///     Global startup timeout — the orchestrator gives up on a resource that doesn't reach
    ///     <see cref="ResourceLifecycle.Ready" /> in this window.
    /// </summary>
    [Range(1, 600, ErrorMessage = $"{SectionName}:{nameof(StartupTimeoutSeconds)} must be 1..600")]
    public int StartupTimeoutSeconds { get; set; } = QylConstants.Orchestrator.StartupTimeoutSeconds;

    /// <summary>
    ///     When <c>true</c>, child-process stdout is streamed into the Spectre UI's "Logs" pane instead of the parent
    ///     console.
    /// </summary>
    public bool CaptureChildOutput { get; set; } = true;
}
