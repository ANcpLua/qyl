using Qyl.ServiceDefaults.Instrumentation.Db;

namespace qyl.collector.tests.Instrumentation;

/// <summary>
///     Unit tests for SqlOperationParser - extracts SQL operation type from command text
///     per OTel semantic convention db.operation.name.
/// </summary>
public sealed class SqlOperationParserTests
{
    #region Whitespace Handling

    [Theory]
    [InlineData("   SELECT * FROM users")]
    [InlineData("\tSELECT * FROM users")]
    [InlineData("\n\nSELECT * FROM users")]
    [InlineData("  \t\n  SELECT * FROM users")]
    public void Parse_LeadingWhitespace_StripsThenParsesCorrectly(string sql)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("SELECT", result);
    }

    #endregion

    #region Basic Operation Detection

    [Theory]
    [InlineData("SELECT * FROM users", "SELECT")]
    [InlineData("select * from users", "SELECT")]
    [InlineData("SeLeCt id FROM products", "SELECT")]
    public void Parse_BasicSelect_ReturnsSelect(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("INSERT INTO users (id, name) VALUES (1, 'John')", "INSERT")]
    [InlineData("insert into users values (1)", "INSERT")]
    public void Parse_BasicInsert_ReturnsInsert(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UPDATE users SET name = 'Jane' WHERE id = 1", "UPDATE")]
    [InlineData("update products set price = 99.99", "UPDATE")]
    public void Parse_BasicUpdate_ReturnsUpdate(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("DELETE FROM users WHERE id = 1", "DELETE")]
    [InlineData("delete from logs", "DELETE")]
    public void Parse_BasicDelete_ReturnsDelete(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Comment Stripping

    [Theory]
    [InlineData("-- This is a comment\nSELECT * FROM users", "SELECT")]
    [InlineData("-- Comment line 1\n-- Comment line 2\nSELECT id FROM users", "SELECT")]
    [InlineData("--Comment with no space\nUPDATE table SET col = 1", "UPDATE")]
    public void Parse_SingleLineComments_StripsThenParsesCorrectly(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/* Block comment */ SELECT * FROM users", "SELECT")]
    [InlineData("/* Multi\nline\ncomment */ INSERT INTO table VALUES (1)", "INSERT")]
    public void Parse_BlockComments_StripsThenParsesCorrectly(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/* Comment */ -- Another comment\nSELECT id FROM users", "SELECT")]
    [InlineData("-- Comment\n/* Block */ INSERT INTO table VALUES (1)", "INSERT")]
    public void Parse_MixedComments_StripsBothThenParsesCorrectly(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    #endregion

    #region CTE (Common Table Expression) Handling

    [Fact]
    public void Parse_WithClauseSelect_IdentifiesAsSelect()
    {
        const string sql = """
                           WITH temp_data AS (
                               SELECT id, name FROM source
                           )
                           SELECT * FROM temp_data
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void Parse_WithClauseInsert_IdentifiesAsInsert()
    {
        const string sql = """
                           WITH source_data AS (
                               SELECT id FROM input_table
                           )
                           INSERT INTO output_table
                           SELECT * FROM source_data
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("INSERT", result);
    }

    [Fact]
    public void Parse_WithClauseUpdate_IdentifiesAsUpdate()
    {
        const string sql = """
                           WITH new_values AS (
                               SELECT id, new_value FROM staging
                           )
                           UPDATE target SET value = new_values.new_value
                           FROM new_values WHERE target.id = new_values.id
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("UPDATE", result);
    }

    [Fact]
    public void Parse_WithClauseDelete_IdentifiesAsDelete()
    {
        const string sql = """
                           WITH ids_to_delete AS (
                               SELECT id FROM old_records WHERE date < '2020-01-01'
                           )
                           DELETE FROM archive WHERE id IN (SELECT id FROM ids_to_delete)
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("DELETE", result);
    }

    [Fact]
    public void Parse_RecursiveWithClause_IdentifiesMainOperation()
    {
        const string sql = """
                           WITH RECURSIVE hierarchy AS (
                               SELECT id, parent_id FROM tree WHERE parent_id IS NULL
                               UNION ALL
                               SELECT t.id, t.parent_id FROM tree t
                               JOIN hierarchy h ON t.parent_id = h.id
                           )
                           SELECT * FROM hierarchy
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void Parse_MultipleWithClauses_IdentifiesMainOperation()
    {
        const string sql = """
                           WITH
                             cte1 AS (SELECT id FROM table1),
                             cte2 AS (SELECT id FROM table2)
                           SELECT * FROM cte1 UNION ALL SELECT * FROM cte2
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("SELECT", result);
    }

    #endregion

    #region Stored Procedure / EXEC / CALL

    [Theory]
    [InlineData("EXEC sp_GetUsers", "EXECUTE")]
    [InlineData("exec sp_UpdateRecord @id = 1", "EXECUTE")]
    [InlineData("EXECUTE stored_procedure_name", "EXECUTE")]
    public void Parse_ExecStatement_ReturnsExecute(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CALL sp_ProcessData()", "CALL")]
    [InlineData("call schema.procedure_name", "CALL")]
    public void Parse_CallStatement_ReturnsCall(string sql, string expected)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Empty and Null Input

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Parse_EmptyOrWhitespaceOnly_ReturnsNull(string sql)
    {
        var result = SqlOperationParser.TryParse(sql);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var result = SqlOperationParser.TryParse(null);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsNull()
    {
        const string sql = """
                           -- Just comments
                           /* And block comments */
                           -- No actual SQL
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Null(result);
    }

    #endregion

    #region DDL Operations

    [Fact]
    public void Parse_CreateTableStatement_ReturnsCreate()
    {
        const string sql = "CREATE TABLE users (id INT, name VARCHAR(255))";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("CREATE", result);
    }

    [Fact]
    public void Parse_DropTableStatement_ReturnsDrop()
    {
        const string sql = "DROP TABLE IF EXISTS archive";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("DROP", result);
    }

    [Fact]
    public void Parse_AlterTableStatement_ReturnsAlter()
    {
        const string sql = "ALTER TABLE users ADD COLUMN email VARCHAR(255)";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("ALTER", result);
    }

    [Fact]
    public void Parse_TruncateStatement_ReturnsTruncate()
    {
        const string sql = "TRUNCATE TABLE logs";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("TRUNCATE", result);
    }

    #endregion

    #region Transaction Operations

    [Fact]
    public void Parse_BeginTransaction_ReturnsBegin()
    {
        const string sql = "BEGIN TRANSACTION";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("BEGIN", result);
    }

    [Fact]
    public void Parse_CommitTransaction_ReturnsCommit()
    {
        const string sql = "COMMIT";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("COMMIT", result);
    }

    [Fact]
    public void Parse_RollbackTransaction_ReturnsRollback()
    {
        const string sql = "ROLLBACK";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("ROLLBACK", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_UnknownOperationKeyword_ReturnsNull()
    {
        const string sql = "PRAGMA table_info(users)";
        var result = SqlOperationParser.TryParse(sql);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_MultipleStatementsBatch_IdentifiesFirstOperation()
    {
        const string sql = """
                           SELECT id FROM users;
                           INSERT INTO log VALUES ('processed');
                           UPDATE stats SET count = count + 1;
                           """;

        var result = SqlOperationParser.TryParse(sql);
        Assert.Equal("SELECT", result);
    }

    #endregion
}