using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

public sealed class LogSourceEnricherTests
{
    private readonly LogSourceEnricher _sut = new(new SourceLocationCache(), new PdbSourceResolver());

    [Fact]
    public void Enrich_UsesCodeAttributes_WhenPresent()
    {
        var log = new OtlpLogRecord
        {
            Attributes =
            [
                new OtlpKeyValue { Key = "code.file.path", Value = new OtlpAnyValue { StringValue = "src/Foo.cs" } },
                new OtlpKeyValue { Key = "code.line.number", Value = new OtlpAnyValue { IntValue = 42 } },
                new OtlpKeyValue { Key = "code.column.number", Value = new OtlpAnyValue { IntValue = 7 } },
                new OtlpKeyValue { Key = "code.function.name", Value = new OtlpAnyValue { StringValue = "Foo.Bar" } }
            ]
        };

        var location = _sut.Enrich(log);

        Assert.NotNull(location);
        Assert.Equal("src/Foo.cs", location.FilePath);
        Assert.Equal(42, location.Line);
        Assert.Equal(7, location.Column);
        Assert.Equal("Foo.Bar", location.MethodName);
    }

    [Fact]
    public void Enrich_UsesStackTraceFallback_WhenCodeAttributesMissing()
    {
        var log = new OtlpLogRecord
        {
            Attributes =
            [
                new OtlpKeyValue
                {
                    Key = "exception.stacktrace",
                    Value = new OtlpAnyValue { StringValue = "at Foo.Bar() in /repo/src/Foo.cs:line 99" }
                }
            ]
        };

        var location = _sut.Enrich(log);

        Assert.NotNull(location);
        Assert.Equal("/repo/src/Foo.cs", location.FilePath);
        Assert.Equal(99, location.Line);
    }
}