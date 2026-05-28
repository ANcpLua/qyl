using Microsoft.AspNetCore.Authentication;

namespace Qyl.Collector.Auth;

internal sealed class BearerOpaqueTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "BearerOpaque";

    public bool TouchLastUsed { get; set; } = true;
}
