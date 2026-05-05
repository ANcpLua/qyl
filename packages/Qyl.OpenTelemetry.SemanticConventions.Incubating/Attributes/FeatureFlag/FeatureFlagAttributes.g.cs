

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.FeatureFlag;

public static class FeatureFlagAttributes
{
    public const string ContextId = "feature_flag.context.id";

    public const string ErrorMessage = "feature_flag.error.message";

    [global::System.Obsolete("Replaced by feature_flag.error.message.", false)]
    public const string EvaluationErrorMessage = "feature_flag.evaluation.error.message";

    [global::System.Obsolete("Replaced by feature_flag.result.reason.", false)]
    public const string EvaluationReason = "feature_flag.evaluation.reason";

    public static class EvaluationReasonValues
    {
        public const string Cached = "cached";

        public const string Default = "default";

        public const string Disabled = "disabled";

        public const string Error = "error";

        public const string Split = "split";

        public const string Stale = "stale";

        public const string Static = "static";

        public const string TargetingMatch = "targeting_match";

        public const string Unknown = "unknown";
    }

    public const string Key = "feature_flag.key";

    public const string ProviderName = "feature_flag.provider.name";

    public const string ResultReason = "feature_flag.result.reason";

    public static class ResultReasonValues
    {
        public const string Cached = "cached";

        public const string Default = "default";

        public const string Disabled = "disabled";

        public const string Error = "error";

        public const string Split = "split";

        public const string Stale = "stale";

        public const string Static = "static";

        public const string TargetingMatch = "targeting_match";

        public const string Unknown = "unknown";
    }

    public const string ResultValue = "feature_flag.result.value";

    public const string ResultVariant = "feature_flag.result.variant";

    public const string SetId = "feature_flag.set.id";

    [global::System.Obsolete("Replaced by feature_flag.result.variant.", false)]
    public const string Variant = "feature_flag.variant";

    public const string Version = "feature_flag.version";
}
