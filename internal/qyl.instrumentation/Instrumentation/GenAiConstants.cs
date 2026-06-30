
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation;

internal static class GenAiConstants
{
    public const string SourceName = "qyl.genai";

    public const string TokenUsageMetricName = "gen_ai.client.token.usage";
    public const string OperationDurationMetricName = "gen_ai.client.operation.duration";

    public const string UnknownOperation = "unknown";

    public static string NormalizeOperationName(string? operation) =>
        operation switch
        {
            GenAiAttributes.OperationNameValues.Chat => GenAiAttributes.OperationNameValues.Chat,
            GenAiAttributes.OperationNameValues.GenerateContent => GenAiAttributes.OperationNameValues.GenerateContent,
            GenAiAttributes.OperationNameValues.InvokeAgent => GenAiAttributes.OperationNameValues.InvokeAgent,
            GenAiAttributes.OperationNameValues.TextCompletion => GenAiAttributes.OperationNameValues.TextCompletion,
            GenAiAttributes.OperationNameValues.Embeddings => GenAiAttributes.OperationNameValues.Embeddings,
            GenAiAttributes.OperationNameValues.ExecuteTool => GenAiAttributes.OperationNameValues.ExecuteTool,
            "image_generation" => "image_generation",
            "speech" => "speech",
            _ => UnknownOperation
        };

    public static string? TryGetDefaultOutputType(string operation) =>
        operation switch
        {
            GenAiAttributes.OperationNameValues.Chat or
            GenAiAttributes.OperationNameValues.GenerateContent or
            GenAiAttributes.OperationNameValues.InvokeAgent or
            GenAiAttributes.OperationNameValues.TextCompletion => GenAiAttributes.OutputTypeValues.Text,
            GenAiAttributes.OperationNameValues.Embeddings => GenAiAttributes.OutputTypeValues.Json,
            "image_generation" => GenAiAttributes.OutputTypeValues.Image,
            "speech" => GenAiAttributes.OutputTypeValues.Speech,
            _ => null
        };
}
