# qyl.gemini

Opus's barking dog 🐕 — Gemini-powered summaries and fetch agent for Claude orchestration.

## Installation

```bash
dotnet add package qyl.gemini
```

## Quick Start

```csharp
// Register in DI
builder.Services.AddGemini(builder.Configuration);

// Use via ISummarizer
public class MyService(ISummarizer summarizer)
{
    public async Task<string?> Summarize(string text)
    {
        return await summarizer.SummarizeAsync(text);
    }
}
```

## Configuration

```json
{
  "Gemini": {
    "ApiKey": "your-api-key",
    "Model": "gemini-2.5-flash",
    "TimeoutSeconds": 30,
    "Temperature": 0.3,
    "MaxOutputTokens": 1024
  }
}
```

## Custom Prompts

```csharp
var result = await summarizer.SummarizeAsync(
    text: documentContent,
    promptTemplate: """
        Extract key entities from this text:
        {text}
        
        Return as JSON array.
        """);
```

## Direct Generation

```csharp
// For advanced usage, inject GeminiClient directly
public class MyService(GeminiClient gemini)
{
    public async Task<string?> Generate(string prompt)
    {
        return await gemini.GenerateAsync(prompt);
    }
}
```

## Features

- ✅ .NET 10 native
- ✅ AOT-compatible (source-generated JSON)
- ✅ HTTP resilience (retries, circuit breaker)
- ✅ Minimal dependencies
- ✅ Free tier friendly (5 queries/hour)

## Why "barking dog"?

The Gemini free tier hallucinates so much it sounds like barking. Perfect for low-stakes summarization where Opus (Claude) needs a cheap fetch assistant. 🐕

## License

MIT