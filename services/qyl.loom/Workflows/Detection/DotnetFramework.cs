// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Workflows.Detection;

/// <summary>
///     .NET application shapes the Loom setup workflow recognises. Each maps to a distinct
///     Sentry NuGet package and init pattern.
/// </summary>
public enum DotnetFramework
{
    /// <summary>Project shape could not be identified.</summary>
    Unknown = 0,

    /// <summary>ASP.NET Core web app — <c>Sentry.AspNetCore</c>.</summary>
    AspNetCore = 1,

    /// <summary>WPF desktop app — <c>Sentry</c> + <c>IsGlobalModeEnabled = true</c>.</summary>
    Wpf = 2,

    /// <summary>WinForms desktop app — <c>Sentry</c> + <c>SetUnhandledExceptionMode(ThrowException)</c>.</summary>
    WinForms = 3,

    /// <summary>.NET MAUI — <c>Sentry.Maui</c>.</summary>
    Maui = 4,

    /// <summary>Blazor WebAssembly — <c>Sentry.AspNetCore.Blazor.WebAssembly</c>.</summary>
    BlazorWasm = 5,

    /// <summary>Azure Functions (Isolated Worker) — <c>Sentry.Extensions.Logging</c> + <c>Sentry.OpenTelemetry</c>.</summary>
    AzureFunctions = 6,

    /// <summary>AWS Lambda — <c>Sentry.AspNetCore</c> + <c>FlushOnCompletedRequest = true</c>.</summary>
    AwsLambda = 7,

    /// <summary>Classic ASP.NET (System.Web) — <c>Sentry.AspNet</c>.</summary>
    ClassicAspNet = 8,

    /// <summary>Console / worker service / generic host — <c>Sentry</c> or <c>Sentry.Extensions.Logging</c>.</summary>
    ConsoleOrWorker = 9,
}
