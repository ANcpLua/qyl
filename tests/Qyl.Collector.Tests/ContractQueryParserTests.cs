using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Hosting;
using QylTimeConversions = Qyl.Collector.Primitives.TimeConversions;

namespace Qyl.Collector.Tests;

public sealed class ContractQueryParserTests
{
    public static TheoryData<QueryEndpoint, string, string, string> MalformedTypedQueries =>
        new()
        {
            { QueryEndpoint.Sessions, "isActive", "not-a-boolean", "query.invalid_boolean" },
            { QueryEndpoint.Sessions, "startTime", "yesterday", "query.invalid_date_time" },
            { QueryEndpoint.Sessions, "endTime", "2026-07-13", "query.invalid_date_time" },
            { QueryEndpoint.Sessions, "limit", "many", "query.invalid_integer" },
            { QueryEndpoint.SessionStats, "startTime", "now", "query.invalid_date_time" },
            { QueryEndpoint.SessionStats, "endTime", "later", "query.invalid_date_time" },
            { QueryEndpoint.Traces, "limit", "1.5", "query.invalid_integer" },
            { QueryEndpoint.Traces, "cursor", "not-a-qyl-cursor", "cursor.invalid" },
            { QueryEndpoint.Logs, "severityMin", "info", "query.invalid_integer" },
            { QueryEndpoint.Logs, "startTime", "13/07/2026", "query.invalid_date_time" },
            { QueryEndpoint.Logs, "endTime", "2026-07-13T10:00:00", "query.invalid_date_time" },
            { QueryEndpoint.Logs, "limit", "0x10", "query.invalid_integer" },
            { QueryEndpoint.LogStream, "minSeverity", "warning", "query.invalid_integer" }
        };

    public static TheoryData<QueryEndpoint, string, string, string> OutOfRangeQueries =>
        new()
        {
            { QueryEndpoint.Sessions, "limit", "0", "limit.out_of_range" },
            { QueryEndpoint.Traces, "limit", "1001", "limit.out_of_range" },
            { QueryEndpoint.Logs, "severityMin", "25", "severity.out_of_range" },
            { QueryEndpoint.Logs, "limit", "10001", "limit.out_of_range" },
            { QueryEndpoint.LogStream, "minSeverity", "0", "severity.out_of_range" }
        };

    [Theory]
    [MemberData(nameof(MalformedTypedQueries))]
    [MemberData(nameof(OutOfRangeQueries))]
    public async Task Every_schema_typed_query_handler_returns_generated_validation_problem(
        QueryEndpoint endpoint,
        string field,
        string value,
        string expectedCode)
    {
        var context = CreateContext(field, value);

        await InvokeEndpointAsync(endpoint, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ValidationError,
            TestContext.Current.CancellationToken);
        var detail = Assert.Single(Assert.IsType<ValidationError>(problem).Errors);
        Assert.Equal(field, detail.Field);
        Assert.Equal(expectedCode, detail.Code);
        Assert.Equal(value, detail.RejectedValue);
    }

    [Fact]
    public async Task Repeated_scalar_query_is_rejected_instead_of_silently_choosing_a_value()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?limit=10&limit=20");

        await InvokeEndpointAsync(QueryEndpoint.Traces, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ValidationError,
            TestContext.Current.CancellationToken);
        var detail = Assert.Single(Assert.IsType<ValidationError>(problem).Errors);
        Assert.Equal("query.invalid_integer", detail.Code);
        Assert.Equal("10,20", detail.RejectedValue);
    }

    [Fact]
    public void Parser_retains_absent_values_and_accepts_contract_formatted_values()
    {
        var empty = CreateContext();
        Assert.Null(ContractQueryParser.ParseSessions(empty.Request, out var defaults));
        Assert.Null(defaults.IsActive);
        Assert.Null(defaults.StartTime);
        Assert.Null(defaults.EndTime);
        Assert.Null(defaults.Limit);

        var populated = CreateContext();
        populated.Request.QueryString = QueryString.Create(
        [
            new KeyValuePair<string, string?>("isActive", "true"),
            new KeyValuePair<string, string?>("startTime", "2026-07-13T10:11:12.1234567Z"),
            new KeyValuePair<string, string?>("endTime", "2026-07-13T12:11:12+02:00"),
            new KeyValuePair<string, string?>("limit", "50")
        ]);

        Assert.Null(ContractQueryParser.ParseSessions(populated.Request, out var parsed));
        Assert.True(parsed.IsActive);
        Assert.Equal(TimeSpan.Zero, parsed.StartTime?.Offset);
        Assert.Equal(TimeSpan.FromHours(2), parsed.EndTime?.Offset);
        Assert.Equal(50, parsed.Limit);
        Assert.Equal(
            123_456_700UL,
            QylTimeConversions.ToUnixNanoUnsigned(parsed.StartTime!.Value) % 1_000_000_000UL);
    }

    private static async Task InvokeEndpointAsync(QueryEndpoint endpoint, DefaultHttpContext context)
    {
        IResult? result = endpoint switch
        {
            QueryEndpoint.Sessions => await CollectorEndpointExtensions.GetSessionsAsync(
                context, null!, cursor: null, TestContext.Current.CancellationToken),
            QueryEndpoint.SessionStats => await CollectorEndpointExtensions.GetSessionStatsAsync(
                context, null!, TestContext.Current.CancellationToken),
            QueryEndpoint.Traces => await CollectorEndpointExtensions.GetTracesAsync(
                context, null!, TestContext.Current.CancellationToken),
            QueryEndpoint.Logs => await CollectorEndpointExtensions.GetLogsAsync(
                context, null!, null, null, null, null, null, TestContext.Current.CancellationToken),
            QueryEndpoint.LogStream => null,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null)
        };

        if (endpoint is QueryEndpoint.LogStream)
        {
            await CollectorEndpointExtensions.StreamLogsAsync(
                context,
                null!,
                new CollectorStreamCapacity(),
                null,
                null,
                TestContext.Current.CancellationToken);
        }
        else
        {
            await Assert.IsAssignableFrom<IResult>(result).ExecuteAsync(context);
        }
    }

    private static DefaultHttpContext CreateContext(string? field = null, string? value = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        if (field is not null) context.Request.QueryString = QueryString.Create(field, value ?? string.Empty);
        return context;
    }

    public enum QueryEndpoint
    {
        Sessions,
        SessionStats,
        Traces,
        Logs,
        LogStream
    }
}
