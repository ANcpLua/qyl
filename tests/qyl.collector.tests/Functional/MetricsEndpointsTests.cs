using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Metrics;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class MetricsEndpointsTests
    : IClassFixture<MetricsEndpointsTests.CollectorFactory>
{
    private readonly CollectorFactory _factory;

    public MetricsEndpointsTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_metrics_lists_derived_catalog_for_reporting_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-{suffix}";
        await SeedSpanAsync($"metrics-api-list-{suffix}", serviceName, TokenStart, 100, 7, 3, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/metrics?serviceName={Uri.EscapeDataString(serviceName)}&namePattern=token&limit=10",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var metric = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        metric.GetProperty("name").GetString().Should().Be("gen_ai.client.token.usage");
        metric.GetProperty("type").GetString().Should().Be("histogram");
        metric.GetProperty("unit").GetString().Should().Be("{token}");
        var labelKeys = metric.GetProperty("label_keys").EnumerateArray()
            .Select(static item => item.GetString());
        labelKeys.Should().Contain("service.name");
        labelKeys.Should().Contain("gen_ai.provider.name");
        labelKeys.Should().Contain("gen_ai.request.model");
        labelKeys.Should().Contain("gen_ai.token.type");
        metric.GetProperty("services").EnumerateArray()
            .Select(static item => item.GetString())
            .Should().Contain(serviceName);
        metric.GetProperty("services_truncated").GetBoolean().Should().BeFalse();
        metric.GetProperty("service_limit").GetInt32().Should().Be(100);
        body.GetProperty("has_more").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Get_metrics_rejects_limit_outside_contract_bounds(int limit)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/metrics?limit={limit}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("limit");
        body.GetProperty("error").GetString().Should().Contain("1000");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Get_metrics_rejects_service_limit_outside_contract_bounds(int serviceLimit)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/metrics?serviceLimit={serviceLimit}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("serviceLimit");
        body.GetProperty("error").GetString().Should().Contain("1000");
    }

    [Fact]
    public async Task Get_metrics_service_filter_prioritizes_selected_service_in_truncated_service_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var firstServiceName = $"!!!!-metrics-api-filter-first-{suffix}";
        var selectedServiceName = $"zzzz-metrics-api-filter-selected-{suffix}";
        await SeedSpanAsync($"metrics-api-filter-first-{suffix}", firstServiceName, TokenStart, 100, 7, 3, ct);
        await SeedSpanAsync($"metrics-api-filter-selected-{suffix}", selectedServiceName, TokenStart.AddMinutes(1), 100, 11, 5, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/metrics?serviceName={Uri.EscapeDataString(selectedServiceName)}&namePattern=token&serviceLimit=1",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var metric = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        metric.GetProperty("name").GetString().Should().Be("gen_ai.client.token.usage");
        var services = metric.GetProperty("services").EnumerateArray()
            .Select(static item => item.GetString())
            .ToList();
        services.Should().ContainSingle();
        services.Should().Contain(selectedServiceName);
        metric.GetProperty("services_truncated").GetBoolean().Should().BeTrue();
        metric.GetProperty("service_limit").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_metric_metadata_returns_derived_metric_contract()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-metadata-{suffix}";
        await SeedSpanAsync($"metrics-api-metadata-{suffix}", serviceName, TokenStart, 100, 11, 13, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/metrics/gen_ai.client.token.usage", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("name").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("unit").GetString().Should().Be("{token}");
        var labelKeys = body.GetProperty("label_keys").EnumerateArray()
            .Select(static item => item.GetString());
        labelKeys.Should().Contain("service.name");
        labelKeys.Should().Contain("gen_ai.provider.name");
        labelKeys.Should().Contain("gen_ai.request.model");
        labelKeys.Should().Contain("gen_ai.token.type");
        body.GetProperty("services").EnumerateArray()
            .Select(static item => item.GetString())
            .Should().Contain(serviceName);
        body.GetProperty("services_truncated").GetBoolean().Should().BeFalse();
        body.GetProperty("service_limit").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Get_metric_metadata_applies_service_limit_to_service_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var firstServiceName = $"!!!!-metrics-api-metadata-a-{suffix}";
        var secondServiceName = $"!!!!-metrics-api-metadata-b-{suffix}";
        var thirdServiceName = $"!!!!-metrics-api-metadata-c-{suffix}";
        await SeedSpanAsync($"metrics-api-metadata-a-{suffix}", firstServiceName, TokenStart, 100, 11, 13, ct);
        await SeedSpanAsync($"metrics-api-metadata-b-{suffix}", secondServiceName, TokenStart.AddMinutes(1), 100, 17, 19, ct);
        await SeedSpanAsync($"metrics-api-metadata-c-{suffix}", thirdServiceName, TokenStart.AddMinutes(2), 100, 23, 29, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/metrics/gen_ai.client.token.usage?serviceLimit=2", ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("services").EnumerateArray()
            .Select(static item => item.GetString())
            .Should().HaveCount(2);
        body.GetProperty("services_truncated").GetBoolean().Should().BeTrue();
        body.GetProperty("service_limit").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Post_metrics_query_returns_derived_series_from_stored_spans()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-query-{suffix}";
        await SeedSpanAsync($"metrics-api-query-1-{suffix}", serviceName, TokenStart, 250, 10, 5, ct);
        await SeedSpanAsync($"metrics-api-query-2-{suffix}", serviceName, TokenStart.AddMinutes(5), 500, 20, 15, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{TokenStart.AddMinutes(-1):O}}",
                            "end_time": "{{TokenStart.AddHours(1):O}}",
                            "step": "1h"
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("series_truncated").GetBoolean().Should().BeFalse();
        body.GetProperty("series_limit").GetInt32().Should().Be(100);
        body.GetProperty("points_truncated").GetBoolean().Should().BeFalse();
        body.GetProperty("point_limit").GetInt32().Should().Be(10_000);
        var series = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject;
        series.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        var point = series.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject;
        point.GetProperty("value").GetDouble().Should().Be(50);
    }

    [Fact]
    public async Task Post_metrics_query_accepts_week_time_bucket_from_contract()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-week-{suffix}";
        await SeedSpanAsync($"metrics-api-week-{suffix}", serviceName, TokenStart, 250, 10, 5, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "request_count",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{TokenStart.AddDays(-1):O}}",
                            "end_time": "{{TokenStart.AddDays(7):O}}",
                            "step": "1w"
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var point = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject;
        point.GetProperty("value").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task Post_metrics_query_groups_request_count_by_service_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceNameA = $"metrics-api-service-a-{suffix}";
        var serviceNameB = $"metrics-api-service-b-{suffix}";
        var start = TokenStart.AddDays(1);
        await SeedSpanAsync($"metrics-api-service-a-1-{suffix}", serviceNameA, start, 100, 7, 3, ct);
        await SeedSpanAsync($"metrics-api-service-a-2-{suffix}", serviceNameA, start.AddMinutes(5), 100, 11, 5, ct);
        await SeedSpanAsync($"metrics-api-service-b-1-{suffix}", serviceNameB, start, 100, 13, 17, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "request_count",
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddHours(1):O}}",
                            "step": "1h",
                            "group_by": [ "service.name" ]
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().ToList();
        series.Should().HaveCount(2);

        var serviceASeries = SingleSeriesByServiceName(series, serviceNameA);
        serviceASeries.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(2);

        var serviceBSeries = SingleSeriesByServiceName(series, serviceNameB);
        serviceBSeries.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task Post_metrics_query_applies_series_limit_to_grouped_results()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceNameA = $"metrics-api-limit-a-{suffix}";
        var serviceNameB = $"metrics-api-limit-b-{suffix}";
        var serviceNameC = $"metrics-api-limit-c-{suffix}";
        var start = TokenStart.AddDays(30);
        await SeedSpanAsync($"metrics-api-limit-a-{suffix}", serviceNameA, start, 100, 7, 3, ct);
        await SeedSpanAsync($"metrics-api-limit-b-{suffix}", serviceNameB, start.AddMinutes(5), 100, 11, 5, ct);
        await SeedSpanAsync($"metrics-api-limit-c-{suffix}", serviceNameC, start.AddMinutes(10), 100, 13, 17, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "request_count",
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddHours(1):O}}",
                            "step": "1h",
                            "group_by": [ "service.name" ],
                            "series_limit": 2
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("series_truncated").GetBoolean().Should().BeTrue();
        body.GetProperty("series_limit").GetInt32().Should().Be(2);
        var series = body.GetProperty("series").EnumerateArray().ToList();
        series.Should().HaveCount(2);
        series.Select(static item => item.GetProperty("labels").GetProperty("service.name").GetString())
            .Should().BeEquivalentTo([serviceNameA, serviceNameB]);
    }

    [Fact]
    public async Task Post_metrics_query_applies_point_limit_to_returned_points()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-point-limit-{suffix}";
        var start = TokenStart.AddDays(31);
        await SeedSpanAsync($"metrics-api-point-limit-1-{suffix}", serviceName, start, 100, 7, 3, ct);
        await SeedSpanAsync($"metrics-api-point-limit-2-{suffix}", serviceName, start.AddMinutes(1), 100, 11, 5, ct);
        await SeedSpanAsync($"metrics-api-point-limit-3-{suffix}", serviceName, start.AddMinutes(2), 100, 13, 17, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "request_count",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddMinutes(10):O}}",
                            "step": "1m",
                            "point_limit": 2
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("points_truncated").GetBoolean().Should().BeTrue();
        body.GetProperty("point_limit").GetInt32().Should().Be(2);
        var series = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject;
        series.GetProperty("points").EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public void Metric_query_sql_applies_row_limit_after_ordering()
    {
        var sql = MetricsEndpoints.BuildMetricQuerySql(
            "COUNT(*)",
            "INTERVAL '1 minute'",
            "start_time_unix_nano >= $1",
            [new MetricLabelColumn("service.name", "service_name")],
            rowLimit: 3);

        sql.Should().EndWith("ORDER BY service_name ASC, bucket ASC LIMIT 3");
    }

    [Fact]
    public async Task Post_metrics_query_treats_end_time_as_exclusive_boundary()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-window-{suffix}";
        await SeedSpanAsync($"metrics-api-window-included-{suffix}", serviceName, TokenStart, 100, 10, 5, ct);
        await SeedSpanAsync(
            $"metrics-api-window-excluded-{suffix}",
            serviceName,
            TokenStart.AddHours(1),
            100,
            100,
            50,
            ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{TokenStart:O}}",
                            "end_time": "{{TokenStart.AddHours(1):O}}",
                            "step": "1h"
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var point = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject;
        point.GetProperty("value").GetDouble().Should().Be(15);
    }

    [Fact]
    public async Task Post_metrics_query_filters_token_usage_by_token_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-token-filter-{suffix}";
        await SeedSpanAsync($"metrics-api-token-filter-1-{suffix}", serviceName, TokenStart, 100, 10, 5, ct);
        await SeedSpanAsync($"metrics-api-token-filter-2-{suffix}", serviceName, TokenStart.AddMinutes(5), 100, 20, 15, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "filters": {
                              "service.name": "{{serviceName}}",
                              "gen_ai.token.type": "output"
                            },
                            "start_time": "{{TokenStart.AddMinutes(-1):O}}",
                            "end_time": "{{TokenStart.AddHours(1):O}}",
                            "step": "1h"
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject;
        series.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        series.GetProperty("labels").GetProperty("gen_ai.token.type").GetString().Should().Be("output");
        series.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(20);
    }

    [Fact]
    public async Task Post_metrics_query_groups_token_usage_by_token_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-token-type-{suffix}";
        await SeedSpanAsync($"metrics-api-token-type-1-{suffix}", serviceName, TokenStart, 100, 10, 5, ct);
        await SeedSpanAsync($"metrics-api-token-type-2-{suffix}", serviceName, TokenStart.AddMinutes(5), 100, 20, 15, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{TokenStart.AddMinutes(-1):O}}",
                            "end_time": "{{TokenStart.AddHours(1):O}}",
                            "step": "1h",
                            "group_by": [ "gen_ai.token.type" ]
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().ToList();
        series.Should().HaveCount(2);

        series[0].GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        series[0].GetProperty("labels").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
        series[0].GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(30);

        series[1].GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        series[1].GetProperty("labels").GetProperty("gen_ai.token.type").GetString().Should().Be("output");
        series[1].GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(20);
    }

    [Fact]
    public async Task Post_metrics_query_groups_token_usage_by_service_name_and_token_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceNameA = $"metrics-api-token-service-a-{suffix}";
        var serviceNameB = $"metrics-api-token-service-b-{suffix}";
        var start = TokenStart.AddDays(2);
        await SeedSpanAsync($"metrics-api-token-service-a-{suffix}", serviceNameA, start, 100, 10, 5, ct);
        await SeedSpanAsync($"metrics-api-token-service-b-{suffix}", serviceNameB, start.AddMinutes(5), 100, 20, 15, ct);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddHours(1):O}}",
                            "step": "1h",
                            "group_by": [ "service.name", "gen_ai.token.type" ]
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().ToList();
        series.Should().HaveCount(4);

        SingleSeriesByLabels(series, serviceNameA, "input")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(10);
        SingleSeriesByLabels(series, serviceNameA, "output")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(5);
        SingleSeriesByLabels(series, serviceNameB, "input")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(20);
        SingleSeriesByLabels(series, serviceNameB, "output")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(15);
    }

    [Fact]
    public async Task Post_metrics_query_filters_genai_cost_by_provider_and_request_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-provider-filter-{suffix}";
        var start = TokenStart.AddDays(3);
        await SeedSpanAsync(
            $"metrics-api-provider-filter-openai-{suffix}",
            serviceName,
            start,
            100,
            10,
            5,
            ct,
            providerName: "openai",
            requestModel: "gpt-5.5",
            costUsd: 0.0025d);
        await SeedSpanAsync(
            $"metrics-api-provider-filter-anthropic-{suffix}",
            serviceName,
            start.AddMinutes(5),
            100,
            20,
            15,
            ct,
            providerName: "anthropic",
            requestModel: "claude-opus",
            costUsd: 0.0100d);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.cost",
                            "filters": {
                              "service.name": "{{serviceName}}",
                              "gen_ai.provider.name": "openai",
                              "gen_ai.request.model": "gpt-5.5"
                            },
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddHours(1):O}}",
                            "step": "1h"
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().Should().ContainSingle().Subject;
        var labels = series.GetProperty("labels");
        labels.GetProperty("service.name").GetString().Should().Be(serviceName);
        labels.GetProperty("gen_ai.provider.name").GetString().Should().Be("openai");
        labels.GetProperty("gen_ai.request.model").GetString().Should().Be("gpt-5.5");
        series.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(0.0025d);
    }

    [Fact]
    public async Task Post_metrics_query_groups_token_usage_by_provider_and_request_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"metrics-api-provider-group-{suffix}";
        var start = TokenStart.AddDays(4);
        await SeedSpanAsync(
            $"metrics-api-provider-group-gpt5-{suffix}",
            serviceName,
            start,
            100,
            10,
            5,
            ct,
            providerName: "openai",
            requestModel: "gpt-5.5");
        await SeedSpanAsync(
            $"metrics-api-provider-group-gpt4-{suffix}",
            serviceName,
            start.AddMinutes(5),
            100,
            20,
            15,
            ct,
            providerName: "openai",
            requestModel: "gpt-4.1");

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/metrics/query",
            JsonContent($$"""
                          {
                            "metric_name": "gen_ai.client.token.usage",
                            "filters": { "service.name": "{{serviceName}}" },
                            "start_time": "{{start.AddMinutes(-1):O}}",
                            "end_time": "{{start.AddHours(1):O}}",
                            "step": "1h",
                            "group_by": [ "gen_ai.provider.name", "gen_ai.request.model" ]
                          }
                          """),
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var series = body.GetProperty("series").EnumerateArray().ToList();
        series.Should().HaveCount(2);

        SingleSeriesByProviderAndModel(series, "openai", "gpt-5.5")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(15);
        SingleSeriesByProviderAndModel(series, "openai", "gpt-4.1")
            .GetProperty("points").EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("value").GetDouble().Should().Be(35);
    }

    public static TheoryData<string, string[]> PostQueryRejectionCases() => new()
    {
        // missing window
        {
            """{ "metric_name": "request_count" }""",
            ["start_time"]
        },
        // duplicate service alias
        {
            """{ "metric_name": "request_count", "filters": { "service.name": "orders-api", "service": "checkout-api" }, "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z" }""",
            ["service.name", "more than once"]
        },
        // token_type filter on non-token metric
        {
            """{ "metric_name": "request_count", "filters": { "gen_ai.token.type": "input" }, "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z" }""",
            ["gen_ai.token.type", "gen_ai.client.token.usage"]
        },
        // unknown token_type value
        {
            """{ "metric_name": "gen_ai.client.token.usage", "filters": { "gen_ai.token.type": "total" }, "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z" }""",
            ["input", "output"]
        },
        // unsupported grouping
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "group_by": [ "host.name" ] }""",
            ["service.name"]
        },
        // empty grouping label
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "group_by": [ null ] }""",
            ["non-empty"]
        },
        // series_limit below contract bound
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "series_limit": 0 }""",
            ["series_limit", "1000"]
        },
        // series_limit above contract bound
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "series_limit": 1001 }""",
            ["series_limit", "1000"]
        },
        // point_limit below contract bound
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "point_limit": 0 }""",
            ["point_limit", "100000"]
        },
        // point_limit above contract bound
        {
            """{ "metric_name": "request_count", "start_time": "2026-05-23T09:59:00Z", "end_time": "2026-05-23T11:00:00Z", "point_limit": 100001 }""",
            ["point_limit", "100000"]
        },
    };

    [Theory]
    [MemberData(nameof(PostQueryRejectionCases))]
    public async Task Post_metrics_query_rejects_invalid_request(string body, string[] expectedFragments)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/api/v1/metrics/query", JsonContent(body), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>(ct))
            .GetProperty("error").GetString();
        foreach (var fragment in expectedFragments)
            error.Should().Contain(fragment);
    }

    [Fact]
    public async Task Get_metric_metadata_returns_not_found_for_unknown_metric()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/metrics/missing.metric", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("Unknown metric");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Get_metric_metadata_rejects_service_limit_outside_contract_bounds(int serviceLimit)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/metrics/gen_ai.client.token.usage?serviceLimit={serviceLimit}",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("serviceLimit");
        body.GetProperty("error").GetString().Should().Contain("1000");
    }

    private static readonly DateTimeOffset TokenStart =
        new(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

    private static JsonElement SingleSeriesByServiceName(
        IReadOnlyCollection<JsonElement> series,
        string serviceName)
    {
        var match = series.Should().ContainSingle(item =>
            item.GetProperty("labels").GetProperty("service.name").GetString() == serviceName).Subject;
        match.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        return match;
    }

    private static JsonElement SingleSeriesByLabels(
        IReadOnlyCollection<JsonElement> series,
        string serviceName,
        string tokenType)
    {
        var match = series.Should().ContainSingle(item =>
            item.GetProperty("labels").GetProperty("service.name").GetString() == serviceName &&
            item.GetProperty("labels").GetProperty("gen_ai.token.type").GetString() == tokenType).Subject;
        match.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        match.GetProperty("labels").GetProperty("gen_ai.token.type").GetString().Should().Be(tokenType);
        return match;
    }

    private static JsonElement SingleSeriesByProviderAndModel(
        IReadOnlyCollection<JsonElement> series,
        string providerName,
        string requestModel)
    {
        var match = series.Should().ContainSingle(item =>
            item.GetProperty("labels").GetProperty("gen_ai.provider.name").GetString() == providerName &&
            item.GetProperty("labels").GetProperty("gen_ai.request.model").GetString() == requestModel).Subject;
        match.GetProperty("labels").GetProperty("gen_ai.provider.name").GetString().Should().Be(providerName);
        match.GetProperty("labels").GetProperty("gen_ai.request.model").GetString().Should().Be(requestModel);
        return match;
    }

    private async Task SeedSpanAsync(
        string spanId,
        string serviceName,
        DateTimeOffset start,
        int durationMs,
        long inputTokens,
        long outputTokens,
        CancellationToken ct,
        string providerName = "openai",
        string requestModel = "gpt-5.5",
        double costUsd = 0.0025d)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<DuckDbStore>();
        var startNano = ToUnixNanoseconds(start);
        var durationNano = durationMs * 1_000_000L;

        await store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind, start_time_unix_nano, end_time_unix_nano,
                                   duration_ns, status_code, service_name, gen_ai_provider_name,
                                   gen_ai_request_model, gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_cost_usd)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = $"trace-{spanId}" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "chat completion" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "client" });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano + durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = "1" });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            cmd.Parameters.Add(new DuckDBParameter { Value = providerName });
            cmd.Parameters.Add(new DuckDBParameter { Value = requestModel });
            cmd.Parameters.Add(new DuckDBParameter { Value = inputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = outputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = costUsd });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static long ToUnixNanoseconds(DateTimeOffset value)
    {
        var seconds = value.ToUnixTimeSeconds();
        var ticksWithinSecond = value.Ticks % TimeSpan.TicksPerSecond;
        return (seconds * 1_000_000_000L) + (ticksWithinSecond * 100L);
    }

    public sealed class CollectorFactory() : CollectorFunctionalFactory("public-metrics")
    {
    }
}
