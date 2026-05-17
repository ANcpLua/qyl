using System.Net.Http.Json;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Qyl.E2E.Tests.Bootstrap;

[Trait("Category", "E2EBootstrap")]
public sealed class WireMockLlmSeamTests
{
    [Fact]
    public async Task ScriptedChatCompletion_RoundtripsAndIsRecordedInLogEntries()
    {
        using var llm = WireMockServer.Start();

        llm.Given(Request.Create().WithPath("/v1/chat/completions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "chatcmpl-bootstrap",
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = "Bearer secret-token-12345" } },
                    },
                }));

        var ct = TestContext.Current.CancellationToken;
        using var client = new HttpClient { BaseAddress = new Uri(llm.Url!) };
        var request = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = "summarize this" } },
        };
        using var response = await client.PostAsJsonAsync("/v1/chat/completions", request, ct);

        response.IsSuccessStatusCode.Should().BeTrue(
            "the WireMock stub must respond 200 for the configured route");

        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        payload.RootElement.GetProperty("id").GetString().Should().Be("chatcmpl-bootstrap");

        llm.LogEntries.Should().ContainSingle(
            "WireMock must record the single POST /v1/chat/completions for assertion replay");
    }
}
