using System.Reflection;

namespace Qyl;

/// <summary>
/// Whether this process is the build-time OpenAPI document tool rather than the API itself.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Extensions.ApiDescription.Server</c> writes the committed contract by running the API's own host inside
/// <c>GetDocument.Insider</c>: it builds the host, reads the document out of it, and never starts it. Two things still
/// happen there. Collector discovery runs during registration — four blocking TCP probes (localhost:4318, localhost:4317,
/// qyl:4318, qyl:4317, 100 ms each) plus a DNS lookup for the host <c>qyl</c> — on every <c>dotnet build</c> of every Qyl
/// API, to find a collector for telemetry the build will not emit. And the OpenTelemetry logger provider is built by
/// <c>Build()</c> itself, unlike the tracer and meter providers, which a hosted service materialises at host start: so
/// every build constructs a batch log exporter and flushes it into a collector on dispose.
/// </para>
/// <para>
/// <c>AddQylApi</c> therefore turns discovery and log export off here, and requires a configured endpoint so nothing
/// falls back to localhost. Only here: nothing else about <c>AddQyl()</c> changes, and a process that serves requests
/// discovers, exports logs, and falls back exactly as before.
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
