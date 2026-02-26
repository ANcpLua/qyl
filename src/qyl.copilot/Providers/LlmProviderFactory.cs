// =============================================================================
// qyl.copilot - LLM Provider Factory
// Provider-agnostic IChatClient construction from env var configuration
// Supports: ollama, openai, anthropic, openai-compatible endpoints
// =============================================================================

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace qyl.copilot.Providers;

/// <summary>
///     Configuration options for the LLM provider.
///     Bound from QYL_LLM_* environment variables.
/// </summary>
public sealed record LlmProviderOptions
{
    /// <summary>Provider name: "ollama", "openai", "anthropic", "openai-compatible".</summary>
    public string? Provider { get; init; }

    /// <summary>API endpoint URL.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Model name to use.</summary>
    public string? Model { get; init; }

    /// <summary>API key for authenticated providers.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Whether any provider is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Provider);
}

/// <summary>
///     LLM provider status for the /copilot/llm/status endpoint.
/// </summary>
public sealed record LlmProviderStatus
{
    /// <summary>Whether an LLM provider is configured.</summary>
    public required bool Configured { get; init; }

    /// <summary>Provider name if configured.</summary>
    public string? Provider { get; init; }

    /// <summary>Model name if configured.</summary>
    public string? Model { get; init; }
}

/// <summary>
///     Creates <see cref="IChatClient" /> instances from configuration.
///     Uses OpenAI-compatible HTTP API (supported by Ollama, OpenAI, Anthropic, and others).
/// </summary>
public static class LlmProviderFactory
{
    /// <summary>
    ///     Binds <see cref="LlmProviderOptions" /> from configuration.
    ///     Reads QYL_LLM_PROVIDER, QYL_LLM_ENDPOINT, QYL_LLM_MODEL, QYL_LLM_API_KEY.
    /// </summary>
    public static LlmProviderOptions BindOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new LlmProviderOptions
        {
            Provider = configuration["QYL_LLM_PROVIDER"],
            Endpoint = configuration["QYL_LLM_ENDPOINT"],
            Model = configuration["QYL_LLM_MODEL"],
            ApiKey = configuration["QYL_LLM_API_KEY"]
        };
    }

    /// <summary>
    ///     Creates an <see cref="IChatClient" /> based on the configured provider.
    ///     Returns null if no provider is configured.
    /// </summary>
    public static IChatClient? Create(LlmProviderOptions options, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!options.IsConfigured) return null;

        var (endpoint, model, headers) = ResolveProvider(options);
        return new OpenAiCompatibleChatClient(httpClient, endpoint, model, headers);
    }

    private static (Uri endpoint, string model, Dictionary<string, string>? headers) ResolveProvider(
        LlmProviderOptions options)
    {
        return options.Provider!.ToLowerInvariant() switch
        {
            "ollama" => (
                new Uri(options.Endpoint ?? "http://localhost:11434"),
                options.Model ?? "llama3",
                null),
            "openai" => (
                new Uri(options.Endpoint ?? "https://api.openai.com"),
                options.Model ?? "gpt-4o-mini",
                BuildAuthHeaders(options.ApiKey ?? throw new InvalidOperationException(
                    "QYL_LLM_API_KEY is required for OpenAI provider."))),
            "anthropic" => (
                new Uri(options.Endpoint ?? "https://api.anthropic.com"),
                options.Model ?? "claude-sonnet-4-20250514",
                new Dictionary<string, string>
                {
                    ["x-api-key"] = options.ApiKey ?? throw new InvalidOperationException(
                        "QYL_LLM_API_KEY is required for Anthropic provider."),
                    ["anthropic-version"] = "2023-06-01"
                }),
            "openai-compatible" => (
                new Uri(options.Endpoint ?? throw new InvalidOperationException(
                    "QYL_LLM_ENDPOINT is required for openai-compatible provider.")),
                options.Model ?? throw new InvalidOperationException(
                    "QYL_LLM_MODEL is required for openai-compatible provider."),
                options.ApiKey is not null ? BuildAuthHeaders(options.ApiKey) : null),
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider '{options.Provider}'. Supported: ollama, openai, anthropic, openai-compatible.")
        };
    }

    private static Dictionary<string, string> BuildAuthHeaders(string apiKey) =>
        new() { ["Authorization"] = $"Bearer {apiKey}" };

    /// <summary>
    ///     IChatClient implementation that speaks the OpenAI-compatible /v1/chat/completions API.
    ///     Works with Ollama, OpenAI, Anthropic, and any compatible endpoint.
    /// </summary>
    private sealed class OpenAiCompatibleChatClient(
        HttpClient httpClient,
        Uri endpoint,
        string model,
        Dictionary<string, string>? headers) : IChatClient
    {
        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly Uri _chatUrl = new($"{endpoint.ToString().TrimEnd('/')}/v1/chat/completions");

        public ChatClientMetadata Metadata => new(nameof(OpenAiCompatibleChatClient), endpoint, model);

        public void Dispose() { }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var body = BuildBody(messages, options, stream: false);
            using var response = await PostAsync(body, cancellationToken).ConfigureAwait(false);

            var result = await response.Content
                .ReadFromJsonAsync<ApiResponse>(s_json, cancellationToken)
                .ConfigureAwait(false);

            var text = result?.Choices is [var c, ..] ? c.Message?.Content ?? "" : "";

            var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                ModelId = result?.Model
            };

            if (result?.Usage is { } u)
            {
                chatResponse.Usage = new UsageDetails
                {
                    InputTokenCount = u.PromptTokens,
                    OutputTokenCount = u.CompletionTokens,
                    TotalTokenCount = u.TotalTokens
                };
            }

            return chatResponse;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var body = BuildBody(messages, options, stream: true);
            using var response = await PostAsync(body, cancellationToken).ConfigureAwait(false);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (!line.StartsWithOrdinal("data: ")) continue;

                var data = line["data: ".Length..];
                if (data is "[DONE]") break;

                ApiResponse? chunk;
                try { chunk = JsonSerializer.Deserialize<ApiResponse>(data, s_json); }
                catch (JsonException) { continue; }

                if (chunk?.Choices is not [var c, ..]) continue;
                var content = c.Delta?.Content;
                if (content is null) continue;

                yield return new ChatResponseUpdate(ChatRole.Assistant, content) { ModelId = chunk.Model };
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(OpenAiCompatibleChatClient)) return this;
            return null;
        }

        private ApiRequest BuildBody(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream) => new()
        {
            Model = options?.ModelId ?? model,
            Messages = messages.Select(static m => new ApiMessage { Role = m.Role.Value, Content = m.Text ?? "" }).ToList(),
            Stream = stream,
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxOutputTokens
        };

        private async Task<HttpResponseMessage> PostAsync(ApiRequest body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _chatUrl)
            {
                Content = JsonContent.Create(body, options: s_json)
            };

            if (headers is not null)
            {
                foreach (var (k, v) in headers)
                    req.Headers.TryAddWithoutValidation(k, v);
            }

            var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return resp;
        }
    }

    #region OpenAI-compatible wire types

    private sealed record ApiRequest
    {
        public required string Model { get; init; }
        public required List<ApiMessage> Messages { get; init; }
        public bool Stream { get; init; }
        public float? Temperature { get; init; }
        [JsonPropertyName("max_tokens")] public int? MaxTokens { get; init; }
    }

    private sealed record ApiMessage
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
    }

    private sealed record ApiResponse
    {
        public string? Model { get; init; }
        public List<ApiChoice>? Choices { get; init; }
        public ApiUsage? Usage { get; init; }
    }

    private sealed record ApiChoice
    {
        public ApiResponseMessage? Message { get; init; }
        public ApiResponseMessage? Delta { get; init; }
    }

    private sealed record ApiResponseMessage
    {
        public string? Content { get; init; }
    }

    private sealed record ApiUsage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; init; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
    }

    #endregion
}
