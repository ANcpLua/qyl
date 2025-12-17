using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Unit tests for SchemaNormalizer - OTel 1.38 attribute migration.
///     Tests VS-01 acceptance criteria: Deprecated attributes migrated to v1.38.
/// </summary>
public sealed class SchemaNormalizerTests
{
    #region Schema Version

    [Fact]
    public void SchemaVersion_IsOtel138()
    {
        Assert.Equal("1.38.0", SchemaVersion.Version);
        Assert.Equal("https://opentelemetry.io/schemas/1.38.0", SchemaVersion.SchemaUrl);
    }

    #endregion

    #region Code Attributes Migration

    [Theory]
    [InlineData("code.function", "code.function.name")]
    [InlineData("code.filepath", "code.file.path")]
    [InlineData("code.lineno", "code.line.number")]
    public void Normalize_CodeAttributes_MapsCorrectly(string deprecated, string expected)
    {
        var result = SchemaNormalizer.Normalize(deprecated);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Database Attributes Migration

    [Fact]
    public void Normalize_DbSystem_MapsToDbSystemName()
    {
        var result = SchemaNormalizer.Normalize("db.system");
        Assert.Equal("db.system.name", result);
    }

    #endregion

    #region OpenAI Specific Migration

    [Fact]
    public void Normalize_OpenAiRequestSeed_MapsToGenericRequestSeed()
    {
        var result = SchemaNormalizer.Normalize("gen_ai.openai.request.seed");
        Assert.Equal("gen_ai.request.seed", result);
    }

    #endregion

    #region Non-Deprecated Attributes

    [Theory]
    [InlineData("gen_ai.provider.name")]
    [InlineData("gen_ai.usage.input_tokens")]
    [InlineData("gen_ai.usage.output_tokens")]
    [InlineData("gen_ai.request.model")]
    [InlineData("gen_ai.response.model")]
    [InlineData("session.id")]
    [InlineData("service.name")]
    [InlineData("http.method")]
    [InlineData("custom.attribute")]
    public void Normalize_CurrentAttribute_ReturnsUnchanged(string attribute)
    {
        var result = SchemaNormalizer.Normalize(attribute);
        Assert.Equal(attribute, result);
    }

    #endregion

    #region Deprecated Mappings Retrieval

    [Fact]
    public void DeprecatedMappings_ContainsAllMappings()
    {
        var mappings = SchemaNormalizer.DeprecatedMappings;

        // Should contain all documented migrations
        Assert.True(mappings.Count >= 10);
        Assert.Equal("gen_ai.provider.name", mappings["gen_ai.system"]);
        Assert.Equal("gen_ai.usage.input_tokens", mappings["gen_ai.usage.prompt_tokens"]);
        Assert.Equal("gen_ai.usage.output_tokens", mappings["gen_ai.usage.completion_tokens"]);
    }

    #endregion

    #region GenAI Provider Migration

    [Fact]
    public void Normalize_GenAiSystem_MapsToProviderName()
    {
        // gen_ai.system → gen_ai.provider.name (deprecated since 1.37)
        var result = SchemaNormalizer.Normalize("gen_ai.system");
        Assert.Equal("gen_ai.provider.name", result);
    }

    [Fact]
    public void IsDeprecated_GenAiSystem_ReturnsTrue()
    {
        Assert.True(SchemaNormalizer.IsDeprecated("gen_ai.system"));
    }

    [Fact]
    public void IsDeprecated_GenAiProviderName_ReturnsFalse()
    {
        Assert.False(SchemaNormalizer.IsDeprecated("gen_ai.provider.name"));
    }

    #endregion

    #region Token Usage Migration (v1.38)

    [Fact]
    public void Normalize_PromptTokens_MapsToInputTokens()
    {
        // gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens (deprecated since 1.38)
        var result = SchemaNormalizer.Normalize("gen_ai.usage.prompt_tokens");
        Assert.Equal("gen_ai.usage.input_tokens", result);
    }

    [Fact]
    public void Normalize_CompletionTokens_MapsToOutputTokens()
    {
        // gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens (deprecated since 1.38)
        var result = SchemaNormalizer.Normalize("gen_ai.usage.completion_tokens");
        Assert.Equal("gen_ai.usage.output_tokens", result);
    }

    [Theory]
    [InlineData("gen_ai.usage.prompt_tokens")]
    [InlineData("gen_ai.usage.completion_tokens")]
    public void IsDeprecated_TokenUsageAttributes_ReturnsTrue(string deprecated)
    {
        Assert.True(SchemaNormalizer.IsDeprecated(deprecated));
    }

    [Theory]
    [InlineData("gen_ai.usage.input_tokens")]
    [InlineData("gen_ai.usage.output_tokens")]
    public void IsDeprecated_CurrentTokenAttributes_ReturnsFalse(string current)
    {
        Assert.False(SchemaNormalizer.IsDeprecated(current));
    }

    #endregion

    #region Legacy Agents.* Migration (OTel 1.38)

    [Fact]
    public void Normalize_LegacyAgentsAgentId_MapsToGenAiAgentId()
    {
        // agents.agent.id → gen_ai.agent.id (legacy non-standard prefix)
        var result = SchemaNormalizer.Normalize("agents.agent.id");
        Assert.Equal("gen_ai.agent.id", result);
    }

    [Fact]
    public void Normalize_LegacyAgentsToolName_MapsToGenAiToolName()
    {
        // agents.tool.name → gen_ai.tool.name (legacy non-standard prefix)
        var result = SchemaNormalizer.Normalize("agents.tool.name");
        Assert.Equal("gen_ai.tool.name", result);
    }

    [Fact]
    public void Normalize_LegacyAgentsToolCallId_MapsToGenAiToolCallId()
    {
        // agents.tool.call_id → gen_ai.tool.call.id (legacy + underscore fix)
        var result = SchemaNormalizer.Normalize("agents.tool.call_id");
        Assert.Equal("gen_ai.tool.call.id", result);
    }

    [Fact]
    public void Normalize_RequestMaxTokens_ReturnsUnchanged()
    {
        // gen_ai.request.max_tokens is CURRENT in OTel 1.38, NOT deprecated
        var result = SchemaNormalizer.Normalize("gen_ai.request.max_tokens");
        Assert.Equal("gen_ai.request.max_tokens", result);
    }

    #endregion

    #region GenAI Content Migration

    [Fact]
    public void Normalize_GenAiPrompt_MapsToInputMessages()
    {
        var result = SchemaNormalizer.Normalize("gen_ai.prompt");
        Assert.Equal("gen_ai.input.messages", result);
    }

    [Fact]
    public void Normalize_GenAiCompletion_MapsToOutputMessages()
    {
        var result = SchemaNormalizer.Normalize("gen_ai.completion");
        Assert.Equal("gen_ai.output.messages", result);
    }

    #endregion

    #region Dictionary Normalization

    [Fact]
    public void NormalizeAttributes_MixedDictionary_NormalizesAll()
    {
        // Arrange
        var input = new Dictionary<string, object?>
        {
            ["gen_ai.system"] = "openai",
            ["gen_ai.usage.prompt_tokens"] = 100L,
            ["gen_ai.usage.completion_tokens"] = 50L,
            ["gen_ai.request.model"] = "gpt-4",
            ["custom.attr"] = "value"
        };

        // Act
        var result = SchemaNormalizer.NormalizeAttributes(input);

        // Assert
        Assert.Equal(5, result.Count);

        // Deprecated keys should be renamed
        Assert.True(result.ContainsKey("gen_ai.provider.name"));
        Assert.True(result.ContainsKey("gen_ai.usage.input_tokens"));
        Assert.True(result.ContainsKey("gen_ai.usage.output_tokens"));

        // Current keys should remain
        Assert.True(result.ContainsKey("gen_ai.request.model"));
        Assert.True(result.ContainsKey("custom.attr"));

        // Old keys should not exist
        Assert.False(result.ContainsKey("gen_ai.system"));
        Assert.False(result.ContainsKey("gen_ai.usage.prompt_tokens"));
        Assert.False(result.ContainsKey("gen_ai.usage.completion_tokens"));

        // Values should be preserved
        Assert.Equal("openai", result["gen_ai.provider.name"]);
        Assert.Equal(100L, result["gen_ai.usage.input_tokens"]);
        Assert.Equal(50L, result["gen_ai.usage.output_tokens"]);
    }

    [Fact]
    public void NormalizeAttributes_EmptyDictionary_ReturnsEmpty()
    {
        var input = new Dictionary<string, object?>();
        var result = SchemaNormalizer.NormalizeAttributes(input);
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeAttributes_OnlyCurrentAttributes_ReturnsUnchanged()
    {
        // Arrange
        var input = new Dictionary<string, object?>
        {
            ["gen_ai.provider.name"] = "anthropic",
            ["gen_ai.usage.input_tokens"] = 200L,
            ["service.name"] = "my-app"
        };

        // Act
        var result = SchemaNormalizer.NormalizeAttributes(input);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("anthropic", result["gen_ai.provider.name"]);
        Assert.Equal(200L, result["gen_ai.usage.input_tokens"]);
        Assert.Equal("my-app", result["service.name"]);
    }

    [Fact]
    public void NormalizeAttributes_NullValue_Preserved()
    {
        // Arrange
        var input = new Dictionary<string, object?>
        {
            ["gen_ai.system"] = null
        };

        // Act
        var result = SchemaNormalizer.NormalizeAttributes(input);

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey("gen_ai.provider.name"));
        Assert.Null(result["gen_ai.provider.name"]);
    }

    #endregion
}