using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class DuckDbValueReaderTests
{
    private const string HugeIntAboveDecimal = "SELECT 100000000000000000000000000000::HUGEINT";
    private const string HugeIntAboveInt64 = "SELECT 10000000000000000000::HUGEINT";
    private const string HugeIntInInt64 = "SELECT 42::HUGEINT";
    private const string StateRowWithNullValueJson =
        "SELECT 'alpha' AS project_id, 'state' AS state_key, NULL::VARCHAR AS value_json, 'etag' AS etag, 7::UBIGINT AS updated_at_unix_nano";
    private const string StateRowWithNullUpdatedAt =
        "SELECT 'alpha' AS project_id, 'state' AS state_key, '{}' AS value_json, 'etag' AS etag, NULL::UBIGINT AS updated_at_unix_nano";
    private const string CompleteStateRow =
        "SELECT 'alpha' AS project_id, 'state' AS state_key, '{}' AS value_json, 'etag' AS etag, 7::UBIGINT AS updated_at_unix_nano";

    [TestMethod]
    public void ReadDouble_HugeIntAboveDecimalRange_IsTheDoubleValue()
    {
        using var query = SingleRow(HugeIntAboveDecimal);

        Assert.AreEqual(1e29, DuckDbValueReader.ReadDouble(query.Reader, 0));
    }

    [TestMethod]
    public void ReadInt64_HugeIntInRange_IsTheValue()
    {
        using var query = SingleRow(HugeIntInInt64);

        Assert.AreEqual(42L, DuckDbValueReader.ReadInt64(query.Reader, 0));
    }

    [TestMethod]
    public void ReadInt64_HugeIntAboveInt64Range_ThrowsOverflow()
    {
        using var query = SingleRow(HugeIntAboveInt64);

        Assert.ThrowsExactly<OverflowException>(() => DuckDbValueReader.ReadInt64(query.Reader, 0));
    }

    [TestMethod]
    public void MapFromReader_NullInRequiredStringColumn_ThrowsInvalidData()
    {
        using var query = SingleRow(StateRowWithNullValueJson);

        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisStateRow.MapFromReader(query.Reader));
    }

    [TestMethod]
    public void MapFromReader_NullInRequiredNumberColumn_ThrowsInvalidData()
    {
        using var query = SingleRow(StateRowWithNullUpdatedAt);

        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisStateRow.MapFromReader(query.Reader));
    }

    [TestMethod]
    public void MapFromReader_CompleteRow_MapsEveryColumn()
    {
        using var query = SingleRow(CompleteStateRow);

        var row = AnalysisStateRow.MapFromReader(query.Reader);

        Assert.AreEqual(new AnalysisStateRow { ProjectId = "alpha", StateKey = "state", ValueJson = "{}", Etag = "etag", UpdatedAtUnixNano = 7 }, row);
    }

    private static OpenQuery SingleRow(string sql)
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        var reader = command.ExecuteReader();
        reader.Read();
        return new OpenQuery(connection, command, reader);
    }

    private sealed class OpenQuery(DuckDBConnection connection, DuckDBCommand command, DuckDBDataReader reader) : IDisposable
    {
        public DuckDBDataReader Reader { get; } = reader;

        public void Dispose()
        {
            Reader.Dispose();
            command.Dispose();
            connection.Dispose();
        }
    }
}
