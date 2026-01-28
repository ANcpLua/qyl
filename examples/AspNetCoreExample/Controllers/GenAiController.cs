using Microsoft.AspNetCore.Mvc;
using OTelConventions;
using qyl.AspNetCore.Example.Models.Telemetry;
using qyl.AspNetCore.Example.Telemetry;

namespace qyl.AspNetCore.Example.Controllers;

[ApiController]
[Route("genai")]
public class GenAiController : ControllerBase
{
    private readonly ILogger<GenAiController> _logger;

    public GenAiController(ILogger<GenAiController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public ActionResult<GenAiSpanData> Process([FromBody] GenAiSpanData? request)
    {
        var spanData = request ?? CreateSampleData();

        using var activity = AppTelemetry.Source.StartActivity("GenAI.Process", System.Diagnostics.ActivityKind.Internal);
        activity?.SetTag(GenAiOperationAttributes.Name, spanData.OperationName);
        activity?.SetTag(GenAiProviderAttributes.Name, spanData.ProviderName);
        activity?.SetTag(GenAiRequestAttributes.Model, spanData.RequestModel);
        activity?.SetTag(GenAiResponseAttributes.Model, spanData.ResponseModel);

        if (spanData.Temperature.HasValue)
            activity?.SetTag(GenAiRequestAttributes.Temperature, spanData.Temperature.Value);

        if (spanData.MaxTokens.HasValue)
            activity?.SetTag(GenAiRequestAttributes.MaxTokens, spanData.MaxTokens.Value);

        if (spanData.ResponseId is not null)
            activity?.SetTag(GenAiResponseAttributes.Id, spanData.ResponseId);

        var stopwatch = Stopwatch.StartNew();
        stopwatch.Stop();

        var totalTokens = spanData.TotalTokens ??
                          (spanData.InputTokens ?? 0) + (spanData.OutputTokens ?? 0);

        if (totalTokens > 0)
            AppTelemetry.GenAiTokenUsage.Record(totalTokens);

        AppTelemetry.GenAiOperationDuration.Record(stopwatch.Elapsed.TotalSeconds);

        Log.GenAiSpanProcessed(_logger, spanData);

        return Ok(spanData);
    }

    private static GenAiSpanData CreateSampleData()
        => new()
        {
            OperationName = GenAiOperationNameValues.Chat,
            ProviderName = GenAiProviderNameValues.Openai,
            RequestModel = "gpt-4o-mini",
            ResponseModel = "gpt-4o-mini",
            InputTokens = 120,
            OutputTokens = 42,
            TotalTokens = 162,
            Temperature = 0.7,
            MaxTokens = 256,
            FinishReason = "stop",
            ResponseId = Guid.NewGuid().ToString("N")
        };
}
