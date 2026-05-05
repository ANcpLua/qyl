
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation;

internal static class GenAiConstants
{
    public const string SourceName = "qyl.genai";

    public const string CaptureMessageContentEnvVar = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";

    public const string AzureAiInferenceProvider = GenAiAttributes.ProviderNameValues.AzureAiInference;

    public static string? TryGetDefaultOutputType(string operation) =>
        operation switch
        {
            GenAiAttributes.OperationNameValues.Chat => GenAiAttributes.OutputTypeValues.Text,
            GenAiAttributes.OperationNameValues.GenerateContent => GenAiAttributes.OutputTypeValues.Text,
            GenAiAttributes.OperationNameValues.InvokeAgent => GenAiAttributes.OutputTypeValues.Text,
            GenAiAttributes.OperationNameValues.TextCompletion => GenAiAttributes.OutputTypeValues.Text,
            GenAiAttributes.OperationNameValues.Embeddings => GenAiAttributes.OutputTypeValues.Json,
            "image_generation" => GenAiAttributes.OutputTypeValues.Image,
            "speech" => GenAiAttributes.OutputTypeValues.Speech,
            _ => null
        };
}
