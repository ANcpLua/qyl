namespace Qyl.Collector.Storage;

internal static partial class DuckDbSchema
{
    public const string ProfilesIndexesDdl = """
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_id ON profiles(project_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_profile_id ON profiles(project_id, profile_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_trace_id ON profiles(project_id, trace_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_span_id ON profiles(project_id, span_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_session_id ON profiles(project_id, session_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_time ON profiles(project_id, time_unix_nano);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_service ON profiles(project_id, service_name);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_project_sample_type ON profiles(project_id, sample_type);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_trace_id ON profiles(trace_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_session_id ON profiles(session_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_time ON profiles(time_unix_nano);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_service ON profiles(service_name);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_sample_type ON profiles(sample_type);
                                             CREATE INDEX IF NOT EXISTS idx_profile_samples_trace ON profile_samples(link_trace_id);
                                             """;
}
