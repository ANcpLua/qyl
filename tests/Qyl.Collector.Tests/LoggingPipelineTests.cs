using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class LoggingPipelineTests(TestContext testContext)
{
    private const string LogCategory = "qyl.tests.logging-pipeline";
    private const string Template = "Telemetry probe {probe_id}";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Enabled_logging_preserves_original_format_enriches_requests_and_redacts_classified_tags(bool modern)
    {
        using var capture = new CaptureProvider();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(capture);
        builder.Services.AddQylTelemetry();
        await using var app = builder.Build();
        var accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        try
        {
            var context = new DefaultHttpContext { Request = { Method = "POST" } };
            context.SetEndpoint(new RouteEndpoint(static _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/api/v1/metrics/{metric_name}"), 0,
                EndpointMetadataCollection.Empty, "logging probe"));
            accessor.HttpContext = context;
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory);
            if (modern)
            {
                var state = new LoggerMessageState();
                state.AddClassifiedTag("credential", "synthetic-secret", QylDataClassification.Secret);
                state.AddTag("probe_id", "visible");
                state.AddTag("{OriginalFormat}", Template);
                logger.Log(LogLevel.Information, new EventId(70), state, null,
                    static (_, _) => "Telemetry probe visible");
            }
            else
            {
                KeyValuePair<string, object?>[] state = [new("probe_id", "visible"), new("{OriginalFormat}", Template)];
                logger.Log(LogLevel.Information, new EventId(70), state, null,
                    static (_, _) => "Telemetry probe visible");
            }
            var record = Assert.ContainsSingle(capture.Records.Where(static record => record.Category == LogCategory));
            Assert.IsNotNull(record.Tags);
            var tags = record.Tags;
            Assert.AreEqual("{OriginalFormat}", tags[^1].Key);
            Assert.AreEqual(Template, tags[^1].Value);
            Assert.AreEqual("visible", Assert.ContainsSingle(tags.Where(static tag => tag.Key == "probe_id")).Value);
            Assert.AreEqual("POST", Assert.ContainsSingle(tags.Where(static tag => tag.Key == "http.request.method")).Value);
            Assert.AreEqual("/api/v1/metrics/{metric_name}", Assert.ContainsSingle(tags.Where(static tag => tag.Key == "http.route")).Value);
            if (modern)
                Assert.AreEqual("", Assert.ContainsSingle(tags.Where(static tag => tag.Key == "credential")).Value);
            Assert.DoesNotContain(static tag => Equals(tag.Value, "synthetic-secret"), tags);
            testContext.WriteLine($"Observed enabled logging state: {record.StateType}; tags={tags.Count}");
        }
        finally { accessor.HttpContext = previous; }
    }

    private sealed record Captured(string Category, string StateType, IReadOnlyList<KeyValuePair<string, object?>>? Tags);

    private sealed class CaptureProvider : ILoggerProvider
    {
        public List<Captured> Records { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Records);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(string category, List<Captured> records) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            List<KeyValuePair<string, object?>>? materialized = null;
            if (state is IEnumerable<KeyValuePair<string, object?>> tags)
            {
                materialized = [];
                foreach (var tag in tags)
                {
                    if (materialized.Count == 64)
                        throw new InvalidDataException("The probe's finite log state exceeded its 64-tag safety bound.");
                    // ExtendedLogger's redactors are valid during this callback and are
                    // returned to a pool afterwards. A sink must consume them here.
                    materialized.Add(new KeyValuePair<string, object?>(tag.Key, tag.Value?.ToString()));
                }
            }
            records.Add(new Captured(category, state?.GetType().FullName ?? typeof(TState).FullName!, materialized));
        }
    }
}
