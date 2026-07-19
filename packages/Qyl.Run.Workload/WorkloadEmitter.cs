using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Qyl.Run.Workload;

internal sealed partial class WorkloadEmitter(
    ILogger<WorkloadEmitter> logger,
    MeterProvider meterProvider,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    private const int SessionLoops = 3;

    private const int ErrorPercent = 7;

    // Real provider model ids, so demo spans carry a plausible gen_ai.request.model.
    // Nothing resolves these against a catalog any more; they only need to look real.
    private static readonly ModelProfile[] Profiles =
    [
        new("anthropic", "anthropic/claude-opus-4.8", 800, 6000, 300, 2200, 900, 3200),
        new("anthropic", "anthropic/claude-sonnet-5", 400, 4000, 150, 1500, 400, 1800),
        new("anthropic", "anthropic/claude-haiku-4.5", 200, 2000, 60, 700, 150, 700),
        new("openai", "openai/gpt-5.6-sol", 400, 4000, 150, 1400, 350, 1600),
        new("openai", "openai/gpt-5.4-mini", 200, 2500, 80, 900, 200, 900),
        new("openai", "openai/gpt-5.5", 300, 3000, 200, 1800, 700, 2800),
        new("google", "google/gemini-3.1-pro-preview", 500, 5000, 200, 1600, 500, 2200),
        new("google", "google/gemini-3.5-flash", 200, 2200, 60, 800, 150, 800),
        new("mistralai", "mistralai/mistral-large-2512", 300, 2800, 100, 1000, 300, 1300),
        new("deepseek", "deepseek/deepseek-v4-flash", 300, 3500, 250, 2000, 800, 3000),
    ];

    private static readonly string[] Routes = ["/api/chat", "/api/agents/plan", "/api/summarize", "/api/search"];

    private static readonly string[] Tables = ["conversations", "messages", "embeddings"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("QYL_WORKLOAD_ONESHOT"), "1",
                StringComparison.Ordinal))
        {
            await EmitConversationTurnAsync($"acceptance-{Guid.NewGuid():N}"[..28], stoppingToken,
                    allowFailure: false)
                .ConfigureAwait(false);
            if (!meterProvider.ForceFlush(10_000))
            {
                throw new InvalidOperationException("Timed out exporting the one-shot workload metrics.");
            }

            applicationLifetime.StopApplication();
            return;
        }

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
    /// Avoids the <c>invoke_agent</c> prefix because session aggregation treats those spans
    /// as roll-ups and would double-count them.
    /// </summary>
    private async Task EmitConversationTurnAsync(string sessionId, CancellationToken stoppingToken,
        bool allowFailure = true)
    {
        var profile = Profiles[Random.Shared.Next(Profiles.Length)];
        var route = Routes[Random.Shared.Next(Routes.Length)];
        var failed = allowFailure && Random.Shared.Next(100) < ErrorPercent;

        using var root = WorkloadTelemetry.Source.StartActivity($"POST {route}", ActivityKind.Server);
        if (root is null)
        {
            return;
        }

        root.SetHttpRequestMethod(HttpSpans.HttpRequestMethodValues.Post)
            .SetHttpRoute(route)
            .SetServerAddress("127.0.0.1")
            .SetSessionId(sessionId);

        await EmitDbSpanAsync(sessionId, stoppingToken).ConfigureAwait(false);
        var latencyMs = await EmitGenAiSpanAsync(sessionId, route, profile, failed, stoppingToken)
            .ConfigureAwait(false);

        var statusCode = failed ? StatusCodes.Status429TooManyRequests : StatusCodes.Status200OK;
        root.SetHttpResponseStatusCode(statusCode);
        if (failed)
        {
            root.SetErrorType(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.SetStatus(ActivityStatusCode.Error, "upstream model rate limited");
        }

        if (failed)
        {
            LogTurnFailed(logger, profile.Provider, profile.Model, latencyMs);
        }
    }

    private static async Task EmitDbSpanAsync(string sessionId, CancellationToken stoppingToken)
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
    }

    private async Task<int> EmitGenAiSpanAsync(string sessionId, string route, ModelProfile profile, bool failed,
        CancellationToken stoppingToken)
    {
        var latencyMs = Random.Shared.Next(profile.LatencyMinMs, profile.LatencyMaxMs);
        var inputTokens = Random.Shared.Next(profile.InputMin, profile.InputMax);
        var outputTokens = Random.Shared.Next(profile.OutputMin, profile.OutputMax);

        using var span = WorkloadTelemetry.Source.StartActivity($"chat {profile.Model}", ActivityKind.Client);
        span?.SetGenAiOperationName(GenAiSpans.GenAiOperationNameValues.Chat)
            .SetGenAiOutputType(route is "/api/search"
                ? GenAiSpans.GenAiOutputTypeValues.Json
                : GenAiSpans.GenAiOutputTypeValues.Text)
            .SetGenAiProviderName(profile.Provider)
            .SetGenAiRequestModel(profile.Model)
            .SetGenAiRequestTemperature(Math.Round(Random.Shared.NextDouble(), 2, MidpointRounding.AwayFromZero))
            .SetSessionId(sessionId);

        await Task.Delay(latencyMs, stoppingToken).ConfigureAwait(false);

        var durationSeconds = latencyMs / 1000.0;

        if (failed)
        {
            span?.SetErrorType("rate_limit_exceeded");
            span?.SetStatus(ActivityStatusCode.Error, "429 from provider");

            WorkloadTelemetry.RecordGenAiOperationDuration(
                GenAiSpans.GenAiOperationNameValues.Chat,
                profile.Provider,
                profile.Model,
                responseModel: null,
                durationSeconds,
                errorType: "rate_limit_exceeded");
        }
        else
        {
            span?.SetGenAiResponseModel(profile.Model)
                .SetGenAiResponseFinishReasons(["stop"])
                .SetGenAiUsageInputTokens(inputTokens)
                .SetGenAiUsageOutputTokens(outputTokens);

            WorkloadTelemetry.RecordGenAiOperationDuration(
                GenAiSpans.GenAiOperationNameValues.Chat,
                profile.Provider,
                profile.Model,
                profile.Model,
                durationSeconds,
                errorType: null);

            WorkloadTelemetry.RecordGenAiTokenUsage(
                GenAiSpans.GenAiOperationNameValues.Chat,
                profile.Provider,
                profile.Model,
                profile.Model,
                inputTokens,
                outputTokens);

            LogTurnCompleted(logger, profile.Provider, profile.Model, inputTokens, outputTokens, latencyMs);
        }

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
