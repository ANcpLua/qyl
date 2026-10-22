using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ExceptionTextJwtTests
{
    private const string CanonicalHeaderToken = "eyJhbGciOiJub25lIn0.eyJzdWIiOiJ4In0.c2ln";
    private const string NonCanonicalHeaderToken = "eyJhbGciOiJub25lIn3.eyJzdWIiOiJ4In0.c2ln";

    [TestMethod]
    [DataRow(CanonicalHeaderToken)]
    [DataRow(NonCanonicalHeaderToken)]
    public void ExceptionTextSanitizer_JwtInText_RedactsTheToken(string token) =>
        Assert.AreEqual("rejected [REDACTED]", ExceptionTextSanitizer.Sanitize("exception.message", new AnyValue { StringValue = "rejected " + token }));
}
