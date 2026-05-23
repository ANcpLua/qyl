using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class SummaryCredentialRedactorTests
{
    [Theory]
    [InlineData("Authorization: Bearer abc.def-123", "abc.def-123")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
    [InlineData("export FORGEJO_API_TOKEN=\"secret-token\"", "secret-token")]
    [InlineData("export FORGEJO_RUNNER_TOKEN=\"runner-env-token\"", "runner-env-token")]
    [InlineData("export FORGEJO_RUNNER_SECRET='runner-env-secret'", "runner-env-secret")]
    [InlineData("INPUT_TOKEN=github-action-input-token", "github-action-input-token")]
    [InlineData("curl --user root:admin1234 https://example.test", "root:admin1234")]
    [InlineData("curl -u start:secret https://example.test", "start:secret")]
    [InlineData("curl -u \"alice:password\" https://example.test", "alice:password")]
    [InlineData("curl --user=start-option:secret https://example.test", "start-option:secret")]
    [InlineData("https://root:admin1234@example.test/root/repo", "root:admin1234")]
    [InlineData("forgejo actions register --secret \"shared-secret-value\"", "shared-secret-value")]
    [InlineData("forgejo-runner register --token runner-registration-token", "runner-registration-token")]
    [InlineData("forgejo admin user create --password admin-password", "admin-password")]
    [InlineData("forgejo dump-repo --auth_token \"cli-personal-token\"", "cli-personal-token")]
    [InlineData("forgejo dump-repo --auth_password=cli-password", "cli-password")]
    [InlineData("forgejo dump-repo --auth_username cli-user", "cli-user")]
    [InlineData("GET /api/v1/repos/a/b/actions/runners?token=abc123&other=1", "abc123")]
    [InlineData("GET /api/v1/repos/migrate?auth_token=abc456&other=1", "abc456")]
    [InlineData("PUT /api/v1/repos/a/b/actions/secrets/MY_SECRET {\"data\":\"repo-secret-value\"}", "repo-secret-value")]
    [InlineData("{\"token\":\"runner-secret\"}", "runner-secret")]
    [InlineData("{\"access_token\":\"api-secret\"}", "api-secret")]
    [InlineData("{\"auth_token\":\"migrate-token\"}", "migrate-token")]
    [InlineData("{\"auth_password\":\"migrate-password\"}", "migrate-password")]
    [InlineData("{\"auth_username\":\"migrate-user\"}", "migrate-user")]
    [InlineData("{\"authorization_header\":\"Bearer webhook-secret\"}", "webhook-secret")]
    [InlineData("{\"client_secret\":\"oauth-client-secret\"}", "oauth-client-secret")]
    [InlineData("{\"remote_password\":\"mirror-password\"}", "mirror-password")]
    [InlineData("{\"password\":\"user-password\"}", "user-password")]
    [InlineData("X-Forgejo-OTP: 123456", "123456")]
    [InlineData("X-Gitea-OTP: 123456", "123456")]
    public void Redact_StripsSecretFromInput(string input, string secret) =>
        SummaryCredentialRedactor.Redact(input).Should().NotContain(secret);

    [Theory]
    [InlineData("TOKEN: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Token: \"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"")]
    [InlineData("token = \"cccccccccccccccccccccccccccccccccccccccc\";")]
    [InlineData("token: \"Sk9wHjBHelH4n1ckQy-mo3KVYRdoaPZ_aaH1ATfgI05\"")]
    [InlineData("--from-literal=token=dddddddddddddddddddddddddddddddddddddddd")]
    public void Redact_StripsBareRunnerTokenLiterals(string input)
    {
        var redacted = SummaryCredentialRedactor.Redact(input);

        redacted.Should().Contain("<redacted>");
        redacted.Should().NotContain("aaaa").And.NotContain("bbbb").And.NotContain("cccc")
            .And.NotContain("dddd").And.NotContain("Sk9wHjBH");
    }

    [Theory]
    [InlineData("Trace ID: xyz789")]
    [InlineData("Span Count: 3")]
    [InlineData("GET /api/v1/repos/owner/repo/actions/runners")]
    public void Redact_KeepsNonCredentialSummaryText(string input) =>
        SummaryCredentialRedactor.Redact(input).Should().Be(input);
}
