

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Enduser;

public static class EnduserAttributes
{
    public const string Id = "enduser.id";

    public const string PseudoId = "enduser.pseudo.id";

    [global::System.Obsolete("Use `user.roles` instead.", false)]
    public const string Role = "enduser.role";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Scope = "enduser.scope";
}
