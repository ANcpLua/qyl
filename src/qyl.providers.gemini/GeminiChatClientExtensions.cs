// Copyright (c) qyl. All rights reserved.
// Thin wrapper around Mscc.GenerativeAI.Microsoft for IChatClient support.
// When Google.GenAI PR #81 merges, consider migrating to the official implementation.

using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Microsoft;

namespace Microsoft.Extensions.AI;

/// <summary>
/// Extension methods for creating IChatClient instances from Gemini models.
/// Uses Mscc.GenerativeAI.Microsoft under the hood.
/// </summary>
/// <remarks>
/// See <see href="https://github.com/mscraftsman/generative-ai"/> for documentation.
/// </remarks>
public static class GeminiChatClientExtensions
{
    /// <summary>
    /// Creates a <see cref="GoogleAI"/> instance with the specified API key.
    /// </summary>
    /// <param name="apiKey">The Google AI API key.</param>
    /// <returns>A configured GoogleAI instance.</returns>
    public static GoogleAI CreateGoogleAI(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return new GoogleAI(apiKey: apiKey);
    }

    /// <summary>
    /// Creates a <see cref="GenerativeModel"/> for the specified model.
    /// </summary>
    /// <param name="googleAI">The GoogleAI instance.</param>
    /// <param name="modelId">The model ID (e.g., "gemini-2.0-flash").</param>
    /// <returns>A configured GenerativeModel instance.</returns>
    public static GenerativeModel CreateModel(this GoogleAI googleAI, string modelId = "gemini-2.0-flash")
    {
        ArgumentNullException.ThrowIfNull(googleAI);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return googleAI.GenerativeModel(model: modelId);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> for the specified Gemini model.
    /// </summary>
    /// <param name="apiKey">The Google AI API key.</param>
    /// <param name="modelId">The model ID (e.g., "gemini-2.0-flash").</param>
    /// <returns>An IChatClient implementation for the model. Caller is responsible for disposal.</returns>
    /// <remarks>
    /// The returned client wraps disposable resources. Callers should dispose the client when done.
    /// </remarks>
#pragma warning disable CA2000 // Dispose objects before losing scope - caller owns lifecycle
    public static IChatClient CreateGeminiChatClient(string apiKey, string modelId = "gemini-2.0-flash")
    {
        return CreateGoogleAI(apiKey).CreateModel(modelId).AsIChatClient();
    }
#pragma warning restore CA2000
}
