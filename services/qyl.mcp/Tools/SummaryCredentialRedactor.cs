using System.Text.RegularExpressions;

namespace qyl.mcp.Tools;

internal static partial class SummaryCredentialRedactor
{
    public static string Redact(string value)
    {
        if (value.Length is 0)
            return value;

        var redacted = AuthorizationHeaderRegex().Replace(value, "$1<redacted>");
        redacted = EnvironmentTokenAssignmentRegex().Replace(redacted, "$1<redacted>");
        redacted = CurlUserRegex().Replace(redacted, "$1<redacted>");
        redacted = TokenQueryParameterRegex().Replace(redacted, "$1<redacted>");
        redacted = JsonTokenPropertyRegex().Replace(redacted, "$1<redacted>");
        redacted = YamlTokenPropertyRegex().Replace(redacted, "$1<redacted>");
        redacted = ForgejoOtpRegex().Replace(redacted, "$1<redacted>");
        return UrlUserInfoRegex().Replace(redacted, "$1<redacted>@");
    }

    [GeneratedRegex("""(Authorization:\s*(?:Bearer|token|Basic)\s+)[^\s"']+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex("""\b((?:export\s+)?(?:FORGEJO_API_TOKEN|FORGEJO_TOKEN|GITEA_TOKEN|GITHUB_TOKEN|INPUTS_TOKEN|FORGEJO__security__INTERNAL_TOKEN)\s*=\s*)(?:"[^"]*"|'[^']*'|[^\s;&|]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentTokenAssignmentRegex();

    [GeneratedRegex("""(\s(?:-u|--user)(?:=|\s+))(?:"[^"]+:[^"]*"|'[^']+:[^']+'|[^\s"']+:[^\s"']+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurlUserRegex();

    [GeneratedRegex("""([?&](?:token|access_token)=)[^&\s"'#]+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenQueryParameterRegex();

    [GeneratedRegex("""("(?:token|access_token)"\s*:\s*)(?:"[^"]*"|'[^']*'|[^,}\]\s]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonTokenPropertyRegex();

    [GeneratedRegex("""(\btoken:\s*)[A-Fa-f0-9]{24,}""",
        RegexOptions.CultureInvariant)]
    private static partial Regex YamlTokenPropertyRegex();

    [GeneratedRegex("""(X-Forgejo-OTP:\s*)\d{6}""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForgejoOtpRegex();

    [GeneratedRegex("""(https?://)[^@\s/:]+:[^@\s/]+@""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlUserInfoRegex();
}
