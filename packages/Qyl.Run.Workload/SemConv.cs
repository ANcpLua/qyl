using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

namespace Qyl.Run.Workload;

// The generator fuses typed members onto these partials from the embedded semconv
// registry (core 1.43.0 + dev GenAI). gen_ai and session are development-stability,
// so they need the Incubating markers — a stable marker would generate an empty class.
// One tier per prefix per namespace: stable + incubating together would make the
// generated extension-method signatures ambiguous at call sites.

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

[SemanticConventionMeters("http.server")]
internal static partial class HttpServerMeters;

[SemanticConventionIncubatingMeters("db.client")]
internal static partial class DbClientMeters;

[SemanticConventionIncubatingMeters("gen_ai.client")]
internal static partial class GenAiClientMeters;

[SemanticConventionIncubatingAttributes("gen_ai")]
internal static partial class GenAiAttrs;

[SemanticConventionAttributes("http")]
internal static partial class HttpAttrs;

[SemanticConventionAttributes("db")]
internal static partial class DbAttrs;
