// =============================================================================
// qyl.instrumentation - GenAI Configuration Constants
// QYL-specific constants only - OTel semconv attributes are in Qyl.Contracts
// =============================================================================

using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     QYL-specific GenAI configuration constants.
///     <para>
///         Note: OTel semantic convention attributes and values are in
///         <c>Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes</c>.
///     </para>
/// </summary>
internal static class GenAiConstants
{
    /// <summary>Default activity source name for qyl GenAI instrumentation.</summary>
    public const string SourceName = "qyl.genai";

    /// <summary>
    ///     Environment variable to enable sensitive data capture.
    ///     Standard OTel GenAI env var respected by M.E.AI.
    /// </summary>
    public const string CaptureMessageContentEnvVar = "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT";

    /// <summary>Canonical provider name for Azure AI Inference.</summary>
    public const string AzureAiInferenceProvider = GenAiAttributes.ProviderNameValues.AzureAiInference;

    /// <summary>
    ///     Returns the default <c>gen_ai.output.type</c> for a known operation.
    ///     Returns <c>null</c> when output modality is not known at compile time.
    /// </summary>
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
