// =============================================================================
// qyl.servicedefaults - SQL Operation Parser
// Parses SQL to extract the primary operation (SELECT, INSERT, UPDATE, DELETE, etc.)
// Per OTel 1.39+ db.operation.name semantic convention
// Owner: qyl.servicedefaults
// =============================================================================

using System.Runtime.CompilerServices;
using qyl.protocol.Attributes;

namespace Qyl.ServiceDefaults.Instrumentation.Db;

/// <summary>
///     Parses SQL statements to extract the primary operation type per OTel semantic conventions.
/// </summary>
/// <remarks>
///     <para>
///         Handles common SQL patterns including:
///         - Single-line comments (-- ...)
///         - Block comments (/* ... */)
///         - Common Table Expressions (WITH ... SELECT/INSERT/UPDATE/DELETE)
///         - Leading whitespace
///     </para>
///     <para>
///         Uses Span-based parsing for zero-allocation where possible.
///     </para>
/// </remarks>
internal static class SqlOperationParser
{
    /// <summary>
    ///     Attempts to parse the SQL operation from a SQL statement.
    /// </summary>
    /// <param name="sql">The SQL statement to parse.</param>
    /// <returns>The operation name (SELECT, INSERT, UPDATE, DELETE, etc.) or null if unparseable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? TryParse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        return TryParseCore(sql.AsSpan());
    }

    /// <summary>
    ///     Core parsing logic using ReadOnlySpan for zero-allocation.
    /// </summary>
    private static string? TryParseCore(ReadOnlySpan<char> sql)
    {
        // Skip leading whitespace and comments
        sql = SkipWhitespaceAndComments(sql);

        if (sql.IsEmpty)
            return null;

        // Check for CTE (WITH ... AS ...)
        if (StartsWithKeyword(sql, "WITH"))
        {
            // Find the actual operation after the CTE
            var afterCte = SkipCte(sql);
            if (!afterCte.IsEmpty)
                sql = afterCte;
        }

        // Match known SQL operations
        return MatchOperation(sql);
    }

    /// <summary>
    ///     Skips leading whitespace and SQL comments.
    /// </summary>
    private static ReadOnlySpan<char> SkipWhitespaceAndComments(ReadOnlySpan<char> sql)
    {
        while (!sql.IsEmpty)
        {
            // Skip whitespace
            sql = sql.TrimStart();

            if (sql.IsEmpty)
                return sql;

            // Check for single-line comment: --
            if (sql.Length >= 2 && sql[0] == '-' && sql[1] == '-')
            {
                var newlineIndex = sql.IndexOfAny('\r', '\n');
                sql = newlineIndex < 0 ? [] : sql[(newlineIndex + 1)..];
                continue;
            }

            // Check for block comment: /* ... */
            if (sql.Length >= 2 && sql[0] == '/' && sql[1] == '*')
            {
                var endIndex = IndexOfBlockCommentEnd(sql, 2);
                sql = endIndex < 0 ? [] : sql[(endIndex + 2)..];
                continue;
            }

            // No more comments to skip
            break;
        }

        return sql;
    }

    /// <summary>
    ///     Finds the end of a block comment, handling nested comments.
    /// </summary>
    private static int IndexOfBlockCommentEnd(ReadOnlySpan<char> sql, int start)
    {
        var depth = 1;
        for (var i = start; i < sql.Length - 1; i++)
        {
            if (sql[i] == '/' && sql[i + 1] == '*')
            {
                depth++;
                i++; // Skip the '*'
            }
            else if (sql[i] == '*' && sql[i + 1] == '/')
            {
                depth--;
                if (depth == 0)
                    return i;
                i++; // Skip the '/'
            }
        }

        return -1; // Unterminated comment
    }

    /// <summary>
    ///     Skips a Common Table Expression (WITH ... AS (...)) to find the main operation.
    /// </summary>
    private static ReadOnlySpan<char> SkipCte(ReadOnlySpan<char> sql)
    {
        // Skip "WITH" keyword
        sql = sql[4..];
        sql = SkipWhitespaceAndComments(sql);

        // Handle RECURSIVE keyword
        if (StartsWithKeyword(sql, "RECURSIVE"))
        {
            sql = sql[9..];
            sql = SkipWhitespaceAndComments(sql);
        }

        // CTEs can have multiple definitions separated by commas
        // WITH cte1 AS (...), cte2 AS (...) SELECT/INSERT/UPDATE/DELETE
        while (!sql.IsEmpty)
        {
            // Skip CTE name
            sql = SkipIdentifier(sql);
            sql = SkipWhitespaceAndComments(sql);

            // Optional column list: (col1, col2)
            if (!sql.IsEmpty && sql[0] == '(')
            {
                sql = SkipParenthesizedBlock(sql);
                sql = SkipWhitespaceAndComments(sql);
            }

            // Expect "AS"
            if (!StartsWithKeyword(sql, "AS"))
                return sql; // Malformed, return what we have

            sql = sql[2..];
            sql = SkipWhitespaceAndComments(sql);

            // Skip the CTE body: (SELECT ...)
            if (!sql.IsEmpty && sql[0] == '(')
            {
                sql = SkipParenthesizedBlock(sql);
                sql = SkipWhitespaceAndComments(sql);
            }

            // Check for comma (another CTE) or end
            if (sql.IsEmpty || sql[0] != ',')
                break;

            sql = sql[1..]; // Skip comma
            sql = SkipWhitespaceAndComments(sql);
        }

        return sql;
    }

    /// <summary>
    ///     Skips an identifier (table name, column name, etc.).
    /// </summary>
    private static ReadOnlySpan<char> SkipIdentifier(ReadOnlySpan<char> sql)
    {
        if (sql.IsEmpty)
            return sql;

        // Handle quoted identifiers
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

        // Unquoted identifier: letters, digits, underscores
        var i = 0;
        while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
            i++;

        return sql[i..];
    }

    /// <summary>
    ///     Skips a parenthesized block, handling nested parentheses.
    /// </summary>
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
                    if (depth == 0)
                        return sql[(i + 1)..];
                    break;
                case '\'': // Skip string literals
                    i = SkipStringLiteral(sql, i);
                    break;
            }
        }

        return []; // Unterminated
    }

    /// <summary>
    ///     Skips a string literal, handling escaped quotes.
    /// </summary>
    private static int SkipStringLiteral(ReadOnlySpan<char> sql, int start)
    {
        for (var i = start + 1; i < sql.Length; i++)
        {
            if (sql[i] == '\'')
            {
                // Check for escaped quote ('')
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++; // Skip the escaped quote
                    continue;
                }

                return i;
            }
        }

        return sql.Length - 1;
    }

    /// <summary>
    ///     Checks if the SQL starts with a keyword (case-insensitive, word boundary).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWithKeyword(ReadOnlySpan<char> sql, ReadOnlySpan<char> keyword)
    {
        if (sql.Length < keyword.Length)
            return false;

        if (!sql[..keyword.Length].Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        // Ensure word boundary (not followed by letter/digit/underscore)
        if (sql.Length > keyword.Length)
        {
            var nextChar = sql[keyword.Length];
            if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Matches the SQL operation at the current position.
    /// </summary>
    private static string? MatchOperation(ReadOnlySpan<char> sql)
    {
        // Order by frequency in typical workloads
        if (StartsWithKeyword(sql, "SELECT"))
            return DbAttributes.Operations.Select;

        if (StartsWithKeyword(sql, "INSERT"))
            return DbAttributes.Operations.Insert;

        if (StartsWithKeyword(sql, "UPDATE"))
            return DbAttributes.Operations.Update;

        if (StartsWithKeyword(sql, "DELETE"))
            return DbAttributes.Operations.Delete;

        if (StartsWithKeyword(sql, "CREATE"))
            return DbAttributes.Operations.Create;

        if (StartsWithKeyword(sql, "DROP"))
            return DbAttributes.Operations.Drop;

        // Additional DDL/DML operations
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

        if (StartsWithKeyword(sql, "ROLLBACK"))
            return "ROLLBACK";

        return null;
    }
}
