namespace qyl.collector.Observe;

/// <summary>
/// Negotiates schema version compatibility between a subscriber's declared semconv pin
/// and the collector's deployed semconv version.
/// </summary>
internal static class SchemaVersionNegotiator
{
    /// <summary>Collector's deployed semconv version (matches DomainContracts.SchemaVersion).</summary>
    internal const string CollectorVersion = "semconv-1.40.0";

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
        /// Reserved: versions differ but a known attribute rename mapping exists.
        /// Not implemented — declared here so future call sites require no changes.
        /// </summary>
        internal sealed record Transform(string CollectorVersion, string RequestedVersion)
            : NegotiationResult;
    }

    // ── Negotiation logic ─────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates compatibility between <paramref name="requestedVersion"/> (from the subscriber)
    /// and <see cref="CollectorVersion"/> (deployed).
    /// </summary>
    internal static NegotiationResult Negotiate(string? requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
            return new NegotiationResult.Accept(CollectorVersion, null);

        if (string.Equals(requestedVersion, CollectorVersion, StringComparison.OrdinalIgnoreCase))
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);

        // Parse both versions. Expected format: "semconv-{major}.{minor}.{patch}"
        if (!TryParseSemconv(CollectorVersion, out var collectorParsed) ||
            !TryParseSemconv(requestedVersion, out var requestedParsed))
        {
            // Unrecognised format: accept permissively (do not block unknown version strings)
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
        }

        // Major version mismatch → Reject
        if (collectorParsed.Major != requestedParsed.Major)
        {
            return new NegotiationResult.Reject(
                Reason: $"Incompatible semconv major version: collector={CollectorVersion}, requested={requestedVersion}",
                CollectorVersion: CollectorVersion,
                RequestedVersion: requestedVersion);
        }

        // Minor/patch delta → Accept (potentially with schema drift, but not blocking)
        return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
    }

    // ── Version parsing ───────────────────────────────────────────────────────

    private static bool TryParseSemconv(string version, out (int Major, int Minor, int Patch) parsed)
    {
        // Expected: "semconv-1.40.0"
        const string prefix = "semconv-";
        parsed = default;

        if (!version.StartsWithIgnoreCase(prefix))
            return false;

        var parts = version[prefix.Length..].Split('.');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor))
            return false;

        var patch = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p : 0;
        parsed = (major, minor, patch);
        return true;
    }
}
