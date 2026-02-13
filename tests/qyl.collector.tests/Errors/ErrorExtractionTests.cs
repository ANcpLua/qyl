using qyl.collector.Errors;

namespace qyl.collector.tests.Errors;

public sealed class ErrorExtractionTests
{
    [Fact]
    public void ExtractErrorEvent_FromErrorSpan_ReturnsEvent()
    {
        var span = SpanBuilder.Create("trace-err", "span-err")
            .WithStatusCode(2)
            .WithStatusMessage("Object reference not set")
            .WithServiceName("my-api")
            .WithProvider("openai")
            .WithAttributes("""{"exception.type":"NullReferenceException","exception.message":"Object reference not set","exception.stacktrace":"at Foo.Bar()"}""")
            .Build();

        var error = ErrorExtractor.Extract(span);

        Assert.NotNull(error);
        Assert.Equal("NullReferenceException", error.ErrorType);
        Assert.Equal("Object reference not set", error.Message);
        Assert.Equal("internal", error.Category);
        Assert.NotEmpty(error.Fingerprint);
        Assert.Equal("my-api", error.ServiceName);
        Assert.Equal("trace-err", error.TraceId);
    }

    [Fact]
    public void ExtractErrorEvent_FromOkSpan_ReturnsNull()
    {
        var span = SpanBuilder.Create("trace-ok", "span-ok")
            .WithStatusCode(1) // OK
            .Build();

        var error = ErrorExtractor.Extract(span);
        Assert.Null(error);
    }

    [Fact]
    public void ExtractErrorEvent_WithGenAiAttributes_IncludesGenAiData()
    {
        var span = SpanBuilder.Create("trace-ai", "span-ai")
            .WithStatusCode(2)
            .WithStatusMessage("rate limited")
            .WithServiceName("llm-gateway")
            .WithProvider("openai")
            .WithModel("gpt-4")
            .WithAttributes("""{"gen_ai.error.type":"rate_limit_exceeded","gen_ai.operation.name":"chat"}""")
            .Build();

        var error = ErrorExtractor.Extract(span);

        Assert.NotNull(error);
        Assert.Equal("rate_limit", error.Category);
        Assert.Equal("openai", error.GenAiProvider);
        Assert.Equal("gpt-4", error.GenAiModel);
        Assert.Equal("chat", error.GenAiOperation);
    }
}
