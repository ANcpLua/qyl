using System.Text.Json;
using qyl.mcp.Scoping;

namespace Qyl.Mcp.Tests.Scoping;

public sealed class QylScopeInjectorTests
{
    private static readonly QylScopeInjector Injector = new();

    [Fact]
    public void Inject_ReturnsNull_WhenScopeIsEmptyAndArgsAreNull() =>
        Injector.Inject(arguments: null, QylScope.ForTest()).Should().BeNull();

    [Fact]
    public void Inject_ReturnsArgsUnchanged_WhenScopeIsEmpty()
    {
        var args = Args(("someExisting", Str("existing-value")));

        Injector.Inject(args, QylScope.ForTest()).Should().BeSameAs(args);
        args.Should().NotContainKey("serviceName").And.NotContainKey("sessionId");
    }

    [Fact]
    public void Inject_MutatesArgsInPlace_AndReturnsSameReference()
    {
        var args = Args();

        var result = Injector.Inject(args, QylScope.ForTest(serviceName: "svc"));

        result.Should().BeSameAs(args);
        args.Should().ContainKey("serviceName");
    }

    [Theory]
    [InlineData("svc", null, "svc", null)]
    [InlineData(null, "sess", null, "sess")]
    [InlineData("svc", "sess", "svc", "sess")]
    public void Inject_PopulatesMissingKeys_FromScope(string? scopeService, string? scopeSession, string? expectedService, string? expectedSession)
    {
        var result = Injector.Inject(Args(), QylScope.ForTest(scopeService, scopeSession));

        Read(result, "serviceName").Should().Be(expectedService);
        Read(result, "sessionId").Should().Be(expectedSession);
    }

    [Theory]
    [InlineData("serviceName", "caller-service", "caller-service", "scope-session")]
    [InlineData("sessionId", "caller-session", "scope-service", "caller-session")]
    public void Inject_PreservesCallerValue_WhenExistingIsNonEmptyString(string callerKey, string callerValue, string expectedService, string expectedSession)
    {
        var args = Args((callerKey, Str(callerValue)));

        var result = Injector.Inject(args, QylScope.ForTest("scope-service", "scope-session"));

        Read(result, "serviceName").Should().Be(expectedService);
        Read(result, "sessionId").Should().Be(expectedSession);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{\"a\":1}")]
    public void Inject_OverwritesCallerServiceName_WhenExistingIsNotNonEmptyString(string existingJson)
    {
        var args = Args(("serviceName", Json(existingJson)));

        var result = Injector.Inject(args, QylScope.ForTest(serviceName: "scope-service"));

        Read(result, "serviceName").Should().Be("scope-service");
    }

    [Fact]
    public void Inject_PreservesMixedCaseCallerKey_OnCaseInsensitiveDict()
    {
        var args = Args(("ServiceName", Str("caller-service")));

        var result = Injector.Inject(args, QylScope.ForTest(serviceName: "scope-service"));

        result.Should().HaveCount(1);
        Read(result, "ServiceName").Should().Be("caller-service");
    }

    [Fact]
    public void Inject_CreatesCaseInsensitiveDict_WhenArgsAreNull()
    {
        var result = Injector.Inject(arguments: null, QylScope.ForTest(serviceName: "svc"));

        result.Should().ContainKey("ServiceName").And.ContainKey("serviceName");
    }

    private static Dictionary<string, JsonElement> Args(params (string key, JsonElement value)[] entries)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries) dict[key] = value;
        return dict;
    }

    private static JsonElement Str(string value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string? Read(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
}
