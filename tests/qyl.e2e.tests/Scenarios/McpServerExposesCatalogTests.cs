using System.Net;
using Qyl.E2E.Tests.Topology;

namespace Qyl.E2E.Tests.Scenarios;

/// <summary>
/// E2E scenario for the qyl-mcp service: an agent / connector / operator
/// fetches the public LLM-discovery document (<c>/llms.txt</c>) from the
/// running container and sees the expected catalog shape.
///
/// This is the smallest meaningful end-to-end claim against qyl-mcp:
///   1. The Native AOT image built.
///   2. The ASP.NET host booted and bound to the configured port.
///   3. The skill configuration and capability registry loaded.
///   4. The source-generator-emitted <c>QylToolManifest</c> and
///      <c>QylCapabilityCatalog</c> are populated.
///   5. The HTTP discovery surface returns the document agent connectors
///      (Anthropic, OpenAI, custom) consume to advertise tools.
///
/// Anything less wouldn't catch an AOT-trimming regression, a missed source
/// generator run, or a misconfigured skill bundle — all of which would still
/// pass a plain <c>/alive</c> liveness probe.
/// </summary>
[Trait("Category", "E2E")]
[Collection(E2ECollection.Name)]
public sealed class McpServerExposesCatalogTests(QylTopologyFixture topology)
{
    [Fact]
    public async Task LlmsTxtAdvertisesToolCatalogAndDiscoveryTools()
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = new HttpClient { BaseAddress = topology.McpBaseUrl };

        using var response = await http.GetAsync("llms.txt", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the MCP container must serve the public agent-discovery document");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain",
            "llms.txt is a plain-text agent discovery surface — not HTML, not JSON");

        var body = await response.Content.ReadAsStringAsync(ct);

        body.Should().StartWith("# qyl MCP Server",
            "the document must announce itself with the qyl heading agent connectors look for");
        body.Should().Contain(
            "qyl exposes observability tools for traces, logs, errors, builds, analytics, RCA, and AI workflows.",
            "the server summary line must reach the wire — proves QylServerMetadata.Summary was not trimmed away");
        body.Should().Contain("- Transport: Streamable HTTP",
            "the transport line must declare the documented Streamable HTTP transport");
        body.Should().MatchRegex(@"- Tool count: [1-9][0-9]*",
            "the tool catalog must report at least one enabled tool — a zero or missing count means QylToolManifest never populated");
        body.Should().MatchRegex(@"- Capability count: [1-9][0-9]*",
            "the capability registry must report at least one enabled capability — proves skill config loaded");
        body.Should().Contain("`qyl.list_capabilities`",
            "the discovery-tools line must point at qyl.list_capabilities — connectors rely on this for catalog walks");
        body.Should().Contain("`qyl.get_capability_guide`",
            "the discovery-tools line must point at qyl.get_capability_guide — paired with list_capabilities");
        body.Should().Contain("## Enabled Capabilities",
            "the document must include the enabled-capabilities section header so connectors can parse the list");
    }
}
