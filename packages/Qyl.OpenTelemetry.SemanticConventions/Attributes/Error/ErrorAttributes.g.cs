

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Error;

public static class ErrorAttributes
{
    public const string Type = "error.type";

    public static class TypeValues
    {
        public const string Other = "_OTHER";
    }
}
