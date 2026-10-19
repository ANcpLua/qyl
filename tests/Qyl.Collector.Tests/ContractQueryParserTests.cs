using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Hosting;
using QylTimeConversions = Qyl.Collector.Ingestion.Primitives.TimeConversions;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ContractQueryParserTests(TestContext testContext)
{
    public static IEnumerable<object[]> MalformedTypedQueries =>
        [
            [ QueryEndpoint.Sessions, "is_active", "not-a-boolean", "query.invalid_boolean" ],
            [ QueryEndpoint.Sessions, "start_time", "yesterday", "query.invalid_date_time" ],
            [ QueryEndpoint.Sessions, "end_time", "2026-07-13", "query.invalid_date_time" ],
            [ QueryEndpoint.Sessions, "limit", "many", "query.invalid_integer" ],
            [ QueryEndpoint.SessionStats, "start_time", "now", "query.invalid_date_time" ],
            [ QueryEndpoint.SessionStats, "end_time", "later", "query.invalid_date_time" ],
            [ QueryEndpoint.Traces, "limit", "1.5", "query.invalid_integer" ],
            [ QueryEndpoint.Traces, "cursor", "not-a-qyl-cursor", "cursor.invalid" ],
            [ QueryEndpoint.Logs, "severity_min", "info", "query.invalid_integer" ],
            [ QueryEndpoint.Logs, "start_time", "13/07/2026", "query.invalid_date_time" ],
            [ QueryEndpoint.Logs, "end_time", "2026-07-13T10:00:00", "query.invalid_date_time" ],
            [ QueryEndpoint.Logs, "limit", "0x10", "query.invalid_integer" ],
            [ QueryEndpoint.LogStream, "min_severity", "warning", "query.invalid_integer" ]
        ];

    public static IEnumerable<object[]> OutOfRangeQueries =>
        [
            [ QueryEndpoint.Sessions, "limit", "0", "limit.out_of_range" ],
            [ QueryEndpoint.Traces, "limit", "1001", "limit.out_of_range" ],
            [ QueryEndpoint.Logs, "severity_min", "25", "severity.out_of_range" ],
            [ QueryEndpoint.Logs, "limit", "10001", "limit.out_of_range" ],
            [ QueryEndpoint.LogStream, "min_severity", "0", "severity.out_of_range" ]
        ];

    [TestMethod]
    [DynamicData(nameof(MalformedTypedQueries))]
    [DynamicData(nameof(OutOfRangeQueries))]
    public async Task Every_schema_typed_query_handler_returns_generated_validation_problem(
        QueryEndpoint endpoint,
        string field,
        string value,
        string expectedCode)
    {
        var context = CreateContext(field, value);

        await InvokeEndpointAsync(endpoint, context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ValidationError,
            testContext.CancellationToken);
        var detail = Assert.ContainsSingle(Assert.IsExactInstanceOfType<ValidationError>(problem).Errors);
        Assert.AreEqual(field, detail.Field);
        Assert.AreEqual(expectedCode, detail.Code);
        Assert.AreEqual(value, detail.RejectedValue);
    }

    [TestMethod]
    public async Task Repeated_scalar_query_is_rejected_instead_of_silently_choosing_a_value()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?limit=10&limit=20");

        await InvokeEndpointAsync(QueryEndpoint.Traces, context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ValidationError,
            testContext.CancellationToken);
        var detail = Assert.ContainsSingle(Assert.IsExactInstanceOfType<ValidationError>(problem).Errors);
        Assert.AreEqual("query.invalid_integer", detail.Code);
        Assert.AreEqual("10,20", detail.RejectedValue);
    }

    [TestMethod]
    public void Parser_retains_absent_values_and_accepts_contract_formatted_values()
    {
        var empty = CreateContext();
        Assert.IsNull(ContractQueryParser.ParseSessions(empty.Request, out var defaults));
        Assert.IsNull(defaults.IsActive);
        Assert.IsNull(defaults.StartTime);
        Assert.IsNull(defaults.EndTime);
        Assert.IsNull(defaults.Limit);

        var populated = CreateContext();
        populated.Request.QueryString = QueryString.Create(
        [
            new KeyValuePair<string, string?>("is_active", "true"),
            new KeyValuePair<string, string?>("start_time", "2026-07-13T10:11:12.1234567Z"),
            new KeyValuePair<string, string?>("end_time", "2026-07-13T12:11:12+02:00"),
            new KeyValuePair<string, string?>("limit", "50")
        ]);

        Assert.IsNull(ContractQueryParser.ParseSessions(populated.Request, out var parsed));
        Assert.IsTrue(parsed.IsActive);
        Assert.AreEqual(TimeSpan.Zero, parsed.StartTime?.Offset);
        Assert.AreEqual(TimeSpan.FromHours(2), parsed.EndTime?.Offset);
        Assert.AreEqual(50, parsed.Limit);
        Assert.AreEqual(
            123_456_700UL,
            QylTimeConversions.ToUnixNanoUnsigned(parsed.StartTime!.Value) % 1_000_000_000UL);
    }

    private async Task InvokeEndpointAsync(QueryEndpoint endpoint, DefaultHttpContext context)
    {
        IResult? result = endpoint switch
        {
            QueryEndpoint.Sessions => await CollectorEndpointExtensions.GetSessionsAsync(
                context, null!, null!, testContext.CancellationToken),
            QueryEndpoint.SessionStats => await CollectorEndpointExtensions.GetSessionStatsAsync(
                context, null!, null!, testContext.CancellationToken),
            QueryEndpoint.Traces => await CollectorEndpointExtensions.GetTracesAsync(
                context, null!, testContext.CancellationToken),
            QueryEndpoint.Logs => await CollectorEndpointExtensions.GetLogsAsync(
                context, null!, null, null, null, null, null, testContext.CancellationToken),
            QueryEndpoint.LogStream => null,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null)
        };

        if (endpoint is QueryEndpoint.LogStream)
        {
            await CollectorEndpointExtensions.StreamLogsAsync(
                context,
                null!,
                new CollectorStreamCapacity(),
                null!,
                null,
                null,
                testContext.CancellationToken);
        }
        else
        {
            await Assert.IsInstanceOfType<IResult>(result).ExecuteAsync(context);
        }
    }

    private static DefaultHttpContext CreateContext(string? field = null, string? value = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream()
            }
        };
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
