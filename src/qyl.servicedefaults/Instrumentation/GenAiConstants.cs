// =============================================================================
// qyl.servicedefaults - GenAI Configuration Constants
// QYL-specific constants only - OTel semconv attributes are in qyl.protocol
// =============================================================================

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
}
