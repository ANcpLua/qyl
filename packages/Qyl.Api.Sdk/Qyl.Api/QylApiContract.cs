using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl;

namespace Qyl;

/// <summary>The names a Qyl API and its consumers agree on, so neither side spells them twice.</summary>
public static class QylApiContract
{
    /// <summary>
    /// The resource attribute carrying the SHA-256 of the committed OpenAPI document, in lowercase hex.
    /// Every span a Qyl API exports is attributable to the exact contract the binary was compiled against;
    /// the value comes from MSBuild, not from anything the process can compute about itself at run time.
    /// </summary>
    /// <remarks>
    /// The name is the registry's, not this repository's: it is generated into
    /// <c>Qyl.Telemetry.SemanticConventions.Incubating</c> from the Weaver registry, so the collector's ingest
    /// policy and the producer that writes it cannot drift apart by being spelled twice.
    /// </remarks>
    public const string RevisionAttributeName = QylAttributes.ApiContractRevision;

    /// <summary>
    /// The algorithm prefix every <see cref="RevisionAttributeName"/> value carries. The registry defines the value
    /// as <c>sha256:&lt;lowercase hex&gt;</c>, and the collector reports its own revision the same way, so the digest
    /// never travels without the algorithm that produced it: a future algorithm changes the value rather than
    /// silently reinterpreting the old one. <c>Qyl.Sdk.Api.targets</c> writes it; this names it.
    /// </summary>
    public const string RevisionAlgorithmPrefix = "sha256:";

    /// <summary>
    /// The qyl session key. It is the <c>baggage</c> request-header member an agent sends and the span tag a Qyl
    /// API stamps from it — one name on purpose, because the wire and the span are the same fact.
    /// </summary>
    public const string SessionIdName = "session.id";

    /// <summary>
    /// The longest <c>session.id</c> a Qyl API accepts from the wire. The value becomes a storage key in the
    /// collector, so an unbounded header must not become an unbounded key; a longer member is ignored rather
    /// than truncated, because a truncated id would silently group two agents' work into one session.
    /// </summary>
    public const int MaxSessionIdLength = 128;
}
