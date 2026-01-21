// =============================================================================
// SpanQueryBuilder - Type-safe query construction with positional parameters
// =============================================================================

namespace qyl.collector.Query;

/// <summary>
///     Fluent query builder for span queries with promoted field optimization.
///     Uses positional parameters ($1, $2, etc.) for DuckDB.NET 1.4.3 compatibility.
/// </summary>
/// <remarks>
///     CRITICAL: DuckDB.NET 1.4.3 has a bug where named parameters ($session_id)
///     don't work with quoted column names ("session.id"). Always use positional
///     parameters ($1, $2, etc.) with DuckDBParameter { Value = ... }.
/// </remarks>
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

    /// <summary>Current parameter count (1-indexed for next param).</summary>
    private int NextParamIndex => _parameters.Count + 1;

    /// <summary>Create new builder for spans table.</summary>
    public static SpanQueryBuilder Create() => new([], [], [], [], [], 0, 0, false, false, false);

    // =========================================================================
    // SELECT
    // =========================================================================

    /// <summary>Select all columns.</summary>
    public SpanQueryBuilder SelectAll() => Select("*");

    /// <summary>Select specific column.</summary>
    public SpanQueryBuilder Select(SpanColumn col) => Select(col.ToSql());

    /// <summary>Select raw expression.</summary>
    public SpanQueryBuilder Select(string expr)
    {
        var cols = new List<string>(_selectCols)
        {
            expr
        };
        return new SpanQueryBuilder(cols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Select with alias.</summary>
    public SpanQueryBuilder SelectAs(SpanColumn col, string alias) => Select($"{col.ToSql()} AS {alias}");

    /// <summary>Select with alias.</summary>
    public SpanQueryBuilder SelectAs(string expr, string alias) => Select($"{expr} AS {alias}");

    /// <summary>Select COUNT(*).</summary>
    public SpanQueryBuilder SelectCount(string alias = "count") => Select($"COUNT(*) AS {alias}");

    /// <summary>Select COUNT(DISTINCT col).</summary>
    public SpanQueryBuilder SelectCountDistinct(SpanColumn col, string alias)
        => Select($"COUNT(DISTINCT {col.ToSql()}) AS {alias}");

    /// <summary>Select SUM(col).</summary>
    public SpanQueryBuilder SelectSum(SpanColumn col, string alias)
        => Select($"COALESCE(SUM({col.ToSql()}), 0) AS {alias}");

    /// <summary>Select AVG(col).</summary>
    public SpanQueryBuilder SelectAvg(SpanColumn col, string alias)
        => Select($"AVG({col.ToSql()}) AS {alias}");

    /// <summary>Select MIN(col).</summary>
    public SpanQueryBuilder SelectMin(SpanColumn col, string alias)
        => Select($"MIN({col.ToSql()}) AS {alias}");

    /// <summary>Select MAX(col).</summary>
    public SpanQueryBuilder SelectMax(SpanColumn col, string alias)
        => Select($"MAX({col.ToSql()}) AS {alias}");

    /// <summary>Select PERCENTILE_CONT.</summary>
    public SpanQueryBuilder SelectPercentile(SpanColumn col, double percentile, string alias)
        => Select($"PERCENTILE_CONT({percentile:F2}) WITHIN GROUP (ORDER BY {col.ToSql()}) AS {alias}");

    /// <summary>Select LIST(DISTINCT col) FILTER.</summary>
    public SpanQueryBuilder SelectDistinctList(SpanColumn col, string alias)
        => Select($"LIST(DISTINCT {col.ToSql()}) FILTER (WHERE {col.ToSql()} IS NOT NULL) AS {alias}");

    /// <summary>Select DISTINCT.</summary>
    public SpanQueryBuilder Distinct()
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, _offset, true,
            _limitIsParam, _offsetIsParam);

    // =========================================================================
    // WHERE - Typed value methods (recommended)
    // =========================================================================

    /// <summary>Add WHERE col = value (adds parameter automatically).</summary>
    public SpanQueryBuilder WhereEq(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var clause = new WhereClause(col.ToSql(), CompareOp.Eq, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col >= value.</summary>
    public SpanQueryBuilder WhereGte(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var clause = new WhereClause(col.ToSql(), CompareOp.Gte, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col > value.</summary>
    public SpanQueryBuilder WhereGt(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var clause = new WhereClause(col.ToSql(), CompareOp.Gt, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col &lt;= value.</summary>
    public SpanQueryBuilder WhereLte(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var clause = new WhereClause(col.ToSql(), CompareOp.Lte, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col &lt; value.</summary>
    public SpanQueryBuilder WhereLt(SpanColumn col, object? value)
    {
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var clause = new WhereClause(col.ToSql(), CompareOp.Lt, $"${NextParamIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col IS NOT NULL.</summary>
    public SpanQueryBuilder WhereNotNull(SpanColumn col)
    {
        var clause = new WhereClause(col.ToSql(), CompareOp.IsNotNull, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col IS NULL.</summary>
    public SpanQueryBuilder WhereNull(SpanColumn col)
    {
        var clause = new WhereClause(col.ToSql(), CompareOp.IsNull, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE ($param IS NULL OR col IS NOT DISTINCT FROM $param) - optional filter.</summary>
    /// <remarks>
    ///     DuckDB.NET 1.4.3 BUG: = operator doesn't work correctly for VARCHAR columns in WHERE.
    ///     Using IS NOT DISTINCT FROM as workaround.
    /// </remarks>
    public SpanQueryBuilder WhereOptional(SpanColumn col, object? value)
    {
        var idx = NextParamIndex;
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var sql = $"(${idx}::VARCHAR IS NULL OR {col.ToSql()} IS NOT DISTINCT FROM ${idx})";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE with raw SQL (no parameters added).</summary>
    public SpanQueryBuilder WhereRaw(string sql)
    {
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE with raw SQL and parameter.</summary>
    public SpanQueryBuilder WhereRawWithParam(string sqlTemplate, object? value)
    {
        var idx = NextParamIndex;
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var sql = sqlTemplate.Replace("{p}", $"${idx}");
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE with fallback (col = value OR (col IS NULL AND other = value)).</summary>
    public SpanQueryBuilder WhereWithFallback(SpanColumn primary, SpanColumn fallback, object? value)
    {
        var idx = NextParamIndex;
        var newParams = new List<object?>(_parameters)
        {
            value
        };
        var sql = $"({primary.ToSql()} = ${idx} OR ({primary.ToSql()} IS NULL AND {fallback.ToSql()} = ${idx}))";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, newParams, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    // =========================================================================
    // WHERE - Index-based methods (for complex scenarios)
    // =========================================================================

    /// <summary>Add WHERE clause referencing parameter by index (1-based).</summary>
    public SpanQueryBuilder Where(SpanColumn col, CompareOp op, int paramIndex)
    {
        var clause = new WhereClause(col.ToSql(), op, $"${paramIndex}");
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE col = $paramIndex (index-based).</summary>
    public SpanQueryBuilder WhereEq(SpanColumn col, int paramIndex) => Where(col, CompareOp.Eq, paramIndex);

    /// <summary>Add WHERE col >= $paramIndex (index-based).</summary>
    public SpanQueryBuilder WhereGte(SpanColumn col, int paramIndex) => Where(col, CompareOp.Gte, paramIndex);

    /// <summary>Add WHERE col > $paramIndex (index-based).</summary>
    public SpanQueryBuilder WhereGt(SpanColumn col, int paramIndex) => Where(col, CompareOp.Gt, paramIndex);

    /// <summary>Add WHERE ($paramIndex IS NULL OR col = $paramIndex) - optional filter (index-based).</summary>
    public SpanQueryBuilder WhereOptional(SpanColumn col, int paramIndex)
    {
        // DuckDB.NET 1.4.3 BUG: = operator doesn't work for VARCHAR in WHERE
        var sql = $"(${paramIndex}::VARCHAR IS NULL OR {col.ToSql()} IS NOT DISTINCT FROM ${paramIndex})";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add WHERE with fallback (col = $p OR (col IS NULL AND other = $p)) (index-based).</summary>
    public SpanQueryBuilder WhereWithFallback(SpanColumn primary, SpanColumn fallback, int paramIndex)
    {
        var sql =
            $"({primary.ToSql()} = ${paramIndex} OR ({primary.ToSql()} IS NULL AND {fallback.ToSql()} = ${paramIndex}))";
        var clause = new WhereClause(sql, CompareOp.Raw, null);
        var clauses = new List<WhereClause>(_whereClauses)
        {
            clause
        };
        return new SpanQueryBuilder(_selectCols, clauses, _groupByCols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    // =========================================================================
    // GROUP BY / ORDER BY
    // =========================================================================

    /// <summary>Add GROUP BY col.</summary>
    public SpanQueryBuilder GroupBy(SpanColumn col)
    {
        var cols = new List<string>(_groupByCols)
        {
            col.ToSql()
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, cols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add GROUP BY expression.</summary>
    public SpanQueryBuilder GroupBy(string expr)
    {
        var cols = new List<string>(_groupByCols)
        {
            expr
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, cols, _orderByCols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add ORDER BY col.</summary>
    public SpanQueryBuilder OrderBy(SpanColumn col, bool descending = false)
    {
        var order = new OrderByClause(col.ToSql(), descending);
        var cols = new List<OrderByClause>(_orderByCols)
        {
            order
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, cols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add ORDER BY expression.</summary>
    public SpanQueryBuilder OrderBy(string expr, bool descending = false)
    {
        var order = new OrderByClause(expr, descending);
        var cols = new List<OrderByClause>(_orderByCols)
        {
            order
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, cols, _parameters, _limit, _offset,
            _distinct, _limitIsParam, _offsetIsParam);
    }

    /// <summary>Add ORDER BY col DESC.</summary>
    public SpanQueryBuilder OrderByDesc(SpanColumn col) => OrderBy(col, true);

    /// <summary>Add ORDER BY expr DESC.</summary>
    public SpanQueryBuilder OrderByDesc(string expr) => OrderBy(expr, true);

    // =========================================================================
    // LIMIT / OFFSET
    // =========================================================================

    /// <summary>Set LIMIT with literal value.</summary>
    public SpanQueryBuilder Limit(int limit)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, limit, _offset, _distinct, false,
            _offsetIsParam);

    /// <summary>Set LIMIT with value (adds parameter).</summary>
    public SpanQueryBuilder LimitValue(int limit)
    {
        var newParams = new List<object?>(_parameters)
        {
            limit
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, _orderByCols, newParams, NextParamIndex,
            _offset, _distinct, true, _offsetIsParam);
    }

    /// <summary>Set OFFSET with literal value.</summary>
    public SpanQueryBuilder Offset(int offset)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, offset, _distinct,
            _limitIsParam, false);

    /// <summary>Set OFFSET with value (adds parameter).</summary>
    public SpanQueryBuilder OffsetValue(int offset)
    {
        var newParams = new List<object?>(_parameters)
        {
            offset
        };
        return new SpanQueryBuilder(_selectCols, _whereClauses, _groupByCols, _orderByCols, newParams, _limit,
            NextParamIndex, _distinct, _limitIsParam, true);
    }

    /// <summary>Set LIMIT via parameter index (1-based).</summary>
    public SpanQueryBuilder LimitParam(int paramIndex)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, paramIndex, _offset, _distinct,
            true, _offsetIsParam);

    /// <summary>Set OFFSET via parameter index (1-based).</summary>
    public SpanQueryBuilder OffsetParam(int paramIndex)
        => new(_selectCols, _whereClauses, _groupByCols, _orderByCols, _parameters, _limit, paramIndex, _distinct,
            _limitIsParam, true);

    // =========================================================================
    // BUILD
    // =========================================================================

    /// <summary>Build SQL string only.</summary>
    public string Build() => BuildQuery().Sql;

    /// <summary>Build SQL string and parameter list.</summary>
    public SpanQuery BuildQuery()
    {
        var sb = new StringBuilder();

        // SELECT
        sb.Append("SELECT ");
        if (_distinct) sb.Append("DISTINCT ");
        sb.AppendLine(_selectCols.Count > 0 ? string.Join(", ", _selectCols) : "*");

        // FROM
        sb.AppendLine("FROM spans");

        // WHERE
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

        // GROUP BY
        if (_groupByCols.Count > 0)
        {
            sb.Append("GROUP BY ");
            sb.AppendLine(string.Join(", ", _groupByCols));
        }

        // ORDER BY
        if (_orderByCols.Count > 0)
        {
            sb.Append("ORDER BY ");
            sb.AppendLine(string.Join(", ", _orderByCols.Select(static o => o.ToSql())));
        }

        // LIMIT
        if (_limit > 0)
            sb.AppendLine(_limitIsParam ? $"LIMIT ${_limit}" : $"LIMIT {_limit}");

        // OFFSET
        if (_offset > 0)
            sb.AppendLine(_offsetIsParam ? $"OFFSET ${_offset}" : $"OFFSET {_offset}");

        return new SpanQuery(sb.ToString().TrimEnd(), _parameters);
    }

    /// <summary>Apply parameters to a DuckDB command.</summary>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Parameterized queries - verified safe")]
    public void ApplyTo(DuckDBCommand cmd)
    {
        cmd.CommandText = Build();
        foreach (var param in _parameters)
        {
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = param ?? DBNull.Value
            });
        }
    }

    public override string ToString() => Build();
}

// =============================================================================
// SpanQuery - Result of building a query
// =============================================================================

/// <summary>Built query with SQL and parameters.</summary>
public readonly record struct SpanQuery(string Sql, IReadOnlyList<object?> Parameters)
{
    /// <summary>Apply to a DuckDB command.</summary>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Parameterized queries - verified safe")]
    public void ApplyTo(DuckDBCommand cmd)
    {
        cmd.CommandText = Sql;
        foreach (var param in Parameters)
        {
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = param ?? DBNull.Value
            });
        }
    }

    /// <summary>Create DuckDB parameters list.</summary>
    public IReadOnlyList<DuckDBParameter> ToDuckDbParameters()
        =>
        [
            .. Parameters.Select(static p => new DuckDBParameter
            {
                Value = p ?? DBNull.Value
            })
        ];
}

// =============================================================================
// SpanColumn - Type-safe column references matching DuckDbSchema.g.cs
// =============================================================================

/// <summary>
///     Span table columns using snake_case names matching the generated schema.
///     Column names must match DuckDbSchema.SpansDdl exactly.
/// </summary>
public readonly struct SpanColumn
{
    private readonly string _name;

    private SpanColumn(string name) => _name = name;

    /// <summary>Get SQL representation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToSql() => _name;

    public override string ToString() => _name;

    // =========================================================================
    // Identity columns
    // =========================================================================
    public static SpanColumn SpanId => new("span_id");
    public static SpanColumn TraceId => new("trace_id");
    public static SpanColumn ParentSpanId => new("parent_span_id");
    public static SpanColumn SessionId => new("session_id");

    // =========================================================================
    // Core span columns
    // =========================================================================
    public static SpanColumn Name => new("name");
    public static SpanColumn Kind => new("kind");

    // =========================================================================
    // Temporal columns (UBIGINT in schema)
    // =========================================================================
    public static SpanColumn StartTimeUnixNano => new("start_time_unix_nano");
    public static SpanColumn EndTimeUnixNano => new("end_time_unix_nano");
    public static SpanColumn DurationNs => new("duration_ns");

    // =========================================================================
    // Status columns
    // =========================================================================
    public static SpanColumn StatusCode => new("status_code");
    public static SpanColumn StatusMessage => new("status_message");

    // =========================================================================
    // Resource columns
    // =========================================================================
    public static SpanColumn ServiceName => new("service_name");

    // =========================================================================
    // GenAI columns (OTel 1.39 semantic conventions)
    // =========================================================================
    public static SpanColumn GenAiSystem => new("gen_ai_system");
    public static SpanColumn GenAiRequestModel => new("gen_ai_request_model");
    public static SpanColumn GenAiResponseModel => new("gen_ai_response_model");
    public static SpanColumn GenAiInputTokens => new("gen_ai_input_tokens");
    public static SpanColumn GenAiOutputTokens => new("gen_ai_output_tokens");
    public static SpanColumn GenAiTemperature => new("gen_ai_temperature");
    public static SpanColumn GenAiStopReason => new("gen_ai_stop_reason");
    public static SpanColumn GenAiToolName => new("gen_ai_tool_name");
    public static SpanColumn GenAiToolCallId => new("gen_ai_tool_call_id");
    public static SpanColumn GenAiCostUsd => new("gen_ai_cost_usd");

    // =========================================================================
    // Flexible storage (JSON columns)
    // =========================================================================
    public static SpanColumn AttributesJson => new("attributes_json");
    public static SpanColumn ResourceJson => new("resource_json");

    // =========================================================================
    // Metadata
    // =========================================================================
    public static SpanColumn CreatedAt => new("created_at");

    // =========================================================================
    // Factory for custom/dynamic columns
    // =========================================================================

    /// <summary>Create column for arbitrary column name.</summary>
    public static SpanColumn Column(string name) => new(name);

    /// <summary>Access non-promoted attribute via JSON extraction.</summary>
    public static string JsonExtract(string attributeKey) => $"attributes_json->'{attributeKey}'";
}

// =============================================================================
// Supporting types
// =============================================================================

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
    private readonly string _column = column;
    private readonly string? _param = param;
    private readonly CompareOp _op = op;

    public string ToSql() => _op switch
    {
        CompareOp.Eq => $"{_column} = {_param}",
        CompareOp.Ne => $"{_column} != {_param}",
        CompareOp.Lt => $"{_column} < {_param}",
        CompareOp.Lte => $"{_column} <= {_param}",
        CompareOp.Gt => $"{_column} > {_param}",
        CompareOp.Gte => $"{_column} >= {_param}",
        CompareOp.IsNull => $"{_column} IS NULL",
        CompareOp.IsNotNull => $"{_column} IS NOT NULL",
        CompareOp.Like => $"{_column} LIKE {_param}",
        CompareOp.In => $"{_column} IN ({_param})",
        CompareOp.Raw => _column,
        _ => throw new ArgumentOutOfRangeException()
    };
}

internal readonly struct OrderByClause(string expr, bool descending)
{
    private readonly string _expr = expr;
    private readonly bool _descending = descending;
    public string ToSql() => _descending ? $"{_expr} DESC" : _expr;
}
