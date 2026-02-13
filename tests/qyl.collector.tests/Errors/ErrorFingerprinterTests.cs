using qyl.collector.Errors;

namespace qyl.collector.tests.Errors;

public sealed class ErrorFingerprinterTests
{
    [Fact]
    public void Fingerprint_SameException_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "Object reference not set", "at Foo.Bar()");
        var fp2 = ErrorFingerprinter.Compute("NullReferenceException", "Object reference not set", "at Foo.Bar()");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentLineNumbers_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "msg",
            "at MyApp.Service.Do() in /src/Service.cs:line 42");
        var fp2 = ErrorFingerprinter.Compute("NullReferenceException", "msg",
            "at MyApp.Service.Do() in /src/Service.cs:line 99");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentGuidsInMessage_ProducesSameHash()
    {
        var fp1 = ErrorFingerprinter.Compute("Exception", "User a1b2c3d4-e5f6-7890-abcd-ef1234567890 not found", "");
        var fp2 = ErrorFingerprinter.Compute("Exception", "User 99999999-8888-7777-6666-555544443333 not found", "");
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_DifferentExceptionType_ProducesDifferentHash()
    {
        var fp1 = ErrorFingerprinter.Compute("NullReferenceException", "msg", "at Foo.Bar()");
        var fp2 = ErrorFingerprinter.Compute("ArgumentException", "msg", "at Foo.Bar()");
        Assert.NotEqual(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_WithGenAiOperation_IncludesInHash()
    {
        var fp1 = ErrorFingerprinter.Compute("Exception", "rate limited", "", genAiOperation: "chat");
        var fp2 = ErrorFingerprinter.Compute("Exception", "rate limited", "", genAiOperation: "embeddings");
        Assert.NotEqual(fp1, fp2);
    }

    [Fact]
    public void Fingerprint_NullStackTrace_DoesNotThrow()
    {
        var fp = ErrorFingerprinter.Compute("Exception", "msg", null);
        Assert.NotNull(fp);
        Assert.NotEmpty(fp);
    }
}
