

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Code;

public static class CodeAttributes
{
    [global::System.Obsolete("Replaced by code.column.number.", false)]
    public const string Column = "code.column";

    [global::System.Obsolete("Replaced by code.file.path.", false)]
    public const string Filepath = "code.filepath";

    [global::System.Obsolete("Value should be included in `code.function.name` which is expected to be a fully-qualified name.", false)]
    public const string Function = "code.function";

    [global::System.Obsolete("Replaced by code.line.number.", false)]
    public const string Lineno = "code.lineno";

    [global::System.Obsolete("Value should be included in `code.function.name` which is expected to be a fully-qualified name.", false)]
    public const string Namespace = "code.namespace";
}
