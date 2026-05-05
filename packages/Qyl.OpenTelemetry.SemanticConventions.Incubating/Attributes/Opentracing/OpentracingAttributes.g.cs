

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Opentracing;

public static class OpentracingAttributes
{
    public const string RefType = "opentracing.ref_type";

    public static class RefTypeValues
    {
        public const string ChildOf = "child_of";

        public const string FollowsFrom = "follows_from";
    }
}
