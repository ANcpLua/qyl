namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation.GenAi;

/// <summary>
///     Represents token usage from a GenAI response.
/// </summary>
/// <param name="InputTokens">Number of tokens in the input/prompt.</param>
/// <param name="OutputTokens">Number of tokens in the output/completion.</param>
public readonly record struct TokenUsage(int InputTokens, int OutputTokens);
