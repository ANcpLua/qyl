using System.Net;
using System.Net.Http.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using qyl.mcp.Agents;
using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class SummaryFacadeTests
{
    [Fact]
    public async Task SummarizeErrorAsync_PreservesGeneratedIssueSnakeCaseShape()
    {
        using var client = new HttpClient(new StubHandler(static request =>
        {
            return request.RequestUri?.PathAndQuery switch
            {
                "/api/v1/issues/issue-1" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        title = "Forgejo workflow dispatch failed",
                        error_type = "ForgejoActionFailure",
                        category = "ci",
                        status = "unresolved",
                        priority = "high",
                        occurrence_count = 3,
                        affected_users_count = 1,
                        first_seen_at = "2026-05-10T20:00:00Z",
                        last_seen_at = "2026-05-10T20:05:00Z",
                        culprit = "POST /api/v1/repos/ANcpLua/qyl/actions/workflows/forgejo-doc-research.yml/dispatches"
                    })
                },
                "/api/v1/issues/issue-1/events?limit=5" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        items = new[]
                        {
                            new
                            {
                                timestamp = "2026-05-10T20:05:00Z",
                                message = "workflow dispatch failed",
                                stack_trace = "Forgejo API returned 422"
                            }
                        }
                    })
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }))
        {
            BaseAddress = new Uri("https://collector.test")
        };

        var facade = new SummaryFacade(client, new UnconfiguredAgentsBuilder());

        var summary = await facade.SummarizeErrorAsync("issue-1", TestContext.Current.CancellationToken);

        Assert.Contains("# Error Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Type: ForgejoActionFailure, Category: ci", summary, StringComparison.Ordinal);
        Assert.Contains("Occurrences: 3, Affected Users: 1", summary, StringComparison.Ordinal);
        Assert.Contains("Stack: Forgejo API returned 422", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeTraceAsync_ReadsCollectorTraceEnvelope()
    {
        using var client = new HttpClient(new StubHandler(static request =>
        {
            Assert.Equal("/api/v1/traces/trace-1", request.RequestUri?.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    spans = new[]
                    {
                        new
                        {
                            traceId = "trace-1",
                            spanId = "span-1",
                            parentSpanId = (string?)null,
                            name = "forgejo.workflow.dispatch",
                            serviceName = "qyl.mcp",
                            startTime = "2026-05-10T20:00:00.0000000Z",
                            durationMs = 25.0,
                            status = "error",
                            statusMessage = "workflow failed"
                        }
                    },
                    durationMs = 25.0,
                    status = "error"
                })
            };
        }))
        {
            BaseAddress = new Uri("https://collector.test")
        };

        var facade = new SummaryFacade(client, new UnconfiguredAgentsBuilder());

        var summary = await facade.SummarizeTraceAsync("trace-1", TestContext.Current.CancellationToken);

        Assert.Contains("# Trace Summary", summary, StringComparison.Ordinal);
        Assert.Contains("Total Spans: 1", summary, StringComparison.Ordinal);
        Assert.Contains("forgejo.workflow.dispatch [ERROR]", summary, StringComparison.Ordinal);
        Assert.Contains("Error: workflow failed", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeTraceAsync_ReturnsNotFoundMessageForCollectorNotFound()
    {
        using var client = new HttpClient(new StubHandler(static _ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("https://collector.test")
        };

        var facade = new SummaryFacade(client, new UnconfiguredAgentsBuilder());

        var summary = await facade.SummarizeTraceAsync("missing-trace", TestContext.Current.CancellationToken);

        Assert.Equal("Trace 'missing-trace' not found or contains no spans.", summary);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }

    private sealed class UnconfiguredAgentsBuilder : IQylMcpAgentsBuilder
    {
        public bool IsConfigured => false;

        public AIAgent BuildSummarizeErrorAgent() => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildSummarizeTraceAgent() => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildSummarizeSessionAgent() => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildTestGenerationAgent() => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildAssistedQueryAgent(int rowLimit) => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildUseQylAgent(IReadOnlyList<AITool> tools) => throw new InvalidOperationException("No agent configured.");

        public AIAgent BuildRcaAgent(IReadOnlyList<AITool> tools) => throw new InvalidOperationException("No agent configured.");
    }
}
