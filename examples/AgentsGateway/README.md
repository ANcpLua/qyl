# AgentGateway

A production-ready AI Agent Gateway for multi-provider orchestration. This library enables dynamic routing between AI
providers (OpenAI, GitHub Models, Ollama, etc.) while providing robust infrastructure for Microsoft Agent Framework.

## Features

- **Multi-Provider Routing**: Route requests based on headers (`X-Provider`, `X-Model`).
- **Resilience**: Built-in Polly pipelines for retries and timeouts.
- **Security**: Production-grade authentication for Microsoft Teams and Azure Bot Service.
- **Observability**: OpenTelemetry integration with Azure Application Insights.
- **Storage**: Scalable state persistence using Azure Blob Storage.

## Getting Started

1. Install the package:
   ```bash
   dotnet add package AgentGateway
   ```

2. Register the gateway in `Program.cs`:
   ```csharp
   builder.Services.AddBotAspNetAuthentication(builder.Configuration);
   builder.Services.AddSingleton<IStorage>(sp => ...);
   ```

3. Define your agents and workflows as usual!

## License

MIT