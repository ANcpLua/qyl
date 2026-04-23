// Alias the library's parser to dodge the collision with the qyl.collector record struct of
// the same name (Qyl.Collector.Ingestion.SemconvVersion — the version record, not the parser).

using SemconvVersionParser = ANcpLua.Roslyn.Utilities.OTel.SemconvVersion;
using Qyl.Contracts.Generated;

namespace Qyl.Collector.Observe;

/// <summary>
///     Negotiates schema version compatibility between a subscriber's declared semconv pin
///     and the collector's deployed semconv version.
/// </summary>
internal static class SchemaVersionNegotiator
{
    /// <summary>Collector's deployed semconv version (matches DomainContracts.SchemaVersion).</summary>
    internal const string CollectorVersion = DomainContracts.SchemaVersion;

    // ── Negotiation logic ─────────────────────────────────────────────────────

    /// <summary>
    ///     Evaluates compatibility between <paramref name="requestedVersion" /> (from the subscriber)
    ///     and <see cref="CollectorVersion" /> (deployed).
    /// </summary>
    internal static NegotiationResult Negotiate(string? requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
            return new NegotiationResult.Accept(CollectorVersion, null);

        if (string.Equals(requestedVersion, CollectorVersion, StringComparison.OrdinalIgnoreCase))
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);

        // SemconvVersion returns true with majors==0 on unknown shapes — IsMajorCompatible
        // is permissive in that case (both unparseable → compatible), matching prior behaviour.
        if (!SemconvVersionParser.TryParse(CollectorVersion, out var cMajor, out _, out _) ||
            !SemconvVersionParser.TryParse(requestedVersion, out var rMajor, out _, out _))
        {
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
        }

        if (cMajor != rMajor)
        {
            return new NegotiationResult.Reject(
                $"Incompatible semconv major version: collector={CollectorVersion}, requested={requestedVersion}",
                CollectorVersion,
                requestedVersion);
        }

        // Minor/patch delta → Accept (potentially with schema drift, but not blocking)
        return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
    }

    // ── Result DU ─────────────────────────────────────────────────────────────

    internal abstract record NegotiationResult
    {
        /// <summary>Versions are compatible. Subscription proceeds.</summary>
        internal sealed record Accept(string CollectorVersion, string? RequestedVersion)
            : NegotiationResult;

        /// <summary>Versions are incompatible. Subscription is refused.</summary>
        internal sealed record Reject(string Reason, string CollectorVersion, string? RequestedVersion)
            : NegotiationResult;

        /// <summary>
        ///     Reserved: versions differ but a known attribute rename mapping exists.
        ///     Not implemented — declared here so future call sites require no changes.
        /// </summary>
        internal sealed record Transform(string CollectorVersion, string RequestedVersion)
            : NegotiationResult;
    }
}
