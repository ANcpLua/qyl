// =============================================================================
// qyl.servicedefaults - GenAI Configuration Constants
// QYL-specific constants only - OTel semconv attributes are in qyl.protocol
// =============================================================================

using qyl.protocol.Attributes;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     QYL-specific GenAI configuration constants.
///     <para>
///         Note: OTel semantic convention attributes and values are in
///         <c>qyl.protocol.Attributes.GenAiAttributes</c>.
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
    public const string AzureAiInferenceProvider = GenAiAttributes.Providers.AzureAiInference;

    /// <summary>
    ///     Returns the default <c>gen_ai.output.type</c> for a known operation.
    ///     Returns <c>null</c> when output modality is not known at compile time.
    /// </summary>
    public static string? TryGetDefaultOutputType(string operation)
    {
        return operation switch
        {
            GenAiAttributes.Operations.Chat => GenAiAttributes.OutputTypes.Text,
            GenAiAttributes.Operations.GenerateContent => GenAiAttributes.OutputTypes.Text,
            GenAiAttributes.Operations.InvokeAgent => GenAiAttributes.OutputTypes.Text,
            GenAiAttributes.Operations.TextCompletion => GenAiAttributes.OutputTypes.Text,
            GenAiAttributes.Operations.Embeddings => GenAiAttributes.OutputTypes.Json,
            "image_generation" => GenAiAttributes.OutputTypes.Image,
            "speech" => GenAiAttributes.OutputTypes.Speech,
            _ => null
        };
    }
}
