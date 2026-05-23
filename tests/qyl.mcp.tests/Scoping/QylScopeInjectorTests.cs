using System.Text.Json;
using qyl.mcp.Scoping;

namespace Qyl.Mcp.Tests.Scoping;

public sealed class QylScopeInjectorTests
{
    private static readonly QylScopeInjector Injector = new();

    [Fact]
    public void Inject_PreservesArguments_WhenScopeIsEmpty()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["someExisting"] = Str("existing-value")
        };

        var result = Injector.Inject(args, QylScope.ForTest());

        result.Should().BeSameAs(args);
        result["someExisting"].GetString().Should().Be("existing-value");
        result.Should().NotContainKey("serviceName");
        result.Should().NotContainKey("sessionId");
    }

    [Fact]
    public void Inject_ReturnsNull_WhenScopeIsEmptyAndArgsAreNull()
    {
        var result = Injector.Inject(arguments: null, QylScope.ForTest());

        result.Should().BeNull();
    }

    [Fact]
    public void Inject_CreatesNewDict_WhenArgsAreNullAndScopeIsPresent()
    {
        var scope = QylScope.ForTest(serviceName: "svc", sessionId: "sess");

        var result = Injector.Inject(arguments: null, scope);

        var injected = RequireInjected(result);
        injected["serviceName"].GetString().Should().Be("svc");
        injected["sessionId"].GetString().Should().Be("sess");
    }

    [Fact]
    public void Inject_AddsServiceNameOnly_WhenScopeHasOnlyServiceName()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var scope = QylScope.ForTest(serviceName: "only-service");

        var result = Injector.Inject(args, scope);

        result.Should().BeSameAs(args);
        result["serviceName"].GetString().Should().Be("only-service");
        result.Should().NotContainKey("sessionId");
    }

    [Fact]
    public void Inject_AddsSessionIdOnly_WhenScopeHasOnlySessionId()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var scope = QylScope.ForTest(sessionId: "only-session");

        var result = Injector.Inject(args, scope);

        result.Should().BeSameAs(args);
        result["sessionId"].GetString().Should().Be("only-session");
        result.Should().NotContainKey("serviceName");
    }

    [Fact]
    public void Inject_PreservesCallerServiceName_WhenCallerSetsNonEmptyString()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = Str("caller-service")
        };
        var scope = QylScope.ForTest(serviceName: "scope-service", sessionId: "scope-session");

        var result = Injector.Inject(args, scope);

        result.Should().NotBeNull();
        result["serviceName"].GetString().Should().Be("caller-service");
        result["sessionId"].GetString().Should().Be("scope-session");
    }

    [Fact]
    public void Inject_PreservesCallerSessionId_WhenCallerSetsNonEmptyString()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["sessionId"] = Str("caller-session")
        };
        var scope = QylScope.ForTest(serviceName: "scope-service", sessionId: "scope-session");

        var result = Injector.Inject(args, scope);

        result.Should().NotBeNull();
        result["sessionId"].GetString().Should().Be("caller-session");
        result["serviceName"].GetString().Should().Be("scope-service");
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{\"a\":1}")]
    public void Inject_OverwritesCallerServiceName_WhenExistingValueIsNotNonEmptyString(string existingJson)
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = Json(existingJson)
        };
        var scope = QylScope.ForTest(serviceName: "scope-service");

        var result = Injector.Inject(args, scope);

        result.Should().NotBeNull();
        result["serviceName"].ValueKind.Should().Be(JsonValueKind.String);
        result["serviceName"].GetString().Should().Be("scope-service");
    }

    [Fact]
    public void Inject_PreservesMixedCaseCallerKey_WhenDictIsCaseInsensitive()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceName"] = Str("caller-service")
        };
        var scope = QylScope.ForTest(serviceName: "scope-service");

        var result = Injector.Inject(args, scope);

        result.Should().HaveCount(1);
        result["ServiceName"].GetString().Should().Be("caller-service");
    }

    [Fact]
    public void Inject_MapsServiceNameAndSessionId_ToTheirOwnKeys()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var scope = QylScope.ForTest(serviceName: "svc-A", sessionId: "sess-B");

        var result = Injector.Inject(args, scope);

        result.Should().NotBeNull();
        result["serviceName"].GetString().Should().Be("svc-A");
        result["sessionId"].GetString().Should().Be("sess-B");
    }

    [Fact]
    public void Inject_MutatesArgsInPlace_AndReturnsSameReference()
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var scope = QylScope.ForTest(serviceName: "svc");

        var result = Injector.Inject(args, scope);

        result.Should().BeSameAs(args);
        args.Should().ContainKey("serviceName");
    }

    [Fact]
    public void Inject_NewlyCreatedDict_IsCaseInsensitive()
    {
        var scope = QylScope.ForTest(serviceName: "svc");

        var result = Injector.Inject(arguments: null, scope);

        result.Should().ContainKey("ServiceName");
        result.Should().ContainKey("serviceName");
    }

    private static JsonElement Str(string value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static IDictionary<string, JsonElement> RequireInjected(IDictionary<string, JsonElement>? result)
    {
        result.Should().NotBeNull();
        return result ?? throw new InvalidOperationException("Expected qyl scope injection to return arguments.");
    }
}
