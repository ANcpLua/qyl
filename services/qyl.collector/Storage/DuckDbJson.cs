namespace Qyl.Collector.Storage;

internal static class DuckDbJson
{
    internal static string ExtractString(string column, string attributeKey) =>
        "json_extract_string(" + column + ", " + JsonPathLiteral(attributeKey) + ")";

    internal static string ArrowString(string column, string attributeKey) =>
        column + "->>" + SqlLiteral(attributeKey);

    private static string JsonPathLiteral(string attributeKey) =>
        SqlLiteral("$.\"" + attributeKey.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"");

    private static string SqlLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
