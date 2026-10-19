using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Session;

namespace Qyl;

/// <summary>The names a Qyl API and its consumers agree on, so neither side spells them twice.</summary>
public static partial class QylApiContract
{
    /// <summary>
    /// The resource attribute carrying the SHA-256 of the committed OpenAPI document, as <c>sha256:&lt;hex&gt;</c>.
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
    /// The qyl session key: the <c>baggage</c> request-header member an agent sends and the span tag a Qyl API
    /// stamps from it. One name, and the registry's — a rename upstream has to break this compilation rather
    /// than leave the producer writing a key the collector no longer groups sessions by.
    /// </summary>
    public const string SessionIdName = SessionAttributes.Id;

    /// <summary>
    /// The longest <c>session.id</c> a Qyl API accepts from the wire, and the bound in
    /// <see cref="SessionIdPattern"/>. The value becomes a storage key in the collector, so an unbounded header
    /// must not become an unbounded key; a longer member is ignored rather than truncated, because a truncated
    /// id would silently group two agents' work into one session.
    /// </summary>
    public const int MaxSessionIdLength = 128;

    /// <summary>
    /// The agent contract for a session id, as a pattern: unreserved URI characters only
    /// (<c>A-Z a-z 0-9 . _ ~ -</c>), at least one, at most <see cref="MaxSessionIdLength"/>.
    /// </summary>
    /// <remarks>
    /// An allow-list, because everything it excludes has a use elsewhere. The decoded value becomes a path
    /// segment in the collector's read API and a key in its storage, so <c>/</c> would let a request name a
    /// different resource, <c>%</c> would re-enter decoding downstream, and the Unicode line and bidi
    /// separators (U+2028, U+2029, U+200E) would let two different ids print identically to whoever reads the
    /// session list. None of that is a session id, and a header is written by whoever is calling.
    /// <para>
    /// The dot is allowed inside an id (<c>agent.1</c>) and a value of nothing but dots is not: <c>.</c> and
    /// <c>..</c> are path segments before they are names, and they pass a plain unreserved-character rule.
    /// </para>
    /// </remarks>
    public const string SessionIdPattern = @"^(?!\.+$)[A-Za-z0-9._~-]{1,128}$";

    /// <summary>Whether <paramref name="candidate"/> is a session id a Qyl API will accept from the wire.</summary>
    public static bool IsSessionId([NotNullWhen(true)] string? candidate) =>
        candidate is not null && SessionId().IsMatch(candidate);

    [GeneratedRegex(SessionIdPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SessionId();
}
