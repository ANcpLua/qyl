using System.Reflection;

namespace Qyl;

/// <summary>
/// Whether this process is the build-time OpenAPI document tool rather than the API itself.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Extensions.ApiDescription.Server</c> writes the committed contract by running the API's own host inside
/// <c>GetDocument.Insider</c>: it builds the host, reads the document out of it, and never starts it. Nothing is exported
/// from there, but the registration path still runs — and <c>AddQyl()</c>'s collector discovery is four blocking TCP probes
/// (localhost:4318, localhost:4317, qyl:4318, qyl:4317, 100 ms each) plus a DNS lookup for the host <c>qyl</c>. Paying
/// that on every <c>dotnet build</c> of every Qyl API, to find a collector for telemetry that will never be emitted, is
/// waste; worse, on a developer machine with a collector running it makes the build's behaviour depend on it.
/// </para>
/// <para>
/// So <c>AddQylApi</c> turns discovery off here, and only here. This is not a telemetry opt-out: nothing else about
/// <c>AddQyl()</c> changes, and a process that actually serves requests always discovers.
/// </para>
/// </remarks>
public static class QylBuildTimeDocumentHost
{
    /// <summary>The entry assembly the document tool runs the API's host under.</summary>
    private const string DocumentToolAssemblyName = "GetDocument.Insider";

    /// <summary>True when this process is the build-time document tool.</summary>
    public static bool IsCurrentProcess { get; } = Matches(Assembly.GetEntryAssembly()?.GetName().Name);

    internal static bool Matches(string? entryAssemblyName) =>
        string.Equals(entryAssemblyName, DocumentToolAssemblyName, StringComparison.Ordinal);
}
