

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Dotnet;

public static class DotnetAttributes
{
    public const string GcHeapGeneration = "dotnet.gc.heap.generation";

    public static class GcHeapGenerationValues
    {
        public const string Gen0 = "gen0";

        public const string Gen1 = "gen1";

        public const string Gen2 = "gen2";

        public const string Loh = "loh";

        public const string Poh = "poh";
    }
}
