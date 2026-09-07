namespace Qyl;

/// <summary>The names a Qyl API and its consumers agree on, so neither side spells them twice.</summary>
public static class QylApiContract
{
    /// <summary>
    /// The resource attribute carrying the SHA-256 of the committed OpenAPI document, in lowercase hex.
    /// Every span a Qyl API exports is attributable to the exact contract the binary was compiled against;
    /// the value comes from MSBuild, not from anything the process can compute about itself at run time.
    /// </summary>
    public const string RevisionAttributeName = "qyl.api.contract.revision";

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
