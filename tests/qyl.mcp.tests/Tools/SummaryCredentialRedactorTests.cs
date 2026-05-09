using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class SummaryCredentialRedactorTests
{
    [Fact]
    public void Redact_RemovesForgejoAndHttpCredentials()
    {
        var syntheticRunnerToken = new string('a', 40);
        var input = $$"""
                      Authorization: Bearer abc.def-123
                      Authorization: Basic dXNlcjpwYXNz
                      export FORGEJO_API_TOKEN="secret-token"
                      curl --user root:admin1234 https://example.test
                      curl -u "alice:password" https://example.test
                      forgejo dump-repo --auth_token "cli-personal-token" --auth_password=cli-password --auth_username cli-user
                      https://root:admin1234@example.test/root/repo
                      GET /api/v1/repos/a/b/actions/runners?token=abc123&other=1
                      GET /api/v1/repos/migrate?auth_token=abc456&other=1
                      {"token":"runner-secret","access_token":"api-secret"}
                      {"auth_token":"migrate-token","auth_password":"migrate-password","auth_username":"migrate-user"}
                      token: {{syntheticRunnerToken}}
                      X-Forgejo-OTP: 123456
                      X-Gitea-OTP: 123456
                      """;

        var redacted = SummaryCredentialRedactor.Redact(input);

        Assert.DoesNotContain("abc.def-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("dXNlcjpwYXNz", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("root:admin1234", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("alice:password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc456", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("runner-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-personal-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(syntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", redacted, StringComparison.Ordinal);
        Assert.Contains("<redacted>", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_KeepsNonCredentialSummaryText()
    {
        const string input = "Trace ID: xyz789\nSpan Count: 3\nGET /api/v1/repos/owner/repo/actions/runners";

        var redacted = SummaryCredentialRedactor.Redact(input);

        Assert.Equal(input, redacted);
    }
}
