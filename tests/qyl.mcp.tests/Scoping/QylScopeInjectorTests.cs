using System.Text.Json;
using qyl.mcp.Scoping;

namespace Qyl.Mcp.Tests.Scoping;

public sealed class QylScopeInjectorTests
{
    private static QylScopeInjector NewInjector() => new();

    [Fact]
    public void Inject_WhenScopeIsEmpty_ReturnsArgumentsUnchanged()
    {
        var scope = QylScope.ForTest();
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement("user-supplied")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal("user-supplied", result!["serviceName"].GetString());
        Assert.Single(result);
    }

    [Fact]
    public void Inject_WhenScopeIsEmptyAndArgumentsAreNull_ReturnsNull()
    {
        var scope = QylScope.ForTest();

        var result = NewInjector().Inject(null, scope);

        Assert.Null(result);
    }

    [Fact]
    public void Inject_WithNullArguments_CreatesDictionaryAndInjectsBothScopeValues()
    {
        var scope = QylScope.ForTest(serviceName: "checkout", sessionId: "abc123");

        var result = NewInjector().Inject(null, scope);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("checkout", result["serviceName"].GetString());
        Assert.Equal("abc123", result["sessionId"].GetString());
    }

    [Fact]
    public void Inject_WithNullArguments_DictionaryUsesOrdinalIgnoreCase()
    {
        var scope = QylScope.ForTest(serviceName: "checkout");

        var result = NewInjector().Inject(null, scope);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("serviceName"));
        Assert.True(result.ContainsKey("ServiceName"));
        Assert.True(result.ContainsKey("SERVICENAME"));
    }

    [Fact]
    public void Inject_WithOnlyServiceNameInScope_InjectsServiceNameAndSkipsSessionId()
    {
        var scope = QylScope.ForTest(serviceName: "checkout");

        var result = NewInjector().Inject(null, scope);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("checkout", result!["serviceName"].GetString());
        Assert.False(result.ContainsKey("sessionId"));
    }

    [Fact]
    public void Inject_WithOnlySessionIdInScope_InjectsSessionIdAndSkipsServiceName()
    {
        var scope = QylScope.ForTest(sessionId: "abc123");

        var result = NewInjector().Inject(null, scope);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("abc123", result!["sessionId"].GetString());
        Assert.False(result.ContainsKey("serviceName"));
    }

    [Fact]
    public void Inject_WhenExistingNonEmptyStringValuePresent_DoesNotOverwrite()
    {
        var scope = QylScope.ForTest(serviceName: "scope-service");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement("user-supplied")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal("user-supplied", result!["serviceName"].GetString());
    }

    [Fact]
    public void Inject_WhenExistingEmptyStringValuePresent_TreatsAsMissingAndOverwrites()
    {
        var scope = QylScope.ForTest(serviceName: "scope-service");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement(string.Empty)
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal("scope-service", result!["serviceName"].GetString());
    }

    [Fact]
    public void Inject_WhenExistingNonStringValuePresent_TreatsAsMissingAndOverwrites()
    {
        var scope = QylScope.ForTest(serviceName: "scope-service");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement(42)
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal(JsonValueKind.String, result!["serviceName"].ValueKind);
        Assert.Equal("scope-service", result["serviceName"].GetString());
    }

    [Theory]
    [InlineData("serviceName")]
    [InlineData("SERVICENAME")]
    [InlineData("ServiceName")]
    public void Inject_ExistingValueLookup_IsCaseInsensitiveWhenCallerDictIsCaseInsensitive(string existingKey)
    {
        var scope = QylScope.ForTest(serviceName: "scope-service");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            [existingKey] = JsonSerializer.SerializeToElement("user-supplied")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Single(result!);
        Assert.Equal("user-supplied", result![existingKey].GetString());
    }

    [Fact]
    public void Inject_PreservesUnrelatedExistingArguments_AndInjectsScope()
    {
        var scope = QylScope.ForTest(serviceName: "checkout", sessionId: "abc123");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["limit"] = JsonSerializer.SerializeToElement(50),
            ["query"] = JsonSerializer.SerializeToElement("error")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal(4, result!.Count);
        Assert.Equal(50, result["limit"].GetInt32());
        Assert.Equal("error", result["query"].GetString());
        Assert.Equal("checkout", result["serviceName"].GetString());
        Assert.Equal("abc123", result["sessionId"].GetString());
    }

    [Fact]
    public void Inject_SessionIdBranchAlsoHonoursExistingNonEmptyString()
    {
        var scope = QylScope.ForTest(sessionId: "scope-session");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["sessionId"] = JsonSerializer.SerializeToElement("user-session")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal("user-session", result!["sessionId"].GetString());
    }

    [Fact]
    public void Inject_WhenOneParamHasExistingValue_StillInjectsTheOther()
    {
        var scope = QylScope.ForTest(serviceName: "scope-service", sessionId: "scope-session");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement("user-service")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("user-service", result["serviceName"].GetString());
        Assert.Equal("scope-session", result["sessionId"].GetString());
    }

    [Fact]
    public void Inject_WhenBothParamsHaveExistingValues_PreservesBoth()
    {
        var scope = QylScope.ForTest(serviceName: "scope-service", sessionId: "scope-session");
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = JsonSerializer.SerializeToElement("user-service"),
            ["sessionId"] = JsonSerializer.SerializeToElement("user-session")
        };

        var result = NewInjector().Inject(arguments, scope);

        Assert.Same(arguments, result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("user-service", result["serviceName"].GetString());
        Assert.Equal("user-session", result["sessionId"].GetString());
    }
}
