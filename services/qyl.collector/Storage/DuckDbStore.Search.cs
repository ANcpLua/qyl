using System.Numerics;
using Qyl.Collector.Search;
using Qyl.Collector.Primitives;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchQuery query,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return ExecuteReadAsync<IReadOnlyList<SearchResult>>(con =>
        {
            var (sql, parameters) = UnifiedQueryEngine.BuildQuery(query);

            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);

            var results = new List<SearchResult>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(MapSearchResult(reader));

            return results;
        }, ct);
    }

    public Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string prefix,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            return Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);

        return ExecuteReadAsync<IReadOnlyList<SearchSuggestion>>(con =>
        {
            var likePattern = $"{SqlLikeEscape.Escape(prefix)}%";

            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              SELECT text, entity_type, cnt FROM (
                                  SELECT name AS text, 'spans' AS entity_type, COUNT(*) AS cnt
                                  FROM spans WHERE name ILIKE $1 ESCAPE '\'
                                  GROUP BY name
                                  UNION ALL
                                  SELECT COALESCE(service_name, '') AS text, 'spans' AS entity_type, COUNT(*) AS cnt
                                  FROM spans WHERE service_name ILIKE $1 ESCAPE '\'
                                  GROUP BY service_name
                                  UNION ALL
                                  SELECT COALESCE(agent_name, '') AS text, 'agent_runs' AS entity_type, COUNT(*) AS cnt
                                  FROM agent_runs WHERE agent_name ILIKE $1 ESCAPE '\'
                                  GROUP BY agent_name
                                  UNION ALL
                                  SELECT COALESCE(workflow_name, '') AS text, 'workflows' AS entity_type, COUNT(*) AS cnt
                                  FROM workflow_executions WHERE workflow_name ILIKE $1 ESCAPE '\'
                                  GROUP BY workflow_name
                                  UNION ALL
                                  SELECT COALESCE(workflow_id, '') AS text, 'workflows' AS entity_type, COUNT(*) AS cnt
                                  FROM workflow_runs WHERE workflow_id ILIKE $1 ESCAPE '\'
                                  GROUP BY workflow_id
                                  UNION ALL
                                  SELECT COALESCE(error_type, '') AS text, 'errors' AS entity_type, COUNT(*) AS cnt
                                  FROM error_issues WHERE error_type ILIKE $1 ESCAPE '\'
                                  GROUP BY error_type
                              ) AS suggestions
                              WHERE text != ''
                              ORDER BY cnt DESC
                              LIMIT 20
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = likePattern });

            var suggestions = new List<SearchSuggestion>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                suggestions.Add(new SearchSuggestion(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2)));
            }

            return suggestions;
        }, ct);
    }


    private static SearchResult MapSearchResult(DbDataReader reader)
    {
        var timestamp = ReadTimestamp(reader.GetValue(4));

        return new SearchResult(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.Col(3).AsString,
            timestamp,
            ReadScore(reader.GetValue(5)));
    }

    private static DateTime ReadTimestamp(object value) => value switch
    {
        ulong ulongValue => QylTimeConversions.UnixNanoToDateTime(ulongValue),
        long longValue when longValue >= 0 => QylTimeConversions.UnixNanoToDateTime((ulong)longValue),
        decimal decimalValue => QylTimeConversions.UnixNanoToDateTime((ulong)decimalValue),
        BigInteger bigIntegerValue => QylTimeConversions.UnixNanoToDateTime((ulong)bigIntegerValue),
        DateTime dateTimeValue => DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc),
        DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.UtcDateTime,
        _ => TimeProvider.System.GetUtcNow().UtcDateTime
    };

    private static double ReadScore(object value) => value switch
    {
        double doubleValue => doubleValue,
        float floatValue => floatValue,
        decimal decimalValue => (double)decimalValue,
        int intValue => intValue,
        long longValue => longValue,
        BigInteger bigIntegerValue => (double)bigIntegerValue,
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };
}
