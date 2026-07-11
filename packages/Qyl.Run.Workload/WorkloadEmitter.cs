using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Run.Workload;

/// <summary>
/// Continuously emits realistic gen_ai + http + db traces (plus metrics and logs) so a demo
/// collector has something to show. Every attribute key and instrument flows through the
/// SemConv source-generated surface — no hand-rolled attribute strings.
/// </summary>
internal sealed partial class WorkloadEmitter(ILogger<WorkloadEmitter> logger) : BackgroundService
{
    // A handful of concurrent "users"; each runs conversations under a rotating session.id.
    private const int SessionLoops = 3;

    private const int ErrorPercent = 7;

    // Providers/models the collector's pricing seed knows, so sessions get non-zero cost.
    private static readonly ModelProfile[] Profiles =
    [
        new("anthropic", "claude-opus-4-6", 800, 6000, 300, 2200, 900, 3200),
        new("anthropic", "claude-sonnet-4-6", 400, 4000, 150, 1500, 400, 1800),
        new("anthropic", "claude-haiku-4-5", 200, 2000, 60, 700, 150, 700),
        new("openai", "gpt-4o", 400, 4000, 150, 1400, 350, 1600),
        new("openai", "gpt-4.1-mini", 200, 2500, 80, 900, 200, 900),
        new("openai", "o3-mini", 300, 3000, 200, 1800, 700, 2800),
        new("google", "gemini-2.5-pro", 500, 5000, 200, 1600, 500, 2200),
        new("google", "gemini-2.5-flash", 200, 2200, 60, 800, 150, 800),
        new("mistral", "mistral-large", 300, 2800, 100, 1000, 300, 1300),
        new("deepseek", "deepseek-r1", 300, 3500, 250, 2000, 800, 3000),
    ];

    private static readonly string[] Routes = ["/api/chat", "/api/agents/plan", "/api/summarize", "/api/search"];

    private static readonly string[] Tables = ["conversations", "messages", "embeddings"];

    private readonly Histogram<double> _httpServerDuration = WorkloadTelemetry.Meter.CreateHttpServerRequestDurationHistogram();

    private readonly Histogram<double> _dbOperationDuration = WorkloadTelemetry.Meter.CreateDbClientOperationDurationHistogram();

    private readonly Histogram<double> _genAiOperationDuration = WorkloadTelemetry.Meter.CreateGenAiClientOperationDurationHistogram();

    private readonly Histogram<long> _genAiTokenUsage = WorkloadTelemetry.Meter.CreateGenAiClientTokenUsageHistogram();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkloadStarted(logger, SessionLoops);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, SessionLoops)
                .Select(slot => RunSessionLoopAsync(slot, stoppingToken))).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown — nothing to flush here; the OTel SDK providers drain on host dispose.
        }
    }

    private async Task RunSessionLoopAsync(int slot, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var sessionId = $"demo-{slot}-{Guid.NewGuid():N}"[..21];
            var turns = Random.Shared.Next(6, 18);
            LogSessionStarted(logger, sessionId, turns);

            for (var turn = 0; turn < turns && !stoppingToken.IsCancellationRequested; turn++)
            {
                await EmitConversationTurnAsync(sessionId, stoppingToken).ConfigureAwait(false);
                await Task.Delay(Random.Shared.Next(400, 2600), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// One trace: an inbound HTTP request that reads conversation state from the database,
    /// then calls a model. The gen_ai span carries session.id + token usage — the keys the
    /// collector projects into session analytics. Span names never start with "invoke_agent"
    /// (the session aggregates exclude those as double-counting roll-ups).
    /// </summary>
    private async Task EmitConversationTurnAsync(string sessionId, CancellationToken stoppingToken)
    {
        var profile = Profiles[Random.Shared.Next(Profiles.Length)];
        var route = Routes[Random.Shared.Next(Routes.Length)];
        var failed = Random.Shared.Next(100) < ErrorPercent;

        using var root = WorkloadTelemetry.Source.StartActivity($"POST {route}", ActivityKind.Server);
        if (root is null) return;

        root.SetHttpRequestMethod(HttpSpans.HttpRequestMethodValues.Post)
            .SetHttpRoute(route)
            .SetServerAddress("127.0.0.1")
            .SetSessionId(sessionId);

        await EmitDbSpanAsync(sessionId, stoppingToken).ConfigureAwait(false);
        var latencyMs = await EmitGenAiSpanAsync(sessionId, profile, failed, stoppingToken).ConfigureAwait(false);

        var statusCode = failed ? StatusCodes.Status429TooManyRequests : StatusCodes.Status200OK;
        root.SetHttpResponseStatusCode(statusCode);
        if (failed)
        {
            root.SetErrorType(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.SetStatus(ActivityStatusCode.Error, "upstream model rate limited");
        }

        root.Stop();
        _httpServerDuration.Record(root.Duration.TotalSeconds,
            new(HttpAttrs.AttributeHttpRequestMethod, HttpSpans.HttpRequestMethodValues.Post),
            new(HttpAttrs.AttributeHttpResponseStatusCode, statusCode),
            new(HttpAttrs.AttributeHttpRoute, route));

        if (failed)
        {
            LogTurnFailed(logger, profile.Provider, profile.Model, latencyMs);
        }
    }

    private async Task EmitDbSpanAsync(string sessionId, CancellationToken stoppingToken)
    {
        var table = Tables[Random.Shared.Next(Tables.Length)];
        var durationMs = Random.Shared.Next(2, 28);

        using var span = WorkloadTelemetry.Source.StartActivity($"SELECT qyl_demo.{table}", ActivityKind.Client);
        span?.SetDbSystemName("postgresql")
            .SetDbNamespace("qyl_demo")
            .SetDbOperationName("SELECT")
            .SetDbCollectionName(table)
            .SetSessionId(sessionId);

        await Task.Delay(durationMs, stoppingToken).ConfigureAwait(false);

        _dbOperationDuration.Record(durationMs / 1000.0,
            new(DbAttrs.AttributeDbSystemName, "postgresql"),
            new(DbAttrs.AttributeDbOperationName, "SELECT"),
            new(DbAttrs.AttributeDbCollectionName, table));
    }

    private async Task<int> EmitGenAiSpanAsync(string sessionId, ModelProfile profile, bool failed,
        CancellationToken stoppingToken)
    {
        var latencyMs = Random.Shared.Next(profile.LatencyMinMs, profile.LatencyMaxMs);
        var inputTokens = Random.Shared.Next(profile.InputMin, profile.InputMax);
        var outputTokens = Random.Shared.Next(profile.OutputMin, profile.OutputMax);

        using var span = WorkloadTelemetry.Source.StartActivity($"chat {profile.Model}", ActivityKind.Client);
        span?.SetGenAiOperationName(GenAiSpans.GenAiOperationNameValues.Chat)
            .SetGenAiProviderName(profile.Provider)
            .SetGenAiRequestModel(profile.Model)
            .SetGenAiRequestTemperature(Math.Round(Random.Shared.NextDouble(), 2, MidpointRounding.AwayFromZero))
            .SetSessionId(sessionId);

        await Task.Delay(latencyMs, stoppingToken).ConfigureAwait(false);

        if (failed)
        {
            span?.SetErrorType("rate_limit_exceeded");
            span?.SetStatus(ActivityStatusCode.Error, "429 from provider");
        }
        else
        {
            span?.SetGenAiResponseModel(profile.Model)
                .SetGenAiResponseFinishReasons(["stop"])
                .SetGenAiUsageInputTokens(inputTokens)
                .SetGenAiUsageOutputTokens(outputTokens);

            _genAiTokenUsage.Record(inputTokens,
                new(GenAiAttrs.AttributeGenAiTokenType, GenAiAttrs.GenAiTokenTypeValues.Input),
                new(GenAiAttrs.AttributeGenAiProviderName, profile.Provider),
                new(GenAiAttrs.AttributeGenAiRequestModel, profile.Model));
            _genAiTokenUsage.Record(outputTokens,
                new(GenAiAttrs.AttributeGenAiTokenType, GenAiAttrs.GenAiTokenTypeValues.Output),
                new(GenAiAttrs.AttributeGenAiProviderName, profile.Provider),
                new(GenAiAttrs.AttributeGenAiRequestModel, profile.Model));
            LogTurnCompleted(logger, profile.Provider, profile.Model, inputTokens, outputTokens, latencyMs);
        }

        _genAiOperationDuration.Record(latencyMs / 1000.0,
            new(GenAiAttrs.AttributeGenAiOperationName, GenAiSpans.GenAiOperationNameValues.Chat),
            new(GenAiAttrs.AttributeGenAiProviderName, profile.Provider),
            new(GenAiAttrs.AttributeGenAiRequestModel, profile.Model));

        return latencyMs;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "workload started: {SessionLoops} concurrent session loops")]
    private static partial void LogWorkloadStarted(ILogger logger, int sessionLoops);

    [LoggerMessage(Level = LogLevel.Information, Message = "session {SessionId} started ({Turns} turns)")]
    private static partial void LogSessionStarted(ILogger logger, string sessionId, int turns);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "chat completed: {Provider}/{Model} {InputTokens}->{OutputTokens} tokens in {LatencyMs} ms")]
    private static partial void LogTurnCompleted(ILogger logger, string provider, string model, int inputTokens,
        int outputTokens, int latencyMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "chat failed: {Provider}/{Model} rate limited after {LatencyMs} ms")]
    private static partial void LogTurnFailed(ILogger logger, string provider, string model, int latencyMs);

    private sealed record ModelProfile(string Provider, string Model, int InputMin, int InputMax, int OutputMin,
        int OutputMax, int LatencyMinMs, int LatencyMaxMs);
}
