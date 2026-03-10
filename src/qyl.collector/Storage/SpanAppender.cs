// DuckDB Mapped Appender for SpanStorageRow and LogStorageRow
// Replaces: DuckDbInsertGenerator + AddParameters + BuildMultiRowInsertSql
// DuckDB.NET v1.4.4 — ulong → UBIGINT natively supported

using DuckDB.NET.Data.Mapping;

namespace qyl.collector.Storage;

public sealed class SpanStorageRowMap : DuckDBAppenderMap<SpanStorageRow>
{
    public SpanStorageRowMap()
    {
        Map(static r => r.SpanId);
        Map(static r => r.TraceId);
        Map(static r => r.ParentSpanId);
        Map(static r => r.SessionId);
        Map(static r => r.Name);
        Map(static r => r.Kind);
        Map(static r => r.StartTimeUnixNano);
        Map(static r => r.EndTimeUnixNano);
        Map(static r => r.DurationNs);
        Map(static r => r.StatusCode);
        Map(static r => r.StatusMessage);
        Map(static r => r.ServiceName);
        Map(static r => r.GenAiProviderName);
        Map(static r => r.GenAiRequestModel);
        Map(static r => r.GenAiResponseModel);
        Map(static r => r.GenAiInputTokens);
        Map(static r => r.GenAiOutputTokens);
        Map(static r => r.GenAiTemperature);
        Map(static r => r.GenAiStopReason);
        Map(static r => r.GenAiToolName);
        Map(static r => r.GenAiToolCallId);
        Map(static r => r.GenAiCostUsd);
        Map(static r => r.AttributesJson);
        Map(static r => r.ResourceJson);
        Map(static r => r.BaggageJson);
        Map(static r => r.SchemaUrl);
        DefaultValue(); // CreatedAt — set by DuckDB DEFAULT CURRENT_TIMESTAMP
    }
}

public sealed class LogStorageRowMap : DuckDBAppenderMap<LogStorageRow>
{
    public LogStorageRowMap()
    {
        Map(static r => r.LogId);
        Map(static r => r.TraceId);
        Map(static r => r.SpanId);
        Map(static r => r.SessionId);
        Map(static r => r.TimeUnixNano);
        Map(static r => r.ObservedTimeUnixNano);
        Map(static r => r.SeverityNumber);
        Map(static r => r.SeverityText);
        Map(static r => r.Body);
        Map(static r => r.ServiceName);
        Map(static r => r.AttributesJson);
        Map(static r => r.ResourceJson);
        Map(static r => r.SourceFile);
        Map(static r => r.SourceLine);
        Map(static r => r.SourceColumn);
        Map(static r => r.SourceMethod);
        DefaultValue(); // CreatedAt
    }
}
