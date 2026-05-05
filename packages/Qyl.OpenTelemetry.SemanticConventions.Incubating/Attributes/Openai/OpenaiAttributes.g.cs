

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Openai;

public static class OpenaiAttributes
{
    public const string ApiType = "openai.api.type";

    public static class ApiTypeValues
    {
        public const string ChatCompletions = "chat_completions";

        public const string Responses = "responses";
    }

    public const string RequestServiceTier = "openai.request.service_tier";

    public static class RequestServiceTierValues
    {
        public const string Auto = "auto";

        public const string Default = "default";
    }

    public const string ResponseServiceTier = "openai.response.service_tier";

    public const string ResponseSystemFingerprint = "openai.response.system_fingerprint";
}
