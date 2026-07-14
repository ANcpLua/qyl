using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

namespace Qyl.Run.Workload;

// Each prefix must use one stability tier; mixing stable and incubating markers
// generates ambiguous extension methods. gen_ai and session are incubating.

[SemanticConventionActivities("http")]
internal static partial class HttpSpans;

[SemanticConventionActivities("server")]
internal static partial class ServerSpans;

[SemanticConventionActivities("db")]
internal static partial class DbSpans;

[SemanticConventionActivities("error")]
internal static partial class ErrorSpans;

[SemanticConventionIncubatingActivities("gen_ai")]
internal static partial class GenAiSpans;

[SemanticConventionIncubatingActivities("session")]
internal static partial class SessionSpans;
