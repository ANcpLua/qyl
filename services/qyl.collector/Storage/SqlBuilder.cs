namespace Qyl.Collector.Storage;

/// <summary>
///     Keeps DuckDB command construction out of string interpolation (AL0111): <see cref="AddParam" /> parameterizes
///     untrusted values and returns the 1-based <c>$N</c> index; <see cref="Whitelisted" /> validates a lexical schema
///     fragment against a pre-declared <see cref="FrozenSet{T}" /> before direct concatenation.
/// </summary>
internal static class SqlBuilder
{
    /// <summary>
    ///     Adds <paramref name="value" /> as a parameter on <paramref name="cmd" /> and returns
    ///     the 1-based positional index. Null becomes <see cref="DBNull.Value" />.
    /// </summary>
    public static int AddParam(this DbCommand cmd, object? value)
    {
        var idx = cmd.Parameters.Count + 1;
        var parameter = cmd.CreateParameter();
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
        return idx;
    }

    /// <summary>
    ///     Returns <paramref name="fragment" /> if it is in <paramref name="allowed" />.
    ///     Throws <see cref="ArgumentException" /> otherwise. Use for schema-fragment
    ///     substitution where parameterization is not possible (column lists, GROUP BY,
    ///     interval literals).
    /// </summary>
    public static string Whitelisted(string fragment, FrozenSet<string> allowed)
    {
        if (!allowed.Contains(fragment))
            throw new ArgumentException($"Not in whitelist: {fragment}", nameof(fragment));
        return fragment;
    }
}
