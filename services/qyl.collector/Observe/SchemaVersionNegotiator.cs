
using Qyl.Contracts.Generated;
using SemconvVersionParser = ANcpLua.Roslyn.Utilities.OTel.SemconvVersion;

namespace Qyl.Collector.Observe;

internal static class SchemaVersionNegotiator
{
    internal const string CollectorVersion = DomainContracts.SchemaVersion;


    internal static NegotiationResult Negotiate(string? requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
            return new NegotiationResult.Accept(CollectorVersion, null);

        if (string.Equals(requestedVersion, CollectorVersion, StringComparison.OrdinalIgnoreCase))
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);

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

        return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
    }


    internal abstract record NegotiationResult
    {
        internal sealed record Accept(string DeployedVersion, string? RequestedVersion)
            : NegotiationResult;

        internal sealed record Reject(string Reason, string DeployedVersion, string? RequestedVersion)
            : NegotiationResult;

        internal sealed record Transform(string DeployedVersion, string RequestedVersion)
            : NegotiationResult;
    }
}
