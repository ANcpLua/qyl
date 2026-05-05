

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Cpython;

public static class CpythonAttributes
{
    public const string GcGeneration = "cpython.gc.generation";

    public static class GcGenerationValues
    {
        public const string Generation0 = "0";

        public const string Generation1 = "1";

        public const string Generation2 = "2";
    }
}
