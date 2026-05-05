namespace Qyl.Collector.Storage;

internal static class SqlBuilder
{
    public static int AddParam(this DbCommand cmd, object? value)
    {
        var idx = cmd.Parameters.Count + 1;
        var parameter = cmd.CreateParameter();
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
        return idx;
    }

    public static string Whitelisted(string fragment, FrozenSet<string> allowed)
    {
        if (!allowed.Contains(fragment))
            throw new ArgumentException($"Not in whitelist: {fragment}", nameof(fragment));
        return fragment;
    }
}
