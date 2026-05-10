using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class SummaryCredentialRedactorTests
{
    [Fact]
    public void Redact_RemovesForgejoAndHttpCredentials()
    {
        var syntheticRunnerToken = new string('a', 40);
        var quotedSyntheticRunnerToken = new string('b', 40);
        var configSyntheticRunnerToken = new string('c', 40);
        var literalSyntheticRunnerToken = new string('d', 40);
        var uppercaseConfigSyntheticRunnerToken = new string('e', 40);
        const string nonHexRunnerToken = "Sk9wHjBHelH4n1ckQy-mo3KVYRdoaPZ_aaH1ATfgI05";
        var input = $$"""
                      -u start:secret
                      Authorization: Bearer abc.def-123
                      Authorization: Basic dXNlcjpwYXNz
                      export FORGEJO_API_TOKEN="secret-token"
                      export FORGEJO_RUNNER_TOKEN="runner-env-token"
                      export FORGEJO_RUNNER_SECRET='runner-env-secret'
                      INPUT_TOKEN=github-action-input-token
                      -u start:secret
                      --user=start-option:secret
                      curl --user root:admin1234 https://example.test
                      curl -u "alice:password" https://example.test
                      forgejo actions register --secret "shared-secret-value"
                      forgejo-runner register --token runner-registration-token
                      forgejo admin user create --username root --password admin-password --email root@example.test
                      forgejo dump-repo --auth_token "cli-personal-token" --auth_password=cli-password --auth_username cli-user
                      https://root:admin1234@example.test/root/repo
                      GET /api/v1/repos/a/b/actions/runners?token=abc123&other=1
                      GET /api/v1/repos/migrate?auth_token=abc456&other=1
                      PUT /api/v1/repos/a/b/actions/secrets/MY_SECRET {"data":"repo-secret-value"}
                      {"token":"runner-secret","access_token":"api-secret"}
                      {"auth_token":"migrate-token","auth_password":"migrate-password","auth_username":"migrate-user"}
                      {"authorization_header":"Bearer webhook-secret","client_secret":"oauth-client-secret","remote_password":"mirror-password","password":"user-password"}
                      TOKEN: {{syntheticRunnerToken}}
                      Token: "{{quotedSyntheticRunnerToken}}"
                      token: {{syntheticRunnerToken}}
                      token: "{{quotedSyntheticRunnerToken}}"
                      token = "{{configSyntheticRunnerToken}}";
                      TOKEN: "{{uppercaseConfigSyntheticRunnerToken}}"
                      kubectl create secret generic forgejo-registration --from-literal=token={{literalSyntheticRunnerToken}}
                      token: "{{nonHexRunnerToken}}"
                      X-Forgejo-OTP: 123456
                      X-Gitea-OTP: 123456
                      """;

        var redacted = SummaryCredentialRedactor.Redact(input);

        Assert.DoesNotContain("abc.def-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("start:secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("dXNlcjpwYXNz", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("runner-env-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("runner-env-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("github-action-input-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("root:admin1234", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("start:secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("start-option:secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("alice:password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-secret-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("runner-registration-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc456", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("repo-secret-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("runner-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate-user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-personal-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cli-user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("webhook-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("oauth-client-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("mirror-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("user-password", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(syntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(quotedSyntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(configSyntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(uppercaseConfigSyntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(literalSyntheticRunnerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(nonHexRunnerToken, redacted, StringComparison.Ordinal);
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
