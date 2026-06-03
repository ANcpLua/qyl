namespace Qyl.Collector.Storage;

internal static partial class DuckDbSchema
{
    public const string ProfilesDdl = """
                                      CREATE TABLE IF NOT EXISTS profiles (
                                          profile_id VARCHAR NOT NULL,
                                          PRIMARY KEY (profile_id),
                                          trace_id VARCHAR,
                                          span_id VARCHAR,
                                          session_id VARCHAR,
                                          time_unix_nano UBIGINT NOT NULL,
                                          duration_nano UBIGINT NOT NULL,
                                          sample_count INTEGER NOT NULL,
                                          sample_type VARCHAR,
                                          sample_unit VARCHAR,
                                          original_payload_format VARCHAR,
                                          service_name VARCHAR,
                                          attributes_json VARCHAR,
                                          resource_json VARCHAR,
                                          schema_url VARCHAR(256),
                                          created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                                      );
                                      """;

    public const string ProfileFunctionsDdl = """
                                              CREATE TABLE IF NOT EXISTS profile_functions (
                                                  profile_id VARCHAR NOT NULL,
                                                  ordinal INTEGER NOT NULL,
                                                  name VARCHAR,
                                                  system_name VARCHAR,
                                                  filename VARCHAR,
                                                  start_line BIGINT,
                                                  PRIMARY KEY (profile_id, ordinal)
                                              );
                                              """;

    public const string ProfileLocationsDdl = """
                                              CREATE TABLE IF NOT EXISTS profile_locations (
                                                  profile_id VARCHAR NOT NULL,
                                                  ordinal INTEGER NOT NULL,
                                                  mapping_ordinal INTEGER,
                                                  address UBIGINT,
                                                  lines_json VARCHAR,
                                                  PRIMARY KEY (profile_id, ordinal)
                                              );
                                              """;

    public const string ProfileMappingsDdl = """
                                             CREATE TABLE IF NOT EXISTS profile_mappings (
                                                 profile_id VARCHAR NOT NULL,
                                                 ordinal INTEGER NOT NULL,
                                                 filename VARCHAR,
                                                 memory_start UBIGINT,
                                                 memory_limit UBIGINT,
                                                 file_offset UBIGINT,
                                                 PRIMARY KEY (profile_id, ordinal)
                                             );
                                             """;

    public const string ProfileSamplesDdl = """
                                            CREATE TABLE IF NOT EXISTS profile_samples (
                                                profile_id VARCHAR NOT NULL,
                                                ordinal INTEGER NOT NULL,
                                                stack_ordinal INTEGER,
                                                link_trace_id VARCHAR,
                                                link_span_id VARCHAR,
                                                values_json VARCHAR,
                                                timestamps_json VARCHAR,
                                                PRIMARY KEY (profile_id, ordinal)
                                            );
                                            """;

    public const string ProfileStacksDdl = """
                                           CREATE TABLE IF NOT EXISTS profile_stacks (
                                               profile_id VARCHAR NOT NULL,
                                               ordinal INTEGER NOT NULL,
                                               location_ordinals_json VARCHAR,
                                               PRIMARY KEY (profile_id, ordinal)
                                           );
                                           """;

    public const string ProfilesIndexesDdl = """
                                             CREATE INDEX IF NOT EXISTS idx_profiles_trace_id ON profiles(trace_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_session_id ON profiles(session_id);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_time ON profiles(time_unix_nano);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_service ON profiles(service_name);
                                             CREATE INDEX IF NOT EXISTS idx_profiles_sample_type ON profiles(sample_type);
                                             CREATE INDEX IF NOT EXISTS idx_profile_samples_trace ON profile_samples(link_trace_id);
                                             """;
}
