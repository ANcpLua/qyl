namespace Qyl.Collector.Ingestion;

internal sealed record SourceLocation(string? FilePath, int? Line, int? Column, string? MethodName)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(FilePath) &&
                           !Line.HasValue &&
                           !Column.HasValue &&
                           string.IsNullOrWhiteSpace(MethodName);
}
