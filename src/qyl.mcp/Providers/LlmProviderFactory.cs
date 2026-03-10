using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace qyl.mcp.Providers;

/// <summary>
///     Configuration options for the LLM provider.
///     Bound from QYL_LLM_* environment variables.
/// </summary>
internal sealed record LlmProviderOptions
{
    /// <summary>Provider name: "ollama", "openai", "anthropic", "openai-compatible", "github-models".</summary>
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
///     Creates <see cref="IChatClient" /> instances from configuration.
///     Uses OpenAI-compatible HTTP API (supported by Ollama, OpenAI, Anthropic, and others).
/// </summary>
internal static class LlmProviderFactory
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

        var (chatUrl, model, headers) = ResolveProvider(options);
        return new OpenAiCompatibleChatClient(httpClient, chatUrl, model, headers);
    }

    private static (Uri chatUrl, string model, Dictionary<string, string>? headers) ResolveProvider(
        LlmProviderOptions options)
    {
        return options.Provider!.ToLowerInvariant() switch
        {
            "ollama" => (
                ChatUrl(options.Endpoint ?? "http://localhost:11434"),
                options.Model ?? "llama3",
                null),
            "openai" => (
                ChatUrl(options.Endpoint ?? "https://api.openai.com"),
                options.Model ?? "gpt-4o-mini",
                BuildAuthHeaders(options.ApiKey ?? throw new InvalidOperationException(
                    "QYL_LLM_API_KEY is required for OpenAI provider."))),
            "anthropic" => (
                ChatUrl(options.Endpoint ?? "https://api.anthropic.com"),
                options.Model ?? "claude-sonnet-4-6",
                new Dictionary<string, string>
                {
                    ["x-api-key"] = options.ApiKey ?? throw new InvalidOperationException(
                        "QYL_LLM_API_KEY is required for Anthropic provider."),
                    ["anthropic-version"] = "2023-06-01"
                }),
            "github-models" => (
                new Uri($"{(options.Endpoint ?? "https://models.github.ai/inference").TrimEnd('/')}/chat/completions"),
                options.Model ?? "gpt-4o-mini",
                BuildAuthHeaders(options.ApiKey ?? throw new InvalidOperationException(
                    "GitHub token is required for GitHub Models provider."))),
            "openai-compatible" => (
                ChatUrl(options.Endpoint ?? throw new InvalidOperationException(
                    "QYL_LLM_ENDPOINT is required for openai-compatible provider.")),
                options.Model ?? throw new InvalidOperationException(
                    "QYL_LLM_MODEL is required for openai-compatible provider."),
                options.ApiKey is not null ? BuildAuthHeaders(options.ApiKey) : null),
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider '{options.Provider}'. Supported: ollama, openai, anthropic, github-models, openai-compatible.")
        };
    }

    private static Uri ChatUrl(string baseEndpoint) =>
        new($"{baseEndpoint.TrimEnd('/')}/v1/chat/completions");

    private static Dictionary<string, string> BuildAuthHeaders(string apiKey) =>
        new() { ["Authorization"] = $"Bearer {apiKey}" };

    private sealed class OpenAiCompatibleChatClient(
        HttpClient httpClient,
        Uri chatUrl,
        string model,
        Dictionary<string, string>? headers) : IChatClient
    {
        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ChatClientMetadata Metadata => new(nameof(OpenAiCompatibleChatClient), chatUrl, model);

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
            return serviceType == typeof(OpenAiCompatibleChatClient) ? this : null;
        }

        private ApiRequest BuildBody(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream) => new()
        {
            Model = options?.ModelId ?? model,
            Messages = [.. messages.Select(static m => new ApiMessage { Role = m.Role.Value, Content = m.Text ?? "" })],
            Stream = stream,
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxOutputTokens
        };

        private async Task<HttpResponseMessage> PostAsync(ApiRequest body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, chatUrl)
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
