
using System.Runtime.CompilerServices;

namespace Qyl.Instrumentation.Instrumentation.Db;

internal static class SqlOperationParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? TryParse(string? sql) =>
        string.IsNullOrWhiteSpace(sql) ? null : TryParseCore(sql.AsSpan());

    private static string? TryParseCore(ReadOnlySpan<char> sql)
    {
        sql = SkipWhitespaceAndComments(sql);

        if (sql.IsEmpty)
            return null;

        if (StartsWithKeyword(sql, "WITH"))
        {
            var afterCte = SkipCte(sql);
            if (!afterCte.IsEmpty)
                sql = afterCte;
        }

        return MatchOperation(sql);
    }

    private static ReadOnlySpan<char> SkipWhitespaceAndComments(ReadOnlySpan<char> sql)
    {
        while (!sql.IsEmpty)
        {
            sql = sql.TrimStart();

            if (sql.IsEmpty)
                return sql;

            switch (sql.Length)
            {
                case >= 2 when sql[0] == '-' && sql[1] == '-':
                {
                    var newlineIndex = sql.IndexOfAny('\r', '\n');
                    sql = newlineIndex < 0 ? [] : sql[(newlineIndex + 1)..];
                    continue;
                }
                case >= 2 when sql[0] == '/' && sql[1] == '*':
                {
                    var endIndex = IndexOfBlockCommentEnd(sql, 2);
                    sql = endIndex < 0 ? [] : sql[(endIndex + 2)..];
                    continue;
                }
            }

            break;
        }

        return sql;
    }

    private static int IndexOfBlockCommentEnd(ReadOnlySpan<char> sql, int start)
    {
        var depth = 1;
        for (var i = start; i < sql.Length - 1; i++)
        {
            switch (sql[i])
            {
                case '/' when sql[i + 1] == '*':
                    depth++;
                    i++;
                    break;
                case '*' when sql[i + 1] == '/':
                {
                    depth--;
                    if (depth is 0)
                        return i;
                    i++;
                    break;
                }
            }
        }

        return -1;
    }

    private static ReadOnlySpan<char> SkipCte(ReadOnlySpan<char> sql)
    {
        sql = sql[4..];
        sql = SkipWhitespaceAndComments(sql);

        if (StartsWithKeyword(sql, "RECURSIVE"))
        {
            sql = sql[9..];
            sql = SkipWhitespaceAndComments(sql);
        }

        while (!sql.IsEmpty)
        {
            sql = SkipIdentifier(sql);
            sql = SkipWhitespaceAndComments(sql);

            if (!sql.IsEmpty && sql[0] == '(')
            {
                sql = SkipParenthesizedBlock(sql);
                sql = SkipWhitespaceAndComments(sql);
            }

            if (!StartsWithKeyword(sql, "AS"))
                return sql;

            sql = sql[2..];
            sql = SkipWhitespaceAndComments(sql);

            if (!sql.IsEmpty && sql[0] == '(')
            {
                sql = SkipParenthesizedBlock(sql);
                sql = SkipWhitespaceAndComments(sql);
            }

            if (sql.IsEmpty || sql[0] != ',')
                break;

            sql = sql[1..];
            sql = SkipWhitespaceAndComments(sql);
        }

        return sql;
    }

    private static ReadOnlySpan<char> SkipIdentifier(ReadOnlySpan<char> sql)
    {
        if (sql.IsEmpty)
            return sql;

        if (sql[0] is '"' or '[' or '`')
        {
            var closeChar = sql[0] switch
            {
                '"' => '"',
                '[' => ']',
                '`' => '`',
                _ => sql[0]
            };

            var endIndex = sql[1..].IndexOf(closeChar);
            return endIndex < 0 ? [] : sql[(endIndex + 2)..];
        }

        var i = 0;
        while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
            i++;

        return sql[i..];
    }

    private static ReadOnlySpan<char> SkipParenthesizedBlock(ReadOnlySpan<char> sql)
    {
        if (sql.IsEmpty || sql[0] != '(')
            return sql;

        var depth = 1;
        for (var i = 1; i < sql.Length; i++)
        {
            switch (sql[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth is 0)
                        return sql[(i + 1)..];
                    break;
                case '\'':
                    i = SkipStringLiteral(sql, i);
                    break;
            }
        }

        return [];
    }

    private static int SkipStringLiteral(ReadOnlySpan<char> sql, int start)
    {
        for (var i = start + 1; i < sql.Length; i++)
        {
            if (sql[i] == '\'')
            {
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                return i;
            }
        }

        return sql.Length - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWithKeyword(ReadOnlySpan<char> sql, ReadOnlySpan<char> keyword)
    {
        if (sql.Length < keyword.Length)
            return false;

        if (!sql[..keyword.Length].Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        if (sql.Length > keyword.Length)
        {
            var nextChar = sql[keyword.Length];
            if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
                return false;
        }

        return true;
    }

    private static string? MatchOperation(ReadOnlySpan<char> sql)
    {
        if (StartsWithKeyword(sql, "SELECT"))
            return "SELECT";

        if (StartsWithKeyword(sql, "INSERT"))
            return "INSERT";

        if (StartsWithKeyword(sql, "UPDATE"))
            return "UPDATE";

        if (StartsWithKeyword(sql, "DELETE"))
            return "DELETE";

        if (StartsWithKeyword(sql, "CREATE"))
            return "CREATE";

        if (StartsWithKeyword(sql, "DROP"))
            return "DROP";

        if (StartsWithKeyword(sql, "ALTER"))
            return "ALTER";

        if (StartsWithKeyword(sql, "TRUNCATE"))
            return "TRUNCATE";

        if (StartsWithKeyword(sql, "MERGE"))
            return "MERGE";

        if (StartsWithKeyword(sql, "CALL"))
            return "CALL";

        if (StartsWithKeyword(sql, "EXEC"))
            return "EXECUTE";

        if (StartsWithKeyword(sql, "EXECUTE"))
            return "EXECUTE";

        if (StartsWithKeyword(sql, "BEGIN"))
            return "BEGIN";

        if (StartsWithKeyword(sql, "COMMIT"))
            return "COMMIT";

        return StartsWithKeyword(sql, "ROLLBACK") ? "ROLLBACK" : null;
    }
}
