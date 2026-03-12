namespace Qyl.Collector.Endpoints;

/// <summary>
///     Lightweight query builder for dynamic DuckDB WHERE clauses with positional parameters.
///     Mirrors the pattern used by DuckDbStore.QueryBuilder but accessible from endpoint code.
/// </summary>
internal struct McpQueryBuilder()
{
    private readonly List<string> _conditions = [];
    private readonly List<DuckDBParameter> _parameters = [];
    private int _paramIndex = 1;

    public void Add(string condition, object value)
    {
        _conditions.Add(condition.Replace("$N", $"${_paramIndex++}"));
        _parameters.Add(new DuckDBParameter { Value = value });
    }

    public readonly string WhereClause =>
        _conditions.Count > 0 ? $"WHERE {string.Join(" AND ", _conditions)}" : "";

    public readonly string NextParam => $"${_paramIndex}";

    public readonly void ApplyTo(DuckDBCommand cmd) => cmd.Parameters.AddRange(_parameters);
}
