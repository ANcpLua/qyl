
namespace Qyl.Collector.Query;

public sealed class SpanQueryBuilder
{
    private readonly bool _distinct;
    private readonly List<string> _groupByCols;
    private readonly int _limit;
    private readonly bool _limitIsParam;
    private readonly int _offset;
    private readonly bool _offsetIsParam;
    private readonly List<OrderByClause> _orderByCols;
    private readonly List<object?> _parameters;
    private readonly List<string> _selectCols;
    private readonly List<WhereClause> _whereClauses;

    private SpanQueryBuilder(
        List<string> selectCols,
        List<WhereClause> whereClauses,
        List<string> groupByCols,
        List<OrderByClause> orderByCols,
        List<object?> parameters,
        int limit,
        int offset,
        bool distinct,
        bool limitIsParam,
        bool offsetIsParam)
    {
        _selectCols = selectCols;
        _whereClauses = whereClauses;
        _groupByCols = groupByCols;
        _orderByCols = orderByCols;
        _parameters = parameters;
        _limit = limit;
        _offset = offset;
        _distinct = distinct;
        _limitIsParam = limitIsParam;
        _offsetIsParam = offsetIsParam;
    }

    private int NextParamIndex => _parameters.Count + 1;

    public static SpanQueryBuilder Create() => new([], [], [], [], [], 0, 0, false, false, false);


    public SpanQueryBuilder SelectAll() => Select("*");

    public SpanQueryBuilder Select(SpanColumn col) => Select(col.ToSql());

    public SpanQueryBuilder Select(string expr)
    {
        var cols = new List<string>(_selectCols) { expr };
        return new SpanQueryBuilder(cols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder SelectCount(string alias = "count") => Select($"COUNT(*) AS {alias}");

    public SpanQueryBuilder SelectSum(SpanColumn col, string alias)
        => Select($"COALESCE(SUM({col.ToSql()}), 0) AS {alias}");

    public SpanQueryBuilder SelectPercentile(SpanColumn col, double percentile, string alias)
        => Select($"PERCENTILE_CONT({percentile:F2}) WITHIN GROUP (ORDER BY {col.ToSql()}) AS {alias}");

    public SpanQueryBuilder SelectDistinctList(SpanColumn col, string alias)
        => Select($"LIST(DISTINCT {col.ToSql()}) FILTER (WHERE {col.ToSql()} IS NOT NULL) AS {alias}");

    public SpanQueryBuilder Distinct()
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, _offset, true,
            _limitIsParam, _offsetIsParam);


    public SpanQueryBuilder WhereEq(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters) { value };
        var clause = new WhereClause(col.ToSql(), CompareOp.Eq, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereNotNull(SpanColumn col)
    {
        var clause = new WhereClause(col.ToSql(), CompareOp.IsNotNull, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereOptional(SpanColumn col, object? value)
    {
        var idx = NextParamIndex;
        var newParams = new List<object?>(_parameters) { value };
        var sql = $"(${idx}::VARCHAR IS NULL OR {col.ToSql()} IS NOT DISTINCT FROM ${idx})";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereRaw(string sql)
    {
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereWithFallback(SpanColumn primary, SpanColumn fallback, object? value)
    {
        var idx = NextParamIndex;
        var newParams = new List<object?>(_parameters) { value };
        var sql = $"({primary.ToSql()} = ${idx} OR ({primary.ToSql()} IS NULL AND {fallback.ToSql()} = ${idx}))";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }


    public SpanQueryBuilder Where(SpanColumn col, CompareOp op, int paramIndex)
    {
        var clause = new WhereClause(col.ToSql(), op, $"${paramIndex}");
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereEq(SpanColumn col, int paramIndex) => Where(col, CompareOp.Eq, paramIndex);

    public SpanQueryBuilder WhereOptional(SpanColumn col, int paramIndex)
    {
        var sql = $"(${paramIndex}::VARCHAR IS NULL OR {col.ToSql()} IS NOT DISTINCT FROM ${paramIndex})";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder WhereWithFallback(SpanColumn primary, SpanColumn fallback, int paramIndex)
    {
        var sql =
            $"({primary.ToSql()} = ${paramIndex} OR ({primary.ToSql()} IS NULL AND {fallback.ToSql()} = ${paramIndex}))";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses) { clause };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }


    public SpanQueryBuilder GroupBy(SpanColumn col)
    {
        var cols = new List<string>(_groupByCols) { col.ToSql() };
        return new SpanQueryBuilder(_selectCols, _whereClauses, cols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder GroupBy(string expr)
    {
        var cols = new List<string>(_groupByCols) { expr };
        return new SpanQueryBuilder(_selectCols, _whereClauses, cols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder OrderBy(SpanColumn col, bool descending = false)
    {
        var order = new OrderByClause(col.ToSql(), descending);
        var cols = new List<OrderByClause>(_orderByCols) { order };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, cols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder OrderBy(string expr, bool descending = false)
    {
        var order = new OrderByClause(expr, descending);
        var cols = new List<OrderByClause>(_orderByCols) { order };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, cols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    public SpanQueryBuilder OrderByDesc(SpanColumn col) => OrderBy(col, true);

    public SpanQueryBuilder OrderByDesc(string expr) => OrderBy(expr, true);


    public SpanQueryBuilder Limit(int limit)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, limit, _offset, _distinct, false,
            _offsetIsParam);

    public SpanQueryBuilder LimitParam(int paramIndex)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, paramIndex, _offset, _distinct,
            true, _offsetIsParam);


    public string Build() => BuildQuery().Sql;

    public SpanQuery BuildQuery()
    {
        var sb = new StringBuilder();

        sb.Append("SELECT ");
        if (_distinct) sb.Append("DISTINCT ");
        sb.AppendLine(_selectCols.Count > 0 ? string.Join(", ", _selectCols) : "*");

        sb.AppendLine("FROM spans");

        if (_whereClauses.Count > 0)
        {
            sb.Append("WHERE ");
            for (var i = 0; i < _whereClauses.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(_whereClauses[i].ToSql());
            }

            sb.AppendLine();
        }

        if (_groupByCols.Count > 0)
        {
            sb.Append("GROUP BY ");
            sb.AppendLine(string.Join(", ", _groupByCols));
        }

        if (_orderByCols.Count > 0)
        {
            sb.Append("ORDER BY ");
            sb.AppendLine(string.Join(", ", _orderByCols.Select(static o => o.ToSql())));
        }

        if (_limit > 0)
            sb.AppendLine(_limitIsParam ? $"LIMIT ${_limit}" : $"LIMIT {_limit}");

        if (_offset > 0)
            sb.AppendLine(_offsetIsParam ? $"OFFSET ${_offset}" : $"OFFSET {_offset}");

        return new SpanQuery(sb.ToString().TrimEnd(), _parameters);
    }

    public void ApplyTo(DuckDBCommand cmd)
    {
        cmd.CommandText = Build();
        foreach (var param in _parameters)
        {
            cmd.Parameters.Add(new DuckDBParameter { Value = param ?? DBNull.Value });
        }
    }

    public override string ToString() => Build();
}


public readonly record struct SpanQuery(string Sql, IReadOnlyList<object?> Parameters)
{
    public void ApplyTo(DuckDBCommand cmd)
    {
        cmd.CommandText = Sql;
        foreach (var param in Parameters)
        {
            cmd.Parameters.Add(new DuckDBParameter { Value = param ?? DBNull.Value });
        }
    }

    public IReadOnlyList<DuckDBParameter> ToDuckDbParameters()
        =>
        [
            .. Parameters.Select(static (object? p) => new DuckDBParameter { Value = p ?? DBNull.Value })
        ];
}


public readonly struct SpanColumn
{
    private readonly string _name;

    private SpanColumn(string name) => _name = name;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToSql() => _name;

    public override string ToString() => _name;

    public static SpanColumn TraceId => new("trace_id");
    public static SpanColumn SessionId => new("session_id");
    public static SpanColumn StartTimeUnixNano => new("start_time_unix_nano");
    public static SpanColumn GenAiProviderName => new("gen_ai_provider_name");
    public static SpanColumn GenAiRequestModel => new("gen_ai_request_model");
    public static SpanColumn GenAiInputTokens => new("gen_ai_input_tokens");
    public static SpanColumn GenAiOutputTokens => new("gen_ai_output_tokens");

    public static SpanColumn Column(string name) => new(name);
}


public enum CompareOp
{
    Eq,
    Ne,
    Lt,
    Lte,
    Gt,
    Gte,
    IsNull,
    IsNotNull,
    Like,
    In,
    Raw
}

internal readonly struct WhereClause(string column, CompareOp op, string? param)
{
    public string ToSql() => op switch
    {
        CompareOp.Eq => $"{column} = {param}",
        CompareOp.Ne => $"{column} != {param}",
        CompareOp.Lt => $"{column} < {param}",
        CompareOp.Lte => $"{column} <= {param}",
        CompareOp.Gt => $"{column} > {param}",
        CompareOp.Gte => $"{column} >= {param}",
        CompareOp.IsNull => $"{column} IS NULL",
        CompareOp.IsNotNull => $"{column} IS NOT NULL",
        CompareOp.Like => $"{column} LIKE {param}",
        CompareOp.In => $"{column} IN ({param})",
        CompareOp.Raw => column,
        _ => throw new ArgumentOutOfRangeException()
    };
}

internal readonly struct OrderByClause(string expr, bool descending)
{
    public string ToSql() => descending ? $"{expr} DESC" : expr;
}
