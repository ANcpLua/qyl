namespace Qyl.Collector.Storage;

internal static partial class DuckDbSchema
{
    public const string CoreIndexesDdl = """
                                         CREATE INDEX IF NOT EXISTS idx_spans_project_id ON spans(project_id);
                                         CREATE INDEX IF NOT EXISTS idx_spans_project_trace_id ON spans(project_id, trace_id);
                                         CREATE INDEX IF NOT EXISTS idx_logs_project_time ON logs(project_id, time_unix_nano);
                                         CREATE INDEX IF NOT EXISTS idx_spans_trace_id ON spans(trace_id);
                                         CREATE INDEX IF NOT EXISTS idx_spans_session_id ON spans(session_id);
                                         CREATE INDEX IF NOT EXISTS idx_spans_start_time ON spans(start_time_unix_nano);
                                         CREATE INDEX IF NOT EXISTS idx_spans_service_name ON spans(service_name);
                                         CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_provider_name ON spans(gen_ai_provider_name);
                                         CREATE INDEX IF NOT EXISTS idx_spans_gen_ai_request_model ON spans(gen_ai_request_model);
                                         """;
}
