using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Qyl.Collector.Cost;

namespace Qyl.Collector.Tests;

public sealed class ProviderCostSourceTests
{
    private static readonly string[] s_openAiGroupBy = ["project_id", "line_item"];
    private static readonly DateTimeOffset s_periodStart =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_periodEnd =
        new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_retrievedAt =
        new(2026, 1, 4, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpenAI_costs_use_admin_auth_and_preserve_provider_aggregate_dimensions_across_pages()
    {
        var firstPage = $$"""
                          {
                            "data": [{
                              "start_time": {{s_periodStart.ToUnixTimeSeconds()}},
                              "end_time": {{s_periodStart.AddDays(1).ToUnixTimeSeconds()}},
                              "results": [{
                                "amount": { "value": 12.345678, "currency": "usd" },
                                "project_id": "proj_alpha",
                                "line_item": "api_usage"
                              }]
                            }],
                            "has_more": true,
                            "next_page": "page two/with spaces"
                          }
                          """;
        var secondPage = $$"""
                           {
                             "data": [{
                               "start_time": {{s_periodStart.AddDays(1).ToUnixTimeSeconds()}},
                               "end_time": {{s_periodEnd.ToUnixTimeSeconds()}},
                               "results": [{
                                 "amount": { "value": 0.75, "currency": "eur" },
                                 "project_id": null,
                                 "line_item": "storage"
                               }]
                             }],
                             "has_more": false,
                             "next_page": null
                           }
                           """;
        using var handler = new RecordingHandler(
            _ => JsonResponse(firstPage),
            _ => JsonResponse(secondPage));
        using var client = new HttpClient(handler);
        var source = new OpenAiOrganizationCostsSource(
            client,
            new FixedTimeProvider(s_retrievedAt),
            new OpenAiOrganizationCostsOptions("openai-admin-secret"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Failure);
        Assert.Equal(s_periodStart, result.PeriodStart);
        Assert.Equal(s_periodEnd, result.PeriodEnd);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(2, result.CoveredPeriods.Count);

        var first = result.Records[0];
        Assert.Equal("openai", first.Provider);
        Assert.Equal(s_periodStart, first.PeriodStart);
        Assert.Equal(s_periodStart.AddDays(1), first.PeriodEnd);
        Assert.Equal(12.345678m, first.Amount);
        Assert.Equal("usd", first.CurrencyCode);
        Assert.Equal(s_retrievedAt, first.RetrievedAt);
        Assert.Equal(source.SourceEndpoint, first.SourceEndpoint);
        Assert.Equal(ProviderCostAttribution.ProviderAggregate, first.Attribution);
        Assert.Equal("proj_alpha", first.ProviderProjectId);
        Assert.Equal("api_usage", first.LineItem);
        Assert.Null(first.ModelName);

        var second = result.Records[1];
        Assert.Equal(0.75m, second.Amount);
        Assert.Equal("eur", second.CurrencyCode);
        Assert.Null(second.ProviderProjectId);
        Assert.Equal("storage", second.LineItem);
        Assert.Null(second.ModelName);

        Assert.Equal(2, handler.Requests.Count);
        foreach (var request in handler.Requests)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(source.SourceEndpoint.GetLeftPart(UriPartial.Path), request.Uri.GetLeftPart(UriPartial.Path));
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("openai-admin-secret", request.AuthorizationParameter);

            var query = QueryHelpers.ParseQuery(request.Uri.Query);
            Assert.Equal(
                s_periodStart.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                query["start_time"].ToString());
            Assert.Equal(
                s_periodEnd.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                query["end_time"].ToString());
            Assert.Equal("1d", query["bucket_width"].ToString());
            Assert.Equal("180", query["limit"].ToString());
            Assert.Equal(s_openAiGroupBy, query["group_by"].ToArray());
        }

        Assert.False(QueryHelpers.ParseQuery(handler.Requests[0].Uri.Query).ContainsKey("page"));
        Assert.Equal(
            "page two/with spaces",
            QueryHelpers.ParseQuery(handler.Requests[1].Uri.Query)["page"].ToString());
    }

    [Fact]
    public async Task Anthropic_cost_report_uses_admin_headers_normalizes_cents_and_only_uses_returned_model()
    {
        const string firstPage = """
                                 {
                                   "data": [{
                                     "starting_at": "2026-01-01T00:00:00Z",
                                     "ending_at": "2026-01-02T00:00:00Z",
                                     "results": [{
                                       "amount": "123.45",
                                       "currency": "USD",
                                       "description": "Claude Sonnet token usage",
                                       "model": "claude-sonnet-4-20250514"
                                     }]
                                   }],
                                   "has_more": true,
                                   "next_page": "page_anthropic_2"
                                 }
                                 """;
        const string secondPage = """
                                  {
                                    "data": [{
                                      "starting_at": "2026-01-02T00:00:00+00:00",
                                      "ending_at": "2026-01-03T00:00:00+00:00",
                                      "results": [{
                                        "amount": "50.000000",
                                        "currency": "USD",
                                        "description": "Claude Opus web search usage",
                                        "model": null
                                      }]
                                    }],
                                    "has_more": false,
                                    "next_page": null
                                  }
                                  """;
        using var handler = new RecordingHandler(
            _ => JsonResponse(firstPage),
            _ => JsonResponse(secondPage));
        using var client = new HttpClient(handler);
        var source = new AnthropicCostReportSource(
            client,
            new FixedTimeProvider(s_retrievedAt),
            new AnthropicCostReportOptions("anthropic-admin-secret"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Records.Count);
        var modelCost = result.Records[0];
        Assert.Equal("anthropic", modelCost.Provider);
        Assert.Equal(1.2345m, modelCost.Amount);
        Assert.Equal("USD", modelCost.CurrencyCode);
        Assert.Equal(s_retrievedAt, modelCost.RetrievedAt);
        Assert.Equal(source.SourceEndpoint, modelCost.SourceEndpoint);
        Assert.Equal(ProviderCostAttribution.ProviderReportedModel, modelCost.Attribution);
        Assert.Equal("Claude Sonnet token usage", modelCost.LineItem);
        Assert.Equal("claude-sonnet-4-20250514", modelCost.ModelName);
        Assert.Null(modelCost.ProviderProjectId);

        var unmodeledCost = result.Records[1];
        Assert.Equal(0.5m, unmodeledCost.Amount);
        Assert.Equal(ProviderCostAttribution.ProviderAggregate, unmodeledCost.Attribution);
        Assert.Equal("Claude Opus web search usage", unmodeledCost.LineItem);
        Assert.Null(unmodeledCost.ModelName);

        Assert.Equal(2, handler.Requests.Count);
        foreach (var request in handler.Requests)
        {
            Assert.Null(request.AuthorizationScheme);
            Assert.Equal("anthropic-admin-secret", Assert.Single(request.HeaderValues["x-api-key"]));
            Assert.Equal("2023-06-01", Assert.Single(request.HeaderValues["anthropic-version"]));

            var query = QueryHelpers.ParseQuery(request.Uri.Query);
            Assert.Equal("2026-01-01T00:00:00.0000000Z", query["starting_at"].ToString());
            Assert.Equal("2026-01-03T00:00:00.0000000Z", query["ending_at"].ToString());
            Assert.Equal("1d", query["bucket_width"].ToString());
            Assert.Equal("31", query["limit"].ToString());
            Assert.Equal(2, query["group_by[]"].Count);
            Assert.Equal("workspace_id", query["group_by[]"][0]);
            Assert.Equal("description", query["group_by[]"][1]);
        }

        Assert.False(QueryHelpers.ParseQuery(handler.Requests[0].Uri.Query).ContainsKey("page"));
        Assert.Equal(
            "page_anthropic_2",
            QueryHelpers.ParseQuery(handler.Requests[1].Uri.Query)["page"].ToString());
    }

    [Fact]
    public async Task OpenAI_project_scope_is_sent_as_an_official_filter_and_rechecked_exactly()
    {
        var payload = $$"""
                        {
                          "data": [{
                            "start_time": {{s_periodStart.ToUnixTimeSeconds()}},
                            "end_time": {{s_periodStart.AddDays(1).ToUnixTimeSeconds()}},
                            "results": [
                              {
                                "amount": { "value": 1.25, "currency": "usd" },
                                "project_id": "proj_qyl / exact",
                                "line_item": "selected"
                              },
                              {
                                "amount": { "value": 99, "currency": "usd" },
                                "project_id": "proj_other",
                                "line_item": "other"
                              }
                            ]
                          }],
                          "has_more": false,
                          "next_page": null
                        }
                        """;
        using var handler = new RecordingHandler(_ => JsonResponse(payload));
        using var client = new HttpClient(handler);
        var source = new OpenAiOrganizationCostsSource(
            client,
            TimeProvider.System,
            new OpenAiOrganizationCostsOptions("admin-secret", "proj_qyl / exact"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Records);
        Assert.Equal(1.25m, record.Amount);
        Assert.Equal("proj_qyl / exact", record.ProviderProjectId);
        var query = QueryHelpers.ParseQuery(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("proj_qyl / exact", query["project_ids"].ToString());
    }

    [Fact]
    public async Task Anthropic_workspace_scope_keeps_only_the_exact_provider_returned_workspace()
    {
        const string payload = """
                               {
                                 "data": [{
                                   "starting_at": "2026-01-01T00:00:00Z",
                                   "ending_at": "2026-01-02T00:00:00Z",
                                   "results": [
                                     {
                                       "amount": "100",
                                       "currency": "USD",
                                       "description": "selected",
                                       "workspace_id": "wrk_qyl",
                                       "model": "claude-test"
                                     },
                                     {
                                       "amount": "900",
                                       "currency": "USD",
                                       "description": "other",
                                       "workspace_id": "wrk_other",
                                       "model": "claude-test"
                                     }
                                   ]
                                 }],
                                 "has_more": false,
                                 "next_page": null
                               }
                               """;
        using var handler = new RecordingHandler(_ => JsonResponse(payload));
        using var client = new HttpClient(handler);
        var source = new AnthropicCostReportSource(
            client,
            TimeProvider.System,
            new AnthropicCostReportOptions(
                "admin-secret",
                ProviderCostScope.ForIdentifier("wrk_qyl")));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        var record = Assert.Single(result.Records);
        Assert.Equal(1m, record.Amount);
        Assert.Equal("wrk_qyl", record.ProviderProjectId);
    }

    [Fact]
    public async Task Anthropic_default_workspace_scope_matches_only_the_provider_null_workspace()
    {
        const string payload = """
                               {
                                 "data": [{
                                   "starting_at": "2026-01-01T00:00:00Z",
                                   "ending_at": "2026-01-02T00:00:00Z",
                                   "results": [
                                     {
                                       "amount": "125",
                                       "currency": "USD",
                                       "description": "default",
                                       "workspace_id": null,
                                       "model": "claude-test"
                                     },
                                     {
                                       "amount": "900",
                                       "currency": "USD",
                                       "description": "named",
                                       "workspace_id": "wrk_other",
                                       "model": "claude-test"
                                     }
                                   ]
                                 }],
                                 "has_more": false,
                                 "next_page": null
                               }
                               """;
        using var handler = new RecordingHandler(_ => JsonResponse(payload));
        using var client = new HttpClient(handler);
        var source = new AnthropicCostReportSource(
            client,
            TimeProvider.System,
            new AnthropicCostReportOptions("admin-secret", ProviderCostScope.DefaultWorkspace));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        var record = Assert.Single(result.Records);
        Assert.Equal(1.25m, record.Amount);
        Assert.Null(record.ProviderProjectId);
    }

    [Fact]
    public async Task Missing_or_header_unsafe_credentials_fail_without_an_HTTP_request()
    {
        using var handler = new RecordingHandler(
            _ => throw new InvalidOperationException("No request was expected."));
        using var client = new HttpClient(handler);

        var missing = await new OpenAiOrganizationCostsSource(
                client,
                TimeProvider.System,
                new OpenAiOrganizationCostsOptions("   "))
            .FetchAsync(s_periodStart, s_periodEnd, TestContext.Current.CancellationToken);
        var invalid = await new AnthropicCostReportSource(
                client,
                TimeProvider.System,
                new AnthropicCostReportOptions("secret\r\ninjected: header"))
            .FetchAsync(s_periodStart, s_periodEnd, TestContext.Current.CancellationToken);

        Assert.Equal(ProviderCostFailureCategory.MissingCredential, missing.Failure?.Category);
        Assert.Equal(ProviderCostFailureCategory.InvalidCredential, invalid.Failure?.Category);
        Assert.Empty(missing.Records);
        Assert.Empty(invalid.Records);
        Assert.Empty(handler.Requests);
    }

    public static TheoryData<HttpStatusCode, int> SafeStatusFailures =>
        new()
        {
            { HttpStatusCode.Unauthorized, (int)ProviderCostFailureCategory.Authentication },
            { HttpStatusCode.Forbidden, (int)ProviderCostFailureCategory.Authorization },
            { HttpStatusCode.TooManyRequests, (int)ProviderCostFailureCategory.RateLimited },
            { HttpStatusCode.ServiceUnavailable, (int)ProviderCostFailureCategory.ProviderUnavailable },
            { (HttpStatusCode)418, (int)ProviderCostFailureCategory.UnexpectedResponseStatus }
        };

    [Theory]
    [MemberData(nameof(SafeStatusFailures))]
    public async Task Provider_status_failures_return_only_safe_categories(
        HttpStatusCode statusCode,
        int expectedCategory)
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("credential=must-not-surface")
        });
        using var client = new HttpClient(handler);
        var source = new OpenAiOrganizationCostsSource(
            client,
            TimeProvider.System,
            new OpenAiOrganizationCostsOptions("admin-secret"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal((ProviderCostFailureCategory)expectedCategory, result.Failure?.Category);
        Assert.Equal(statusCode, result.Failure?.StatusCode);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task Invalid_provider_payload_discards_partial_records_and_reports_invalid_response()
    {
        const string payload = """
                               {
                                 "data": [{
                                   "starting_at": "2026-01-01T00:00:00Z",
                                   "ending_at": "2026-01-02T00:00:00Z",
                                   "results": [
                                     { "amount": "100", "currency": "USD", "model": "claude-valid" },
                                     { "amount": "not-money", "currency": "USD", "model": "claude-invalid" }
                                   ]
                                 }],
                                 "has_more": false,
                                 "next_page": null
                               }
                               """;
        using var handler = new RecordingHandler(_ => JsonResponse(payload));
        using var client = new HttpClient(handler);
        var source = new AnthropicCostReportSource(
            client,
            TimeProvider.System,
            new AnthropicCostReportOptions("admin-secret"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProviderCostFailureCategory.InvalidResponse, result.Failure?.Category);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task Provider_payload_rejects_negative_cost_and_out_of_range_daily_buckets()
    {
        var negative = $$"""
                         {
                           "data": [{
                             "start_time": {{s_periodStart.ToUnixTimeSeconds()}},
                             "end_time": {{s_periodStart.AddDays(1).ToUnixTimeSeconds()}},
                             "results": [{
                               "amount": { "value": -0.01, "currency": "usd" },
                               "project_id": "proj_qyl",
                               "line_item": "invalid"
                             }]
                           }],
                           "has_more": false,
                           "next_page": null
                         }
                         """;
        var outside = $$"""
                        {
                          "data": [{
                            "start_time": {{s_periodStart.AddDays(-1).ToUnixTimeSeconds()}},
                            "end_time": {{s_periodStart.ToUnixTimeSeconds()}},
                            "results": []
                          }],
                          "has_more": false,
                          "next_page": null
                        }
                        """;
        using var handler = new RecordingHandler(
            _ => JsonResponse(negative),
            _ => JsonResponse(outside));
        using var client = new HttpClient(handler);
        var source = new OpenAiOrganizationCostsSource(
            client,
            TimeProvider.System,
            new OpenAiOrganizationCostsOptions("admin-secret"));

        var negativeResult = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);
        var outsideResult = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.Equal(ProviderCostFailureCategory.InvalidResponse, negativeResult.Failure?.Category);
        Assert.Equal(ProviderCostFailureCategory.InvalidResponse, outsideResult.Failure?.Category);
        Assert.Empty(negativeResult.Records);
        Assert.Empty(outsideResult.Records);
    }

    [Fact]
    public async Task Transport_failures_are_categorized_without_exception_details()
    {
        using var handler = new RecordingHandler(
            _ => throw new HttpRequestException("contains-private-hostname"));
        using var client = new HttpClient(handler);
        var source = new AnthropicCostReportSource(
            client,
            TimeProvider.System,
            new AnthropicCostReportOptions("admin-secret"));

        var result = await source.FetchAsync(
            s_periodStart,
            s_periodEnd,
            TestContext.Current.CancellationToken);

        Assert.Equal(ProviderCostFailureCategory.Transport, result.Failure?.Category);
        Assert.Null(result.Failure?.StatusCode);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_instead_of_being_reported_as_a_provider_failure()
    {
        using var handler = new RecordingHandler(_ => JsonResponse("{}"));
        using var client = new HttpClient(handler);
        var source = new OpenAiOrganizationCostsSource(
            client,
            TimeProvider.System,
            new OpenAiOrganizationCostsOptions("admin-secret"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.FetchAsync(s_periodStart, s_periodEnd, cancellation.Token));
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);

        public List<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI is required."),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase)));
            if (_responses.Count == 0) throw new InvalidOperationException("No fake response remains.");
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        IReadOnlyDictionary<string, string[]> HeaderValues);
}
